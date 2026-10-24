using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public class Sprite {
 public string Name,Costume="robot",Speech="";public double X,Y,Direction,Size=100;public bool Visible=true,Pen;public Color Color=Color.MediumPurple,PenColor=Color.MediumPurple;public float PenSize=3;
 public Sprite Clone(){return (Sprite)MemberwiseClone();}
}
public class UiElement {public Control Control;public Rectangle Bounds;public Block Source;public float FontSize=10;}
public class Stroke {public PointF A,B;public Color Color;public float Width;}
public class RuntimeCanvas:Panel {
 public Size LogicalSize {get;set;}public Dictionary<string,Sprite> Sprites=new Dictionary<string,Sprite>();public Dictionary<string,UiElement> Elements=new Dictionary<string,UiElement>();
 public Dictionary<string,string> Monitors=new Dictionary<string,string>();public List<Stroke> Lines=new List<Stroke>();public List<Sprite> Stamps=new List<Sprite>();
 public Dictionary<string,Image> Images=new Dictionary<string,Image>();public bool Grid,Design;public double MouseX,MouseY;public Action<string> SpriteClicked;public Action BackgroundClicked;
 public Action DesignBegin,DesignEnd;public Action<string> DesignSelected;
 public float ScaleFactor {get{return Math.Max(.05f,Math.Min((float)ClientSize.Width/LogicalSize.Width,(float)ClientSize.Height/LogicalSize.Height));}}
 public PointF Offset {get{float s=ScaleFactor;return new PointF((ClientSize.Width-LogicalSize.Width*s)/2,(ClientSize.Height-LogicalSize.Height*s)/2);}}
 public RuntimeCanvas(){LogicalSize=new Size(640,420);DoubleBuffered=true;BackColor=Color.FromArgb(244,246,252);TabStop=true;SetStyle(ControlStyles.Selectable,true);Resize+=delegate{Arrange();};MouseMove+=delegate(object s,MouseEventArgs e){PointF o=Offset;float z=ScaleFactor;MouseX=(e.X-o.X)/z-LogicalSize.Width/2;MouseY=LogicalSize.Height/2-(e.Y-o.Y)/z;};MouseDown+=OnStageMouse;PreviewKeyDown+=delegate(object s,PreviewKeyDownEventArgs e){e.IsInputKey=true;};}
 void OnStageMouse(object s,MouseEventArgs e){Focus();PointF o=Offset;double x=(e.X-o.X)/ScaleFactor-LogicalSize.Width/2,y=LogicalSize.Height/2-(e.Y-o.Y)/ScaleFactor;MouseX=x;MouseY=y;if(Design)return;Sprite hit=Sprites.Values.Reverse().FirstOrDefault(a=>a.Visible&&Math.Abs(a.X-x)<28*a.Size/100&&Math.Abs(a.Y-y)<28*a.Size/100);if(hit!=null){if(SpriteClicked!=null)SpriteClicked(hit.Name);}else if(BackgroundClicked!=null)BackgroundClicked();}
 public void ClearScene(){foreach(UiElement u in Elements.Values)u.Control.Dispose();Elements.Clear();Sprites.Clear();Monitors.Clear();Lines.Clear();Stamps.Clear();foreach(Image i in Images.Values)i.Dispose();Images.Clear();LogicalSize=new Size(640,420);BackColor=Color.FromArgb(244,246,252);Invalidate();}
 public void Arrange(){float s=ScaleFactor;PointF o=Offset;foreach(UiElement u in Elements.Values){u.Control.Bounds=new Rectangle((int)(o.X+u.Bounds.X*s),(int)(o.Y+u.Bounds.Y*s),Math.Max(1,(int)(u.Bounds.Width*s)),Math.Max(1,(int)(u.Bounds.Height*s)));float fs=Math.Max(6,u.FontSize*s);if(Math.Abs(u.Control.Font.Size-fs)>.2){Font old=u.Control.Font;u.Control.Font=new Font("Segoe UI",fs);if(old!=null&&!old.Equals(SystemFonts.DefaultFont))old.Dispose();}}Invalidate();}
 public void Add(string id,Control c,Rectangle bounds,Block source){if(Elements.ContainsKey(id))throw new Exception("Powtórzony identyfikator GUI: "+id);UiElement u=new UiElement{Control=c,Bounds=bounds,Source=source};Elements.Add(id,u);Controls.Add(c);bool drag=false;Point start=Point.Empty;Rectangle original=Rectangle.Empty;
  c.MouseDown+=delegate(object sender,MouseEventArgs e){if(!Design)return;drag=true;start=Cursor.Position;original=u.Bounds;c.Capture=true;if(DesignBegin!=null)DesignBegin();if(DesignSelected!=null)DesignSelected(id);};
  c.MouseMove+=delegate{if(!Design||!drag)return;Point now=Cursor.Position;u.Bounds=new Rectangle(Math.Max(0,Math.Min(LogicalSize.Width-10,original.X+(int)((now.X-start.X)/ScaleFactor))),Math.Max(0,Math.Min(LogicalSize.Height-10,original.Y+(int)((now.Y-start.Y)/ScaleFactor))),original.Width,original.Height);Arrange();};
  c.MouseUp+=delegate{if(!drag)return;drag=false;c.Capture=false;source.C=u.Bounds.X.ToString(CultureInfo.InvariantCulture);source.D=u.Bounds.Y.ToString(CultureInfo.InvariantCulture);if(DesignEnd!=null)DesignEnd();};Arrange();
 }
 public PointF StagePoint(double x,double y){return new PointF((float)(LogicalSize.Width/2+x),(float)(LogicalSize.Height/2-y));}
 protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);Graphics g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;float s=ScaleFactor;PointF o=Offset;g.TranslateTransform(o.X,o.Y);g.ScaleTransform(s,s);g.SetClip(new Rectangle(0,0,LogicalSize.Width,LogicalSize.Height));
  if(Grid)using(Pen p=new Pen(Color.FromArgb(24,65,85,120),1)){for(int x=0;x<LogicalSize.Width;x+=20)g.DrawLine(p,x,0,x,LogicalSize.Height);for(int y=0;y<LogicalSize.Height;y+=20)g.DrawLine(p,0,y,LogicalSize.Width,y);}
  foreach(Stroke l in Lines)using(Pen p=new Pen(l.Color,l.Width){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawLine(p,StagePoint(l.A.X,l.A.Y),StagePoint(l.B.X,l.B.Y));
  foreach(Sprite a in Stamps)DrawSprite(g,a);foreach(Sprite a in Sprites.Values)if(a.Visible)DrawSprite(g,a);
  int row=9;using(Font f=new Font("Segoe UI",10,FontStyle.Bold))foreach(var v in Monitors){string t=v.Key+": "+v.Value;SizeF z=g.MeasureString(t,f);using(SolidBrush br=new SolidBrush(Color.FromArgb(230,240,174,57)))g.FillRectangle(br,9,row,z.Width+18,27);g.DrawString(t,f,Brushes.White,17,row+4);row+=32;}
  if(Design)using(Pen p=new Pen(Color.FromArgb(100,90,230),1){DashStyle=DashStyle.Dash})foreach(UiElement u in Elements.Values)g.DrawRectangle(p,u.Bounds);
 }
 void DrawSprite(Graphics g,Sprite a){PointF p=StagePoint(a.X,a.Y);GraphicsState state=g.Save();g.TranslateTransform(p.X,p.Y);g.RotateTransform((float)-a.Direction);g.ScaleTransform((float)a.Size/100,(float)a.Size/100);Image img;if(Images.TryGetValue(a.Costume,out img)){g.DrawImage(img,-28,-28,56,56);}else using(SolidBrush br=new SolidBrush(a.Color))using(Pen pen=new Pen(Color.FromArgb(60,30,40,60),2)){
  switch(a.Costume){case "koło":g.FillEllipse(br,-25,-25,50,50);g.DrawEllipse(pen,-25,-25,50,50);break;
  case "kwadrat":g.FillRectangle(br,-25,-25,50,50);g.DrawRectangle(pen,-25,-25,50,50);break;
  case "gwiazda":PointF[] pts=new PointF[10];for(int i=0;i<10;i++){double angle=-Math.PI/2+i*Math.PI/5,r=i%2==0?29:13;pts[i]=new PointF((float)(Math.Cos(angle)*r),(float)(Math.Sin(angle)*r));}g.FillPolygon(br,pts);g.DrawPolygon(pen,pts);break;
  case "rakieta":g.FillPolygon(Brushes.Orange,new Point[]{new Point(-14,17),new Point(0,43),new Point(14,17)});g.FillEllipse(br,-17,-30,34,58);g.FillEllipse(Brushes.White,-8,-15,16,16);g.FillPolygon(br,new Point[]{new Point(-15,5),new Point(-30,25),new Point(-13,20)});g.FillPolygon(br,new Point[]{new Point(15,5),new Point(30,25),new Point(13,20)});break;
  case "kot":g.FillPolygon(br,new Point[]{new Point(-24,-8),new Point(-23,-34),new Point(-4,-21),new Point(8,-21),new Point(26,-34),new Point(25,-5)});g.FillEllipse(br,-28,-24,56,50);g.FillEllipse(Brushes.White,-17,-11,13,17);g.FillEllipse(Brushes.White,6,-11,13,17);g.FillEllipse(Brushes.Black,-12,-6,5,8);g.FillEllipse(Brushes.Black,10,-6,5,8);g.FillEllipse(Brushes.Pink,-3,4,7,5);g.DrawLine(Pens.DimGray,-18,5,-38,0);g.DrawLine(Pens.DimGray,18,5,38,0);break;
  default:g.FillRectangle(br,-24,-21,48,42);g.FillRectangle(br,-16,22,12,9);g.FillRectangle(br,4,22,12,9);using(Pen antenna=new Pen(a.Color,3))g.DrawLine(antenna,0,-20,0,-34);g.FillEllipse(Brushes.Gold,-4,-39,8,8);g.FillRectangle(Brushes.White,-17,-12,13,13);g.FillRectangle(Brushes.White,4,-12,13,13);g.FillRectangle(Brushes.MidnightBlue,-12,-8,5,7);g.FillRectangle(Brushes.MidnightBlue,8,-8,5,7);g.DrawLine(Pens.White,-9,11,9,11);break;
  }}g.Restore(state);
  if(!string.IsNullOrEmpty(a.Speech)){using(Font f=new Font("Segoe UI",11)){string text=a.Speech.Length>180?a.Speech.Substring(0,180)+"…":a.Speech;SizeF size=g.MeasureString(text,f,250);float x=Math.Max(4,Math.Min(LogicalSize.Width-size.Width-20,p.X+22)),y=Math.Max(4,p.Y-55-size.Height);RectangleF r=new RectangleF(x,y,size.Width+18,size.Height+13);g.FillRectangle(Brushes.White,r);g.DrawRectangle(Pens.LightGray,r.X,r.Y,r.Width,r.Height);g.DrawString(text,f,Brushes.DarkSlateGray,new RectangleF(x+9,y+6,size.Width,size.Height));}}
 }
 protected override void Dispose(bool disposing){if(disposing){foreach(Image img in Images.Values)img.Dispose();Images.Clear();}base.Dispose(disposing);}
}

