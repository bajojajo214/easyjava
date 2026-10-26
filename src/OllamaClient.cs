using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace EasyJava2 {
public sealed class OllamaProgress {
 public string Text,Preview;
 public int Characters,BlockCount;
}
// Keep a budget-truncated response separate from a broken connection. The
// complete object may already be present; only the strict project parser can
// decide whether it is usable, otherwise request one whole replacement.
internal sealed class OllamaLengthException : Exception {
 public string Content {get;private set;}
 public OllamaLengthException(string content):base("Model osiągnął limit długości odpowiedzi."){Content=content;}
}
public sealed class OllamaClient {
 public const string DefaultEndpoint="http://127.0.0.1:11434";
 public string Endpoint {get;private set;}
 readonly Uri baseUri;
 public OllamaClient(string endpoint){
  Uri parsed;
  if(!Uri.TryCreate((endpoint??"").Trim(),UriKind.Absolute,out parsed)||(parsed.Scheme!="http"&&parsed.Scheme!="https")||!string.IsNullOrEmpty(parsed.UserInfo)||!string.IsNullOrEmpty(parsed.Query)||!string.IsNullOrEmpty(parsed.Fragment))
   throw new Exception("Podaj poprawny adres Ollamy, np. http://127.0.0.1:11434 (bez hasła i parametrów).");
  Endpoint=parsed.AbsoluteUri.TrimEnd('/');baseUri=new Uri(Endpoint+"/");
 }

 public async Task<List<string>> ListModelsAsync(CancellationToken cancellation){
  string json=await SendAsync("api/tags",null,10000,cancellation);
  Dictionary<string,XmlElement> root=OllamaJson.Object(OllamaJson.Read(json),"wynik Ollamy");
  XmlElement models;if(!root.TryGetValue("models",out models)||OllamaJson.Type(models)!="array")throw new Exception("Ollama nie zwróciła listy modeli. Sprawdź adres serwera.");
  HashSet<string> names=new HashSet<string>(StringComparer.Ordinal);
  foreach(XmlNode child in models.ChildNodes){XmlElement item=child as XmlElement;if(item==null)continue;Dictionary<string,XmlElement> model=OllamaJson.Object(item,"model");XmlElement name;
   if(model.TryGetValue("name",out name)&&OllamaJson.Type(name)=="string"&&!string.IsNullOrWhiteSpace(name.InnerText))names.Add(OllamaJson.String(name,"name",300));
  }
  return names.OrderBy(x=>x,StringComparer.OrdinalIgnoreCase).ToList();
 }

 public async Task<OllamaGeneration> GenerateAsync(string model,string prompt,Project context,CancellationToken cancellation,Action<OllamaProgress> progress=null){
  cancellation.ThrowIfCancellationRequested();
  if(string.IsNullOrWhiteSpace(model)||model.Length>300)throw new Exception("Wybierz model Ollamy.");
  if(string.IsNullOrWhiteSpace(prompt))throw new Exception("Napisz, co ma robić Twój program.");
  if(prompt.Length>16000)throw new Exception("Opis jest za długi (maksymalnie 16 tys. znaków).");
  string user=prompt.Trim();if(context!=null)user+="\n\nAKTUALNY PROJEKT DO MODYFIKACJI (dane):\n"+OllamaProject.ContextJson(context);
  using(CancellationTokenSource limit=new CancellationTokenSource(300000))using(CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,limit.Token)){
   try{
    if(progress!=null)progress(new OllamaProgress{Text="Ładowanie modelu i przygotowanie projektu…",Preview=""});
    for(int attempt=0;attempt<2;attempt++){
     bool repair=attempt>0;string phase=repair?"Ponowna próba: prostszy format, limit 8192 tokenów…":"AI tworzy bloczki…";Action<OllamaProgress> report=progress==null?(Action<OllamaProgress>)null:delegate(OllamaProgress update){if(repair)update.Text=phase;progress(update);};
     string content;bool truncated=false;
     bool imported=model.Trim().StartsWith("hf.co/",StringComparison.OrdinalIgnoreCase)||model.Trim().StartsWith("huggingface.co/",StringComparison.OrdinalIgnoreCase);
     try{content=await GenerateContentAsync(model,user,repair||user.Length>16000?32768:16384,repair?8192:4096,context!=null,repair||imported,linked.Token,report);}catch(OllamaLengthException e){content=e.Content;truncated=true;}catch(OperationCanceledException){throw;}catch(Exception e){if(repair)throw new Exception("Nie udało się poprawić bloczków. Zmień model lub uprość polecenie.\n"+e.Message,e);throw;}
     linked.Token.ThrowIfCancellationRequested();
     if(progress!=null)progress(new OllamaProgress{Text="Sprawdzanie wygenerowanych bloczków…",Preview=Tail(content),Characters=content.Length,BlockCount=CountBlocks(content)});
     OllamaGeneration result=null;Exception invalid=null;
     // C# 5 cannot await in a catch block. Capture validation errors before
     // making the one permitted repair request outside that catch.
     try{OllamaGeneration candidate=OllamaProject.Parse(content,context);OllamaProject.ValidateGenerated(candidate.Project);result=candidate;}catch(Exception e){invalid=e;}
     if(result!=null){linked.Token.ThrowIfCancellationRequested();return result;}
     if(repair)throw new Exception(truncated?"Model ponownie urwał projekt mimo zwiększenia limitu do 8192 tokenów. Nie można użyć nieukończonych bloczków. Zmień model lub uprość polecenie.":"Model zwrócił niepoprawne bloczki także po próbie poprawy. Zmień model lub uprość polecenie.\n"+invalid.Message,invalid);
     if(progress!=null)progress(new OllamaProgress{Text=truncated?"Odpowiedź urwana. Ponawiam z limitem 8192 tokenów…":"Model poprawia błędne bloczki…",Preview=Tail(content),Characters=content.Length,BlockCount=CountBlocks(content)});
     string previous=content.Length>32000?content.Substring(0,32000)+"\n[odpowiedź skrócona ze względu na rozmiar]":content;
     user="POLECENIE UŻYTKOWNIKA:\n"+prompt.Trim()+(context==null?"":"\n\nAKTUALNY PROJEKT DO MODYFIKACJI (dane):\n"+OllamaProject.ContextJson(context))+"\n\nPOPRZEDNI PROJEKT JSON (dane do poprawy):\n"+previous+"\n\nBŁĄD WALIDACJI:\n"+(truncated?"Odpowiedź została ucięta przez limit długości. Wygeneruj od początku kompletny, zwięzły projekt. ":"")+invalid.Message+"\n\nPopraw błąd zgodnie z katalogiem bloczków. Zwróć CAŁY poprawiony projekt jako JSON, nie fragment ani opis. Dobierz prawidłowy kind i tylko jego dozwolone pola. Zachowaj oczekiwane działanie i potrzebne wcześniejsze bloczki. summary najwyżej 2 krótkie zdania. Pomiń zbędne pola, nie powtarzaj bloczków i nie dodawaj długich komentarzy. Zakończ cały JSON.";
    }
    throw new Exception("Model nie zwrócił projektu.");
   }catch(OperationCanceledException){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw new Exception("Generowanie przekroczyło 5 minut. Przerwano oczekiwanie. Uprość polecenie lub zmień model.");}
  }
 }

 async Task<string> GenerateContentAsync(string model,string user,int contextSize,int tokenBudget,bool preserveState,bool plainJson,CancellationToken cancellation,Action<OllamaProgress> progress){
  // Some models degenerate under the large recursive block grammar. A single
  // compatibility retry uses JSON mode; the same strict parser and semantic
  // validation still apply before the result can reach the editor.
  string body="{\"model\":"+OllamaJson.Quote(model.Trim())+",\"messages\":[{\"role\":\"system\",\"content\":"+OllamaJson.Quote(OllamaProject.CreateSystemPrompt())+"},{\"role\":\"user\",\"content\":"+OllamaJson.Quote(user)+"}],\"format\":"+(plainJson?"\"json\"":OllamaProject.CreateSchema(preserveState))+",\"stream\":true,\"think\":false,\"options\":{\"temperature\":0,\"num_ctx\":"+contextSize+",\"num_predict\":"+tokenBudget+"}}";
  Stopwatch clock=Stopwatch.StartNew();
  using(HttpClient client=CreateClient())using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,new Uri(baseUri,"api/chat")))using(CancellationTokenSource first=new CancellationTokenSource(90000))using(CancellationTokenSource headers=CancellationTokenSource.CreateLinkedTokenSource(cancellation,first.Token)){
   request.Headers.Accept.ParseAdd("application/x-ndjson");request.Content=new StringContent(body,Encoding.UTF8,"application/json");
   try{
    using(HttpResponseMessage response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,headers.Token))using(cancellation.Register(response.Dispose)){
     first.CancelAfter(Timeout.Infinite);
     await EnsureSuccessAsync(response,cancellation);
     using(Stream stream=await response.Content.ReadAsStreamAsync())return await ReadGenerationStreamAsync(stream,cancellation,progress,Math.Max(1,90000-(int)clock.ElapsedMilliseconds),45000);
    }
   }catch(HttpRequestException e){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw new Exception("Nie można połączyć się z Ollamą pod adresem "+Endpoint+". Uruchom Ollamę i odśwież modele.",e);}
   catch(OperationCanceledException){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw new Exception("Model nie zaczął generować w ciągu 90 sekund. Uprość polecenie lub zmień model.");}
   catch(IOException e){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw new Exception("Połączenie z Ollamą przerwało się podczas generowania. Spróbuj ponownie lub zmień model.",e);}
   catch(ObjectDisposedException){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw;}
  }
 }

 // Raw byte reads avoid StreamReader.ReadAsync waiting to fill a character
 // buffer before reporting small streamed tokens. Decoder preserves UTF-8 when
 // a Polish letter or emoji spans several TCP reads.
 internal static async Task<string> ReadGenerationStreamAsync(Stream input,CancellationToken cancellation,Action<OllamaProgress> progress,int firstFragmentMilliseconds=90000,int inactivityMilliseconds=45000){
  if(firstFragmentMilliseconds<1||inactivityMilliseconds<1)throw new ArgumentOutOfRangeException("firstFragmentMilliseconds");
  GenerationStream result=new GenerationStream(progress);StringBuilder pending=new StringBuilder();Decoder decoder=new UTF8Encoding(false,true).GetDecoder();byte[] buffer=new byte[4096];char[] chars=new char[4098];long wireBytes=0;Stopwatch clock=Stopwatch.StartNew();long lastContent=0;bool hasContent=false;
  while(!result.Done){
   cancellation.ThrowIfCancellationRequested();long remaining=hasContent?inactivityMilliseconds-(clock.ElapsedMilliseconds-lastContent):firstFragmentMilliseconds-clock.ElapsedMilliseconds;
   if(remaining<=0)throw new Exception(hasContent?"Ollama przestała przesyłać odpowiedź na 45 sekund. Przerwano generowanie. Uprość polecenie lub zmień model.":"Model nie zaczął generować w ciągu 90 sekund. Uprość polecenie lub zmień model.");
   Task<int> read=input.ReadAsync(buffer,0,buffer.Length,cancellation);int length;
   using(CancellationTokenSource timer=CancellationTokenSource.CreateLinkedTokenSource(cancellation)){
    Task delay=Task.Delay((int)Math.Min(int.MaxValue,remaining),timer.Token);Task ended=await Task.WhenAny(read,delay);timer.Cancel();
    if(ended!=read){ObserveFault(read);cancellation.ThrowIfCancellationRequested();throw new Exception(hasContent?"Ollama przestała przesyłać odpowiedź na 45 sekund. Przerwano generowanie. Uprość polecenie lub zmień model.":"Model nie zaczął generować w ciągu 90 sekund. Uprość polecenie lub zmień model.");}
    length=await read;
   }
   wireBytes+=length;if(wireBytes>4194304)throw new Exception("Odpowiedź Ollamy jest za duża (limit 4 MB).");
   int count;try{count=decoder.GetChars(buffer,0,length,chars,0,length==0);}catch(DecoderFallbackException e){throw new Exception("Ollama zwróciła nieprawidłowy tekst UTF-8. Spróbuj ponownie lub zmień model.",e);}
   for(int i=0;i<count&&!result.Done;i++){
    if(chars[i]=='\n'){
     string line=pending.ToString().Trim().TrimStart('\uFEFF');pending.Clear();if(line.Length==0)continue;
     if(result.Append(line)){hasContent=true;lastContent=clock.ElapsedMilliseconds;}
    }else {pending.Append(chars[i]);if(pending.Length>4194304)throw new Exception("Wiersz odpowiedzi Ollamy jest za duży.");}
   }
   if(length==0){if(pending.Length>0&&result.Append(pending.ToString().Trim().TrimStart('\uFEFF'))){hasContent=true;lastContent=clock.ElapsedMilliseconds;}break;}
  }
  cancellation.ThrowIfCancellationRequested();
  if(!result.Done)throw new Exception("Ollama przerwała odpowiedź przed ukończeniem projektu. Spróbuj ponownie lub zmień model.");
  return result.Content.ToString();
 }
 sealed class GenerationStream {
  public readonly StringBuilder Content=new StringBuilder();public bool Done;readonly Action<OllamaProgress> progress;readonly Stopwatch clock=Stopwatch.StartNew();long lastReport=-100;
  public GenerationStream(Action<OllamaProgress> progress){this.progress=progress;}
  public bool Append(string line){
   Dictionary<string,XmlElement> root=OllamaJson.Object(OllamaJson.Read(line),"fragment odpowiedzi Ollamy");XmlElement value;
   if(root.TryGetValue("error",out value))throw new Exception("Ollama: "+OllamaJson.String(value,"error",12000));
   bool added=false;XmlElement message;
   if(root.TryGetValue("message",out message)){
    Dictionary<string,XmlElement> fields=OllamaJson.Object(message,"wiadomość Ollamy");
    if(fields.TryGetValue("content",out value)){string chunk=OllamaJson.String(value,"content",OllamaProject.MaxJsonLength);if(Content.Length+chunk.Length>OllamaProject.MaxJsonLength)throw new Exception("Projekt AI jest za duży (limit 1 MB tekstu).");if(chunk.Length>0){Content.Append(chunk);added=true;}}
   }
   XmlElement done;if(!root.TryGetValue("done",out done)||OllamaJson.Type(done)!="boolean")throw new Exception("Ollama zwróciła fragment bez informacji o zakończeniu. Sprawdź adres serwera lub zmień model.");
   if(done.InnerText=="true"){
    if(root.TryGetValue("done_reason",out value)&&OllamaJson.Type(value)=="string"&&value.InnerText=="length")throw new OllamaLengthException(Content.ToString());
    Done=true;
   }
   if(progress!=null&&(Done||added&&(lastReport<0||clock.ElapsedMilliseconds-lastReport>=100))){string content=Content.ToString();progress(new OllamaProgress{Text=Done?"Sprawdzanie wygenerowanych bloczków…":"AI tworzy bloczki…",Preview=Tail(content),Characters=content.Length,BlockCount=CountBlocks(content)});lastReport=clock.ElapsedMilliseconds;}
   return added;
  }
 }
 static string Tail(string content){int start=Math.Max(0,content.Length-1600);if(start>0&&start<content.Length&&char.IsLowSurrogate(content[start])&&char.IsHighSurrogate(content[start-1]))start--;return content.Substring(start);}
 static int CountBlocks(string content){int count=0,start=0;while((start=content.IndexOf("\"kind\"",start,StringComparison.Ordinal))>=0){count++;start+=6;}return count;}

 // Pulling a model is always started by an explicit button in the UI. Progress
 // is delivered on the caller's synchronization context; no model is auto-pulled.
 public async Task PullModelAsync(string model,CancellationToken cancellation,Action<string> progress){
  cancellation.ThrowIfCancellationRequested();
  if(string.IsNullOrWhiteSpace(model)||model.Length>300)throw new Exception("Wpisz nazwę modelu do pobrania.");
  using(HttpClient client=CreateClient())using(HttpRequestMessage request=new HttpRequestMessage(HttpMethod.Post,new Uri(baseUri,"api/pull")))using(CancellationTokenSource limit=new CancellationTokenSource(1800000))using(CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,limit.Token)){
   request.Content=new StringContent("{\"model\":"+OllamaJson.Quote(model.Trim())+",\"stream\":true}",Encoding.UTF8,"application/json");request.Headers.Accept.ParseAdd("application/x-ndjson");
   try{
    using(HttpResponseMessage response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,linked.Token))using(linked.Token.Register(response.Dispose)){
     await EnsureSuccessAsync(response,linked.Token);
     using(Stream stream=await response.Content.ReadAsStreamAsync())using(StreamReader reader=new StreamReader(stream,Encoding.UTF8)){
     StringBuilder pending=new StringBuilder();char[] buffer=new char[2048];int read;bool success=false;DateTime lastProgress=DateTime.MinValue;string lastStatus="";
     while((read=await AwaitCancellationAsync(reader.ReadAsync(buffer,0,buffer.Length),linked.Token))>0){
      linked.Token.ThrowIfCancellationRequested();
      for(int i=0;i<read;i++){
       char c=buffer[i];if(c!='\n'){if(pending.Length>=65536)throw new Exception("Nieprawidłowa odpowiedź podczas pobierania modelu (za długi wiersz).");pending.Append(c);continue;}
       string line=pending.ToString().Trim();pending.Clear();if(line.Length==0)continue;
       ProcessPullLine(line,progress,ref success,ref lastStatus,ref lastProgress);
      }
     }
     if(pending.Length>0)ProcessPullLine(pending.ToString(),progress,ref success,ref lastStatus,ref lastProgress);
     cancellation.ThrowIfCancellationRequested();
     if(!success)throw new Exception("Pobieranie modelu zakończyło się bez potwierdzenia. Kliknij Pobierz model, aby wznowić.");
     }
    }
   }catch(HttpRequestException e){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    if(limit.IsCancellationRequested)throw new Exception("Przekroczono czas pobierania modelu. Ponów pobieranie, aby wznowić.",e);
    throw new Exception("Nie można pobrać modelu z Ollamy. Sprawdź połączenie z serwerem i nazwę modelu.",e);
   }catch(IOException e){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    if(limit.IsCancellationRequested)throw new Exception("Przekroczono czas pobierania modelu. Ponów pobieranie, aby wznowić.",e);
    throw new Exception("Połączenie z Ollamą zostało przerwane podczas pobierania. Spróbuj ponownie.",e);
   }catch(ObjectDisposedException e){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);if(limit.IsCancellationRequested)throw new Exception("Przekroczono czas pobierania modelu. Ponów pobieranie, aby wznowić.",e);throw;}
   catch(OperationCanceledException){if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);throw new Exception("Przekroczono czas pobierania modelu. Ponów pobieranie, aby wznowić.");}
  }
 }

 static void ProcessPullLine(string line,Action<string> progress,ref bool success,ref string lastStatus,ref DateTime lastProgress){
  Dictionary<string,XmlElement> map=OllamaJson.Object(OllamaJson.Read(line),"postęp pobierania");XmlElement value;
  if(map.TryGetValue("error",out value))throw new Exception("Ollama: "+OllamaJson.String(value,"error",12000));
  if(!map.TryGetValue("status",out value)||OllamaJson.Type(value)!="string")return;
  string status=value.InnerText;
  if(status=="success"){success=true;if(progress!=null)progress("Model pobrany. Gotowe.");return;}
  string text=status;
  XmlElement completedNode,totalNode;double completed,total;
  if(map.TryGetValue("completed",out completedNode)&&map.TryGetValue("total",out totalNode)&&double.TryParse(completedNode.InnerText,NumberStyles.Float,CultureInfo.InvariantCulture,out completed)&&double.TryParse(totalNode.InnerText,NumberStyles.Float,CultureInfo.InvariantCulture,out total)&&total>0){
   text="Pobieranie modelu: "+Math.Min(100,Math.Max(0,100*completed/total)).ToString("0",CultureInfo.InvariantCulture)+"% ("+(completed/1048576).ToString("0",CultureInfo.InvariantCulture)+" / "+(total/1048576).ToString("0",CultureInfo.InvariantCulture)+" MB)";
  }
  if(progress!=null&&(status!=lastStatus||(DateTime.UtcNow-lastProgress).TotalMilliseconds>100)){progress(text);lastProgress=DateTime.UtcNow;lastStatus=status;}
 }

 async Task<string> SendAsync(string path,string body,int timeout,CancellationToken cancellation){
  cancellation.ThrowIfCancellationRequested();
  using(HttpClient client=CreateClient())using(HttpRequestMessage request=new HttpRequestMessage(body==null?HttpMethod.Get:HttpMethod.Post,new Uri(baseUri,path)))using(CancellationTokenSource limit=new CancellationTokenSource(timeout))using(CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,limit.Token)){
   request.Headers.Accept.ParseAdd("application/json");if(body!=null)request.Content=new StringContent(body,Encoding.UTF8,"application/json");
   try{
    using(HttpResponseMessage response=await client.SendAsync(request,HttpCompletionOption.ResponseHeadersRead,linked.Token))using(linked.Token.Register(response.Dispose)){
     await EnsureSuccessAsync(response,linked.Token);
     using(Stream stream=await response.Content.ReadAsStreamAsync()){
      string result=await ReadBoundedAsync(stream,4194304,linked.Token);
      Dictionary<string,XmlElement> envelope=OllamaJson.Object(OllamaJson.Read(result),"odpowiedź Ollamy");XmlElement error;
      if(envelope.TryGetValue("error",out error))throw new Exception("Ollama: "+OllamaJson.String(error,"error",12000));
      return result;
     }
    }
   }catch(HttpRequestException e){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    if(limit.IsCancellationRequested)throw new Exception("Ollama nie odpowiedziała w wyznaczonym czasie. Wybierz mniejszy model lub spróbuj ponownie.",e);
    throw new Exception("Nie można połączyć się z Ollamą pod adresem "+Endpoint+". Uruchom aplikację Ollama i kliknij Odśwież modele.",e);
   }catch(OperationCanceledException){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    throw new Exception("Przekroczono czas oczekiwania na Ollamę. Spróbuj z mniejszym modelem.");
   }catch(IOException e){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    if(limit.IsCancellationRequested)throw new Exception("Przekroczono czas oczekiwania na Ollamę. Spróbuj z mniejszym modelem.",e);
    throw new Exception("Połączenie z Ollamą zostało przerwane. Spróbuj ponownie.",e);
   }catch(ObjectDisposedException e){
    if(cancellation.IsCancellationRequested)throw new OperationCanceledException(cancellation);
    if(limit.IsCancellationRequested)throw new Exception("Przekroczono czas oczekiwania na Ollamę. Spróbuj z mniejszym modelem.",e);
    throw;
   }
  }
 }
 static async Task<string> ReadBoundedAsync(Stream input,int maximum,CancellationToken cancellation){
  using(MemoryStream output=new MemoryStream()){byte[] buffer=new byte[8192];int read;while((read=await AwaitCancellationAsync(input.ReadAsync(buffer,0,buffer.Length,cancellation),cancellation))>0){if(output.Length+read>maximum)throw new Exception("Odpowiedź Ollamy jest za duża (limit 4 MB).");output.Write(buffer,0,read);}return Encoding.UTF8.GetString(output.ToArray());}
 }
 // HttpClient with ResponseHeadersRead returns HTTP errors as soon as headers
 // arrive. HttpWebRequest.GetResponseAsync pre-buffers error bodies, which can
 // otherwise keep a broken server's error response pending for many minutes.
 static HttpClient CreateClient(){return new HttpClient(new HttpClientHandler{UseProxy=false,AllowAutoRedirect=false}){Timeout=Timeout.InfiniteTimeSpan};}
 static async Task EnsureSuccessAsync(HttpResponseMessage response,CancellationToken cancellation){
  if(response.IsSuccessStatusCode)return;
  int status=(int)response.StatusCode;
  if(status>=300&&status<400)throw new Exception("Ollama zwróciła przekierowanie zamiast odpowiedzi. Sprawdź adres serwera.");
  string detail=await ReadErrorAsync(response,cancellation);
  throw new Exception("Ollama (HTTP "+status+"): "+(detail.Length==0?"serwer nie zwrócił poprawnej odpowiedzi.":detail)+"\nSprawdź nazwę modelu i jego obsługę odpowiedzi JSON.");
 }
 static async Task<string> ReadErrorAsync(HttpResponseMessage response,CancellationToken cancellation){
  string detail="";
  using(CancellationTokenSource timeout=new CancellationTokenSource(3000))using(CancellationTokenSource linked=CancellationTokenSource.CreateLinkedTokenSource(cancellation,timeout.Token))using(linked.Token.Register(response.Dispose)){
   Task<string> read=null;
   try{
    read=ReadBoundedAsync(await response.Content.ReadAsStreamAsync(),12000,linked.Token);
    // A faulty server/stream must not keep a dialog busy for the 15-minute
    // generation limit. WhenAny also covers streams that ignore cancellation.
    Task ended=await Task.WhenAny(read,Task.Delay(Timeout.Infinite,linked.Token));
    if(ended==read){Dictionary<string,XmlElement> map=OllamaJson.Object(OllamaJson.Read(await read),"błąd Ollamy");XmlElement message;if(map.TryGetValue("error",out message)&&OllamaJson.Type(message)=="string")detail=message.InnerText;}
   }catch(Exception){}
   finally{if(read!=null&&!read.IsCompleted)ObserveFault(read);}
   cancellation.ThrowIfCancellationRequested();
  }
  return detail;
 }
 static async Task<T> AwaitCancellationAsync<T>(Task<T> operation,CancellationToken cancellation){
  if(operation.IsCompleted)return await operation;
  TaskCompletionSource<bool> cancelled=new TaskCompletionSource<bool>();
  using(cancellation.Register(()=>cancelled.TrySetResult(true))){
   if(await Task.WhenAny(operation,cancelled.Task)!=operation){ObserveFault(operation);cancellation.ThrowIfCancellationRequested();}
   return await operation;
  }
 }
 static void ObserveFault(Task task){task.ContinueWith(t=>{var observed=t.Exception;},CancellationToken.None,TaskContinuationOptions.OnlyOnFaulted,TaskScheduler.Default);}
}
}
