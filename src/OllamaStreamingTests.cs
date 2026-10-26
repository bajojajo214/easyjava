using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
// Each server is local to one test. It sends genuine HTTP bytes in controlled
// fragments, without downloading models or executing the generated blocks.
public static class OllamaStreamingTests {
 static void Check(bool condition,string text){if(!condition)throw new Exception("AI streaming: "+text);}
 static string Q(string value){StringBuilder b=new StringBuilder("\"");foreach(char c in value){switch(c){case '\\':b.Append("\\\\");break;case '"':b.Append("\\\"");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
 static string Piece(string value,bool done=false,string reason=null){return "{\"message\":{\"role\":\"assistant\",\"content\":"+Q(value)+"},\"done\":"+(done?"true":"false")+(reason==null?"":",\"done_reason\":"+Q(reason))+"}\n";}
 static string Document(string text){return "{\"name\":\"stream_ai\",\"summary\":\"Projekt z odpowiedzi strumieniowej.\",\"blocks\":[{\"kind\":\"event_start\",\"children\":[{\"kind\":\"say\",\"A\":"+Q(text)+"}]}]}";}
 static async Task ExpectFailure(Task task,string phrase,string label){Task ended=await Task.WhenAny(task,Task.Delay(3000));Check(ended==task,label+" — brak szybkiego zakończenia");bool failed=false;try{await task;}catch(OperationCanceledException){throw;}catch(Exception ex){failed=phrase==null||ex.Message.IndexOf(phrase,StringComparison.OrdinalIgnoreCase)>=0;}Check(failed,label);}
 static async Task ExpectCancelled(Task task,string label){Task ended=await Task.WhenAny(task,Task.Delay(2500));Check(ended==task,label+" — brak szybkiego przerwania");bool cancelled=false;try{await task;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,label);}

 public static async Task Run(List<string> results,string directory){
  await LiveProgress(results);
  await DialogProgress(results);
  await Utf8Fragments(results);
  await InvalidCompletion(results);
  await RepairOnce(results);
  await LengthRecovery(results);
  await CompleteAtLengthLimit(results);
  await LengthBoundedAndCancelled(results);
  await CancelWhileStreaming(results);
  await StallDetection(results);
  await SizeLimit(results);
  string imported=Piece(Document("Model importowany"),true,"stop");using(StreamServer server=StreamServer.Body(imported)){
   OllamaGeneration generated=await new OllamaClient(server.Url).GenerateAsync("hf.co/example/model:Q4_0","Prosty projekt",null,CancellationToken.None);Check(generated.Project.Blocks.Count==1&&server.RequestBody.Contains("\"format\":\"json\""),"model importowany nie używa zgodnego trybu JSON");await server.Completion;
  }
  results.Add("PASS AI zgodność: modele hf.co używają prostego JSON od początku i nadal przechodzą parser projektu");
 }

 static async Task LiveProgress(List<string> results){
  string document=Document("Cześć ze strumienia");int middle=document.IndexOf("blocks",StringComparison.Ordinal);string first=Piece(document.Substring(0,middle)),second=Piece(document.Substring(middle)),final=Piece("",true,"stop");
  using(StreamServer server=new StreamServer(Encoding.UTF8.GetByteCount(first+second+final),async delegate(NetworkStream stream,CancellationToken token){await Write(stream,first,token);await Task.Delay(350,token);await Write(stream,second,token);await Task.Delay(350,token);await Write(stream,final,token);}))using(System.Windows.Forms.Timer heartbeat=new System.Windows.Forms.Timer{Interval=25}){
   TaskCompletionSource<bool> partial=new TaskCompletionSource<bool>();int callbacks=0;int beats=0;heartbeat.Tick+=delegate{beats++;};heartbeat.Start();
   // The concrete progress overload is filled in when the backend DTO is ready.
   Task<OllamaGeneration> pending=GenerateWithProgress(new OllamaClient(server.Url),delegate{callbacks++;partial.TrySetResult(true);});
   Task firstResult=await Task.WhenAny(partial.Task,Task.Delay(2000));Check(firstResult==partial.Task,"brak postępu przed zakończeniem odpowiedzi");Check(!pending.IsCompleted,"pierwszy postęp pojawił się dopiero po pełnej odpowiedzi");
   OllamaGeneration generated=await pending;heartbeat.Stop();Check(callbacks>=2&&beats>=3,"strumień nie aktualizuje postępu lub blokuje GUI");Check(generated.Project.Blocks[0].Children[0].A=="Cześć ze strumienia","utracona treść po sklejeniu fragmentów");Check(server.RequestBody.Contains("\"stream\":true"),"klient nie włączył stream:true");await server.Completion;
  }
  string complete=Piece(Document("Gotowe przed zamknięciem połączenia"),true,"stop");
  using(StreamServer server=new StreamServer(Encoding.UTF8.GetByteCount(complete)+10,async delegate(NetworkStream stream,CancellationToken token){await Write(stream,complete,token);await Task.Delay(30000,token);})){
   Task<OllamaGeneration> pending=new OllamaClient(server.Url).GenerateAsync("fixture:latest","Gotowy projekt",null,CancellationToken.None);Task ended=await Task.WhenAny(pending,Task.Delay(2500));Check(ended==pending,"done:true nie zakończyło odczytu bez czekania na zamknięcie połączenia");Check((await pending).Project.Blocks.Count==1,"utracony projekt po done:true");
  }
  results.Add("PASS AI strumień: postęp przychodzi przed końcem odpowiedzi, GUI pozostaje responsywne i powstaje kompletny projekt");
 }

 static async Task Utf8Fragments(List<string> results){
  string content=Document("Zażółć gęślą jaźń 🦜\nDrugi wiersz");byte[] body=Encoding.UTF8.GetBytes(Piece(content,true,"stop"));int split=Array.IndexOf(body,(byte)0xC5);Check(split>0,"brak polskiego znaku w fixture");
  using(StreamServer server=new StreamServer(body.Length,async delegate(NetworkStream stream,CancellationToken token){await stream.WriteAsync(body,0,split+1,token);await stream.FlushAsync(token);await Task.Delay(30,token);await stream.WriteAsync(body,split+1,1,token);await stream.FlushAsync(token);await Task.Delay(30,token);await stream.WriteAsync(body,split+2,body.Length-split-2,token);await stream.FlushAsync(token);})){
   OllamaGeneration generated=await new OllamaClient(server.Url).GenerateAsync("fixture:latest","Test Unicode",null,CancellationToken.None);Check(generated.Project.Blocks[0].Children[0].A=="Zażółć gęślą jaźń 🦜\nDrugi wiersz","granica bajtów UTF-8 uszkodziła treść lub polskie znaki");await server.Completion;
  }
  results.Add("PASS AI strumień: fragmenty rozdzielające bajty UTF-8 zachowują polskie znaki, emoji i nowe wiersze");
 }

 static async Task DialogProgress(List<string> results){
  string document=Document("Propozycja pozostaje niewykonana");int middle=document.Length/2;string first=Piece(document.Substring(0,middle)),final=Piece(document.Substring(middle),true,"stop");TaskCompletionSource<bool> sent=new TaskCompletionSource<bool>();Project original=new Project();string snapshot=ProjectIO.Json(original);
  using(StreamServer server=new StreamServer(Encoding.UTF8.GetByteCount(first+final),async delegate(NetworkStream stream,CancellationToken token){await Write(stream,first,token);sent.TrySetResult(true);await Task.Delay(1800,token);await Write(stream,final,token);}))using(OllamaDialog dialog=new OllamaDialog(original,false,false)){
   dialog.ShowInTaskbar=false;dialog.WindowState=FormWindowState.Minimized;dialog.Show();dialog.EndpointInput.Text=server.Url;dialog.ModelInput.Text="fixture:latest";dialog.PromptInput.Text="Pokaż postęp projektu";
   Task pending=dialog.GenerateProjectAsync();Task received=await Task.WhenAny(sent.Task,Task.Delay(2000));Check(received==sent.Task,"dialog nie otrzymał początku odpowiedzi");Stopwatch limit=Stopwatch.StartNew();while(!dialog.LiveOutput.Text.StartsWith("AI tworzy projekt",StringComparison.Ordinal)&&limit.ElapsedMilliseconds<1200)await Task.Delay(25);
   Check(dialog.LiveOutput.Text.StartsWith("AI tworzy projekt",StringComparison.Ordinal)&&!pending.IsCompleted,"dialog nie pokazuje postępu w trakcie odpowiedzi");Check(dialog.Result==null&&!dialog.UseButton.Enabled&&ProjectIO.Json(original)==snapshot,"częściowa propozycja może zostać wstawiona lub zmienia oryginał");Check(dialog.State.Text.Contains("Czas:")&&dialog.State.Text.Contains("znaków"),"brak czasu i liczby odebranych znaków");
   await pending;Check(dialog.Result!=null&&dialog.UseButton.Enabled&&dialog.ResultTree.Nodes.Count==1,"pełny, poprawny strumień nie odblokował użycia projektu");Check(ProjectIO.Json(original)==snapshot,"samo generowanie zmieniło projekt");dialog.Close();await server.Completion;
  }
  results.Add("PASS AI dialog: pokazuje żywy postęp i czas, a użycie projektu odblokowuje dopiero po pełnej odpowiedzi");
 }

 static async Task InvalidCompletion(List<string> results){
  foreach(string body in new[]{Piece(Document("niekompletny")),Piece("{\"name\":\"ucięty",true,"length"),Piece("{\"name\":",true,"stop"),"{\"error\":\"fixture stream failed\"}\n"}){
   string text=body;using(StreamServer server=new StreamServer(new[]{text,text}))await ExpectFailure(new OllamaClient(server.Url).GenerateAsync("fixture:latest","Test błędu",null,CancellationToken.None),null,"przerwany, ucięty lub błędny strumień został uznany za poprawny projekt");
  }
  results.Add("PASS AI strumień: brak done, ponownie ucięty JSON, niepełny JSON i błąd serwera nie zwracają projektu do wstawienia");
 }

 static async Task CancelWhileStreaming(List<string> results){
  string prefix=Piece("{\"name\":\"czekaj\",\"summary\":\""),tail=Piece("\",\"blocks\":[]}",true,"stop");TaskCompletionSource<bool> sent=new TaskCompletionSource<bool>();
  using(StreamServer server=new StreamServer(Encoding.UTF8.GetByteCount(prefix+tail),async delegate(NetworkStream stream,CancellationToken token){await Write(stream,prefix,token);sent.TrySetResult(true);await Task.Delay(30000,token);await Write(stream,tail,token);}))using(CancellationTokenSource cancel=new CancellationTokenSource()){
   Task pending=new OllamaClient(server.Url).GenerateAsync("fixture:latest","Test anulowania",null,cancel.Token);Task arrived=await Task.WhenAny(sent.Task,Task.Delay(2000));Check(arrived==sent.Task,"serwer nie wysłał początku odpowiedzi");await Task.Delay(75);cancel.Cancel();await ExpectCancelled(pending,"anulowanie po odebraniu początku strumienia");
  }
  results.Add("PASS AI strumień: Przerwij szybko kończy oczekiwanie także po odebraniu części odpowiedzi");
 }

 static async Task RepairOnce(List<string> results){
  string invalid="{\"name\":\"bledny\",\"summary\":\"Model użył złego bloczka.\",\"blocks\":[{\"kind\":\"invented_block\"}]}";
  using(StreamServer server=new StreamServer(new[]{Piece(invalid,true,"stop"),Piece(Document("Naprawiono"),true,"stop")})){
   List<string> updates=new List<string>();OllamaGeneration generated=await new OllamaClient(server.Url).GenerateAsync("fixture:latest","Pierwotny opis naprawy",null,CancellationToken.None,delegate(OllamaProgress value){updates.Add(value.Text??"");});
   Check(generated.Project.Blocks[0].Children[0].A=="Naprawiono"&&server.Requests.Count==2,"poprawny drugi wynik nie naprawił nieznanego bloczka");string repair=server.Requests[1];Check(repair.Contains("Pierwotny opis naprawy")&&repair.Contains("invented_block")&&repair.IndexOf("nieznanego",StringComparison.OrdinalIgnoreCase)>=0,"naprawa nie otrzymała pierwotnego opisu, odpowiedzi i przyczyny błędu");Check(updates.Any(x=>x.IndexOf("popraw",StringComparison.OrdinalIgnoreCase)>=0),"brak informacji o automatycznej poprawce");await server.Completion;
   Check(server.Requests[0].Contains("\"num_predict\":4096")&&server.Requests[1].Contains("\"num_predict\":8192"),"poprawka walidacji nie ma większego, skończonego budżetu odpowiedzi");
  }
  using(StreamServer server=new StreamServer(new[]{Piece(invalid,true,"stop"),Piece(invalid,true,"stop")})){
   await ExpectFailure(new OllamaClient(server.Url).GenerateAsync("fixture:latest","Wciąż błędny projekt",null,CancellationToken.None),null,"drugi błędny projekt został przyjęty albo naprawa nie ma końca");Check(server.Requests.Count==2,"naprawa wykonała inną liczbę prób niż jedna");await server.Completion;
  }
  results.Add("PASS AI strumień: jedna poprawka dostaje opis i błąd walidacji; drugi błędny wynik kończy się błędem bez dalszych prób");
 }

 static async Task LengthRecovery(List<string> results){
  const string partial="{\"name\":\"kalkulator_przerwany\",\"summary\":\"Niedokończony projekt";
  using(MemoryStream stream=new MemoryStream(Encoding.UTF8.GetBytes(Piece(partial,true,"length")))){
   bool retained=false;try{await OllamaClient.ReadGenerationStreamAsync(stream,CancellationToken.None,null);}catch(OllamaLengthException error){retained=error.Content==partial;}
   Check(retained,"sygnał limitu nie zachowuje dokładnej treści potrzebnej do ponowienia");
  }
  using(StreamServer server=new StreamServer(new[]{Piece(partial,true,"length"),Piece(Document("Pełny projekt po zwiększeniu limitu"),true,"stop")})){
   OllamaGeneration generated=await new OllamaClient(server.Url).GenerateAsync("fixture:latest","Pierwotny opis kalkulatora do ponowienia",null,CancellationToken.None);
   Check(generated.Project.Blocks[0].Children[0].A=="Pełny projekt po zwiększeniu limitu"&&server.Requests.Count==2,"ucięta odpowiedź nie została zastąpiona pełnym projektem z jednej ponownej próby");
   string first=server.Requests[0],retry=server.Requests[1];
   Check(first.Contains("\"num_predict\":4096")&&retry.Contains("\"num_predict\":8192"),"ponowienie po limicie nie zmienia budżetu 4096 na skończone 8192");
    Check(first.Contains("\"think\":false")&&retry.Contains("\"think\":false"),"zwiększenie budżetu przypadkiem włącza myślenie modelu");
    Check(first.Contains("\"format\":{")&&retry.Contains("\"format\":\"json\""),"ponowienie nie używa prostszego formatu JSON z tą samą walidacją projektu");
   Check(retry.Contains("Pierwotny opis kalkulatora do ponowienia")&&retry.Contains("kalkulator_przerwany")&&retry.IndexOf("limit",StringComparison.OrdinalIgnoreCase)>=0,"ponowienie utraciło opis, uciętą treść albo przyczynę przerwania");
   Check(!retry.Contains("\"num_predict\":-1"),"ponowienie ma nieograniczony budżet");await server.Completion;
  }
  results.Add("PASS AI limit: ucięta treść trafia do jednej ponownej próby z budżetem 8192 zamiast 4096 i pełną walidacją wyniku");
 }

 static async Task CompleteAtLengthLimit(List<string> results){
  using(StreamServer server=StreamServer.Body(Piece(Document("Pełny JSON mimo limitu")+"  \n",true,"length"))){
   OllamaGeneration generated=await new OllamaClient(server.Url).GenerateAsync("fixture:latest","Kompletny projekt na końcu budżetu",null,CancellationToken.None);
   Check(generated.Project.Blocks[0].Children[0].A=="Pełny JSON mimo limitu"&&server.Requests.Count==1,"kompletny poprawny JSON z done_reason=length został odrzucony lub ponowiony");await server.Completion;
  }
  results.Add("PASS AI limit: kompletny poprawny JSON zakończony limitem jest przyjmowany po walidacji bez kolejnego żądania");
 }

 static async Task LengthBoundedAndCancelled(List<string> results){
  string truncated=Piece("{\"name\":\"niedokończony",true,"length");
  foreach(string second in new[]{truncated,Piece("{\"name\":\"zły\",\"summary\":\"Błąd\",\"blocks\":[{\"kind\":\"invented_block\"}]}",true,"stop")}){
   using(StreamServer server=new StreamServer(new[]{truncated,second})){
    await ExpectFailure(new OllamaClient(server.Url).GenerateAsync("fixture:latest","Wciąż ucięty lub błędny projekt",null,CancellationToken.None),null,"błędne ponowienie zostało przyjęte lub klient czeka na trzecią próbę");
    Check(server.Requests.Count==2,"klient nie ogranicza ponowień po limicie do jednej próby");await server.Completion;
   }
  }
  using(CancellationTokenSource cancel=new CancellationTokenSource())using(StreamServer server=StreamServer.Body(truncated)){
   bool cancelledAtRetry=false;
   Task pending=new OllamaClient(server.Url).GenerateAsync("fixture:latest","Anulowanie pomiędzy próbami",null,cancel.Token,delegate(OllamaProgress update){
    string text=update.Text??"";
    if(text.IndexOf("limit",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("ponown",StringComparison.OrdinalIgnoreCase)>=0||text.IndexOf("popraw",StringComparison.OrdinalIgnoreCase)>=0){cancelledAtRetry=true;cancel.Cancel();}
   });
   await ExpectCancelled(pending,"anulowanie po rozpoznaniu limitu nie zostało uszanowane");Check(cancelledAtRetry&&server.Requests.Count==1,"anulowanie pomiędzy próbami rozpoczęło dodatkowe żądanie");await server.Completion;
  }
  results.Add("PASS AI limit: druga ucięta lub błędna odpowiedź kończy generowanie; Przerwij przed ponowieniem nie wysyła następnego żądania");
 }

 static async Task SizeLimit(List<string> results){
  // One valid NDJSON envelope exceeds the whole generated-content allowance.
  string excessive=Piece(new string('a',OllamaProject.MaxJsonLength+1),true,"stop");
  using(StreamServer server=StreamServer.Body(excessive))await ExpectFailure(new OllamaClient(server.Url).GenerateAsync("fixture:latest","Test limitu",null,CancellationToken.None),null,"zbyt duża odpowiedź nie została odrzucona");
  results.Add("PASS AI strumień: nadmiernie duży fragment jest odrzucany bez tworzenia projektu");
 }

 // Backend integration uses its new overload, while preserving all older calls.
 static Task<OllamaGeneration> GenerateWithProgress(OllamaClient client,Action onProgress){return client.GenerateAsync("fixture:latest","Projekt testowy",null,CancellationToken.None,delegate(OllamaProgress value){if(value.Characters>0)onProgress();});}
 static async Task StallDetection(List<string> results){
  using(StalledStream stream=new StalledStream(new byte[0]))await ExpectFailure(OllamaClient.ReadGenerationStreamAsync(stream,CancellationToken.None,null,150,150),null,"brak pierwszego fragmentu nie ma krótkiego limitu");
  using(StalledStream stream=new StalledStream(Encoding.UTF8.GetBytes(Piece("{\"name\":\"projekt"))))await ExpectFailure(OllamaClient.ReadGenerationStreamAsync(stream,CancellationToken.None,null,150,150),null,"zatrzymany strumień po pierwszym fragmencie nie ma krótkiego limitu");
  results.Add("PASS AI strumień: osobne limity wykrywają brak pierwszej odpowiedzi i zatrzymanie po otrzymaniu fragmentu");
 }
 static async Task Write(NetworkStream stream,string value,CancellationToken token){byte[] bytes=Encoding.UTF8.GetBytes(value);await stream.WriteAsync(bytes,0,bytes.Length,token);await stream.FlushAsync(token);}

 sealed class StreamServer:IDisposable {
  readonly TcpListener listener;readonly CancellationTokenSource stop=new CancellationTokenSource();TcpClient active;bool disposed;
  public string Url {get;private set;}public string RequestBody {get;private set;}public Task Completion {get;private set;}public readonly List<string> Requests=new List<string>();
  public StreamServer(int length,Func<NetworkStream,CancellationToken,Task> send){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;Completion=Task.Run(()=>Serve(length,send));}
  public StreamServer(string[] bodies){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;Completion=Task.Run(async delegate{foreach(string body in bodies){if(disposed)return;await Serve(Encoding.UTF8.GetByteCount(body),(stream,token)=>Write(stream,body,token));}});}
  public static StreamServer Body(string value){return new StreamServer(Encoding.UTF8.GetByteCount(value),(stream,token)=>Write(stream,value,token));}
  async Task Serve(int length,Func<NetworkStream,CancellationToken,Task> send){try{using(TcpClient client=await listener.AcceptTcpClientAsync()){active=client;client.NoDelay=true;using(NetworkStream stream=client.GetStream()){
   stream.ReadTimeout=5000;List<byte> header=new List<byte>();int a=0,b=0,c=0,d=0;while(true){int next=stream.ReadByte();if(next<0)throw new EndOfStreamException();header.Add((byte)next);a=b;b=c;c=d;d=next;if(a==13&&b==10&&c==13&&d==10)break;if(header.Count>32768)throw new IOException("Zbyt długi nagłówek fixture");}
   string text=Encoding.ASCII.GetString(header.ToArray());int requestLength=0;foreach(string line in text.Split(new[]{"\r\n"},StringSplitOptions.None)){if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))requestLength=int.Parse(line.Substring(15).Trim());if(line.StartsWith("Expect:",StringComparison.OrdinalIgnoreCase)&&line.IndexOf("100-continue",StringComparison.OrdinalIgnoreCase)>=0){byte[] interim=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");await stream.WriteAsync(interim,0,interim.Length,stop.Token);await stream.FlushAsync(stop.Token);}}
   if(requestLength>1000000)throw new IOException("Za duże żądanie fixture");byte[] request=new byte[requestLength];int offset=0;while(offset<request.Length){int read=await stream.ReadAsync(request,offset,request.Length-offset,stop.Token);if(read==0)throw new EndOfStreamException();offset+=read;}RequestBody=Encoding.UTF8.GetString(request);Requests.Add(RequestBody);
   byte[] response=Encoding.ASCII.GetBytes("HTTP/1.1 200 OK\r\nContent-Type: application/x-ndjson; charset=utf-8\r\nContent-Length: "+length+"\r\nConnection: close\r\n\r\n");await stream.WriteAsync(response,0,response.Length,stop.Token);await stream.FlushAsync(stop.Token);await send(stream,stop.Token);
  }}}catch(Exception){if(!disposed)throw;}}
  public void Dispose(){disposed=true;stop.Cancel();listener.Stop();if(active!=null)active.Close();stop.Dispose();Completion.ContinueWith(t=>{var observed=t.Exception;},TaskContinuationOptions.OnlyOnFaulted);}
 }
 sealed class StalledStream:Stream {
  readonly byte[] prefix;int offset;readonly CancellationTokenSource closed=new CancellationTokenSource();
  public StalledStream(byte[] prefix){this.prefix=prefix;}
  public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}public override long Length{get{throw new NotSupportedException();}}public override long Position{get{return offset;}set{throw new NotSupportedException();}}
  public override async Task<int> ReadAsync(byte[] buffer,int start,int count,CancellationToken token){if(offset<prefix.Length){int length=Math.Min(count,prefix.Length-offset);Array.Copy(prefix,offset,buffer,start,length);offset+=length;return length;}using(CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(token,closed.Token)){await Task.Delay(Timeout.Infinite,linked.Token);return 0;}}
  protected override void Dispose(bool disposing){if(disposing)closed.Cancel();base.Dispose(disposing);}
  public override int Read(byte[] buffer,int offset,int count){throw new NotSupportedException();}public override void Flush(){}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
 }
}
}
