using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class StandaloneTests {
 static void Check(bool ok,string message){if(!ok)throw new Exception("Eksport .js: "+message);}
 static string Ps(string s){return "'"+s.Replace("'","''")+"'";}
 public static async Task Run(List<string> results,string directory){
  string folder=Path.Combine(Path.GetFullPath(directory),"Samodzielny program ż "+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  string title="EasyJava-test-"+Guid.NewGuid().ToString("N");
  Project p=new Project{Name=title};p.Features["Terminal"]=true;p.Features["Pliki"]=true;
  p.Blocks.Add(Block.Make("window",title,"640","420","#f3f5fb"));
  p.Blocks.Add(Block.Make("cmd","echo CMD-dziala & echo marker-CMD>polecenie.txt & echo blad 1>&2 & exit /b 7","out","err","code","","10"));
  p.Blocks.Add(Block.Make("file_write","cmd.txt","{out}|{err}|{code}"));
  p.Blocks.Add(Block.Make("powershell","[Console]::Out.Write('Zażółć'); [Console]::Error.Write('blad PS'); exit 9","out","err","code","","15"));
  p.Blocks.Add(Block.Make("file_write","ps.txt","{out}|{err}|{code}"));
  p.Blocks.Add(Block.Make("file_write","notatka.txt","Zażółć gęślą jaźń"));
  p.Blocks.Add(Block.Make("file_append","notatka.txt"," + dopisek"));
  p.Blocks.Add(Block.Make("file_read","notatka.txt","tekst"));
  p.Blocks.Add(Block.Make("file_write","gotowe.txt","{tekst}"));
  string js=Path.Combine(folder,"program ze spacją ż.js");ProjectIO.Write(js,p,Application.ExecutablePath);
  Process player=null;Exception failure=null;
  using(Process host=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"cscript.exe"),"//E:JScript //nologo \""+js+"\""){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,RedirectStandardError=true,WorkingDirectory=Path.GetDirectoryName(folder)})){
   try{
    Stopwatch watch=Stopwatch.StartNew();while(!File.Exists(Path.Combine(folder,"gotowe.txt"))&&watch.ElapsedMilliseconds<30000&&!host.HasExited)await Task.Delay(50);
    Check(File.Exists(Path.Combine(folder,"gotowe.txt")),"brak pliku końcowego; CMD lub zapis nie wykonały się w normalnym --player (bez --smoke)");
    Check(File.ReadAllText(Path.Combine(folder,"polecenie.txt")).Contains("marker-CMD"),"CMD nie zapisało pliku obok projektu");
    string cmd=File.ReadAllText(Path.Combine(folder,"cmd.txt")),ps=File.ReadAllText(Path.Combine(folder,"ps.txt"));
    Check(cmd.Contains("CMD-dziala")&&cmd.Contains("blad")&&cmd.EndsWith("|7"),"CMD: stdout/stderr/kod po uruchomieniu z WSH");
    Check(ps=="Zażółć|blad PS|9","PowerShell: Unicode/stdout/stderr/kod po uruchomieniu z WSH");
    Check(File.ReadAllText(Path.Combine(folder,"gotowe.txt"))=="Zażółć gęślą jaźń + dopisek","zapis/dopisanie/odczyt TXT w samodzielnym programie");
    foreach(Process process in Process.GetProcessesByName("easyjava-player")){if(process.MainWindowTitle==title){player=process;break;}process.Dispose();}
    Check(player!=null,"brak okna własnego testowego odtwarzacza");
    Check(Directory.GetFiles(folder,"*.lnk").Length==0,"eksport utworzył skrót");
   }catch(Exception ex){failure=ex;}
   // Close only the uniquely named player created by this fixture. No user editor is touched.
   if(player==null)foreach(Process process in Process.GetProcessesByName("easyjava-player")){if(process.MainWindowTitle==title){player=process;break;}process.Dispose();}
   if(player!=null){player.CloseMainWindow();await Task.Run(()=>player.WaitForExit(5000));player.Dispose();}
   bool ended=await Task.Run(()=>host.WaitForExit(8000));if(!ended){host.Kill();if(failure==null)failure=new Exception("Eksport .js: host nie zakończył własnego testu.");}
   if(ended&&host.ExitCode!=0&&failure==null)failure=new Exception("Eksport .js: "+host.StandardError.ReadToEnd()+host.StandardOutput.ReadToEnd());
  }
  if(failure!=null)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
  results.Add("PASS rzeczywisty eksport: WSH -> osadzony EXE -> normalny --player; CMD, PowerShell, stdout/stderr/kody, zapis/dopisanie/odczyt TXT UTF-8; bez --smoke i bez Node.js");
 }
}
}
