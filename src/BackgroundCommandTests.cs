using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class BackgroundCommandTests {
 static void Check(bool value,string text){if(!value)throw new Exception("Komendy w tle: "+text);}
 static string Ps(string s){return "'"+s.Replace("'","''")+"'";}
 static Block B(string kind,params string[] args){return Block.Make(kind,args);}
 static async Task Until(Func<bool> condition,int limit,string text){Stopwatch watch=Stopwatch.StartNew();while(!condition()){if(watch.ElapsedMilliseconds>limit)throw new Exception("Komendy w tle: timeout "+text);await Task.Delay(20);}}
 static Project ProjectAt(string folder){Project p=new Project{SourcePath=Path.Combine(folder,"projekt.js")};p.Features["Terminal"]=true;p.Features["Pliki"]=true;return p;}
 static Block Command(string script,string timeout="10"){Block block=B("powershell",script,"out","err","code","",timeout);block.RunInBackground=true;return block;}
 public static async Task Run(List<string> results,string directory){
  string folder=Path.Combine(Path.GetFullPath(directory),"Komendy w tle "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  Check(!B("cmd").RunInBackground,"nowe polecenie powinno domyślnie czekać");Project old=ProjectIO.FromJson("{\"Version\":2,\"Blocks\":[{\"Kind\":\"cmd\",\"A\":\"echo test\"}]}");Check(!old.Blocks[0].RunInBackground,"zgodność starszych projektów");
  Project roundtrip=ProjectAt(folder);roundtrip.Blocks.Add(Command("Write-Output 'ok'"));Check(ProjectIO.Parse(ProjectIO.Export(roundtrip,Application.ExecutablePath)).Blocks[0].RunInBackground,"zapis .js zgubił tryb w tle");Check(roundtrip.Blocks[0].Get("G")=="W tle","pole edytora");roundtrip.Blocks[0].Set("G","Czekaj");Check(!roundtrip.Blocks[0].RunInBackground,"przełącznik Czekaj");
  Check(Examples.Terminal().Blocks.SelectMany(x=>x.Children).Any(b=>b.Kind=="cmd"&&b.A=="curl parrot.live"&&b.RunInBackground),"przykład papugi nadal blokuje");results.Add("PASS komendy w tle: wybór Czekaj/W tle, zgodność starych projektów, zapis .js i przykład papugi");
  foreach(bool player in new[]{false,true})await RuntimeCases(player,folder,results);
  await Exported(folder,results);
 }
 static async Task RuntimeCases(bool player,string folder,List<string> results){
  using(Form host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000),Size=new Size(800,600)})using(RuntimeView runtime=new RuntimeView(player)){
   host.Controls.Add(runtime);host.Show();runtime.Silent=false;Task active=null;Exception failure=null;
   try{
    string prefix=player?"odtwarzacz":"podglad",waiting=Path.Combine(folder,prefix+"-czekaj.txt");Project p=ProjectAt(folder);Block wait=Command("Start-Sleep -Seconds 5");wait.RunInBackground=false;p.Blocks.Add(wait);p.Blocks.Add(B("file_write",waiting,"NIE"));active=runtime.Start(p);await Task.Delay(100);Check(!active.IsCompleted&&!File.Exists(waiting),"Czekaj nie wstrzymał następnego bloczka");runtime.Stop();await active;Check(!File.Exists(waiting),"Stop wykonał blok po komendzie czekającej");
    results.Add("PASS "+prefix+": odtworzenie przyczyny — komenda Czekaj blokuje zapis, a Stop przerywa skrypt");

    string target=Path.Combine(folder,prefix+"-notatka.txt"),text="Zażółć gęślą jaźń\r\nDrugi wiersz\r\nUTF-8 ✓";
    p=ProjectAt(folder);p.Blocks.Add(Command("[Console]::Out.Write('START'); Start-Sleep -Milliseconds 1000; [Console]::Out.Write(' END')"));p.Blocks.Add(B("file_write",target,text));
    active=runtime.Start(p);await active;Check(File.ReadAllText(target)==text&&runtime.ActiveBackgroundCommands==1,"TXT nie zapisał się podczas aktywnej komendy");Check(runtime.LastSavedPath==target&&runtime.Output.Text.Contains("Zapisano plik: "+target),"brak potwierdzenia zapisu");
    await Until(()=>runtime.ActiveBackgroundCommands==0,15000,"wynik komendy");Check(Convert.ToString(runtime.Get("out"))=="START END"&&RuntimeView.Number(runtime.Get("code"))==0&&runtime.Errors.Count==0,"wynik komendy po zakończeniu w tle");
    results.Add("PASS "+prefix+": TXT UTF-8 z wieloma wierszami zapisuje się PRZED końcem komendy; GUI pokazuje zapis, wynik komendy trafia później do zmiennych");

    p=ProjectAt(folder);p.Blocks.Add(B("ui_button","btn","Działa"));p.Blocks.Add(B("set","clicks","0"));Block evt=B("event_button","btn");evt.Children.Add(B("change","clicks","1"));p.Blocks.Add(evt);p.Blocks.Add(Command("Start-Sleep -Seconds 5"));p.Blocks.Add(B("file_write",target+"-stop","ZAPIS"));await runtime.Start(p);
    CommandWindow popup=host.OwnedForms.OfType<CommandWindow>().Single(w=>w.Running);popup.StopButton.PerformClick();await Until(()=>runtime.ActiveBackgroundCommands==0,3000,"lokalny Stop");((Button)runtime.Stage.Elements["btn"].Control).PerformClick();await Task.Delay(30);Check(File.Exists(target+"-stop")&&RuntimeView.Number(runtime.Get("clicks"))==1&&runtime.Errors.Count==0,"zatrzymanie komendy w tle zatrzymało inne zdarzenia lub zapis");
    results.Add("PASS "+prefix+": Zatrzymaj w oknie komendy w tle kończy ją, a zapis i przyciski aplikacji nadal działają");

    p=ProjectAt(folder);p.Blocks.Add(Command("Start-Sleep -Seconds 5","1"));p.Blocks.Add(B("set","after_timeout","1"));await runtime.Start(p);await Until(()=>runtime.ActiveBackgroundCommands==0,4500,"limit w tle");Check(RuntimeView.Number(runtime.Get("after_timeout"))==1&&runtime.Errors.Count==1&&Convert.ToString(runtime.Get("err")).Contains("limit"),"limit komendy w tle przerwał dalszy skrypt lub nie podał błędu");
    results.Add("PASS "+prefix+": limit czasu komendy w tle zgłasza błąd komendy i nie cofa wykonanych kolejnych bloczków");

    string late=Path.Combine(folder,prefix+"-niedozwolony-po-stop.txt");p=ProjectAt(folder);p.Blocks.Add(Command("Start-Sleep -Milliseconds 1500; [IO.File]::WriteAllText("+Ps(late)+",'late')"));await runtime.Start(p);runtime.Stop();await Until(()=>runtime.ActiveBackgroundCommands==0,3000,"Stop globalny");await Task.Delay(1700);Check(!File.Exists(late),"globalny Stop nie zakończył komendy");
    p=ProjectAt(folder);p.Blocks.Add(Command("Start-Sleep -Milliseconds 500; Write-Output 'stary'"));await runtime.Start(p);Project fresh=ProjectAt(folder);fresh.Blocks.Add(B("set","out","nowy"));await runtime.Start(fresh);await Until(()=>runtime.ActiveBackgroundCommands==0,3000,"zmiana projektu");Check((string)runtime.Get("out")=="nowy"&&runtime.Errors.Count==0,"wynik starej komendy nadpisał nowy projekt");
    results.Add("PASS "+prefix+": globalny Stop kończy komendy w tle; stary wynik nie nadpisuje ponownie uruchomionego projektu");
   }catch(Exception ex){failure=ex;}
   runtime.Stop();if(active!=null)try{await active;}catch(Exception ex){if(failure==null)failure=ex;}
   await Until(()=>runtime.ActiveBackgroundCommands==0,4000,"sprzątanie własnych procesów");host.Close();if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
  }
 }
 static async Task Exported(string folder,List<string> results){
  string dir=Path.Combine(folder,"Samodzielny JS ż");Directory.CreateDirectory(dir);string title="EasyJava-bg-"+Guid.NewGuid().ToString("N");string ready=Path.Combine(dir,"gotowe-polecenie.txt"),late=Path.Combine(dir,"po-zamknieciu.txt"),txt=Path.Combine(dir,"notatka.txt");
  Project p=ProjectAt(dir);p.Name=title;p.Blocks.Add(B("window",title,"640","420","#ffffff"));p.Blocks.Add(Command("[IO.File]::WriteAllText("+Ps(ready)+",'ready'); [Console]::Out.Write('STRUMIEN'); Start-Sleep -Seconds 3; [IO.File]::WriteAllText("+Ps(late)+",'late'); while($true){[Console]::Out.Write('.');Start-Sleep -Milliseconds 100}"));p.Blocks.Add(B("file_write","notatka.txt","test po komendzie w tle — ż\r\nDrugi wiersz"));
  string js=Path.Combine(dir,"program.js");ProjectIO.Write(js,p,Application.ExecutablePath);Process player=null;Exception failure=null;
  using(Process host=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cscript.exe"),"//E:JScript //nologo \""+js+"\""){UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=folder})){
   try{await Until(()=>File.Exists(ready)||host.HasExited,15000,"uruchomienie komendy w samodzielnym JS");Check(File.Exists(ready)&&File.Exists(txt)&&File.ReadAllText(txt)=="test po komendzie w tle — ż\r\nDrugi wiersz"&&!host.HasExited,"samodzielny JS nie zapisał TXT podczas niekończącej się komendy");}
   catch(Exception ex){failure=ex;}
   foreach(Process process in Process.GetProcessesByName("easyjava-player")){if(process.MainWindowTitle==title){player=process;break;}process.Dispose();}
   if(player!=null){player.CloseMainWindow();bool done=await Task.Run(()=>player.WaitForExit(5000));if(!done&&failure==null)failure=new Exception("Komendy w tle: odtwarzacz nie zamknął się");player.Dispose();}
   bool ended=await Task.Run(()=>host.WaitForExit(7000));if(!ended){host.Kill();if(failure==null)failure=new Exception("Komendy w tle: WSH nie zamknął się");}
   await Task.Delay(3300);if(File.Exists(late)&&failure==null)failure=new Exception("Komendy w tle: proces działał po zamknięciu samodzielnego JS");
  }
  if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();results.Add("PASS prawdziwy WSH i normalny .js: aktywna komenda strumieniująca + zapis TXT UTF-8; zamknięcie aplikacji kończy komendę bez opóźnionego zapisu");
 }
 public static int CreateLiveCheck(string directory){Directory.CreateDirectory(directory);Project p=ProjectAt(Path.GetFullPath(directory));p.Name="Papuga i zapis TXT";p.Blocks.Add(B("window","EasyJava 2.0.8 — test papugi i TXT","700","420","#f4f5fb"));p.Blocks.Add(B("ui_label","status","Papuga działa w osobnym oknie. Plik notatka.txt jest już zapisany.","20","30","650","100"));Block cmd=B("cmd","curl parrot.live","out","err","code","","300");cmd.RunInBackground=true;p.Blocks.Add(cmd);p.Blocks.Add(B("file_write","notatka.txt","test\r\nZażółć gęślą jaźń\r\nZapis podczas animacji curl"));ProjectIO.Write(Path.Combine(directory,"papuga-i-txt.js"),p,Application.ExecutablePath);return 0;}
}
}
