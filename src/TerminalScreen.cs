using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

namespace EasyJava2 {
public sealed class TerminalScreen:Control {
 struct Cell {public string Text;public Color Foreground,Background;public bool Bold;}
 enum ParserState {Text,Escape,Csi,Osc,OscEscape,IgnoreString,IgnoreStringEscape,Charset}
 static readonly Color DefaultForeground=Color.FromArgb(229,229,229);
 static readonly Color DefaultBackground=Color.Black;
 static readonly Color[] Palette16={Color.FromArgb(0,0,0),Color.FromArgb(205,0,0),Color.FromArgb(0,205,0),Color.FromArgb(205,205,0),Color.FromArgb(0,0,238),Color.FromArgb(205,0,205),Color.FromArgb(0,205,205),Color.FromArgb(229,229,229),Color.FromArgb(127,127,127),Color.FromArgb(255,0,0),Color.FromArgb(0,255,0),Color.FromArgb(255,255,0),Color.FromArgb(92,92,255),Color.FromArgb(255,0,255),Color.FromArgb(0,255,255),Color.FromArgb(255,255,255)};
 Cell[,] cells;
 int rows=30,columns=100,row,column,savedRow,savedColumn,cellWidth=8,cellHeight=16,foregroundIndex=-1;
 Color foreground=DefaultForeground,background=DefaultBackground;
 bool bold,inverse,wrapPending,cursorVisible,underline;
 ParserState parser;
 readonly StringBuilder sequence=new StringBuilder();
 char pendingHigh;
 readonly Font ownedFont;

 public int Rows {get{return rows;}}
 public int Columns {get{return columns;}}
 public int CursorRow {get{return row;}}
 public int CursorColumn {get{return column;}}
 public int CellWidth {get{return cellWidth;}}
 public int CellHeight {get{return cellHeight;}}
 public bool CursorVisible {get{return cursorVisible;}}
 public TerminalScreen(){
  SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.ResizeRedraw,true);
  Name="terminalScreen";BackColor=DefaultBackground;ForeColor=DefaultForeground;
  ownedFont=new Font("Consolas",10,FontStyle.Regular);Font=ownedFont;TabStop=false;
  cells=new Cell[rows,columns];ClearAll();Size=new Size(800,480);
 }

 public string PlainText {get{
  StringBuilder text=new StringBuilder();int lastRow=-1;
  for(int r=0;r<rows;r++)for(int c=0;c<columns;c++)if(!IsBlank(cells[r,c].Text))lastRow=r;
  for(int r=0;r<=lastRow;r++){
   if(r>0)text.Append("\r\n");int lastColumn=columns-1;while(lastColumn>=0&&IsBlank(cells[r,lastColumn].Text))lastColumn--;
   for(int c=0;c<=lastColumn;c++)text.Append(cells[r,c].Text??" ");
  }
  return text.ToString();
 }}
 static bool IsBlank(string text){return string.IsNullOrEmpty(text)||text==" ";}
 public Color ForegroundAt(int r,int c){CheckCell(r,c);return cells[r,c].Foreground;}
 public Color BackgroundAt(int r,int c){CheckCell(r,c);return cells[r,c].Background;}
 void CheckCell(int r,int c){if(r<0||r>=rows||c<0||c>=columns)throw new ArgumentOutOfRangeException("r","Komórka jest poza ekranem terminala.");}