public class ExecContext {public string Sprite;public bool NeedsPause;}
class StopScript:Exception{}
class BreakLoop:Exception{}
public class RuntimeView:UserControl {
 public RuntimeCanvas Stage=new RuntimeCanvas();public RichTextBox Output=new RichTextBox();public ListView Variables=new ListView();public Project Project;public bool Designing,Silent;public bool ShowCommandWindows=true;
 public Action<Block> Highlight;public Action StateChanged;public Action<string,Size> WindowChanged;
 public Dictionary<string,object> Values=new Dictionary<string,object>();public Dictionary<string,List<object>> Lists=new Dictionary<string,List<object>>();
 readonly Dictionary<string,List<Block>> events=new Dictionary<string,List<Block>>();readonly HashSet<Block> activeEvents=new HashSet<Block>();readonly HashSet<string> keys=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
 readonly bool playerMode;readonly Random random=new Random();readonly Stopwatch clock=new Stopwatch();readonly List<System.Windows.Forms.Timer> eventTimers=new List<System.Windows.Forms.Timer>();
 readonly List<CommandWindow> commandWindows=new List<CommandWindow>();
 CancellationTokenSource cts;bool resourcesDisposed;System.Windows.Forms.Timer refresh;Label state;Button flag,stop;CheckBox grid;SplitContainer layout;bool stopped=true;long steps;int nesting;public List<string> Errors=new List<string>();
 public RuntimeView(bool player=false){playerMode=player;Dock=DockStyle.Fill;BackColor=Color.FromArgb(26,32,48);Font=new Font("Segoe UI",10);ForeColor=Color.FromArgb(230,235,248);
  FlowLayoutPanel top=new FlowLayoutPanel{Dock=DockStyle.Top,Height=43,Padding=new Padding(7,6,0,0),WrapContents=false};flag=Ui.Button("▶  Flaga",Color.FromArgb(38,139,99));flag.Size=new Size(100,31);flag.Click+=async delegate{if(Project!=null)await Start(Project);};stop=Ui.Button("■",Color.FromArgb(172,72,89));stop.Size=new Size(37,31);stop.Click+=delegate{Stop();};grid=new CheckBox{Text="Siatka",AutoSize=true,ForeColor=Color.SlateGray,Margin=new Padding(9,7,0,0)};grid.CheckedChanged+=delegate{Stage.Grid=grid.Checked;Stage.Invalidate();};top.Controls.Add(flag);top.Controls.Add(stop);top.Controls.Add(grid);state=new Label{Text="Podgląd gotowy",AutoSize=true,ForeColor=Color.Silver,Margin=new Padding(10,8,0,0),Font=new Font("Segoe UI",8)};top.Controls.Add(state);
  layout=new SplitContainer{Dock=DockStyle.Fill,Orientation=Orientation.Horizontal,Panel1MinSize=120,Panel2MinSize=80,SplitterWidth=5,Size=new Size(500,600),SplitterDistance=395};layout.Panel1.Controls.Add(Stage);
  Stage.Dock=DockStyle.Fill;TabControl tabs=new TabControl{Dock=DockStyle.Fill};TabPage log=new TabPage("Dziennik"),vars=new TabPage("Zmienne i listy");Output.Dock=DockStyle.Fill;Output.ReadOnly=true;Output.BorderStyle=BorderStyle.None;Output.BackColor=Color.FromArgb(20,25,39);Output.ForeColor=Color.FromArgb(185,206,225);Output.Font=new Font("Consolas",9);log.Controls.Add(Output);Variables.Dock=DockStyle.Fill;Variables.View=View.Details;Variables.FullRowSelect=true;Variables.Columns.Add("Nazwa",130);Variables.Columns.Add("Wartość",260);Variables.BackColor=Output.BackColor;Variables.ForeColor=Output.ForeColor;vars.Controls.Add(Variables);tabs.TabPages.Add(log);tabs.TabPages.Add(vars);layout.Panel2.Controls.Add(tabs);if(player)layout.Panel2Collapsed=true;Controls.Add(layout);Controls.Add(top);
  Stage.SpriteClicked=s=>Fire("sprite:"+s,s);Stage.BackgroundClicked=()=>Fire("click",null);Stage.KeyDown+=delegate(object s,KeyEventArgs e){FeedKey(e.KeyCode,true);e.Handled=true;};Stage.KeyUp+=delegate(object s,KeyEventArgs e){FeedKey(e.KeyCode,false);e.Handled=true;};Stage.LostFocus+=delegate{keys.Clear();};
  refresh=new System.Windows.Forms.Timer{Interval=120};refresh.Tick+=delegate{UpdateMonitors();if(StateChanged!=null)StateChanged();};refresh.Start();
 }
 public void Log(string text){if(Output.TextLength>100000)Output.Clear();Output.AppendText(text+Environment.NewLine);Output.SelectionStart=Output.TextLength;Output.ScrollToCaret();}
 public void Reset(Project p){Stop();Project=p;Designing=false;Stage.Design=false;Stage.ClearScene();Values.Clear();Lists.Clear();Errors.Clear();Output.Clear();Variables.Items.Clear();state.Text=p.Blocks.Count==0?"Pusty projekt":"Kliknij flagę, aby uruchomić";}
 public void FeedKey(Keys key,bool down){string k=key==Keys.Return?"Enter":key.ToString();if(down){keys.Add(k);Fire("key:"+k,null);}else keys.Remove(k);}
 public void Stop(){stopped=true;if(cts!=null){cts.Cancel();}foreach(var t in eventTimers){t.Stop();t.Dispose();}eventTimers.Clear();keys.Clear();state.Text="Zatrzymano";if(Highlight!=null)Highlight(null);}
 public async Task Start(Project p){Stop();cts=new CancellationTokenSource();CancellationToken token=cts.Token;Project=p;Designing=false;Stage.Design=false;stopped=false;steps=0;nesting=0;Errors.Clear();Values.Clear();Lists.Clear();events.Clear();activeEvents.Clear();Output.Clear();Stage.ClearScene();clock.Restart();LoadAssets(p);Register(p.Blocks);state.Text="Działa";Log("▶ "+p.Name);try{await Execute(p.Blocks,new ExecContext(),token);if(!token.IsCancellationRequested)state.Text="Gotowe • zdarzenia aktywne";}catch(OperationCanceledException){}catch(StopScript){}catch(BreakLoop){Fail("Bloczek Przerwij pętlę jest poza pętlą.");}catch(Exception e){Fail(e.Message);}if(!resourcesDisposed&&!IsDisposed)UpdateMonitors();}
 void LoadAssets(Project p){foreach(var a in p.Assets){try{byte[] bytes=Convert.FromBase64String(a.Value);using(MemoryStream m=new MemoryStream(bytes))using(Image img=Image.FromStream(m)){if((long)img.Width*img.Height>16000000)throw new Exception("Za duży obraz.");Stage.Images[a.Key]=new Bitmap(img);}}catch(Exception){Log("Nie można wczytać obrazu: "+a.Key);}}}
 public async Task Design(Project p){Stop();Project=p;Designing=true;Stage.Design=true;Values.Clear();Lists.Clear();Stage.ClearScene();LoadAssets(p);Output.Clear();state.Text="Projektant • przeciągaj elementy";cts=new CancellationTokenSource();await DesignWalk(p.Blocks,cts.Token);Stage.Arrange();}
 async Task DesignWalk(List<Block> list,CancellationToken token){foreach(Block b in list){if(b.Disabled||!Project.Allows(Catalog.All[b.Kind].Group))continue;try{if(b.Kind=="event_start")await DesignWalk(b.Children,token);else if(b.Kind=="window"||b.Kind.StartsWith("ui_")&&new[]{"ui_label","ui_button","ui_input","ui_textarea","ui_checkbox","ui_combo","ui_listbox","ui_slider","ui_progress","ui_image"}.Contains(b.Kind)||new[]{"sprite_add","background","set"}.Contains(b.Kind))await One(b,new ExecContext(),token);}catch(Exception e){Log("Projektant: "+e.Message);}}}
 void Register(List<Block> list){foreach(Block b in list){Spec s=Catalog.All[b.Kind];if(b.Disabled||!Project.Allows(s.Group))continue;if(s.Event&&b.Kind!="event_start"){string key=null;switch(b.Kind){case "event_button":key="button:"+b.A;break;case "event_change":key="change:"+b.A;break;case "event_key":key="key:"+b.A;break;case "event_sprite":key="sprite:"+b.A;break;case "event_message":key="message:"+b.A;break;case "event_click":key="click";break;case "event_tick":double interval; if(!double.TryParse(b.A.Replace(',','.'),NumberStyles.Any,CultureInfo.InvariantCulture,out interval)||interval<.02||interval>3600){Log("Nieprawidłowy odstęp zdarzenia czasowego: "+b.A);break;}Block captured=b;var timer=new System.Windows.Forms.Timer{Interval=(int)(interval*1000)};timer.Tick+=delegate{StartEvent(captured,null);};eventTimers.Add(timer);timer.Start();break;}if(key!=null){if(!events.ContainsKey(key))events[key]=new List<Block>();events[key].Add(b);}}else{Register(b.Children);Register(b.Else);}}}
 void Fire(string key,string sprite){if(stopped||Designing)return;List<Block> list;if(events.TryGetValue(key,out list))foreach(Block b in list.ToArray())StartEvent(b,sprite);}
 async void StartEvent(Block block,string sprite){if(cts==null||cts.IsCancellationRequested||activeEvents.Contains(block))return;if(activeEvents.Count>100){Fail("Za dużo równoczesnych zdarzeń.");return;}activeEvents.Add(block);CancellationToken token=cts.Token;try{await Execute(block.Children,new ExecContext{Sprite=sprite},token);}catch(OperationCanceledException){}catch(StopScript){}catch(BreakLoop){Fail("Przerwij pętlę poza pętlą.");}catch(Exception ex){Fail(ex.Message);}finally{activeEvents.Remove(block);}}
 void Fail(string text){if(resourcesDisposed||IsDisposed)return;if(playerMode)layout.Panel2Collapsed=false;Errors.Add(text);Log("BŁĄD: "+text);state.Text="Błąd • zobacz dziennik";Stop();state.Text="Błąd • zobacz dziennik";}
 public static bool UsesStepDelay(Block b){return !Catalog.All[b.Kind].Container&&b.Kind!="comment"&&b.Kind!="break"&&b.Kind!="stop_this"&&b.Kind!="stop";}
 public async Task Execute(List<Block> list,ExecContext context,CancellationToken token){
  foreach(Block b in list){
   token.ThrowIfCancellationRequested();Spec spec=Catalog.All[b.Kind];
   if(b.Disabled||!Project.Allows(spec.Group))continue;
   if(spec.Event&&b.Kind!="event_start")continue;
   bool paced=UsesStepDelay(b);
   if(paced&&context.NeedsPause){
    double seconds=Project.StepDelaySeconds;
    if(double.IsNaN(seconds)||double.IsInfinity(seconds)||seconds<0||seconds>3600)throw new Exception("Odstęp między bloczkami musi wynosić od 0 do 3600 sekund.");
    if(!Silent&&seconds>0)await Task.Delay((int)Math.Ceiling(seconds*1000),token);
    token.ThrowIfCancellationRequested();context.NeedsPause=false;
   }
   if(Highlight!=null)Highlight(b);if(++steps%50==0)await Task.Delay(1,token);
   await One(b,context,token);if(paced)context.NeedsPause=true;
  }
 }

