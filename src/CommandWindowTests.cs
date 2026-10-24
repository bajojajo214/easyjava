using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
public static class CommandWindowTests {
 static void Check(bool condition,string message){if(!condition)throw new Exception("Okno komendy: "+message);}
 static async Task Until(Func<bool> predicate,int milliseconds,string message){
  System.Diagnostics.Stopwatch watch=System.Diagnostics.Stopwatch.StartNew();
  while(!predicate()){if(watch.ElapsedMilliseconds>milliseconds)throw new Exception("Okno komendy: przekroczono czas: "+message);await Task.Delay(20);}
 }
 static async Task Within(Task task,int milliseconds,string message){
  using(CancellationTokenSource timeout=new CancellationTokenSource()){
   Task winner=await Task.WhenAny(task,Task.Delay(milliseconds,timeout.Token));
   if(winner!=task)throw new Exception("Okno komendy: przekroczono czas: "+message);
   timeout.Cancel();await task;
  }
 }
 static void ShowFixture(CommandWindow window){window.ShowInTaskbar=false;window.StartPosition=FormStartPosition.Manual;window.Location=new Point(-4000,-4000);window.Show();}

 public static async Task Run(List<string> results){
  const string code="Write-Output 'Zażółć {dosłownie}'";
  using(CommandWindow window=new CommandWindow("powershell",code,Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),30,delegate{})){
   ShowFixture(window);
   Check(window.Running&&window.CommandText.ReadOnly&&window.CommandText.Text==code,"okno nie pokazuje dosłownej treści polecenia");
   await Task.Run(delegate{window.AppendOutput("Pierwszy wynik\r\n");window.AppendError("Ostrzeżenie\r\n");});
   await Until(()=>window.OutputText.Text.Contains("Pierwszy wynik")&&window.ErrorText.Text.Contains("Ostrzeżenie"),3000,"wynik z wątku roboczego");
   window.Complete(new CommandResult{Output="Gotowy wynik\r\n",Error="Gotowy błąd\r\n",ExitCode=7});
   Check(!window.Running&&!window.StopButton.Enabled&&window.CloseButton.Enabled,"stan po zakończeniu komendy");
   Check(window.StatusText.Text.Contains("7")&&window.OutputText.Text.Replace("\r\n","\n")=="Gotowy wynik\n"&&window.ErrorText.Text.Contains("Gotowy błąd"),"zakończenie nie zachowało tekstu lub kodu");
   string finalOutput=window.OutputText.Text;await Task.Delay(150);Check(window.OutputText.Text==finalOutput,"kolejka dopisała dane po końcowym wyniku");
   Check(!window.IsDisposed,"okno zamknęło się przed odczytaniem wyniku");
  }
  results.Add("PASS okno komendy: pokazuje kod, odbiera wynik i błędy z wątku roboczego oraz zachowuje końcowy wynik i kod wyjścia");

  int cancellations=0;
  using(CommandWindow window=new CommandWindow("cmd","echo test","",10,delegate{cancellations++;})){
   ShowFixture(window);window.StopButton.PerformClick();window.StopButton.PerformClick();
   Check(cancellations==1&&!window.StopButton.Enabled&&!window.IsDisposed,"przycisk zatrzymania nie anuluje raz lub zamyka wynik");
   window.Cancelled();Check(!window.Running&&!window.StopButton.Enabled,"stan po anulowaniu");
   window.Close();Check(cancellations==1,"zamknięcie zakończonego okna ponawia anulowanie");
  }
  results.Add("PASS okno komendy: Zatrzymaj anuluje raz i pozostawia okno, a zamknięcie zakończonego okna nie anuluje ponownie");

  int closeCancellation=0;
  CommandWindow closed=new CommandWindow("cmd","echo test","",10,delegate{closeCancellation++;});
  ShowFixture(closed);closed.Close();
  Check(closeCancellation==1&&closed.IsDisposed,"zamknięcie aktywnego okna nie przerywa komendy");
  await Task.Run(delegate{closed.AppendOutput("Późny wynik");closed.AppendError("Późny błąd");});closed.Dispose();
  results.Add("PASS okno komendy: zamknięcie aktywnego okna anuluje komendę, późne dane nie odwołują się do usuniętych kontrolek");