 public void ResetScreen(){
  row=column=savedRow=savedColumn=0;foreground=DefaultForeground;background=DefaultBackground;foregroundIndex=-1;bold=inverse=underline=wrapPending=cursorVisible=false;
  parser=ParserState.Text;sequence.Clear();pendingHigh='\0';ClearAll();Invalidate();
 }
 public void Feed(string text){
  if(IsDisposed||string.IsNullOrEmpty(text))return;
  foreach(char ch in text)Parse(ch);
  Invalidate();
 }
 void Parse(char ch){
  switch(parser){
   case ParserState.Escape:
    parser=ParserState.Text;
    if(ch=='['){parser=ParserState.Csi;sequence.Clear();}
    else if(ch==']'){parser=ParserState.Osc;sequence.Clear();}
    else if(ch=='P'||ch=='^'||ch=='_'){parser=ParserState.IgnoreString;sequence.Clear();}
    else if(ch=='('||ch==')'||ch=='*'||ch=='+')parser=ParserState.Charset;
    else if(ch=='7'){savedRow=row;savedColumn=column;}
    else if(ch=='8'){row=Clamp(savedRow,0,rows-1);column=Clamp(savedColumn,0,columns-1);wrapPending=false;}
    else if(ch=='D')LineFeed(false);
    else if(ch=='E')LineFeed(true);
    else if(ch=='M'){if(row>0)row--;else ScrollDown(1);wrapPending=false;}
    else if(ch=='c')ResetScreen();
    else if(ch=='\x1b')parser=ParserState.Escape;
    return;
   case ParserState.Csi:
    if(ch=='\x1b'){parser=ParserState.Escape;sequence.Clear();return;}
    if(ch>='@'&&ch<='~'){string parameters=sequence.ToString();sequence.Clear();parser=ParserState.Text;Csi(ch,parameters);return;}
    if(sequence.Length<512)sequence.Append(ch);else{sequence.Clear();parser=ParserState.Text;}
    return;
   case ParserState.Osc:if(ch=='\a')parser=ParserState.Text;else if(ch=='\x1b')parser=ParserState.OscEscape;return;
   case ParserState.OscEscape:parser=ch=='\\'?ParserState.Text:ParserState.Osc;return;
   case ParserState.IgnoreString:if(ch=='\x1b')parser=ParserState.IgnoreStringEscape;return;
   case ParserState.IgnoreStringEscape:parser=ch=='\\'?ParserState.Text:ParserState.IgnoreString;return;
   case ParserState.Charset:parser=ParserState.Text;return;
  }
  if(ch=='\x1b'){FlushHigh();parser=ParserState.Escape;return;}
  if(ch=='\x9b'){FlushHigh();parser=ParserState.Csi;sequence.Clear();return;}
  if(ch=='\r'){FlushHigh();column=0;wrapPending=false;return;}
  // Redirected HTTP and tool output commonly contains bare LF. Treat it as a
  // new terminal line, matching the usual cooked-console newline behaviour.
  if(ch=='\n'||ch=='\v'||ch=='\f'){FlushHigh();LineFeed(true);return;}
  if(ch=='\b'){FlushHigh();column=Math.Max(0,column-1);wrapPending=false;return;}
  if(ch=='\t'){FlushHigh();column=Math.Min(columns-1,((column/8)+1)*8);wrapPending=false;return;}
  if(ch<32||ch=='\x7f')return;
  if(char.IsHighSurrogate(ch)){FlushHigh();pendingHigh=ch;return;}
  if(char.IsLowSurrogate(ch)){if(pendingHigh!='\0'){string pair=new string(new[]{pendingHigh,ch});pendingHigh='\0';Put(pair);}else Put("\ufffd");return;}
  FlushHigh();
  UnicodeCategory category=char.GetUnicodeCategory(ch);
  if(category==UnicodeCategory.NonSpacingMark||category==UnicodeCategory.EnclosingMark||category==UnicodeCategory.SpacingCombiningMark){int previous=wrapPending?column:column-1;if(previous>=0){Cell cell=cells[row,previous];cell.Text=(cell.Text??" ")+ch;cells[row,previous]=cell;}return;}
  Put(ch.ToString());
 }
 void FlushHigh(){if(pendingHigh!='\0'){pendingHigh='\0';Put("\ufffd");}}
 void Put(string text){
  if(wrapPending){LineFeed(true);wrapPending=false;}
  Color fg=foreground;if(bold&&foregroundIndex>=0&&foregroundIndex<8)fg=Palette16[foregroundIndex+8];
  cells[row,column]=new Cell{Text=text,Foreground=inverse?background:fg,Background=inverse?fg:background,Bold=bold};
  if(column==columns-1)wrapPending=true;else column++;
 }
 void LineFeed(bool carriageReturn){if(carriageReturn)column=0;wrapPending=false;if(row<rows-1)row++;else ScrollUp(1);}
 void ScrollUp(int count){count=Clamp(count,1,rows);for(int r=0;r<rows-count;r++)for(int c=0;c<columns;c++)cells[r,c]=cells[r+count,c];for(int r=rows-count;r<rows;r++)ClearRow(r,0,columns-1);}
 void ScrollDown(int count){count=Clamp(count,1,rows);for(int r=rows-1;r>=count;r--)for(int c=0;c<columns;c++)cells[r,c]=cells[r-count,c];for(int r=0;r<count;r++)ClearRow(r,0,columns-1);}
 Cell Blank(){return new Cell{Text=" ",Foreground=inverse?background:foreground,Background=inverse?foreground:background};}
 void ClearAll(){if(cells==null)return;for(int r=0;r<rows;r++)ClearRow(r,0,columns-1);}
 void ClearRow(int r,int from,int to){Cell empty=Blank();for(int c=Math.Max(0,from);c<=Math.Min(columns-1,to);c++)cells[r,c]=empty;}
 static int Clamp(int value,int low,int high){return Math.Max(low,Math.Min(high,value));}
 static int Parameter(int[] values,int index,int fallback){return index>=values.Length||values[index]<=0?fallback:values[index];}
 void Csi(char command,string raw){
  bool privateMode=raw.StartsWith("?",StringComparison.Ordinal);string parameters=privateMode?raw.Substring(1):raw;
  if(parameters.StartsWith(">",StringComparison.Ordinal)||parameters.StartsWith("!",StringComparison.Ordinal))return;
  string[] parts=parameters.Split(';');int[] values=new int[parts.Length];
  for(int i=0;i<parts.Length;i++){int value;if(!int.TryParse(parts[i],NumberStyles.Integer,CultureInfo.InvariantCulture,out value))value=0;values[i]=Clamp(value,0,1000000);}
  if(privateMode){if(command=='h'||command=='l'){foreach(int mode in values){if(mode==25)cursorVisible=command=='h';else if(mode==1049||mode==47||mode==1047){if(command=='h'){savedRow=row;savedColumn=column;row=column=0;ClearAll();}else{row=Clamp(savedRow,0,rows-1);column=Clamp(savedColumn,0,columns-1);}wrapPending=false;}}}return;}
  int first=Parameter(values,0,1);
  switch(command){
   case 'A':row=Math.Max(0,row-first);break;case 'B':case 'e':row=Math.Min(rows-1,row+first);break;
   case 'C':case 'a':column=Math.Min(columns-1,column+first);break;case 'D':column=Math.Max(0,column-first);break;
   case 'E':row=Math.Min(rows-1,row+first);column=0;break;case 'F':row=Math.Max(0,row-first);column=0;break;
   case 'G':case '`':column=Clamp(first-1,0,columns-1);break;case 'd':row=Clamp(first-1,0,rows-1);break;
   case 'H':case 'f':row=Clamp(first-1,0,rows-1);column=Clamp(Parameter(values,1,1)-1,0,columns-1);break;
   case 'J':
    if(values[0]==2||values[0]==3)ClearAll();
    else if(values[0]==1){for(int r=0;r<row;r++)ClearRow(r,0,columns-1);ClearRow(row,0,column);}
    else{ClearRow(row,column,columns-1);for(int r=row+1;r<rows;r++)ClearRow(r,0,columns-1);}break;
   case 'K':if(values[0]==2)ClearRow(row,0,columns-1);else if(values[0]==1)ClearRow(row,0,column);else ClearRow(row,column,columns-1);break;
   case 'S':ScrollUp(first);break;case 'T':ScrollDown(first);break;
   case 's':savedRow=row;savedColumn=column;break;case 'u':row=Clamp(savedRow,0,rows-1);column=Clamp(savedColumn,0,columns-1);break;
   case 'm':Sgr(values);return;
   case 'X':ClearRow(row,column,Math.Min(columns-1,column+first-1));break;
   case 'P':{int count=Math.Min(first,columns-column);for(int c=column;c<columns-count;c++)cells[row,c]=cells[row,c+count];ClearRow(row,columns-count,columns-1);break;}
   case '@':{int count=Math.Min(first,columns-column);for(int c=columns-1;c>=column+count;c--)cells[row,c]=cells[row,c-count];ClearRow(row,column,column+count-1);break;}
  }
  wrapPending=false;
 }
 void Sgr(int[] values){
  for(int i=0;i<values.Length;i++){
   int mode=values[i];
   if(mode==0){foreground=DefaultForeground;background=DefaultBackground;foregroundIndex=-1;bold=inverse=underline=false;}
   else if(mode==1)bold=true;else if(mode==22)bold=false;else if(mode==4)underline=true;else if(mode==24)underline=false;
   else if(mode==7)inverse=true;else if(mode==27)inverse=false;
   else if(mode>=30&&mode<=37){foregroundIndex=mode-30;foreground=Palette16[foregroundIndex];}
   else if(mode>=90&&mode<=97){foregroundIndex=mode-90+8;foreground=Palette16[foregroundIndex];}
   else if(mode>=40&&mode<=47)background=Palette16[mode-40];
   else if(mode>=100&&mode<=107)background=Palette16[mode-100+8];
   else if(mode==39){foreground=DefaultForeground;foregroundIndex=-1;}else if(mode==49)background=DefaultBackground;
   else if(mode==38||mode==48){
    Color color;bool valid=false;
    if(i+2<values.Length&&values[i+1]==5){color=Color256(Clamp(values[i+2],0,255));i+=2;valid=true;}
    else if(i+4<values.Length&&values[i+1]==2){color=Color.FromArgb(Clamp(values[i+2],0,255),Clamp(values[i+3],0,255),Clamp(values[i+4],0,255));i+=4;valid=true;}
    else color=DefaultForeground;
    if(valid){if(mode==38){foreground=color;foregroundIndex=-1;}else background=color;}
   }
  }
 }
 static Color Color256(int index){if(index<16)return Palette16[index];if(index>=232){int gray=8+(index-232)*10;return Color.FromArgb(gray,gray,gray);}int n=index-16;int[] level={0,95,135,175,215,255};return Color.FromArgb(level[n/36],level[n/6%6],level[n%6]);}

 protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);TextFormatFlags flags=TextFormatFlags.NoPadding|TextFormatFlags.SingleLine;Size measured=TextRenderer.MeasureText("M",Font,Size.Empty,flags);Size pair=TextRenderer.MeasureText("MM",Font,Size.Empty,flags);cellWidth=Math.Max(1,pair.Width-measured.Width);cellHeight=Math.Max(1,measured.Height);ResizeGrid();}
 protected override void OnResize(EventArgs e){base.OnResize(e);ResizeGrid();}
 void ResizeGrid(){
  if(cells==null||ClientSize.Width<1||ClientSize.Height<1)return;
  int newColumns=Clamp(ClientSize.Width/cellWidth,1,300),newRows=Clamp(ClientSize.Height/cellHeight,1,200);
  if(newColumns==columns&&newRows==rows)return;
  Cell[,] old=cells;int oldRows=rows,oldColumns=columns;rows=newRows;columns=newColumns;cells=new Cell[rows,columns];ClearAll();
  for(int r=0;r<Math.Min(rows,oldRows);r++)for(int c=0;c<Math.Min(columns,oldColumns);c++)cells[r,c]=old[r,c];
  row=Clamp(row,0,rows-1);column=Clamp(column,0,columns-1);savedRow=Clamp(savedRow,0,rows-1);savedColumn=Clamp(savedColumn,0,columns-1);wrapPending=false;Invalidate();
 }
 protected override void OnPaint(PaintEventArgs e){
  e.Graphics.Clear(DefaultBackground);if(cells==null)return;
  int startRow=Clamp(e.ClipRectangle.Top/cellHeight,0,rows-1),endRow=Clamp(e.ClipRectangle.Bottom/cellHeight,0,rows-1);
  TextFormatFlags flags=TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine|TextFormatFlags.PreserveGraphicsClipping;
  for(int r=startRow;r<=endRow;r++){
   int c=0;while(c<columns){Cell first=cells[r,c];int start=c;StringBuilder run=new StringBuilder();
    do{run.Append(cells[r,c].Text??" ");c++;}while(c<columns&&cells[r,c].Foreground==first.Foreground&&cells[r,c].Background==first.Background&&cells[r,c].Bold==first.Bold);
    Rectangle bounds=new Rectangle(start*cellWidth,r*cellHeight,(c-start)*cellWidth,cellHeight);
    TextRenderer.DrawText(e.Graphics,run.ToString(),Font,bounds,first.Foreground,first.Background,flags);
   }
  }
  if(cursorVisible&&Focused){using(Pen pen=new Pen(foreground))e.Graphics.DrawLine(pen,column*cellWidth,row*cellHeight+cellHeight-2,(column+1)*cellWidth-1,row*cellHeight+cellHeight-2);}
  base.OnPaint(e);
 }
 protected override void Dispose(bool disposing){base.Dispose(disposing);if(disposing&&ownedFont!=null)ownedFont.Dispose();}
}
}
