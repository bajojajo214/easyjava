using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class CommandTests {
 static void Check(bool condition,string message){if(!condition)throw new Exception("Terminal: "+message);}
 static string Ps(string value){return "'"+value.Replace("'","''")+"'";}
 static Block B(string kind,params string[] values){return Block.Make(kind,values);}
 static void Healthy(RuntimeView runtime){Check(runtime.Errors.Count==0,string.Join("; ",runtime.Errors));}
 static async Task Until(Func<bool> predicate,int milliseconds,string message){Stopwatch clock=Stopwatch.StartNew();while(!predicate()){if(clock.ElapsedMilliseconds>milliseconds)throw new Exception("Terminal: przekroczono czas: "+message);await Task.Delay(20);}}
 static async Task Within(Task task,int milliseconds,string message){using(CancellationTokenSource timeout=new CancellationTokenSource()){Task winner=await Task.WhenAny(task,Task.Delay(milliseconds,timeout.Token));if(winner!=task)throw new Exception("Terminal: przekroczono czas: "+message);timeout.Cancel();await task;}}

 public static async Task Run(RuntimeView runtime,List<string> results,string directory){
  bool savedSilent=runtime.Silent;Action<Block> savedHighlight=runtime.Highlight;Task active=null;Exception failure=null;
  string fixture=Path.Combine(Path.GetFullPath(directory),"komendy ze spacją "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
  try{
   CommandResult cmd=await CommandRunner.Run("cmd","echo EasyJava & echo blad 1>&2 & exit /b 7",fixture,10,CancellationToken.None);
   Check(cmd.Output.Contains("EasyJava")&&cmd.Error.Contains("blad")&&cmd.ExitCode==7,"CMD nie zachował wyjścia, błędu lub kodu wyjścia");
   CommandResult cwd=await CommandRunner.Run("cmd","cd",fixture,10,CancellationToken.None);
   Check(cwd.ExitCode==0&&string.Equals(cwd.Output.Trim(),fixture,StringComparison.OrdinalIgnoreCase),"CMD nie uruchamia się w katalogu ze spacją");
   results.Add("PASS terminal: prawdziwe CMD, oddzielne wyjście i błędy, kod wyjścia i katalog ze spacją");

   CommandResult multi=await CommandRunner.Run("cmd","echo Zażółć {dosłownie} \"cytat\"\r\necho drugi wiersz",fixture,10,CancellationToken.None);
   Check(multi.ExitCode==0&&multi.Output.Contains("Zażółć {dosłownie} \"cytat\"")&&multi.Output.Contains("drugi wiersz"),"CMD nie wykonał wszystkich wierszy lub zgubił Unicode");
   results.Add("PASS terminal: wielowierszowy CMD zachowuje oba polecenia, polskie znaki, klamry i cudzysłowy");

   string literal="Zażółć gęślą jaźń {dosłownie} \"cytat\"";
   CommandResult ps=await CommandRunner.Run("powershell","[Console]::Out.Write("+Ps(literal)+"); [Console]::Error.Write('blad PS'); exit 9",fixture,15,CancellationToken.None);
   Check(ps.Output.Contains(literal)&&ps.Error.Contains("blad PS")&&ps.ExitCode==9,"PowerShell nie zachował Unicode, nawiasów, cudzysłowów, błędu lub kodu wyjścia");
   results.Add("PASS terminal: PowerShell zachowuje polskie znaki, literalne nawiasy i cudzysłowy oraz stderr i kod wyjścia");

   CommandResult large=await CommandRunner.Run("powershell","[Console]::Error.Write(('y' * 1200000)); [Console]::Out.Write(('x' * 1200000))",fixture,20,CancellationToken.None);
   Check(large.ExitCode==0&&large.Output.Length>500000&&large.Error.Length>500000,"duży wynik jednego ze strumieni nie został odebrany");
   Check(large.Output.Length<=1100000&&large.Error.Length<=1100000,"wyniki komend nie mają ograniczenia rozmiaru");
   results.Add("PASS terminal: duże stdout i stderr są odczytywane bez zakleszczenia i mają limit rozmiaru");

   bool timedOut=false;Stopwatch watch=Stopwatch.StartNew();
   try{await CommandRunner.Run("powershell","Start-Sleep -Seconds 5",fixture,1,CancellationToken.None);}catch(TimeoutException){timedOut=true;}
   Check(timedOut&&watch.ElapsedMilliseconds<4500,"limit czasu nie przerywa uruchomionego polecenia");
   results.Add("PASS terminal: limit czasu przerywa oczekujące polecenie");

   string ready=Path.Combine(fixture,"dziecko gotowe.txt"),late=Path.Combine(fixture,"nie powinno powstac.txt");
   string child="Start-Sleep -Milliseconds 1200; [IO.File]::WriteAllText("+Ps(late)+", 'late')";
   string encoded=Convert.ToBase64String(Encoding.Unicode.GetBytes(child));
   string psExe=Path.Combine(Environment.SystemDirectory,"WindowsPowerShell","v1.0","powershell.exe");
   string parent="$child = Start-Process -FilePath "+Ps(psExe)+" -ArgumentList '-NoLogo','-NoProfile','-NonInteractive','-EncodedCommand',"+Ps(encoded)+" -WindowStyle Hidden -PassThru; [IO.File]::WriteAllText("+Ps(ready)+",'ready'); Start-Sleep -Seconds 10";
   using(CancellationTokenSource cancel=new CancellationTokenSource()){
    Task<CommandResult> pending=CommandRunner.Run("powershell",parent,fixture,15,cancel.Token);active=pending;
    Exception cancellationFailure=null;try{
     await Until(()=>File.Exists(ready)||pending.IsCompleted,7000,"utworzenie procesu potomnego");
     Check(File.Exists(ready),"proces rodzica nie potwierdził uruchomienia dziecka");
     watch.Restart();cancel.Cancel();bool cancelled=false;try{await Within(pending,2000,"anulowanie komendy");}catch(OperationCanceledException){cancelled=true;}
     Check(cancelled&&watch.ElapsedMilliseconds<2000,"anulowanie nie przerywa polecenia");
     await Task.Delay(2200);Check(!File.Exists(late),"proces potomny działał po anulowaniu komendy");
    }catch(Exception ex){cancellationFailure=ex;}
    cancel.Cancel();try{await pending;}catch(OperationCanceledException){}catch(Exception ex){if(cancellationFailure==null)cancellationFailure=ex;}active=null;
    if(cancellationFailure!=null)ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
   }
   results.Add("PASS terminal: anulowanie kończy polecenie i jego proces potomny bez opóźnionych skutków");

   Project empty=new Project();Check(!empty.Allows("Terminal"),"Terminal jest włączony w nowym projekcie");
   Project old=ProjectIO.FromJson("{\"Version\":2,\"Name\":\"stary\",\"Blocks\":[],\"Features\":{\"Pliki\":true}}");Check(!old.Allows("Terminal"),"Terminal jest włączony w starym projekcie");
   string blocked=Path.Combine(fixture,"wylaczone.txt");Block command=B("powershell","[IO.File]::WriteAllText("+Ps(blocked)+",'unexpected')","out","err","code",fixture,"10");
   Project disabled=new Project();disabled.Blocks.Add(command);runtime.Silent=false;runtime.Highlight=null;active=runtime.Start(disabled);await Within(active,5000,"wyłączona kategoria");Healthy(runtime);
   Check(!File.Exists(blocked)&&!runtime.Values.ContainsKey("out"),"wyłączona kategoria uruchomiła komendę");
   disabled.Features["Terminal"]=true;command.Disabled=true;active=runtime.Start(disabled);await Within(active,5000,"wyłączony bloczek");Healthy(runtime);
   Check(!File.Exists(blocked)&&!runtime.Values.ContainsKey("out"),"wyłączony bloczek uruchomił komendę");
   command.Disabled=false;runtime.Silent=true;active=runtime.Start(disabled);await Within(active,5000,"tryb smoke");Healthy(runtime);
   Check(!File.Exists(blocked),"tryb smoke uruchomił komendę");
   results.Add("PASS terminal: nowe i starsze projekty mają kategorię wyłączoną; wyłączenie bloczka i tryb smoke pomijają komendy");

   Project project=new Project();project.Features["Terminal"]=true;
   project.Blocks.Add(B("cmd","echo EasyJava","stdout","stderr","code",fixture,"10"));project.Blocks.Add(B("say","Odebrano: {stdout}"));
   Project reopened=ProjectIO.Parse(ProjectIO.Export(project,Application.ExecutablePath));
   Check(reopened.Allows("Terminal")&&reopened.Blocks[0].Kind=="cmd"&&reopened.Blocks[0].E==fixture,"zapis .js zgubił zgodę, komendę lub folder");
   runtime.Silent=false;active=runtime.Start(reopened);await Within(active,10000,"bloczek CMD");Healthy(runtime);
   Check(Convert.ToString(runtime.Get("stdout")).Contains("EasyJava")&&Convert.ToString(runtime.Get("stderr")).Trim()==""&&RuntimeView.Number(runtime.Get("code"))==0,"wyniki CMD nie trafiły do zmiennych");
   Check(runtime.Output.Text.Contains("Odebrano: EasyJava"),"następny bloczek nie wykorzystuje wyniku komendy");
   results.Add("PASS terminal: bloczek CMD zapisuje wynik, błąd i kod do zmiennych; następny bloczek używa wyniku; .js zachowuje ustawienia");

   Project literalProject=new Project();literalProject.Features["Terminal"]=true;literalProject.Blocks.Add(B("powershell","Write-Output "+Ps(literal),"wynik","blad","kod",fixture,"15"));
   active=runtime.Start(literalProject);await Within(active,15000,"bloczek PowerShell z nawiasami");Healthy(runtime);
   Check(Convert.ToString(runtime.Get("wynik")).Contains(literal)&&RuntimeView.Number(runtime.Get("kod"))==0,"bloczek PowerShell podmienił literalne nawiasy na zmienne EasyJava");
   results.Add("PASS terminal: bloczek PowerShell wykonuje skrypt z literalnymi nawiasami bez podstawiania zmiennych EasyJava");

   Project stopProject=new Project();stopProject.Features["Terminal"]=true;stopProject.Blocks.Add(B("powershell","Start-Sleep -Seconds 5","out","err","code",fixture,"10"));stopProject.Blocks.Add(B("set","after_stop","1"));
   active=runtime.Start(stopProject);await Task.Delay(150);watch.Restart();runtime.Stop();await Within(active,2000,"Stop podczas komendy");Healthy(runtime);
   Check(watch.ElapsedMilliseconds<2000&&!runtime.Values.ContainsKey("after_stop"),"Stop nie przerywa komendy albo uruchamia kolejny bloczek");
   results.Add("PASS terminal: Stop w podglądzie przerywa komendę i nie uruchamia kolejnego bloczka");

   active=runtime.Start(Examples.Terminal());await Within(active,5000,"uruchomienie przykładu GUI");Healthy(runtime);
   Check(!runtime.Values.ContainsKey("wynik_komendy"),"przykład uruchamia komendy bez kliknięcia");
   ((Button)runtime.Stage.Elements["run_cmd"].Control).PerformClick();await Until(()=>runtime.Values.ContainsKey("wynik_komendy")&&Convert.ToString(runtime.Get("wynik_komendy")).Contains("Witaj z CMD"),10000,"przycisk CMD");Healthy(runtime);
   Check(runtime.Stage.Elements["output"].Control.Text.Contains("Witaj z CMD"),"wynik CMD nie jest widoczny w GUI");
   ((Button)runtime.Stage.Elements["run_ps"].Control).PerformClick();await Until(()=>Convert.ToString(runtime.Get("wynik_komendy")).Contains("Witaj z PowerShell"),15000,"przycisk PowerShell");Healthy(runtime);
   Check(runtime.Stage.Elements["output"].Control.Text.Contains("Witaj z PowerShell"),"wynik PowerShell nie jest widoczny w GUI");
   results.Add("PASS terminal: przykład GUI wykonuje CMD i PowerShell dopiero po kliknięciu i pokazuje wyniki w oknie");
  }catch(Exception ex){failure=ex;}
  runtime.Stop();if(active!=null){try{await active;}catch(OperationCanceledException){}catch(Exception ex){if(failure==null)failure=ex;}}runtime.Silent=savedSilent;runtime.Highlight=savedHighlight;
  if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
 }
}
}
