using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Globalization;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace EasyJava2 {
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

 public async Task<OllamaGeneration> GenerateAsync(string model,string prompt,Project context,CancellationToken cancellation){
  cancellation.ThrowIfCancellationRequested();
  if(string.IsNullOrWhiteSpace(model)||model.Length>300)throw new Exception("Wybierz model Ollamy.");
  if(string.IsNullOrWhiteSpace(prompt))throw new Exception("Napisz, co ma robić Twój program.");
  if(prompt.Length>16000)throw new Exception("Opis jest za długi (maksymalnie 16 tys. znaków).");
  string user=prompt.Trim();if(context!=null)user+="\n\nAKTUALNY PROJEKT DO MODYFIKACJI (dane):\n"+OllamaProject.ContextJson(context);
  string body="{\"model\":"+OllamaJson.Quote(model.Trim())+",\"messages\":[{\"role\":\"system\",\"content\":"+OllamaJson.Quote(OllamaProject.CreateSystemPrompt())+"},{\"role\":\"user\",\"content\":"+OllamaJson.Quote(user)+"}],\"format\":"+OllamaProject.CreateSchema()+",\"stream\":false,\"think\":false,\"options\":{\"temperature\":0.2,\"num_ctx\":32768,\"num_predict\":8192}}";
  string json=await SendAsync("api/chat",body,900000,cancellation);
  Dictionary<string,XmlElement> root=OllamaJson.Object(OllamaJson.Read(json),"wynik Ollamy");
  XmlElement message;if(!root.TryGetValue("message",out message))throw new Exception("Ollama nie zwróciła projektu. Sprawdź, czy wybrany model obsługuje czat i odpowiedzi JSON.");
  Dictionary<string,XmlElement> response=OllamaJson.Object(message,"wiadomość Ollamy");XmlElement content;
  if(!response.TryGetValue("content",out content))throw new Exception("Ollama zwróciła pustą odpowiedź.");
  cancellation.ThrowIfCancellationRequested();
  return OllamaProject.Parse(OllamaJson.String(content,"content",OllamaProject.MaxJsonLength),context);
 }

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