 public object Get(string name){object v;if(Values.TryGetValue(name,out v))return v;if(name=="pi")return Math.PI;if(name=="czas")return clock.Elapsed.TotalSeconds;if(name=="mysz_x")return Stage.MouseX;if(name=="mysz_y")return Stage.MouseY;if(name=="szerokosc")return Stage.LogicalSize.Width;if(name=="wysokosc")return Stage.LogicalSize.Height;throw new Exception("Nie ustawiono zmiennej: "+name);}
 public string TextValue(string s){return Regex.Replace(s??"",@"\{([^{}]+)\}",m=>Convert.ToString(Get(m.Groups[1].Value),CultureInfo.InvariantCulture));}
 public object Value(string s){s=s??"";if(s.TrimStart().StartsWith("="))return new Expression(this,s.TrimStart().Substring(1)).Evaluate();Match m=Regex.Match(s,@"^\{([^{}]+)\}$");if(m.Success)return Get(m.Groups[1].Value);string t=TextValue(s);double n;if(double.TryParse(t.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out n)&&!double.IsInfinity(n)&&!double.IsNaN(n))return n;return t;}
 public static double Number(object value){double n;if(value is bool)return (bool)value?1:0;if(!double.TryParse(Convert.ToString(value,CultureInfo.InvariantCulture).Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out n)||double.IsInfinity(n)||double.IsNaN(n))throw new Exception("Oczekiwano liczby: "+value);return n;}
 public static bool Truth(object v){if(v is bool)return (bool)v;double n;if(double.TryParse(Convert.ToString(v,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out n))return n!=0;return !string.IsNullOrEmpty(Convert.ToString(v))&&!string.Equals(Convert.ToString(v),"false",StringComparison.OrdinalIgnoreCase);}
 void Set(string name,object value){if(!Regex.IsMatch(name??"",@"^[\p{L}_][\p{L}\p{N}_]{0,79}$"))throw new Exception("Nieprawidłowa nazwa zmiennej: "+name);if(value is double&&(double.IsNaN((double)value)||double.IsInfinity((double)value)))throw new Exception("Wynik działania nie jest skończoną liczbą.");Values[name]=value;}
 double N(string s){return Number(Value(s));}int Int(string s,int min,int max){double n=N(s);if(n<min||n>max)throw new Exception("Wartość musi być od "+min+" do "+max+".");return (int)Math.Round(n);}
 Color ColorValue(string s){try{return ColorTranslator.FromHtml(TextValue(s));}catch{throw new Exception("Nieprawidłowy kolor: "+s+". Użyj np. #8b68f6.");}}
 public bool Compare(object a,string op,object b){double x,y;bool numeric=double.TryParse(Convert.ToString(a,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out x)&&double.TryParse(Convert.ToString(b,CultureInfo.InvariantCulture),NumberStyles.Any,CultureInfo.InvariantCulture,out y);int c=numeric?Number(a).CompareTo(Number(b)):string.Compare(Convert.ToString(a),Convert.ToString(b),StringComparison.Ordinal);switch(op){case "==":return c==0;case "!=":return c!=0;case ">":return c>0;case "<":return c<0;case ">=":return c>=0;case "<=":return c<=0;default:throw new Exception("Nieznany operator: "+op);}}
 Sprite SpriteFor(ExecContext c){Sprite s;if(c.Sprite!=null&&Stage.Sprites.TryGetValue(c.Sprite,out s))return s;if(Stage.Sprites.Count==0){s=new Sprite{Name="robot"};Stage.Sprites.Add("robot",s);}else s=Stage.Sprites.Values.First();c.Sprite=s.Name;return s;}
 Sprite NamedSprite(string name){Sprite s;if(!Stage.Sprites.TryGetValue(TextValue(name),out s))throw new Exception("Nie ma duszka: "+name);return s;}
 UiElement Element(string id){UiElement e;if(!Stage.Elements.TryGetValue(TextValue(id),out e))throw new Exception("Nie ma elementu GUI: "+id);return e;}
 List<object> List(string name){List<object> l;if(!Lists.TryGetValue(name,out l))throw new Exception("Najpierw utwórz listę: "+name);return l;}
 int Index(List<object> l,string s){int i=Int(s,1,1000000)-1;if(i>=l.Count)throw new Exception("Nie ma elementu listy o numerze "+(i+1));return i;}
 void MoveSprite(Sprite s,double x,double y){if(Math.Abs(x)>1000000||Math.Abs(y)>1000000)throw new Exception("Pozycja poza zakresem sceny.");if(s.Pen&&Project.Allows("Pisak")){if(Stage.Lines.Count>50000)Stage.Lines.RemoveRange(0,10000);Stage.Lines.Add(new Stroke{A=new PointF((float)s.X,(float)s.Y),B=new PointF((float)x,(float)y),Color=s.PenColor,Width=s.PenSize});}s.X=x;s.Y=y;Stage.Invalidate();}
 void CreateUi(Block b){string id=TextValue(b.A);if(string.IsNullOrWhiteSpace(id))throw new Exception("Wpisz identyfikator elementu GUI.");string text=TextValue(b.B);Control c;switch(b.Kind){case "ui_label":c=new Label{Text=text,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.Transparent,ForeColor=Color.FromArgb(38,46,65)};break;
  case "ui_button":Button bt=new Button{Text=text,FlatStyle=FlatStyle.Flat,BackColor=Color.FromArgb(119,98,231),ForeColor=Color.White,Cursor=Cursors.Hand};bt.FlatAppearance.BorderSize=0;bt.Click+=delegate{Fire("button:"+id,null);};c=bt;break;
  case "ui_input":case "ui_textarea":TextBox tx=new TextBox{Text=text,Multiline=b.Kind=="ui_textarea",BorderStyle=BorderStyle.FixedSingle,ScrollBars=b.Kind=="ui_textarea"?ScrollBars.Vertical:ScrollBars.None};tx.TextChanged+=delegate{Fire("change:"+id,null);};c=tx;break;
  case "ui_checkbox":CheckBox ch=new CheckBox{Text=text,AutoSize=false};ch.CheckedChanged+=delegate{Fire("change:"+id,null);};c=ch;break;
  case "ui_combo":ComboBox combo=new ComboBox{DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.AddRange(text.Split('|'));if(combo.Items.Count>0)combo.SelectedIndex=0;combo.SelectedIndexChanged+=delegate{Fire("change:"+id,null);};c=combo;break;
  case "ui_listbox":ListBox list=new ListBox{IntegralHeight=false};list.Items.AddRange(text.Length==0?new string[0]:text.Split('|'));list.SelectedIndexChanged+=delegate{Fire("change:"+id,null);};c=list;break;
  case "ui_slider":TrackBar slider=new TrackBar{Minimum=0,Maximum=100,TickStyle=TickStyle.None,Value=Math.Max(0,Math.Min(100,(int)N(b.B)))};slider.ValueChanged+=delegate{Fire("change:"+id,null);};c=slider;break;
  case "ui_progress":c=new ProgressBar{Minimum=0,Maximum=100,Value=Math.Max(0,Math.Min(100,(int)N(b.B)))};break;
  case "ui_image":Image image=null;if(text.Length>0&&!Stage.Images.TryGetValue(text,out image))throw new Exception("Zaimportuj obraz w edytorze i wpisz jego identyfikator: "+text);c=new PictureBox{SizeMode=PictureBoxSizeMode.Zoom,Image=image};break;
  default:throw new Exception("Nieznany element GUI.");}
  c.Name=id;if(!(c is Button))c.ForeColor=Color.FromArgb(38,46,65);c.Font=new Font("Segoe UI",10);Stage.Add(id,c,new Rectangle(Int(b.C,0,4000),Int(b.D,0,4000),Int(b.E,10,4000),Int(b.F,10,4000)),b);
 }
 public async Task One(Block b,ExecContext ctx,CancellationToken token){
  switch(b.Kind){
   case "event_start":await Execute(b.Children,ctx,token);break;
   case "window":Stage.LogicalSize=new Size(Int(b.B,200,2400),Int(b.C,150,1600));Stage.BackColor=ColorValue(b.D.Length==0?"#f3f5fb":b.D);Stage.Arrange();if(WindowChanged!=null)WindowChanged(TextValue(b.A),Stage.LogicalSize);break;
   case "ui_label":case "ui_button":case "ui_input":case "ui_textarea":case "ui_checkbox":case "ui_combo":case "ui_listbox":case "ui_slider":case "ui_progress":case "ui_image":CreateUi(b);break;
   case "ui_text":Element(b.A).Control.Text=TextValue(b.B);break;
   case "ui_get":Control ec=Element(b.A).Control;object val=ec.Text;if(ec is CheckBox)val=((CheckBox)ec).Checked?1:0;else if(ec is TrackBar)val=((TrackBar)ec).Value;else if(ec is ProgressBar)val=((ProgressBar)ec).Value;else if(ec is ListBox)val=((ListBox)ec).SelectedItem??"";Set(b.B,val);break;
   case "ui_set":SetUi(Element(b.A).Control,TextValue(b.B),Value(b.C));break;
   case "ui_move":UiElement em=Element(b.A);em.Bounds=new Rectangle(Int(b.B,-4000,4000),Int(b.C,-4000,4000),em.Bounds.Width,em.Bounds.Height);Stage.Arrange();break;
   case "ui_size":UiElement es=Element(b.A);es.Bounds=new Rectangle(es.Bounds.X,es.Bounds.Y,Int(b.B,10,4000),Int(b.C,10,4000));Stage.Arrange();break;
   case "ui_focus":Element(b.A).Control.Focus();break;
   case "ui_item":Control item=Element(b.A).Control;if(item is ListBox)((ListBox)item).Items.Add(TextValue(b.B));else if(item is ComboBox)((ComboBox)item).Items.Add(TextValue(b.B));else throw new Exception("Ten element nie jest listą.");break;
   case "ui_clear":Control clear=Element(b.A).Control;if(clear is ListBox)((ListBox)clear).Items.Clear();else if(clear is ComboBox)((ComboBox)clear).Items.Clear();else clear.Text="";break;
   case "ui_remove":string remove=TextValue(b.A);Element(remove).Control.Dispose();Stage.Elements.Remove(remove);break;
   case "dialog":Log(TextValue(b.A));if(!Silent&&!Designing)MessageBox.Show(FindForm(),TextValue(b.A),TextValue(b.B),MessageBoxButtons.OK,MessageBoxIcon.Information);break;
   case "ask":Set(b.B,Silent?1:(MessageBox.Show(FindForm(),TextValue(b.A),Project.Name,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes?1:0));break;
   case "ask_text":Set(b.B,Silent?TextValue(b.C):Prompt(TextValue(b.A),TextValue(b.C)));break;
   case "set":Set(b.A,Value(b.B));break;
   case "change":Set(b.A,Number(Get(b.A))+N(b.B));break;
   case "random":double lo=Math.Ceiling(N(b.B)),hi=Math.Floor(N(b.C));if(lo>hi||Math.Abs(lo)>1e12||Math.Abs(hi)>1e12)throw new Exception("Nieprawidłowy zakres losowania.");Set(b.A,Math.Floor(random.NextDouble()*(hi-lo+1))+lo);break;
   case "math":Set(b.A,Expression.Arithmetic(N(b.B),b.C,N(b.D)));break;
   case "function":Set(b.A,Expression.Function(b.B,new List<object>{Value(b.C)}));break;
   case "join":Set(b.A,TextValue(b.B)+TextValue(b.C));break;
   case "text_length":Set(b.B,TextValue(b.A).Length);break;
   case "text_replace":string needle=TextValue(b.C);if(needle.Length==0)throw new Exception("Wpisz szukany fragment tekstu.");Set(b.A,TextValue(b.B).Replace(needle,TextValue(b.D)));break;
   case "watch":if(Truth(Value(b.B)))Stage.Monitors[b.A]="";else Stage.Monitors.Remove(b.A);break;
   case "list_create":Lists[b.A]=b.B.Length==0?new List<object>():b.B.Split('|').Select(x=>Value(x)).ToList();break;
   case "list_add":if(List(b.A).Count>=100000)throw new Exception("Lista ma już 100 000 elementów.");List(b.A).Add(Value(b.B));break;
   case "list_get":Set(b.C,List(b.A)[Index(List(b.A),b.B)]);break;
   case "list_set":List(b.A)[Index(List(b.A),b.B)]=Value(b.C);break;
   case "list_remove":List(b.A).RemoveAt(Index(List(b.A),b.B));break;
   case "list_length":Set(b.B,List(b.A).Count);break;
   case "list_contains":Set(b.C,List(b.A).Any(x=>Compare(x,"==",Value(b.B)))?1:0);break;
   case "list_clear":List(b.A).Clear();break;
   case "sprite_add":string name=TextValue(b.A);if(Stage.Sprites.ContainsKey(name))throw new Exception("Taki duszek już istnieje: "+name);if(Stage.Sprites.Count>=200)throw new Exception("Limit 200 duszków.");Stage.Sprites[name]=new Sprite{Name=name,Costume=TextValue(b.B),Color=ColorValue(b.C)};ctx.Sprite=name;Stage.Invalidate();break;
   case "sprite_select":ctx.Sprite=NamedSprite(b.A).Name;break;
   case "costume":SpriteFor(ctx).Costume=TextValue(b.A);Stage.Invalidate();break;
   case "sprite_color":SpriteFor(ctx).Color=ColorValue(b.A);Stage.Invalidate();break;
   case "sprite_size":SpriteFor(ctx).Size=Int(b.A,1,1000);Stage.Invalidate();break;
   case "sprite_show":SpriteFor(ctx).Visible=true;Stage.Invalidate();break;
   case "sprite_hide":SpriteFor(ctx).Visible=false;Stage.Invalidate();break;
   case "say":case "say_for":Sprite speaking=SpriteFor(ctx);string speech=TextValue(b.A);speaking.Speech=speech;Stage.Invalidate();Log(speaking.Name+": "+speech);if(b.Kind=="say_for"){await Delay(b.B,token);if(speaking.Speech==speech)speaking.Speech="";Stage.Invalidate();}break;
   case "background":Stage.BackColor=ColorValue(b.A);break;
   case "move":Sprite mv=SpriteFor(ctx);double angle=mv.Direction*Math.PI/180,dist=N(b.A);MoveSprite(mv,mv.X+Math.Cos(angle)*dist,mv.Y+Math.Sin(angle)*dist);break;
   case "turn":SpriteFor(ctx).Direction=(SpriteFor(ctx).Direction+N(b.A))%360;Stage.Invalidate();break;
   case "direction":SpriteFor(ctx).Direction=N(b.A)%360;Stage.Invalidate();break;
   case "goto":MoveSprite(SpriteFor(ctx),N(b.A),N(b.B));break;
   case "change_x":Sprite sx=SpriteFor(ctx);MoveSprite(sx,sx.X+N(b.A),sx.Y);break;
   case "change_y":Sprite sy=SpriteFor(ctx);MoveSprite(sy,sy.X,sy.Y+N(b.A));break;
   case "glide":Sprite gl=SpriteFor(ctx);double duration=N(b.A),gx=N(b.B),gy=N(b.C),startX=gl.X,startY=gl.Y;if(duration<0||duration>3600)throw new Exception("Czas lotu: od 0 do 3600 s.");if(Silent||duration==0){MoveSprite(gl,gx,gy);break;}Stopwatch glide=Stopwatch.StartNew();while(glide.Elapsed.TotalSeconds<duration){double f=Math.Min(1,glide.Elapsed.TotalSeconds/duration);MoveSprite(gl,startX+(gx-startX)*f,startY+(gy-startY)*f);await Task.Delay(16,token);}MoveSprite(gl,gx,gy);break;
   case "bounce":Sprite bs=SpriteFor(ctx);double edgeX=Math.Max(0,Stage.LogicalSize.Width/2-28*bs.Size/100),edgeY=Math.Max(0,Stage.LogicalSize.Height/2-28*bs.Size/100);if(Math.Abs(bs.X)>=edgeX){bs.X=Math.Max(-edgeX,Math.Min(edgeX,bs.X));bs.Direction=180-bs.Direction;}if(Math.Abs(bs.Y)>=edgeY){bs.Y=Math.Max(-edgeY,Math.Min(edgeY,bs.Y));bs.Direction=-bs.Direction;}Stage.Invalidate();break;
   case "pen_down":SpriteFor(ctx).Pen=true;break;case "pen_up":SpriteFor(ctx).Pen=false;break;
   case "pen_color":SpriteFor(ctx).PenColor=ColorValue(b.A);break;case "pen_size":SpriteFor(ctx).PenSize=Int(b.A,1,50);break;
   case "pen_clear":Stage.Lines.Clear();Stage.Stamps.Clear();Stage.Invalidate();break;
   case "stamp":if(Stage.Stamps.Count>=10000)throw new Exception("Limit 10 000 stempli.");Stage.Stamps.Add(SpriteFor(ctx).Clone());Stage.Invalidate();break;
   case "mouse":Set(b.A,Stage.MouseX);Set(b.B,Stage.MouseY);break;
   case "key_pressed":Set(b.B,keys.Contains(TextValue(b.A))?1:0);break;
   case "sprite_position":Sprite pos=SpriteFor(ctx);Set(b.A,pos.X);Set(b.B,pos.Y);break;
   case "distance":Sprite da=NamedSprite(b.A),db=NamedSprite(b.B);Set(b.C,Math.Sqrt(Math.Pow(da.X-db.X,2)+Math.Pow(da.Y-db.Y,2)));break;
   case "touch_edge":Sprite se=SpriteFor(ctx);Set(b.A,Math.Abs(se.X)+28*se.Size/100>=Stage.LogicalSize.Width/2||Math.Abs(se.Y)+28*se.Size/100>=Stage.LogicalSize.Height/2?1:0);break;
   case "timer":Set(b.A,clock.Elapsed.TotalSeconds);break;case "timer_reset":clock.Restart();break;
   case "repeat":int count=Int(b.A,0,1000000);try{for(int i=0;i<count;i++){token.ThrowIfCancellationRequested();await Execute(b.Children,ctx,token);if(i%40==0)await Task.Delay(1,token);}}catch(BreakLoop){}break;
   case "forever":try{while(true){token.ThrowIfCancellationRequested();await Execute(b.Children,ctx,token);await Task.Delay(16,token);}}catch(BreakLoop){}break;
   case "until":try{while(!Truth(Value(b.A))){token.ThrowIfCancellationRequested();await Execute(b.Children,ctx,token);await Task.Delay(Silent?1:16,token);}}catch(BreakLoop){}break;
   case "for":double first=N(b.B),last=N(b.C),step=N(b.D);if(step==0)throw new Exception("Krok pętli nie może wynosić 0.");if(Math.Abs((last-first)/step)>1000000)throw new Exception("Pętla przekracza milion powtórzeń.");try{for(double i=first;step>0?i<=last:i>=last;i+=step){token.ThrowIfCancellationRequested();Set(b.A,i);await Execute(b.Children,ctx,token);await Task.Delay(1,token);}}catch(BreakLoop){}break;
   case "if":case "if_else":await Execute(Compare(Value(b.A),b.Op,Value(b.B))?b.Children:b.Else,ctx,token);break;
   case "wait":await Delay(b.A,token);break;
   case "wait_until":while(!Truth(Value(b.A)))await Task.Delay(16,token);break;
   case "break":throw new BreakLoop();case "stop_this":throw new StopScript();case "stop":Stop();throw new OperationCanceledException();case "comment":break;
   case "broadcast":if(++nesting>100){nesting--;throw new Exception("Zbyt długi łańcuch wiadomości.");}Fire("message:"+TextValue(b.A),ctx.Sprite);nesting--;break;
   case "beep":int frequency=Int(b.A,37,32767),ms=(int)(N(b.B)*1000);if(ms<0||ms>10000)throw new Exception("Czas tonu: do 10 sekund.");if(!Silent)await Task.Run(()=>{try{Console.Beep(frequency,Math.Max(1,ms));}catch(PlatformNotSupportedException){}},token);break;
   case "sound":if(!Silent){switch(TextValue(b.A)){case "Asterisk":SystemSounds.Asterisk.Play();break;case "Beep":SystemSounds.Beep.Play();break;case "Exclamation":SystemSounds.Exclamation.Play();break;case "Hand":SystemSounds.Hand.Play();break;default:SystemSounds.Question.Play();break;}}break;
   case "cmd":case "powershell":
    if(!Project.Allows("Terminal")||Designing)break;
    foreach(string variable in new[]{b.B,b.C,b.D})if(!string.IsNullOrWhiteSpace(variable)&&!Regex.IsMatch(variable,@"^[\p{L}_][\p{L}\p{N}_]{0,79}$"))throw new Exception("Nieprawidłowa nazwa zmiennej wyniku komendy: "+variable);
    if(Silent){if(!string.IsNullOrWhiteSpace(b.B))Set(b.B,"");if(!string.IsNullOrWhiteSpace(b.C))Set(b.C,"Pominięto polecenie w teście automatycznym.");if(!string.IsNullOrWhiteSpace(b.D))Set(b.D,-1);Log("Terminal: pominięto uruchomienie w teście automatycznym.");break;}
    CommandResult commandResult=await RunCommand(b,token);token.ThrowIfCancellationRequested();
    if(!string.IsNullOrWhiteSpace(b.B))Set(b.B,commandResult.Output);if(!string.IsNullOrWhiteSpace(b.C))Set(b.C,commandResult.Error);if(!string.IsNullOrWhiteSpace(b.D))Set(b.D,commandResult.ExitCode);
    Log((b.Kind=="cmd"?"CMD":"PowerShell")+" • kod wyjścia: "+commandResult.ExitCode);if(commandResult.Output.Length>0)Log(CommandLog(commandResult.Output));if(commandResult.Error.Length>0)Log("Błędy komendy: "+CommandLog(commandResult.Error));break;
   case "file_open":if(Silent)throw new Exception("Wybór pliku wymaga użytkownika.");using(OpenFileDialog of=new OpenFileDialog{Filter="Pliki tekstowe|*.txt;*.csv;*.json;*.js;*.md|Wszystkie pliki|*.*"})Set(b.A,of.ShowDialog(FindForm())==DialogResult.OK?of.FileName:"");break;
   case "file_save":if(Silent)throw new Exception("Wybór pliku wymaga użytkownika.");using(SaveFileDialog sf=new SaveFileDialog{Filter="Plik tekstowy|*.txt|Wszystkie pliki|*.*",FileName=TextValue(b.B),OverwritePrompt=false})Set(b.A,sf.ShowDialog(FindForm())==DialogResult.OK?sf.FileName:"");break;
   case "file_read":string path=TextValue(b.A);if(new FileInfo(path).Length>5*1024*1024)throw new Exception("Plik przekracza 5 MB.");Set(b.B,File.ReadAllText(path,Encoding.UTF8));break;
   case "file_write":string dest=TextValue(b.A);if(Silent)throw new Exception("Zapis plików jest wyłączony w teście automatycznym.");if(!File.Exists(dest)||MessageBox.Show(FindForm(),"Zastąpić plik?\n"+dest,"Zapis pliku",MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes)File.WriteAllText(dest,TextValue(b.B),new UTF8Encoding(false));break;
   case "file_append":if(Silent)throw new Exception("Zapis plików jest wyłączony w teście automatycznym.");File.AppendAllText(TextValue(b.A),TextValue(b.B),new UTF8Encoding(false));break;
   case "file_exists":Set(b.B,File.Exists(TextValue(b.A))?1:0);break;
  }
 }
 async Task<CommandResult> RunCommand(Block block,CancellationToken token){
  string folder=TextValue(block.E);double limit=N(block.F);token.ThrowIfCancellationRequested();
  using(CancellationTokenSource commandCancel=CancellationTokenSource.CreateLinkedTokenSource(token)){
   CommandWindow popup=null;
   if(ShowCommandWindows&&!resourcesDisposed&&!IsDisposed){
    while(commandWindows.Count>=20){CommandWindow old=commandWindows.FirstOrDefault(w=>!w.Running);if(old==null)break;old.Close();old.Dispose();}
    popup=new CommandWindow(block.Kind,block.A,folder,limit,delegate{commandCancel.Cancel();});
    commandWindows.Add(popup);CommandWindow captured=popup;popup.FormClosed+=delegate{commandWindows.Remove(captured);};
    Form owner=FindForm();if(owner!=null&&!owner.IsDisposed)popup.Show(owner);else popup.Show();
   }
   try{
    Action<bool,string> progress=popup==null?(Action<bool,string>)null:delegate(bool error,string chunk){if(error)popup.AppendError(chunk);else popup.AppendOutput(chunk);};
    CommandResult result=await CommandRunner.Run(block.Kind,block.A,folder,limit,commandCancel.Token,progress);token.ThrowIfCancellationRequested();if(popup!=null)popup.Complete(result);return result;
   }catch(OperationCanceledException){if(popup!=null)popup.Cancelled();if(token.IsCancellationRequested)throw;state.Text="Zatrzymano komendę";throw new StopScript();}
   catch(Exception error){if(popup!=null)popup.Failed(error.Message);throw;}
  }
 }
 static string CommandLog(string text){return text.Length<=8192?text:text.Substring(0,8192)+"\r\n… Dalsza część jest w zmiennej wyniku.";}
 async Task Delay(string seconds,CancellationToken token){double n=N(seconds);if(n<0||n>3600)throw new Exception("Czekanie: od 0 do 3600 sekund.");if(!Silent)await Task.Delay((int)(n*1000),token);}
 void SetUi(Control c,string property,object value){switch(property){case "Text":c.Text=Convert.ToString(value,CultureInfo.InvariantCulture);break;case "Value":int n=(int)Number(value);if(c is TrackBar)((TrackBar)c).Value=Math.Max(((TrackBar)c).Minimum,Math.Min(((TrackBar)c).Maximum,n));else if(c is ProgressBar)((ProgressBar)c).Value=Math.Max(0,Math.Min(100,n));else throw new Exception("Ten element nie ma wartości liczbowej.");break;case "Checked":if(!(c is CheckBox))throw new Exception("Element nie jest polem wyboru.");((CheckBox)c).Checked=Truth(value);break;case "Enabled":c.Enabled=Truth(value);break;case "Visible":c.Visible=Truth(value);break;case "BackColor":c.BackColor=ColorValue(Convert.ToString(value));break;case "ForeColor":c.ForeColor=ColorValue(Convert.ToString(value));break;case "FontSize":double fs=Number(value);if(fs<6||fs>96)throw new Exception("Rozmiar czcionki: od 6 do 96.");UiElement match=Stage.Elements.Values.FirstOrDefault(x=>x.Control==c);if(match!=null)match.FontSize=(float)fs;c.Font=new Font("Segoe UI",(float)fs*Stage.ScaleFactor);break;default:throw new Exception("Nieznana właściwość: "+property);}}
 string Prompt(string question,string initial){using(Form f=new Form{Text=Project.Name,ClientSize=new Size(420,165),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){Label l=new Label{Text=question,Left=18,Top=18,Width=385,Height=45};TextBox t=new TextBox{Text=initial,Left=18,Top=70,Width=385};Button ok=new Button{Text="OK",DialogResult=DialogResult.OK,Left=220,Top=112,Width=85},cancel=new Button{Text="Anuluj",DialogResult=DialogResult.Cancel,Left=315,Top=112,Width=85};f.Controls.AddRange(new Control[]{l,t,ok,cancel});f.AcceptButton=ok;f.CancelButton=cancel;return f.ShowDialog(FindForm())==DialogResult.OK?t.Text:"";}}
 public void UpdateMonitors(){foreach(string key in Stage.Monitors.Keys.ToArray()){object v;Stage.Monitors[key]=Values.TryGetValue(key,out v)?Convert.ToString(v,CultureInfo.InvariantCulture):"—";}Stage.Invalidate();Variables.BeginUpdate();Variables.Items.Clear();foreach(var v in Values)Variables.Items.Add(new ListViewItem(new[]{v.Key,Convert.ToString(v.Value,CultureInfo.InvariantCulture)}));foreach(var l in Lists)Variables.Items.Add(new ListViewItem(new[]{l.Key+" [lista]",string.Join(", ",l.Value.Take(30).Select(x=>Convert.ToString(x)).ToArray())}));Variables.EndUpdate();}
 protected override void Dispose(bool disposing){if(disposing&&!resourcesDisposed){resourcesDisposed=true;Stop();foreach(CommandWindow popup in commandWindows.ToArray()){popup.Close();popup.Dispose();}commandWindows.Clear();refresh.Stop();refresh.Dispose();if(cts!=null){cts.Dispose();cts=null;}}base.Dispose(disposing);}
}

// Parser wyrażeń: bez wykonywania kodu użytkownika, procesów ani dostępu do systemu.
public class Expression {
 readonly RuntimeView runtime;readonly string source;int at;
 public Expression(RuntimeView r,string s){runtime=r;source=s;if(s.Count(c=>c=='('||c=='{')>100||s.Count(c=>c=='!'||c=='-'||c=='+'||c=='^')>128)throw new Exception("Za dużo nawiasów w wyrażeniu.");}
 public object Evaluate(){if(source.Length>2000)throw new Exception("Wyrażenie jest za długie (limit 2000 znaków).");object v=Parse(0);Space();if(at<source.Length)throw new Exception("Nieprawidłowe wyrażenie przy: "+source.Substring(at));return v;}
 void Space(){while(at<source.Length&&char.IsWhiteSpace(source[at]))at++;}
 static int Priority(string op){switch(op){case "||":return 1;case "&&":return 2;case "==":case "!=":return 3;case ">":case "<":case ">=":case "<=":return 4;case "+":case "-":return 5;case "*":case "/":case "%":return 6;case "^":return 7;default:return -1;}}
 string Operator(){Space();foreach(string op in new[]{"||","&&","==","!=",">=","<=","+","-","*","/","%","^",">","<"})if(source.Substring(at).StartsWith(op,StringComparison.Ordinal))return op;return "";}
 object Parse(int min){object left=Atom();while(true){string op=Operator();int p=Priority(op);if(p<min)break;at+=op.Length;object right=Parse(p+(op=="^"?0:1));if(op=="&&")left=RuntimeView.Truth(left)&&RuntimeView.Truth(right);else if(op=="||")left=RuntimeView.Truth(left)||RuntimeView.Truth(right);else if(p==3||p==4)left=runtime.Compare(left,op,right);else if(op=="+"&&(left is string||right is string))left=Convert.ToString(left)+Convert.ToString(right);else left=Arithmetic(RuntimeView.Number(left),op,RuntimeView.Number(right));}return left;}
 object Atom(){Space();if(at>=source.Length)throw new Exception("Niedokończone wyrażenie.");char c=source[at];if(c=='-'||c=='+'||c=='!'){at++;object v=Atom();return c=='!'?(object)!RuntimeView.Truth(v):(c=='-'?-RuntimeView.Number(v):RuntimeView.Number(v));}if(c=='('){at++;object v=Parse(0);Space();if(at>=source.Length||source[at++]!=')')throw new Exception("Brakuje nawiasu ).");return v;}if(c=='\''||c=='\"'){char quote=c;at++;StringBuilder b=new StringBuilder();while(at<source.Length&&source[at]!=quote){char t=source[at++];if(t=='\\'&&at<source.Length){t=source[at++];if(t=='n')t='\n';}b.Append(t);}if(at>=source.Length)throw new Exception("Brakuje zamknięcia tekstu.");at++;return b.ToString();}
  if(char.IsDigit(c)||c=='.'){int start=at;while(at<source.Length&&(char.IsDigit(source[at])||source[at]=='.'))at++;return double.Parse(source.Substring(start,at-start),CultureInfo.InvariantCulture);}
  if(c=='{'){int end=source.IndexOf('}',at+1);if(end<0)throw new Exception("Brakuje }.");string key=source.Substring(at+1,end-at-1);at=end+1;return runtime.Get(key);}
  if(char.IsLetter(c)||c=='_'){int start=at;while(at<source.Length&&(char.IsLetterOrDigit(source[at])||source[at]=='_'))at++;string name=source.Substring(start,at-start);Space();if(at<source.Length&&source[at]=='('){at++;List<object> args=new List<object>();Space();if(at>=source.Length)throw new Exception("Niedokończona funkcja.");if(source[at]!=')')while(true){args.Add(Parse(0));Space();if(at<source.Length&&source[at]==','){at++;continue;}break;}if(at>=source.Length||source[at++]!=')')throw new Exception("Brakuje nawiasu funkcji.");return Function(name,args);}if(name=="true")return true;if(name=="false")return false;return runtime.Get(name);}
  throw new Exception("Nieprawidłowy znak w wyrażeniu: "+c);
 }
 public static double Arithmetic(double a,string op,double b){switch(op){case "+":return a+b;case "-":return a-b;case "*":return a*b;case "/":if(b==0)throw new Exception("Dzielenie przez zero.");return a/b;case "%":if(b==0)throw new Exception("Dzielenie modulo przez zero.");return a%b;case "^":return Math.Pow(a,b);default:throw new Exception("Nieznane działanie: "+op);}}
 public static object Function(string f,List<object> args){if(f=="len"){if(args.Count!=1)throw new Exception("len wymaga jednego argumentu.");return Convert.ToString(args[0]).Length;}if(f=="min"||f=="max"){if(args.Count<1)throw new Exception("Funkcja wymaga argumentów.");return f=="min"?args.Select(x=>RuntimeView.Number(x)).Min():args.Select(x=>RuntimeView.Number(x)).Max();}if(args.Count!=1)throw new Exception("Funkcja "+f+" wymaga jednego argumentu.");double n=RuntimeView.Number(args[0]);switch(f){case "sqrt":if(n<0)throw new Exception("Pierwiastek z liczby ujemnej.");return Math.Sqrt(n);case "abs":return Math.Abs(n);case "round":return Math.Round(n,MidpointRounding.AwayFromZero);case "floor":return Math.Floor(n);case "ceil":return Math.Ceiling(n);case "sin":return Math.Sin(n*Math.PI/180);case "cos":return Math.Cos(n*Math.PI/180);case "tan":return Math.Tan(n*Math.PI/180);case "log":if(n<=0)throw new Exception("Logarytm wymaga dodatniej liczby.");return Math.Log(n);default:throw new Exception("Nieznana funkcja: "+f);}}
}
}
