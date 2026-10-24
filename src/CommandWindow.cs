using System;
using System.Drawing;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace EasyJava2 {
public sealed class CommandWindow:Form {
 public readonly TerminalScreen Terminal;
 public readonly TextBox CommandText;
 public readonly RichTextBox OutputText,ErrorText;
 public readonly Label StatusText;
 public readonly Button StopButton,CloseButton;
 public bool Running {get{return running;}}

 const int VisibleLimit=1000000;
 const string Truncation="\r\n[Wynik skrócony do 1 000 000 znaków.]";
 readonly object gate=new object();
 readonly StringBuilder pendingOutput=new StringBuilder(),pendingError=new StringBuilder(),pendingTerminal=new StringBuilder();
 const int LiveQueueLimit=262144;
 bool terminalReceived;
 readonly Action cancel;
 readonly int uiThread;
 readonly System.Windows.Forms.Timer flushTimer;
 readonly TabControl resultTabs;
 int outputAccepted,errorAccepted;
 bool outputTruncated,errorTruncated,outputNotice,errorNotice,accepting=true,finishedQueued,cancelRequested,disposed;
 volatile bool running=true;
 Action pendingFinish;

 public CommandWindow(string shell,string command,string folder,double timeoutSeconds,Action cancel){
  this.cancel=cancel;uiThread=Thread.CurrentThread.ManagedThreadId;
  string shellName=shell=="cmd"?"CMD":"PowerShell";
  Name="commandWindow";Text=shellName+" — polecenie EasyJava";ClientSize=new Size(900,740);MinimumSize=new Size(620,450);StartPosition=FormStartPosition.CenterParent;BackColor=Ui.Panel;ForeColor=Ui.Text;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;Icon=Program.Icon;
  TableLayoutPanel root=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=6,Padding=new Padding(18,13,18,10),Margin=new Padding(0)};
  root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,58));root.RowStyles.Add(new RowStyle(SizeType.Absolute,47));root.RowStyles.Add(new RowStyle(SizeType.Absolute,23));root.RowStyles.Add(new RowStyle(SizeType.Absolute,80));root.RowStyles.Add(new RowStyle(SizeType.Percent,100));root.RowStyles.Add(new RowStyle(SizeType.Absolute,51));Controls.Add(root);
  Panel heading=new Panel{Dock=DockStyle.Fill,Margin=new Padding(0)};root.Controls.Add(heading,0,0);
  Label title=Ui.Label(shellName+" — Twoja komenda",18);title.Font=new Font("Segoe UI",18,FontStyle.Bold);title.SetBounds(0,0,700,35);title.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;heading.Controls.Add(title);
  StatusText=Ui.Label("Wykonywanie polecenia…",9);StatusText.Name="commandStatus";StatusText.ForeColor=Color.FromArgb(190,173,255);StatusText.SetBounds(1,36,700,22);StatusText.Anchor=AnchorStyles.Top|AnchorStyles.Left|AnchorStyles.Right;heading.Controls.Add(StatusText);
  Label context=Ui.Label("Folder: "+(string.IsNullOrWhiteSpace(folder)?Environment.GetFolderPath(Environment.SpecialFolder.UserProfile):folder)+"\r\nLimit czasu: "+timeoutSeconds.ToString("0.##",CultureInfo.GetCultureInfo("pl-PL"))+" s",9);context.Name="commandContext";context.ForeColor=Ui.Muted;context.Dock=DockStyle.Fill;context.Margin=new Padding(0,2,0,0);context.AutoEllipsis=true;root.Controls.Add(context,0,1);
  Label commandLabel=Ui.Label("POLECENIE",9);commandLabel.ForeColor=Ui.Muted;commandLabel.Font=new Font("Segoe UI",9,FontStyle.Bold);commandLabel.Dock=DockStyle.Fill;commandLabel.Margin=new Padding(0);root.Controls.Add(commandLabel,0,2);
  CommandText=Ui.Input(command??"");CommandText.Name="commandText";CommandText.ReadOnly=true;CommandText.Multiline=true;CommandText.ScrollBars=ScrollBars.Both;CommandText.WordWrap=false;CommandText.Font=new Font("Consolas",10);CommandText.Dock=DockStyle.Fill;CommandText.Margin=new Padding(0,0,0,10);root.Controls.Add(CommandText,0,3);
  resultTabs=new TabControl{Name="commandResults",Dock=DockStyle.Fill,Margin=new Padding(0)};
  TabPage terminalPage=new TabPage("Terminal"),outputPage=new TabPage("Wynik (tekst)"),errorPage=new TabPage("Błędy");Terminal=new TerminalScreen{Name="commandTerminal",Dock=DockStyle.Fill};terminalPage.Controls.Add(Terminal);OutputText=ResultBox("commandOutput",Color.FromArgb(196,226,211));ErrorText=ResultBox("commandError",Color.FromArgb(255,190,196));outputPage.Controls.Add(OutputText);errorPage.Controls.Add(ErrorText);resultTabs.TabPages.AddRange(new[]{terminalPage,outputPage,errorPage});root.Controls.Add(resultTabs,0,4);
  TableLayoutPanel footer=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,Margin=new Padding(0),Padding=new Padding(0,13,0,0)};footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,134));footer.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,116));root.Controls.Add(footer,0,5);
  Label hint=Ui.Label("Terminal pokazuje wynik i animacje komendy.",8);hint.ForeColor=Ui.Muted;hint.Dock=DockStyle.Fill;hint.Padding=new Padding(0,8,0,0);hint.Margin=new Padding(0);footer.Controls.Add(hint,0,0);
  StopButton=Ui.Button("■  Zatrzymaj",Color.FromArgb(172,72,89));StopButton.Name="commandStop";StopButton.Dock=DockStyle.Fill;StopButton.Margin=new Padding(0,0,8,0);StopButton.Click+=delegate{RequestCancellation(true);};footer.Controls.Add(StopButton,1,0);
  CloseButton=Ui.Button("Zamknij",Ui.Raised);CloseButton.Name="commandClose";CloseButton.Dock=DockStyle.Fill;CloseButton.Margin=new Padding(0);CloseButton.Click+=delegate{Close();};footer.Controls.Add(CloseButton,2,0);CancelButton=CloseButton;
  flushTimer=new System.Windows.Forms.Timer{Interval=33};flushTimer.Tick+=delegate{FlushPending();};flushTimer.Start();
  FormClosing+=delegate{RequestCancellation(false);};
 }

 static RichTextBox ResultBox(string name,Color foreground){return new RichTextBox{Name=name,Dock=DockStyle.Fill,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Ui.Bg,ForeColor=foreground,Font=new Font("Consolas",10),WordWrap=false,DetectUrls=false,HideSelection=false};}

 public void AppendOutput(string chunk){Enqueue(chunk,false);}
 public void AppendError(string chunk){Enqueue(chunk,true);}
 void Enqueue(string chunk,bool error){
  if(string.IsNullOrEmpty(chunk))return;
  lock(gate){
   if(disposed||!accepting)return;
   if(!error){
    terminalReceived=true;
    if(pendingTerminal.Length+chunk.Length>LiveQueueLimit){pendingTerminal.Clear();pendingTerminal.Append("\x1b[0m\x1b[2J\x1b[H");int start=Math.Max(0,chunk.Length-LiveQueueLimit+32);if(start>0&&char.IsLowSurrogate(chunk[start]))start++;pendingTerminal.Append(chunk,start,chunk.Length-start);}
    else pendingTerminal.Append(chunk);
   }
   int accepted=error?errorAccepted:outputAccepted;int take=Math.Min(Math.Max(0,VisibleLimit-accepted),chunk.Length);
   if(take>0)(error?pendingError:pendingOutput).Append(chunk,0,take);
   if(error){errorAccepted+=take;if(take<chunk.Length)errorTruncated=true;}else{outputAccepted+=take;if(take<chunk.Length)outputTruncated=true;}
  }
 }
 void FlushPending(){
  if(disposed||IsDisposed)return;
  string output,error,terminal;Action finish;
  lock(gate){
   output=pendingOutput.ToString();pendingOutput.Clear();error=pendingError.ToString();pendingError.Clear();terminal=pendingTerminal.ToString();pendingTerminal.Clear();
   if(outputTruncated&&!outputNotice){output+=Truncation;outputNotice=true;}if(errorTruncated&&!errorNotice){error+=Truncation;errorNotice=true;}
   finish=pendingFinish;pendingFinish=null;
  }
  if(terminal.Length>0)Terminal.Feed(terminal);AppendText(OutputText,output);AppendText(ErrorText,error);if(finish!=null)finish();
 }
 static void AppendText(RichTextBox box,string text){if(text.Length==0)return;box.AppendText(text);box.SelectionStart=box.TextLength;box.ScrollToCaret();}
 static string Bounded(string value){value=value??"";if(value.Length<=VisibleLimit)return value;int length=VisibleLimit;if(length>0&&char.IsHighSurrogate(value[length-1]))length--;return value.Substring(0,length)+Truncation;}

 public void Complete(CommandResult result){
  if(result==null)throw new ArgumentNullException("result");
  Finish(delegate{if(!terminalReceived){Terminal.ResetScreen();Terminal.Feed(result.Output??"");}OutputText.Text=Bounded(result.Output);ErrorText.Text=Bounded(result.Error);StatusText.Text="Zakończono • kod wyjścia: "+result.ExitCode;StatusText.ForeColor=result.ExitCode==0?Color.FromArgb(134,214,174):Color.FromArgb(255,186,163);if(result.ExitCode!=0&&ErrorText.TextLength>0)resultTabs.SelectedIndex=2;});
 }
 public void Cancelled(){Finish(delegate{StatusText.Text="Zatrzymano polecenie";StatusText.ForeColor=Ui.Muted;});}
 public void Failed(string message){Finish(delegate{string detail=message??"Nie udało się wykonać polecenia.";ErrorText.Text=Bounded(ErrorText.Text+(ErrorText.TextLength>0?Environment.NewLine:"")+detail);resultTabs.SelectedIndex=2;StatusText.Text="Błąd polecenia";StatusText.ForeColor=Color.FromArgb(255,163,173);});}
 void Finish(Action update){
  lock(gate){if(disposed||finishedQueued)return;finishedQueued=true;accepting=false;pendingFinish=delegate{if(disposed||IsDisposed)return;update();running=false;StopButton.Enabled=false;CloseButton.Enabled=true;flushTimer.Stop();};}
  if(Thread.CurrentThread.ManagedThreadId==uiThread)FlushPending();
 }
 void RequestCancellation(bool showStatus){
  lock(gate){if(disposed||!running||cancelRequested)return;cancelRequested=true;}
  if(showStatus&&!IsDisposed){StopButton.Enabled=false;StatusText.Text="Zatrzymywanie polecenia…";StatusText.ForeColor=Ui.Muted;}
  try{if(cancel!=null)cancel();}catch(ObjectDisposedException){}catch(Exception error){if(showStatus&&!IsDisposed){StatusText.Text="Nie udało się zatrzymać polecenia";AppendError(error.Message);}}
 }
 protected override void Dispose(bool disposing){
  if(disposing){RequestCancellation(false);lock(gate){if(!disposed){disposed=true;accepting=false;running=false;pendingOutput.Clear();pendingError.Clear();pendingTerminal.Clear();pendingFinish=null;}}if(flushTimer!=null){flushTimer.Stop();flushTimer.Dispose();}}
  base.Dispose(disposing);
 }
}
}
