using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class AllFeatureTests {
 public static int Suite(string directory){Directory.CreateDirectory(directory);int exit=0;List<string> lines=new List<string>();using(Form form=new Form{ShowInTaskbar=false,WindowState=FormWindowState.Minimized})using(RuntimeView runtime=new RuntimeView()){runtime.ShowCommandWindows=false;form.Controls.Add(runtime);form.Shown+=async delegate{try{await Run(runtime,lines,directory);await StandaloneTests.Run(lines,directory);}catch(Exception ex){exit=1;lines.Add("FAIL "+ex);}File.WriteAllLines(Path.Combine(directory,"results.txt"),lines,Encoding.UTF8);form.Close();};Application.Run(form);}return exit;}
 sealed class Answers:RuntimeDialogs {
  public bool Yes=true;public string Text="odpowiedź ż",File="";public int Messages,Questions,Choices;
  public override void Inform(IWin32Window owner,string text,string title){Messages++;}
  public override bool Confirm(IWin32Window owner,string text,string title){Questions++;return Yes;}
  public override string ChooseFile(IWin32Window owner,bool save,string initial,string folder){Choices++;return File;}
  public override string AskText(IWin32Window owner,string title,string question,string initial){return Text;}
 }
 static Block B(string k,params string[] a){return Block.Make(k,a);}
 static void Check(bool ok,string message){if(!ok)throw new Exception("Pełny przegląd: "+message);}
 static double N(RuntimeView r,string name){return RuntimeView.Number(r.Get(name));}
 static void Event(Control control,string name,EventArgs args){typeof(Control).GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic).Invoke(control,new object[]{args});}
 static IEnumerable<Control> Desc(Control root){foreach(Control c in root.Controls){yield return c;foreach(Control child in Desc(c))yield return child;}}
 public static async Task Run(RuntimeView r,List<string> results,string directory){
  string folder=Path.Combine(Path.GetFullPath(directory),"Wszystkie funkcje ż "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  HashSet<string> covered=new HashSet<string>();List<string> matrix=new List<string>();
  bool originalSilent=r.Silent;RuntimeDialogs originalDialogs=r.Dialogs;Action<Block> originalHighlight=r.Highlight;Answers answers=new Answers();
  r.Silent=false;r.Dialogs=answers;r.Highlight=b=>{if(b!=null)covered.Add(b.Kind);};
  Func<Block[],Project> project=blocks=>{Project p=new Project{Name="test wszystkich funkcji",SourcePath=Path.Combine(folder,"projekt.js")};foreach(string group in Catalog.Groups)p.Features[group]=true;p.Blocks.AddRange(blocks);return p;};
  Func<Project,Task> start=async p=>{await r.Start(p);Check(r.Errors.Count==0,string.Join("; ",r.Errors));};
  Exception failure=null;
  try{
   await start(project(new[]{B("set","n","2,5"),B("change","n","1,5"),B("math","mul","{n}","*","3"),B("function","sqrt","sqrt","25"),B("random","los","4","4"),B("join","tekst","Cześć ","ż"),B("text_length","{tekst}","len"),B("text_replace","replace","Ala ma kota","kota","psa"),B("watch","n","1"),B("comment","notatka")}));
   r.UpdateMonitors();Check(N(r,"n")==4&&N(r,"mul")==12&&N(r,"sqrt")==5&&N(r,"los")==4&&N(r,"len")==7&&(string)r.Get("replace")=="Ala ma psa"&&r.Stage.Monitors["n"]=="4","zmienne/tekst/losowanie/monitor");
   foreach(string op in new[]{"+","-","*","/","%","^"}){await r.One(B("math","m","8",op,"2"),new ExecContext(),CancellationToken.None);Check(N(r,"m")==new Dictionary<string,double>{{"+",10},{"-",6},{"*",16},{"/",4},{"%",0},{"^",64}}[op],"operator "+op);}
   foreach(var f in new Dictionary<string,double>{{"sqrt",2},{"abs",4},{"round",4},{"floor",4},{"ceil",4},{"sin",Math.Sin(4*Math.PI/180)},{"cos",Math.Cos(4*Math.PI/180)},{"tan",Math.Tan(4*Math.PI/180)},{"log",Math.Log(4)}}){await r.One(B("function","f",f.Key,"4"),new ExecContext(),CancellationToken.None);Check(Math.Abs(N(r,"f")-f.Value)<1e-8,"funkcja "+f.Key);}
   await r.One(B("watch","n","0"),new ExecContext(),CancellationToken.None);Check(!r.Stage.Monitors.ContainsKey("n"),"ukrycie monitora");
   Check(N(r,"n")==4&&RuntimeView.Number(r.Value("=min(4, 9) + max(1, 6) + len('abc')"))==13,"parser funkcji i priorytetów");
   results.Add("PASS wszystkie zmienne i teksty; sześć działań, dziewięć funkcji, wyrażenia, losowanie i monitory");

   await start(project(new[]{B("list_create","l","A|B"),B("list_add","l","C"),B("list_get","l","2","item"),B("list_set","l","2","D"),B("list_contains","l","D","has"),B("list_remove","l","1"),B("list_length","l","count"),B("list_clear","l")}));
   Check((string)r.Get("item")=="B"&&N(r,"has")==1&&N(r,"count")==2&&r.Lists["l"].Count==0,"osiem operacji na listach");results.Add("PASS wszystkie operacje na listach: tworzenie, dodawanie, odczyt, zmiana, usunięcie, długość, wyszukiwanie, czyszczenie");

   Block begin=B("event_start");begin.Children.Add(B("set","n","0"));Block repeat=B("repeat","3");repeat.Children.Add(B("change","n","1"));Block loop=B("for","i","5","1","-1");loop.Children.Add(B("change","n","{i}"));Block until=B("until","=n >= 20");until.Children.Add(B("change","n","1"));
   Block condition=B("if","{n}","20");condition.Children.Add(B("set","if_ok","1"));Block alternative=B("if_else","{n}","0");alternative.Op="==";alternative.Children.Add(B("set","else_ok","0"));alternative.Else.Add(B("set","else_ok","1"));Block forever=B("forever");forever.Children.Add(B("change","n","1"));forever.Children.Add(B("break"));
   await start(project(new[]{begin,repeat,loop,until,condition,alternative,forever,B("wait","0.03"),B("wait_until","=n == 21")}));Check(N(r,"n")==21&&N(r,"if_ok")==1&&N(r,"else_ok")==1,"pętle/gałęzie/przerwanie");
   foreach(string op in new[]{"==","!=",">","<",">=","<="})Check(r.Compare(2,op,3)==new Dictionary<string,bool>{{"==",false},{"!=",true},{">",false},{"<",true},{">=",false},{"<=",true}}[op],"porównanie "+op);
   await start(project(new[]{B("set","before","1"),B("stop_this"),B("set","after","1")}));Check(N(r,"before")==1&&!r.Values.ContainsKey("after"),"stop_this");
   await start(project(new[]{B("set","before","1"),B("stop"),B("set","after","1")}));Check(!r.Values.ContainsKey("after"),"stop wszystko");results.Add("PASS wszystkie bloczki sterowania, obie gałęzie, pętle rosnące/malejące, przerwanie, czekanie i oba zatrzymania");

   Project gui=project(new[]{B("window","Test GUI","640","420","#ffffff")});
   using(Bitmap image=new Bitmap(5,5)){image.SetPixel(0,0,Color.Red);using(MemoryStream stream=new MemoryStream()){image.Save(stream,System.Drawing.Imaging.ImageFormat.Png);gui.Assets["obraz"]=Convert.ToBase64String(stream.ToArray());}}
   foreach(string kind in new[]{"ui_label","ui_button","ui_input","ui_textarea","ui_checkbox","ui_combo","ui_listbox","ui_slider","ui_progress","ui_image"})gui.Blocks.Add(B(kind,kind,kind=="ui_image"?"obraz":kind=="ui_slider"||kind=="ui_progress"?"25":kind=="ui_combo"||kind=="ui_listbox"?"A|B":"Tekst","10","10","120","40"));
   gui.Blocks.AddRange(new[]{B("ui_text","ui_label","Nowy napis"),B("ui_get","ui_label","label"),B("ui_set","ui_checkbox","Checked","1"),B("ui_get","ui_checkbox","checked"),B("ui_set","ui_slider","Value","72"),B("ui_get","ui_slider","slider"),B("ui_set","ui_progress","Value","63"),B("ui_get","ui_progress","progress"),B("ui_get","ui_combo","combo"),B("ui_move","ui_button","70","90"),B("ui_size","ui_button","180","55"),B("ui_focus","ui_input"),B("ui_item","ui_listbox","C"),B("ui_item","ui_combo","C")});
   await start(gui);Check(r.Stage.Elements.Count==10&&r.Stage.Images.ContainsKey("obraz")&&((PictureBox)r.Stage.Elements["ui_image"].Control).Image!=null,"dziesięć kontrolek/import obrazu");
   Check((string)r.Get("label")=="Nowy napis"&&N(r,"checked")==1&&N(r,"slider")==72&&N(r,"progress")==63&&(string)r.Get("combo")=="A","odczyty kontrolek");
   Check(r.Stage.Elements["ui_button"].Bounds==new Rectangle(70,90,180,55)&&((ListBox)r.Stage.Elements["ui_listbox"].Control).Items.Count==3&&((ComboBox)r.Stage.Elements["ui_combo"].Control).Items.Count==3,"rozmiar/pozycja/dodanie pozycji");
   Check(r.Stage.Elements["ui_input"].Control.CanSelect,"pole fokusu nie jest wybieralne");
   foreach(var prop in new Dictionary<string,string>{{"Text","Zmieniony"},{"Enabled","0"},{"Visible","0"},{"BackColor","#123456"},{"ForeColor","#abcdef"},{"FontSize","18"}})await r.One(B("ui_set","ui_button",prop.Key,prop.Value),new ExecContext(),CancellationToken.None);
   Control button=r.Stage.Elements["ui_button"].Control;Check(button.Text=="Zmieniony"&&!button.Enabled&&!button.Visible&&button.BackColor.ToArgb()==ColorTranslator.FromHtml("#123456").ToArgb()&&r.Stage.Elements["ui_button"].FontSize==18,"wszystkie właściwości GUI");
   foreach(string id in new[]{"ui_input","ui_textarea","ui_listbox","ui_combo"}){await r.One(B("ui_clear",id),new ExecContext(),CancellationToken.None);covered.Add("ui_clear");}
   Check(r.Stage.Elements["ui_textarea"].Control.Text==""&&((ListBox)r.Stage.Elements["ui_listbox"].Control).Items.Count==0&&((ComboBox)r.Stage.Elements["ui_combo"].Control).Items.Count==0,"czyszczenie GUI");
   await r.One(B("ui_remove","ui_image"),new ExecContext(),CancellationToken.None);covered.Add("ui_remove");Check(!r.Stage.Elements.ContainsKey("ui_image"),"usunięcie GUI");results.Add("PASS wszystkie kontrolki GUI, import PNG, odczyty i właściwości, pozycja, rozmiar, żądanie fokusu, listy, czyszczenie i usuwanie");

   Project events=project(new[]{B("ui_button","btn"),B("ui_input","input"),B("sprite_add","robot"),B("set","button_n","0"),B("set","change_n","0"),B("set","key_n","0"),B("set","sprite_n","0"),B("set","click_n","0"),B("set","message_n","0"),B("set","tick_n","0")});
   string[] eventKinds={"event_button","event_change","event_key","event_sprite","event_click","event_message","event_tick"},eventArgs={"btn","input","Right","robot","","test","0.02"},variables={"button_n","change_n","key_n","sprite_n","click_n","message_n","tick_n"};
   for(int i=0;i<eventKinds.Length;i++){Block evt=B(eventKinds[i],eventArgs[i]);evt.Children.Add(B("change",variables[i],"1"));events.Blocks.Add(evt);}
   await start(events);((Button)r.Stage.Elements["btn"].Control).PerformClick();r.Stage.Elements["input"].Control.Text="zmiana";
   Event(r.Stage,"OnKeyDown",new KeyEventArgs(Keys.Right));await r.One(B("key_pressed","Right","pressed"),new ExecContext(),CancellationToken.None);covered.Add("key_pressed");Check(N(r,"pressed")==1,"czujnik klawisza");Event(r.Stage,"OnKeyUp",new KeyEventArgs(Keys.Right));
   PointF origin=r.Stage.Offset;float scale=r.Stage.ScaleFactor;int centerX=(int)(origin.X+320*scale),centerY=(int)(origin.Y+210*scale);
   Event(r.Stage,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,centerX,centerY,0));Event(r.Stage,"OnMouseDown",new MouseEventArgs(MouseButtons.Left,1,(int)(origin.X+40*scale),(int)(origin.Y+80*scale),0));
   await r.One(B("broadcast","test"),new ExecContext(),CancellationToken.None);covered.Add("broadcast");await Task.Delay(80);
   for(int i=0;i<eventKinds.Length;i++){Check(N(r,variables[i])>=1,"zdarzenie "+eventKinds[i]);covered.Add(eventKinds[i]);}r.Stop();results.Add("PASS wszystkie osiem zdarzeń: start, przycisk, zmiana pola, klawisz, duszek, tło, wiadomość i zegar (prawdziwe sygnały kontrolek)");

   await start(project(new[]{B("sprite_add","robot","robot","#ff0000"),B("sprite_add","cel","gwiazda","#00ff00"),B("goto","30","40"),B("sprite_select","robot"),B("goto","0","0"),B("distance","robot","cel","d"),B("direction","90"),B("move","10"),B("turn","90"),B("change_x","5"),B("change_y","2"),B("glide","0.03","20","30"),B("sprite_position","x","y"),B("sprite_color","#123456"),B("sprite_size","150"),B("costume","kot"),B("sprite_hide")}));
   Check(N(r,"d")==50&&N(r,"x")==20&&N(r,"y")==30&&!r.Stage.Sprites["robot"].Visible&&r.Stage.Sprites["robot"].Size==150&&r.Stage.Sprites["robot"].Costume=="kot","pozycje/ruch/wygląd");
   ExecContext context=new ExecContext{Sprite="robot"};await r.Execute(new List<Block>{B("sprite_show"),B("say","Cześć"),B("say_for","Chwila","0.02"),B("background","#334455"),B("goto","1000","1000"),B("touch_edge","edge"),B("bounce"),B("pen_color","#abcdef"),B("pen_size","7"),B("pen_down"),B("move","5"),B("pen_up"),B("move","5"),B("stamp")},context,CancellationToken.None);
   Check(N(r,"edge")==1&&r.Stage.Lines.Count==1&&r.Stage.Stamps.Count==1&&r.Stage.Sprites["robot"].Visible&&r.Stage.Sprites["robot"].Speech==""&&r.Stage.BackColor==ColorTranslator.FromHtml("#334455"),"krawędzie/odbicie/dymki/pisak");
   Check(r.Stage.Lines[0].Width==7&&r.Stage.Lines[0].Color==ColorTranslator.FromHtml("#abcdef"),"kolor/grubość pisaka");
   foreach(string costume in new[]{"robot","kot","rakieta","koło","kwadrat","gwiazda"}){await r.One(B("costume",costume),context,CancellationToken.None);using(Bitmap shot=new Bitmap(Math.Max(1,r.Stage.Width),Math.Max(1,r.Stage.Height)))r.Stage.DrawToBitmap(shot,r.Stage.ClientRectangle);}
   await r.Execute(new List<Block>{B("pen_clear"),B("timer_reset"),B("wait","0.03"),B("timer","elapsed"),B("mouse","mx","my")},context,CancellationToken.None);
   Check(r.Stage.Lines.Count==0&&r.Stage.Stamps.Count==0&&N(r,"elapsed")>=.02&&Math.Abs(N(r,"mx")-r.Stage.MouseX)<1e-8&&Math.Abs(N(r,"my")-r.Stage.MouseY)<1e-8,"czyszczenie/stoper/mysz: linie="+r.Stage.Lines.Count+", stemple="+r.Stage.Stamps.Count+", czas="+N(r,"elapsed")+", mx="+N(r,"mx")+"/"+r.Stage.MouseX+", my="+N(r,"my")+"/"+r.Stage.MouseY);results.Add("PASS wszystkie bloczki ruchu, wyglądu, sześć kostiumów, czujniki, pisak i stemple; animowany lot i dymek z rzeczywistym czasem");

   await start(project(new[]{B("dialog","Informacja","Test"),B("ask","Tak?","answer"),B("ask_text","Tekst?","text","domyślny")}));Check(answers.Messages==1&&N(r,"answer")==1&&(string)r.Get("text")==answers.Text,"odpowiedzi dialogów");answers.Yes=false;answers.Text="";await start(project(new[]{B("ask","Nie?","answer"),B("ask_text","Anuluj?","text","")}));Check(N(r,"answer")==0&&(string)r.Get("text")=="","odmowa/anulowanie");
   results.Add("PASS logika trzech dialogów: OK, TAK/NIE, odpowiedź i anulowanie; odpowiedzi podstawione przez adapter testowy, bez klikania okien użytkownika");

   string txt=Path.Combine(folder,"notatka.txt");answers.File=txt;answers.Yes=true;
   await start(project(new[]{B("file_save","plik","notatka.txt"),B("file_write","{plik}","Zażółć gęślą jaźń"),B("file_append","notatka.txt"," + dopisek"),B("file_open","odczyt"),B("file_read","{odczyt}","tekst"),B("file_exists","notatka.txt","exists"),B("file_exists","brak.txt","missing")}));
   Check((string)r.Get("tekst")=="Zażółć gęślą jaźń + dopisek"&&N(r,"exists")==1&&N(r,"missing")==0&&answers.Choices==2,"wszystkie operacje plikowe/rzeczywisty UTF8");
   Check(((string)r.Get("pulpit"))==Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory)&&((string)r.Get("folder_projektu"))==folder,"stałe ścieżki");
   Check(RuntimeFiles.Resolve(r.Project,"%USERPROFILE%\\notatka.txt")==Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"notatka.txt"),"rozwijanie zmiennych środowiskowych");
   Check(RuntimeFiles.CommandFolder(r.Project,"\".\"")==folder,"względny folder roboczy komendy w cudzysłowach");
   answers.Yes=false;await start(project(new[]{B("file_write",txt,"nie zastępuj")}));Check(File.ReadAllText(txt).Contains("dopisek"),"odmowa zastąpienia pliku");answers.Yes=true;await start(project(new[]{B("file_write",txt,"nowy tekst")}));Check(File.ReadAllText(txt)=="nowy tekst","zastąpienie pliku");
   await start(project(new[]{B("file_write","\""+txt+"\"","cytat"),B("file_read",txt,"tekst")}));Check((string)r.Get("tekst")=="cytat","ścieżka w cudzysłowach");
   foreach(string invalid in new[]{"",folder,Path.Combine(folder,"brak-folderu","plik.txt")}){await r.Start(project(new[]{B("file_write",invalid,"błędna ścieżka")}));Check(r.Errors.Count==1&&r.Errors[0].Length>30,"czytelny błąd ścieżki: "+invalid);}
   answers.File="";await start(project(new[]{B("file_save","cancelled","notatka.txt"),B("file_open","cancelled_open")}));Check((string)r.Get("cancelled")==""&&(string)r.Get("cancelled_open")=="","anulowanie wyboru pliku");
   results.Add("PASS wszystkie sześć bloczków plików: rzeczywisty zapis, dopisanie, odczyt UTF-8, istnienie; zastąpienie TAK/NIE, nazwy względne, cudzysłowy i błędne ścieżki; wybór/anulowanie przez adapter");

   await start(project(new[]{B("cmd","cd","cmd","err","code","","10"),B("powershell","(Get-Location).Path","ps","ps_err","ps_code","","15")}));Check(((string)r.Get("cmd")).Trim()==folder&&((string)r.Get("ps")).Trim()==folder&&N(r,"code")==0&&N(r,"ps_code")==0,"domyślny folder obu komend musi być folderem projektu");results.Add("PASS CMD i PowerShell: domyślny folder poleceń jest folderem projektu, spójnym z plikami TXT");
   await start(project(new[]{B("beep","440","0.02"),B("sound","Beep")}));results.Add("PASS wywołanie obu API dźwięku; słyszalność zależy od sprzętu i ustawień Windows");

   using(Editor editor=new Editor("")){
    editor.Project=project(new Block[0]);editor.Project.Features["Terminal"]=false;editor.Project.Features["Pliki"]=false;editor.Render();
    foreach(string kind in new[]{"cmd","file_write"}){DataObject data=new DataObject();data.SetData(typeof(DragInfo),new DragInfo{Kind=kind});editor.Drop(new DragEventArgs(data,1,0,0,DragDropEffects.Copy,DragDropEffects.None),editor.Project.Blocks,editor.Project.Blocks.Count);}
    Check(editor.Project.Allows("Terminal")&&editor.Project.Allows("Pliki"),"nowy bloczek nie włączył kategorii");
    typeof(Editor).GetMethod("Undo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Check(!editor.Project.Allows("Pliki")&&editor.Project.SourcePath==Path.Combine(folder,"projekt.js"),"cofanie włączenia kategorii lub zachowanie folderu projektu");
    typeof(Editor).GetMethod("Redo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);Check(editor.Project.Allows("Pliki")&&editor.Project.SourcePath==Path.Combine(folder,"projekt.js"),"ponawianie i folder projektu");
    editor.Project.Features["Terminal"]=false;editor.Render();Check(Desc(editor).OfType<Button>().Any(b=>b.Name=="enableMissingFeatures"),"brak widocznego ostrzeżenia o pomijaniu komend");
    editor.EnableGroups(new[]{"Terminal"});Check(editor.Project.Allows("Terminal"),"przycisk włączenia kategorii");
   }
   Project disabled=project(new[]{B("cmd","echo nie"),B("file_write",Path.Combine(folder,"nie.txt"),"nie")});disabled.Features["Terminal"]=false;disabled.Features["Pliki"]=false;await start(disabled);Check(!File.Exists(Path.Combine(folder,"nie.txt"))&&r.Output.Text.Contains("POMINIĘTE KATEGORIE"),"wyłączenie funkcji lub komunikat");results.Add("PASS dodawanie CMD/Plików przez upuszczenie włącza kategorię; ręczne wyłączenie nadal działa i pokazuje komunikat oraz przycisk naprawy");

   string[] missing=Catalog.All.Keys.Where(k=>!covered.Contains(k)).ToArray();Check(missing.Length==0,"nieprzetestowane bloczki: "+string.Join(", ",missing));
   foreach(Spec spec in Catalog.All.Values)matrix.Add("PASS | "+spec.Group+" | "+spec.Kind+" | "+spec.Name);
   File.WriteAllLines(Path.Combine(directory,"pokrycie-bloczkow.txt"),matrix,Encoding.UTF8);results.Add("PASS pokrycie wykonania: "+covered.Count+" / "+Catalog.All.Count+" bloczków, wszystkie "+Catalog.Groups.Length+" kategorii; osobna lista pokrycia");
  }catch(Exception ex){failure=ex;}
  r.Stop();r.Silent=originalSilent;r.Dialogs=originalDialogs;r.Highlight=originalHighlight;
  if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
 }
}
}