  using(CommandWindow window=new CommandWindow("powershell","Write-Output 'test'","",10,delegate{})){
   ShowFixture(window);window.Failed("Przykładowy błąd uruchomienia");
   Check(!window.Running&&!window.StopButton.Enabled&&(window.StatusText.Text+window.ErrorText.Text).Contains("Przykładowy błąd uruchomienia"),"okno nie pokazuje błędu uruchomienia");
  }
  results.Add("PASS okno komendy: błąd uruchomienia jest widoczny, a okno pozostaje gotowe do odczytania");

  using(CommandWindow window=new CommandWindow("cmd","echo test","",10,delegate{})){
   ShowFixture(window);string large=new string('x',1100000);
   await Task.Run(delegate{window.AppendOutput(large);window.AppendError(large);});
   await Until(()=>window.OutputText.TextLength>=1000000&&window.ErrorText.TextLength>=1000000,5000,"duży wynik w oknie");
   Check(window.OutputText.TextLength<=1000100&&window.ErrorText.TextLength<=1000100,"okno nie ogranicza pamięci wyjścia i błędów");
   window.Cancelled();
  }
  results.Add("PASS okno komendy: duże wyniki i błędy mają ograniczenie widocznego tekstu");

  Exception failure=null;Task<CommandResult> running=null;
  using(CancellationTokenSource cancel=new CancellationTokenSource()){
   try{
    TaskCompletionSource<bool> firstChunk=new TaskCompletionSource<bool>();
    running=CommandRunner.Run("powershell","[Console]::Out.Write('pierwszy'); Start-Sleep -Milliseconds 800; [Console]::Out.Write('drugi')",Path.GetTempPath(),10,cancel.Token,delegate(bool error,string chunk){if(!error&&chunk.Length>0)firstChunk.TrySetResult(true);});
    await Within(firstChunk.Task,6000,"pierwszy wynik przed zakończeniem");
    Check(!running.IsCompleted,"wynik strumienia pojawia się dopiero po zakończeniu komendy");
    CommandResult result=await running;
    Check(result.Output.Contains("pierwszy")&&result.Output.Contains("drugi")&&result.ExitCode==0,"końcowy wynik po podglądzie strumienia");
   }catch(Exception ex){failure=ex;}
   cancel.Cancel();if(running!=null){try{await running;}catch(OperationCanceledException){}catch(Exception ex){if(failure==null)failure=ex;}}
  }
  if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
  results.Add("PASS okno komendy: wynik PowerShell jest dostępny na żywo przed zakończeniem procesu");
  await RuntimePopup(false,results);
  await RuntimePopup(true,results);
  await DisposeActiveRuntime(results);
 }

 static async Task DisposeActiveRuntime(List<string> results){
  using(Form host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000),Size=new Size(800,650)})
  using(RuntimeView runtime=new RuntimeView(true)){
   Task active=null;CommandWindow popup=null;Exception failure=null;
   try{
    host.Controls.Add(runtime);host.Show();runtime.Silent=false;
    Project project=new Project();project.Features["Terminal"]=true;project.Blocks.Add(Block.Make("powershell","Start-Sleep -Seconds 5","wynik","blad","kod","","10"));
    active=runtime.Start(project);
    popup=host.OwnedForms.OfType<CommandWindow>().Single(x=>x.Running);popup.ShowInTaskbar=false;popup.Location=new Point(-4000,-4000);
    Check(!active.IsCompleted&&popup.Visible,"komenda nie jest aktywna przed zamknięciem właściciela");
    host.Dispose();runtime.Dispose();
    await Within(active,2000,"zamknięcie odtwarzacza podczas komendy");
    Check(popup.IsDisposed,"okno komendy pozostało po usunięciu odtwarzacza");
   }catch(Exception ex){failure=ex;}
   if(!runtime.IsDisposed)runtime.Dispose();if(!host.IsDisposed)host.Dispose();
   if(active!=null){try{await Within(active,2000,"sprzątanie zamkniętego odtwarzacza");}catch(OperationCanceledException){}catch(Exception ex){if(failure==null)failure=ex;}}
   if(popup!=null&&!popup.IsDisposed)popup.Dispose();
   if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
  }
  results.Add("PASS okno komendy: zamknięcie odtwarzacza podczas aktywnego polecenia kończy proces i okno bez dostępu do usuniętych kontrolek");
 }

 static async Task RuntimePopup(bool player,List<string> results){
  using(Form host=new Form{ShowInTaskbar=false,StartPosition=FormStartPosition.Manual,Location=new Point(-4000,-4000),Size=new Size(800,650)})
  using(RuntimeView runtime=new RuntimeView(player)){
   host.Controls.Add(runtime);host.Show();runtime.Silent=false;
   Task active=null;Exception failure=null;
   try{
    Check(runtime.ShowCommandWindows,"wyskakujące okna nie są domyślnie włączone");
    string command="[Console]::Out.Write('Start {dosłownie}'); Start-Sleep -Milliseconds 800; [Console]::Out.Write(' koniec')";
    Project project=new Project();project.Features["Terminal"]=true;project.Blocks.Add(Block.Make("powershell",command,"wynik","blad","kod","","10"));
    active=runtime.Start(project);
    CommandWindow popup=host.OwnedForms.OfType<CommandWindow>().Single();popup.ShowInTaskbar=false;popup.Location=new Point(-4000,-4000);
    Check(popup.Visible&&popup.CommandText.Text==command,"runtime nie otworzył okna z dosłownym poleceniem");
    await Until(()=>popup.OutputText.Text.Contains("Start {dosłownie}")||active.IsCompleted,10000,"wynik na żywo w oknie runtime");
    Check(popup.OutputText.Text.Contains("Start {dosłownie}")&&!active.IsCompleted,"runtime pokazuje tekst dopiero po zakończeniu komendy");
    await Within(active,15000,"zakończenie komendy w runtime");
    Check(runtime.Errors.Count==0&&Convert.ToString(runtime.Get("wynik")).Contains("koniec"),"runtime nie zachował wyniku komendy");
    Check(popup.Visible&&!popup.IsDisposed&&!popup.Running&&popup.OutputText.Text.Contains("koniec"),"runtime zamknął okno przed odczytaniem wyniku");

    Project stopped=new Project();stopped.Features["Terminal"]=true;stopped.Blocks.Add(Block.Make("powershell","Start-Sleep -Seconds 5","wynik","blad","kod","","10"));stopped.Blocks.Add(Block.Make("set","po_komendzie","1"));
    active=runtime.Start(stopped);
    CommandWindow activePopup=host.OwnedForms.OfType<CommandWindow>().Single(x=>x.Running);activePopup.ShowInTaskbar=false;activePopup.Location=new Point(-4000,-4000);activePopup.StopButton.PerformClick();
    await Within(active,4000,"Zatrzymaj w oknie runtime");
    Check(runtime.Errors.Count==0&&!runtime.Values.ContainsKey("po_komendzie"),"Zatrzymaj uruchomił następny bloczek albo spowodował błąd");
    Check(activePopup.Visible&&!activePopup.Running,"okno anulowanej komendy nie pozostało do odczytania");
   }catch(Exception ex){failure=ex;}
   runtime.Stop();if(active!=null){try{await active;}catch(OperationCanceledException){}catch(Exception ex){if(failure==null)failure=ex;}}
   foreach(CommandWindow popup in host.OwnedForms.OfType<CommandWindow>().ToArray()){popup.Close();popup.Dispose();}
   runtime.Dispose();host.Close();
   if(failure!=null)ExceptionDispatchInfo.Capture(failure).Throw();
  }
  results.Add(player?"PASS okno komendy: odtwarzacz .js otwiera GUI z wynikiem na żywo, zachowuje wynik i zatrzymuje skrypt przyciskiem okna":"PASS okno komendy: podgląd otwiera GUI z wynikiem na żywo, zachowuje wynik i zatrzymuje skrypt przyciskiem okna");
 }
}
}
