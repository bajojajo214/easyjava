using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace EasyJava2 {
// Local protocol fixtures never require a downloaded model or run generated commands.
public static class OllamaTests {
 static void Check(bool condition,string text){if(!condition)throw new Exception("Ollama: "+text);}
 static string Q(string value){StringBuilder b=new StringBuilder("\"");foreach(char c in value){switch(c){case '\\':b.Append("\\\\");break;case '"':b.Append("\\\"");break;case '\n':b.Append("\\n");break;case '\r':b.Append("\\r");break;case '\t':b.Append("\\t");break;default:if(c<32)b.Append("\\u"+((int)c).ToString("x4"));else b.Append(c);break;}}return b.Append('"').ToString();}
 static string Document(string blocks,string extra=""){return "{\"name\":\"projekt_ai\",\"summary\":\"Projekt utworzony z opisu.\",\"blocks\":["+blocks+"]"+extra+"}";}
 static string Envelope(string content){return "{\"model\":\"fixture:latest\",\"message\":{\"role\":\"assistant\",\"content\":"+Q(content)+"},\"done\":true}";}
 static void Reject(string json,string text){bool rejected=false;try{OllamaProject.Parse(json);}catch(Exception){rejected=true;}Check(rejected,text);}
 static async Task Cancelled(Task task,string text){Task finished=await Task.WhenAny(task,Task.Delay(3000));Check(finished==task,text+" — brak szybkiego przerwania");bool cancelled=false;try{await task;}catch(OperationCanceledException){cancelled=true;}Check(cancelled,text);}
 static async Task Fail(Task task,string contains,string text){bool failed=false;try{await task;}catch(OperationCanceledException){throw;}catch(Exception ex){failed=string.IsNullOrEmpty(contains)||ex.Message.IndexOf(contains,StringComparison.OrdinalIgnoreCase)>=0;}Check(failed,text);}

 public static async Task Run(List<string> results,string directory){
  ParseCases(results);
  await ProtocolCases(results);
  await DialogCases(results,directory);
  await GeneratedGui(results,directory);
 }

 static void ParseCases(List<string> results){
  string plain=Document("{\"kind\":\"ui_button\"}");OllamaGeneration basic=OllamaProject.Parse(plain);Block button=basic.Project.Blocks.Single(),defaults=Block.Make("ui_button");
  Check(basic.Project.Name=="projekt_ai"&&basic.Summary=="Projekt utworzony z opisu.","nazwa i opis projektu");
  foreach(Field field in Catalog.All["ui_button"].Fields)Check(button.Get(field.Key)==defaults.Get(field.Key),"domyślne pole "+field.Key+" pominięte przez model");
  Check(button.Children.Count==0&&button.Else.Count==0&&basic.Project.Allows("Okna i GUI"),"struktura nowego bloczka");
  Project numeric=OllamaProject.Parse(Document("{\"kind\":\"set\",\"A\":\"liczba1\",\"B\":\"0\"},{\"kind\":\"ui_get\",\"A\":\"input2\",\"B\":\"liczba2\"},{\"kind\":\"math\",\"A\":\"wynik\",\"B\":\"liczba1\",\"C\":\"+\",\"D\":\"liczba2\"},{\"kind\":\"change\",\"A\":\"wynik\",\"B\":\"liczba1\"},{\"kind\":\"math\",\"A\":\"test\",\"B\":\"12\",\"C\":\"+\",\"D\":\"=liczba1 * 2\"},{\"kind\":\"math\",\"A\":\"test2\",\"B\":\"nieznana\",\"C\":\"+\",\"D\":\"LICZBA1\"},{\"kind\":\"say\",\"A\":\"liczba1\"}")).Project;
  Check(numeric.Blocks[2].B=="{liczba1}"&&numeric.Blocks[2].D=="{liczba2}"&&numeric.Blocks[3].B=="{liczba1}","AI nie poprawiło zapisu znanych zmiennych w operandach liczbowych");
  Check(numeric.Blocks[4].B=="12"&&numeric.Blocks[4].D=="=liczba1 * 2"&&numeric.Blocks[5].B=="nieznana"&&numeric.Blocks[5].D=="LICZBA1"&&numeric.Blocks[6].A=="liczba1","poprawa referencji AI zmienia literały, wyrażenia, nieznane nazwy lub zwykły tekst");
  Project manual=new Project();manual.Blocks.Add(Block.Make("set","liczba1","0"));manual.Blocks.Add(Block.Make("math","wynik","liczba1","+","1"));Check(ProjectIO.FromJson(ProjectIO.Json(manual)).Blocks[1].B=="liczba1","poprawa AI nie może zmieniać zwykłego odczytu projektu użytkownika");
  Project escaped=OllamaProject.Parse(Document("{\"kind\":\"math\",\"A\":\"wynik\",\"B\":\"20\",\"C\":"+Q("\\/")+",\"D\":\"4\"},{\"kind\":\"say\",\"A\":"+Q("\\/")+"}")).Project;OllamaProject.ValidateGenerated(escaped);Check(escaped.Blocks[0].C=="/"&&escaped.Blocks[1].A=="\\/","poprawa dodatkowo ucieczonego dzielenia AI zmienia zwykły tekst lub nie poprawia math.C");
  Project badOperator=new Project();badOperator.Blocks.Add(Block.Make("math","wynik","1","nonsense","2"));bool badOp=false;try{OllamaProject.ValidateGenerated(badOperator);}catch(Exception error){badOp=error.Message.Contains("znak działania");}Check(badOp,"AI przyjęło nieznany operator działania");
  foreach(string field in new[]{"B","C"}){
   Project badWindow=new Project();Block window=Block.Make("window","Okno","640","420","#eef2ff");window.Set(field,";");badWindow.Blocks.Add(window);bool rejected=false;
   try{OllamaProject.ValidateGenerated(badWindow);}catch(Exception error){rejected=error.Message.Contains(";");}Check(rejected,"AI przyjęło średnik zamiast wymiaru okna w polu "+field);
  }
  foreach(string field in new[]{"C","D","E","F"}){
   Project badBounds=new Project();Block label=Block.Make("ui_label","opis","Tekst; nie jest liczbą","30","40","200","35");label.Set(field,";");badBounds.Blocks.Add(label);bool rejected=false;
   try{OllamaProject.ValidateGenerated(badBounds);}catch(Exception error){rejected=error.Message.Contains(";");}Check(rejected,"AI przyjęło nieprawidłową współrzędną lub rozmiar GUI w polu "+field);
  }
  Project commaDimensions=new Project();commaDimensions.Blocks.Add(Block.Make("window","Okno","640,5","420,25","#eef2ff"));commaDimensions.Blocks.Add(Block.Make("ui_label","opis","Tekst; pozostaje tekstem","30,5","40,25","200,5","35,25"));OllamaProject.ValidateGenerated(commaDimensions);
  Project dynamicDimensions=new Project();dynamicDimensions.Blocks.Add(Block.Make("window","Okno","=szerokosc + 20","{wysokosc}","#eef2ff"));dynamicDimensions.Blocks.Add(Block.Make("ui_label","opis","Tekst","{pozycja_x}","=pozycja_y + 10","{szerokosc_pola}","=wysokosc_pola * 2"));OllamaProject.ValidateGenerated(dynamicDimensions);
  Project manualDimensions=new Project();manualDimensions.Blocks.Add(Block.Make("window","Ręczny projekt",";","420","#eef2ff"));manualDimensions.Blocks.Add(Block.Make("ui_label","opis","Tekst",";","40","200","35"));Project reopenedDimensions=ProjectIO.FromJson(ProjectIO.Json(manualDimensions));Check(reopenedDimensions.Blocks[0].B==";"&&reopenedDimensions.Blocks[1].C==";","walidacja wymiarów AI zmienia lub blokuje zwykły odczyt ręcznego projektu");
  bool missingGui=false;try{OllamaProject.ValidateGenerated(OllamaProject.Parse(Document("{\"kind\":\"ui_get\",\"A\":\"brak_pola\",\"B\":\"tekst\"}")).Project);}catch(Exception error){missingGui=error.Message.Contains("brak_pola");}Check(missingGui,"AI przyjęło odwołanie do nieistniejącego elementu GUI");
  OllamaProject.ValidateGenerated(Examples.Counter());
  Project inert=new Project();inert.Blocks.Add(Block.Make("ui_button","martwy","Dodaj"));bool dead=false;try{OllamaProject.ValidateGenerated(inert);}catch(Exception error){dead=error.Message.Contains("martwy")&&error.Message.Contains("obsługi");}Check(dead,"AI przyjęło przycisk bez obsługi kliknięcia");
  Block handler=Block.Make("event_button","martwy");handler.Children.Add(Block.Make("set","test","1"));inert.Blocks.Add(handler);OllamaProject.ValidateGenerated(inert);
  Project wrongAction=new Project();wrongAction.Blocks.Add(Block.Make("ui_button","dziel","÷"));Block wrongHandler=Block.Make("event_button","dziel");wrongHandler.Children.Add(Block.Make("math","wynik","20","*","4"));wrongAction.Blocks.Add(wrongHandler);bool mismatch=false;try{OllamaProject.ValidateGenerated(wrongAction);}catch(Exception error){mismatch=error.Message.Contains("math.C");}Check(mismatch,"AI przyjęło mnożenie pod przyciskiem dzielenia");wrongHandler.Children[0].C="/";OllamaProject.ValidateGenerated(wrongAction);
  Project inactive=new Project();Block off=Block.Make("repeat","1");off.Disabled=true;off.Children.Add(Block.Make("ui_get","brak_w_wylaczonej_galezi","tekst"));inactive.Blocks.Add(off);OllamaProject.ValidateGenerated(inactive);
  Project dynamicGui=new Project();dynamicGui.Blocks.Add(Block.Make("ui_text","{dynamiczny_id}","tekst"));OllamaProject.ValidateGenerated(dynamicGui);
  bool hiddenGui=false;Project hidden=new Project();hidden.Blocks.Add(Block.Make("ui_label","cover","Opis","0","0","400","300"));hidden.Blocks.Add(Block.Make("ui_button","run","Uruchom","20","30","120","40"));try{OllamaProject.ValidateGenerated(hidden);}catch(Exception error){hiddenGui=error.Message.Contains("zasłonięty");}Check(hiddenGui,"AI przyjęło przycisk całkowicie zakryty wcześniejszym napisem");
  bool badFormula=false;Project formula=new Project();formula.Blocks.Add(Block.Make("change","wynik","liczba1 + liczba2"));try{OllamaProject.ValidateGenerated(formula);}catch(Exception error){badFormula=error.Message.Contains("=");}Check(badFormula,"AI przyjęło wzór bez = w polu liczbowym");
  Project hiddenByEditor=new Project();hiddenByEditor.Blocks.Add(Block.Make("ui_textarea","editor","","0","0","400","300"));hiddenByEditor.Blocks.Add(Block.Make("ui_button","save","Zapisz","20","30","120","40"));bool coveredByEditor=false;try{OllamaProject.ValidateGenerated(hiddenByEditor);}catch(Exception error){coveredByEditor=error.Message.Contains("zasłonięty");}Check(coveredByEditor,"AI przyjęło przycisk zakryty dużym polem tekstowym");
  Check(OllamaProject.CreateSchema(false).Contains("\"disabled\":{\"type\":\"boolean\",\"const\":false}")&&OllamaProject.CreateSchema(true).Contains("\"disabled\":{\"type\":\"boolean\"}"),"schemat nowego projektu nie blokuje przypadkowego wyłączania bloczków lub zmienia stan edytowanego projektu");
  results.Add("PASS AI: odpowiedź modelu staje się edytowalnym projektem; pola pominięte przez model otrzymują wartości katalogu");

  string text="Zażółć gęślą jaźń\r\nTekst z {punkty}, cudzysłowem \" oraz \\";
  string nested=Document("{\"kind\":\"event_button\",\"A\":\"plus\",\"children\":[{\"kind\":\"if_else\",\"A\":\"{punkty}\",\"B\":\"5\",\"Op\":\">=\",\"children\":[{\"kind\":\"say\",\"A\":"+Q(text)+"}],\"otherwise\":[{\"kind\":\"change\",\"A\":\"punkty\",\"B\":\"1\"}]}]}",",\"stepDelaySeconds\":0.25");
  Project branch=OllamaProject.Parse(nested).Project;Block condition=branch.Blocks[0].Children.Single();
  Check(condition.Op==">="&&condition.Children.Single().A==text&&condition.Else.Single().Kind=="change"&&branch.StepDelaySeconds==.25,"zdarzenia, gałęzie warunku, Unicode lub przerwa");
  Check(ProjectIO.Json(ProjectIO.FromJson(ProjectIO.Json(branch)))==ProjectIO.Json(branch),"zgenerowany projekt nie przechodzi normalnego zapisu");
  results.Add("PASS AI: zagnieżdżone zdarzenia i obie gałęzie warunku zachowują operatory, polskie znaki, nowe wiersze i przerwy");

  Project context=new Project{Name="oryginalny",SourcePath=Path.Combine(Path.GetTempPath(),"projekt-ai-kontekst.js")};context.Assets["obraz"]=Convert.ToBase64String(new byte[]{1,2,3});context.Features["Ruch"]=false;context.Blocks.Add(Block.Make("say","stary"));string before=ProjectIO.Json(context);
  OllamaGeneration edited=OllamaProject.Parse(Document("{\"kind\":\"cmd\",\"A\":\"echo fixture\",\"background\":true},{\"kind\":\"file_write\",\"A\":\"fixture.txt\",\"B\":\"test\"},{\"kind\":\"say\",\"disabled\":true}"),context);
  Check(edited.Project.Assets["obraz"]==context.Assets["obraz"]&&edited.Project.SourcePath==context.SourcePath,"utrata obrazów lub lokalizacji edytowanego projektu");
  Check(edited.Project.Allows("Terminal")&&edited.Project.Allows("Pliki")&&!edited.Project.Allows("Ruch"),"używane kategorie nie zostały włączone albo zmieniono nieużywaną kategorię");
  Check(edited.Project.Blocks[0].RunInBackground&&edited.Project.Blocks[2].Disabled,"tryb w tle lub wyłączony bloczek");
  edited.Project.Assets["obraz"]="zmieniony";Check(ProjectIO.Json(context)==before,"generowanie zmieniło oryginalny projekt lub współdzieli jego obrazy");
  results.Add("PASS AI: modyfikacja zachowuje kopię obrazów i folder projektu, włącza używane Pliki/Terminal oraz nie zmienia oryginału");

  Reject(Document("{\"kind\":\"invented_block\"}"),"nieznany bloczek został przyjęty");
  Reject(Document("{\"kind\":\"say\",\"A\":123}"),"pole liczbowe zamiast tekstowego zostało przyjęte");
  Reject(Document("{\"kind\":\"say\",\"mistyped\":\"x\"}"),"nieznane pole bloczka zostało przemilczane");
  Reject(Document("{\"kind\":\"say\",\"children\":[{\"kind\":\"say\"}]}"),"bloczek bez wnętrza przyjął dzieci");
  Reject(Document("{\"kind\":\"repeat\",\"otherwise\":[{\"kind\":\"say\"}]}"),"pętla przyjęła gałąź else");
  Reject(Document("{\"kind\":\"repeat\",\"children\":[{\"kind\":\"event_start\",\"children\":[{\"kind\":\"say\"}]}]}"),"zdarzenie wewnątrz pętli zostało przyjęte");
  Reject(Document("{\"kind\":\"say\"}",",\"unknown\":true"),"nieznane pole projektu zostało przemilczane");
  Reject("{\"name\":\"a\",\"name\":\"b\",\"blocks\":[]}","powtórzone klucze zostały przyjęte");
  Reject("To jest kod JavaScript: alert('x');","surowy kod został uznany za projekt");
  Reject("{\"name\":\"a\",\"blocks\":[", "niepełny JSON został przyjęty");
  results.Add("PASS AI: nieznane bloczki/pola, błędne typy, niedozwolone wnętrza i else, powtórzone klucze oraz uszkodzony JSON są odrzucane");

  string deep="{\"kind\":\"say\"}";for(int i=0;i<20;i++)deep="{\"kind\":\"repeat\",\"children\":["+deep+"]}";Reject(Document(deep),"brak limitu głębokości");
  Check(OllamaProject.Parse(Document(string.Join(",",Enumerable.Repeat("{\"kind\":\"say\"}",500)))).Project.Blocks.Count==500,"dozwolone 500 bloczków zostało odrzucone");
  Reject(Document(string.Join(",",Enumerable.Repeat("{\"kind\":\"say\"}",501))),"brak limitu 500 bloczków");
  Check(OllamaProject.Parse(Document("{\"kind\":\"say\",\"A\":"+Q(new string('a',32768))+"}")).Project.Blocks[0].A.Length==32768,"dozwolona granica długości pola została odrzucona");
  Reject(Document("{\"kind\":\"say\",\"A\":"+Q(new string('a',32769))+"}"),"brak limitu długości pola");
  Reject(Document("{\"kind\":\"say\"}",",\"stepDelaySeconds\":-1"),"ujemna przerwa została przyjęta");
  Reject(Document("{\"kind\":\"say\"}",",\"stepDelaySeconds\":3601"),"zbyt duża przerwa została przyjęta");
  results.Add("PASS AI: limity głębokości, 500 bloczków, długości pól i dozwolonego zakresu przerw ograniczają odpowiedź modelu");

  string schema=OllamaProject.CreateSchema(),system=OllamaProject.CreateSystemPrompt();
  Check(!system.Contains("PEŁNY PRZYKŁAD KALKULATORA")&&OllamaProject.CreateSystemPrompt("zrob mi prosty kalkulator").Contains("PEŁNY PRZYKŁAD KALKULATORA")&&!OllamaProject.CreateSystemPrompt("Zrób notatnik").Contains("PEŁNY PRZYKŁAD KALKULATORA"),"przykład kalkulatora musi dotyczyć polecenia, zamiast narzucać kalkulator wszystkim zadaniom");
  Check(schema.Contains("\"blocks\"")&&schema.Contains("\"kind\"")&&schema.Contains("\"required\""),"brak schematu projektu");
  foreach(string kind in Catalog.All.Keys)Check(system.Contains(kind)&&schema.Contains(Q(kind)),"brak definicji bloczka w instrukcji/schemacie: "+kind);
  Dictionary<string,System.Xml.XmlElement> schemaRoot=OllamaJson.Object(OllamaJson.Read(schema),"schemat");
  Dictionary<string,System.Xml.XmlElement> definitions=OllamaJson.Object(schemaRoot["$defs"],"definicje");
  Dictionary<string,System.Xml.XmlElement> blockSchema=OllamaJson.Object(definitions["block"],"bloczek");
  Dictionary<string,Dictionary<string,System.Xml.XmlElement>> shapes=new Dictionary<string,Dictionary<string,System.Xml.XmlElement>>();
  foreach(System.Xml.XmlNode item in blockSchema["oneOf"].ChildNodes){System.Xml.XmlElement shape=item as System.Xml.XmlElement;if(shape==null)continue;Dictionary<string,System.Xml.XmlElement> properties=OllamaJson.Object(OllamaJson.Object(shape,"wariant")["properties"],"pola");string kind=OllamaJson.Object(properties["kind"],"rodzaj")["const"].InnerText;shapes.Add(kind,properties);}
  Check(shapes.Count==Catalog.All.Count&&!shapes["change"].ContainsKey("C")&&!shapes["file_open"].ContainsKey("B")&&shapes["math"].ContainsKey("C")&&shapes["math"].ContainsKey("D"),"schemat ponownie pozwala modelowi mieszać pola dodawania, matematyki lub wyboru pliku");
  Check(shapes["event_button"].ContainsKey("children")&&!shapes["say"].ContainsKey("children")&&shapes["if_else"].ContainsKey("otherwise")&&!shapes["repeat"].ContainsKey("otherwise"),"schemat dopuszcza niedozwolone wnętrza bloczków");
  results.Add("PASS AI: instrukcja i schemat zawierają wszystkie 102 rodzaje bloczków bieżącego katalogu");
 }

 static async Task ProtocolCases(List<string> results){
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"models\":[{\"name\":\"qwen3:4b\"},{\"name\":\"a-model:latest\"},{\"name\":\"qwen3:4b\"}]}"));})){
   List<string> models=await new OllamaClient(server.Url).ListModelsAsync(CancellationToken.None);Request request=await server.Received.Task;
   Check(request.Method=="GET"&&request.Path=="/api/tags", "lista modeli nie używa GET /api/tags");
   Check(models.SequenceEqual(new[]{"a-model:latest","qwen3:4b"}),"lista modeli nie jest uporządkowana lub zawiera duplikaty");await server.Completion;
  }
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"models\":[]}"));}))Check((await new OllamaClient(server.Url).ListModelsAsync(CancellationToken.None)).Count==0,"pusta lista modeli");
  results.Add("PASS AI HTTP: prawdziwe żądanie GET /api/tags odczytuje zainstalowane modele, usuwa duplikaty i obsługuje pustą listę");

  string prompt="Zrób licznik z przyciskiem +1 i polskim tekstem żółw.";Project context=new Project{Name="kontekst_ai"};context.Blocks.Add(Block.Make("set","punkty","0"));
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,Envelope(Document("{\"kind\":\"ui_label\",\"A\":\"plus\",\"B\":\"+1\"}"))));})){
   OllamaGeneration generation=await new OllamaClient(server.Url).GenerateAsync("fixture:latest",prompt,context,CancellationToken.None);Request request=await server.Received.Task;ChatRequest body=Read<ChatRequest>(request.Body);
   Check(request.Method=="POST"&&request.Path=="/api/chat", "generowanie nie używa POST /api/chat");
   Check(body.Model=="fixture:latest"&&body.Stream&&body.Messages!=null&&body.Messages.Any(m=>m.Role=="system")&&body.Messages.Any(m=>m.Role=="user"&&m.Content.Contains(prompt)),"niepoprawny model, prompt Unicode lub struktura rozmowy");
   Check(body.Messages.Any(m=>m.Content.Contains("kontekst_ai"))&&body.Messages.Any(m=>m.Content.Contains("punkty")),"brak kontekstu bieżącego projektu");
   int format=request.Body.IndexOf("\"format\"",StringComparison.Ordinal);Check(format>=0,"brak formatu odpowiedzi");int colon=request.Body.IndexOf(':',format);Check(request.Body.Substring(colon+1).TrimStart().StartsWith("{"),"format nie zawiera obiektu JSON Schema");
   Check(generation.Project.Blocks.Single().Kind=="ui_label"&&generation.Project.Blocks[0].B=="+1","odpowiedź /api/chat nie została zamieniona na bloczki");await server.Completion;
  }
  results.Add("PASS AI HTTP: POST /api/chat przesyła wybrany model, opis UTF-8, schemat i kontekst, a odpowiedź zamienia na edytowalne bloczki");

  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(404,"{\"error\":\"model fixture missing\"}"));}))await Fail(new OllamaClient(server.Url).GenerateAsync("missing","test",null,CancellationToken.None),"fixture missing","brak czytelnego błędu modelu HTTP 404");
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"error\":\"fixture model failed\"}"));}))await Fail(new OllamaClient(server.Url).GenerateAsync("fixture","test",null,CancellationToken.None),"fixture model failed","błąd Ollamy w odpowiedzi 200 został przeoczony");
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"not JSON"));}))await Fail(new OllamaClient(server.Url).ListModelsAsync(CancellationToken.None),null,"uszkodzona lista modeli została przyjęta");
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,Envelope(Document("{\"kind\":\"invented_block\"}"))));},2))await Fail(new OllamaClient(server.Url).GenerateAsync("fixture","test",null,CancellationToken.None),null,"nieznany bloczek przeszedł walidację klienta HTTP");
  results.Add("PASS AI HTTP: brak modelu, błąd silnika, uszkodzony JSON i nieznany bloczek zwracają błąd zamiast zmieniać projekt");

  List<string> progress=new List<string>();
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"status\":\"pulling manifest\"}\n{\"status\":\"pulling fixture\",\"total\":1048576,\"completed\":524288}\n{\"status\":\"success\"}\n"));})){
   await new OllamaClient(server.Url).PullModelAsync("fixture:latest",CancellationToken.None,progress.Add);Request request=await server.Received.Task;
   Check(request.Method=="POST"&&request.Path=="/api/pull"&&request.Body.Contains("fixture:latest"),"pobieranie modelu nie używa /api/pull");
   Check(progress.Any(x=>x.Contains("50%"))&&progress.Last().Contains("Gotowe"),"brak postępu i potwierdzenia pobrania modelu");await server.Completion;
  }
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"status\":\"pulling manifest\"}\n{\"error\":\"fixture pull failed\"}\n"));}))await Fail(new OllamaClient(server.Url).PullModelAsync("fixture",CancellationToken.None,null),"fixture pull failed","błąd strumienia pobierania został przeoczony");
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,"{\"status\":\"pulling manifest\"}\n"));}))await Fail(new OllamaClient(server.Url).PullModelAsync("fixture",CancellationToken.None,null),null,"przerwane pobranie zostało zgłoszone jako sukces");
  results.Add("PASS AI HTTP: pobieranie modelu /api/pull odczytuje strumień postępu, wymaga sukcesu i wykrywa błędy oraz przerwany transfer");

  using(CancellationTokenSource cancel=new CancellationTokenSource()){
   cancel.Cancel();await Cancelled(new OllamaClient("http://127.0.0.1:1").ListModelsAsync(cancel.Token),"anulowanie przed żądaniem listy modeli");
   await Cancelled(new OllamaClient("http://127.0.0.1:1").GenerateAsync("fixture","test",null,cancel.Token),"anulowanie przed generowaniem");
  }
  using(FakeServer server=new FakeServer(async delegate(Request request){await Task.Delay(30000);return new Response(200,Envelope(Document("{\"kind\":\"say\"}")));}))using(CancellationTokenSource cancel=new CancellationTokenSource()){
   Task<OllamaGeneration> pending=new OllamaClient(server.Url).GenerateAsync("fixture","test",null,cancel.Token);Task first=await Task.WhenAny(server.Received.Task,Task.Delay(5000));Check(first==server.Received.Task,"serwer testowy nie otrzymał generowania");cancel.Cancel();await Cancelled(pending,"anulowanie czekającego generowania");
  }
  results.Add("PASS AI HTTP: anulowanie przed wysłaniem i podczas odpowiedzi szybko przerywa operację bez uruchamiania programu");

  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(404,"{\"error\":\"slow fixture\"}"){BodyDelay=10000});}))using(System.Windows.Forms.Timer heartbeat=new System.Windows.Forms.Timer{Interval=30}){
   int beats=0;heartbeat.Tick+=delegate{beats++;};heartbeat.Start();Stopwatch clock=Stopwatch.StartNew();Task pending=new OllamaClient(server.Url).GenerateAsync("fixture","test",null,CancellationToken.None);Task ended=await Task.WhenAny(pending,Task.Delay(5000));Check(ended==pending,"odczyt błędu HTTP utknął na niekończącej się odpowiedzi");await Fail(pending,null,"zawieszona odpowiedź błędu została przyjęta");heartbeat.Stop();Check(clock.ElapsedMilliseconds<5000&&beats>2,"odczyt błędu HTTP blokuje wątek GUI");
  }
  results.Add("PASS AI HTTP: zawieszony strumień błędu ma krótki limit i nie blokuje responsywności GUI");
 }

 static async Task DialogCases(List<string> results,string directory){
  Project original=Examples.Counter();original.SourcePath=Path.Combine(Path.GetFullPath(directory),"stary-projekt-ai.js");string before=ProjectIO.Json(original);string unexpected=Path.Combine(Path.GetFullPath(directory),"ai-dialog-nie-uruchamiaj-"+Guid.NewGuid().ToString("N")+".txt");string generated=Document("{\"kind\":\"file_write\",\"A\":"+Q(unexpected)+",\"B\":\"nie powinno się zapisać bez podglądu\"}");
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,request.Path=="/api/tags"?"{\"models\":[{\"name\":\"inny-model:latest\"}]}":Envelope(generated)));},2))using(OllamaDialog dialog=new OllamaDialog(original,false,false)){
   dialog.ShowInTaskbar=false;dialog.WindowState=FormWindowState.Minimized;dialog.Show();dialog.EndpointInput.Text=server.Url;dialog.ModelInput.Text="fixture:latest";dialog.PromptInput.Text="Przygotuj projekt z zapisem, ale go nie uruchamiaj.";await dialog.GenerateProjectAsync();
   Check(dialog.Result!=null&&dialog.UseButton.Enabled&&dialog.ResultTree.Nodes.Count==1,"dialog nie udostępnił gotowej propozycji");
   Check(dialog.ResultModifiesCurrent&&ProjectIO.Json(original)==before&&!File.Exists(unexpected),"generowanie w dialogu zmieniło oryginał lub uruchomiło zapis pliku");
   await dialog.RefreshModelsAsync();Check(dialog.ModelInput.Text=="inny-model:latest"&&dialog.Result==null&&!dialog.UseButton.Enabled,"odświeżenie zmieniło model, ale pozostawiło starą propozycję");await server.Completion;dialog.Close();
  }
  using(FakeServer server=new FakeServer(delegate(Request request){return Task.FromResult(new Response(200,Envelope(generated)));}))using(OllamaDialog dialog=new OllamaDialog(original,false,false)){
   dialog.ShowInTaskbar=false;dialog.WindowState=FormWindowState.Minimized;dialog.Show();dialog.EndpointInput.Text=server.Url;dialog.ModelInput.Text="fixture:latest";dialog.PromptInput.Text="Opis testowy";await dialog.GenerateProjectAsync();Check(dialog.Result!=null,"brak propozycji przed zmianą opisu");dialog.PromptInput.Text="Nowy opis";Check(dialog.Result==null&&!dialog.UseButton.Enabled&&dialog.ResultTree.Nodes.Count==0,"zmiana opisu pozostawiła starą propozycję");dialog.Close();
  }
  results.Add("PASS AI dialog: generowanie pokazuje bloczki bez wykonywania zapisu i zmiany projektu; zmiana opisu lub modelu usuwa starą propozycję");

  using(FakeServer server=new FakeServer(async delegate(Request request){await Task.Delay(30000);return new Response(200,Envelope(generated));}))using(OllamaDialog dialog=new OllamaDialog(original,false,false)){
   dialog.ShowInTaskbar=false;dialog.WindowState=FormWindowState.Minimized;dialog.Show();dialog.EndpointInput.Text=server.Url;dialog.ModelInput.Text="fixture:latest";dialog.PromptInput.Text="Opis testowy";Task pending=dialog.GenerateProjectAsync();Task first=await Task.WhenAny(server.Received.Task,Task.Delay(5000));Check(first==server.Received.Task,"dialog nie rozpoczął żądania");dialog.Close();Task ended=await Task.WhenAny(pending,Task.Delay(3000));Check(ended==pending,"zamknięcie dialogu nie przerwało generowania");await pending;Check(dialog.IsDisposed&&dialog.Result==null&&!File.Exists(unexpected)&&ProjectIO.Json(original)==before,"zamknięty dialog zmienił projekt lub uruchomił wynik");
  }
  results.Add("PASS AI dialog: zamknięcie podczas generowania przerywa żądanie i chroni projekt przed spóźnionym wynikiem");
 }

 static async Task GeneratedGui(List<string> results,string directory){
  string json=Document("{\"kind\":\"set\",\"A\":\"punkty\",\"B\":\"0\"},{\"kind\":\"ui_label\",\"A\":\"wynik\",\"B\":\"Wynik: 0\"},{\"kind\":\"ui_button\",\"A\":\"plus\",\"B\":\"+1\"},{\"kind\":\"event_button\",\"A\":\"plus\",\"children\":[{\"kind\":\"change\",\"A\":\"punkty\",\"B\":\"1\"},{\"kind\":\"ui_text\",\"A\":\"wynik\",\"B\":\"Wynik: {punkty}\"}]}");Project generated=OllamaProject.Parse(json).Project;
  string fixture=Path.Combine(directory,"projekt-ai-licznik.js");ProjectIO.Write(fixture,generated,Application.ExecutablePath);Project reopened=ProjectIO.Read(fixture);Check(reopened.Blocks.Count==4,"zapis .js utracił wygenerowane bloczki");
  using(Form form=new Form{ShowInTaskbar=false,WindowState=FormWindowState.Minimized,Size=new Size(800,600)})using(RuntimeView runtime=new RuntimeView()){
   form.Controls.Add(runtime);form.Show();runtime.Silent=true;await runtime.Start(reopened);Check(runtime.Errors.Count==0&&runtime.Stage.Elements.ContainsKey("plus"),"wygenerowany projekt nie tworzy GUI");((Button)runtime.Stage.Elements["plus"].Control).PerformClick();await Task.Delay(50);Check(RuntimeView.Number(runtime.Get("punkty"))==1&&runtime.Stage.Elements["wynik"].Control.Text=="Wynik: 1","wygenerowane zdarzenie przycisku nie działa");runtime.Stop();form.Close();
  }
  results.Add("PASS AI runtime: wygenerowany licznik zapisuje się do .js, otwiera do edycji i działa w prawdziwym GUI z obsługą kliknięcia");

  Project calculator=Examples.Calculator();OllamaProject.ValidateGenerated(calculator);string calculatorJson=ProjectIO.Json(calculator);
  string calculatorPath=Path.Combine(directory,"wzorzec-kalkulator.js");ProjectIO.Write(calculatorPath,calculator,Application.ExecutablePath);Project calculatorReopened=ProjectIO.Read(calculatorPath);
  Check(ProjectIO.Json(calculatorReopened)==calculatorJson,"wzorzec kalkulatora traci bloczki lub ustawienia po zapisie i ponownym otwarciu .js");
  using(Form form=new Form{ShowInTaskbar=false,WindowState=FormWindowState.Minimized,Size=new Size(800,600)})using(RuntimeView runtime=new RuntimeView()){
   form.Controls.Add(runtime);form.Show();runtime.Silent=true;await runtime.Start(calculatorReopened);
   Check(runtime.Errors.Count==0,"wzorzec kalkulatora nie uruchamia się: "+string.Join(";",runtime.Errors));
   foreach(string[] test in new[]{new[]{"7","5","dodaj","12"},new[]{"7","5","odejmij","2"},new[]{"6","7","pomnoz","42"},new[]{"20","4","podziel","5"},new[]{"2,5","0,5","dodaj","3"},new[]{"-3","2","dodaj","-1"}}){
    ((TextBox)runtime.Stage.Elements["liczba1_input"].Control).Text=test[0];((TextBox)runtime.Stage.Elements["liczba2_input"].Control).Text=test[1];((Button)runtime.Stage.Elements[test[2]].Control).PerformClick();await Task.Delay(60);
    Check(runtime.Errors.Count==0&&runtime.Stage.Elements["wynik"].Control.Text=="Wynik: "+test[3],"wzorzec kalkulatora: "+test[0]+" "+test[2]+" "+test[1]+" ma wynik "+runtime.Stage.Elements["wynik"].Control.Text);
   }
   ((TextBox)runtime.Stage.Elements["liczba2_input"].Control).Text="0";((Button)runtime.Stage.Elements["podziel"].Control).PerformClick();await Task.Delay(60);
   Check(runtime.Errors.Count==0&&runtime.Stage.Elements["wynik"].Control.Text.Contains("zero"),"wzorzec kalkulatora nie wyjaśnia dzielenia przez zero");
   ((Button)runtime.Stage.Elements["wyczysc"].Control).PerformClick();await Task.Delay(60);
   Check(runtime.Errors.Count==0&&runtime.Stage.Elements["liczba1_input"].Control.Text=="0"&&runtime.Stage.Elements["liczba2_input"].Control.Text=="0"&&runtime.Stage.Elements["wynik"].Control.Text=="Wynik: 0","Wyczyść nie zeruje pól i wyniku wzorca kalkulatora");
   foreach(string id in new[]{"liczba1_input","liczba2_input","dodaj","odejmij","pomnoz","podziel","wyczysc","wynik"}){
    UiElement element=runtime.Stage.Elements[id];Check(element.Bounds.Left>=0&&element.Bounds.Top>=0&&element.Bounds.Right<=runtime.Stage.LogicalSize.Width&&element.Bounds.Bottom<=runtime.Stage.LogicalSize.Height,"element wzorca kalkulatora poza sceną: "+id);
   }
   runtime.Stop();form.Close();
  }
  results.Add("PASS kalkulator wzorcowy: zapis i odczyt .js, kliknięcia czterech działań, przecinki, liczby ujemne, dzielenie przez zero, Wyczyść oraz widoczne położenie kontrolek");
 }

 [DataContract]class ChatRequest{[DataMember(Name="model")]public string Model{get;set;}[DataMember(Name="stream")]public bool Stream{get;set;}[DataMember(Name="messages")]public List<ChatMessage> Messages{get;set;}}
 [DataContract]class ChatMessage{[DataMember(Name="role")]public string Role{get;set;}[DataMember(Name="content")]public string Content{get;set;}}
 static T Read<T>(string json){using(MemoryStream stream=new MemoryStream(Encoding.UTF8.GetBytes(json)))return (T)new DataContractJsonSerializer(typeof(T)).ReadObject(stream);}
 class Request{public string Method,Path,Body;}
 class Response{public int Status,BodyDelay;public string Body;public Response(int status,string body){Status=status;Body=body;}}
 sealed class FakeServer:IDisposable{
  readonly TcpListener listener;TcpClient active;volatile bool disposed;public readonly string Url;public readonly TaskCompletionSource<Request> Received=new TaskCompletionSource<Request>();public readonly Task Completion;
  public FakeServer(Func<Request,Task<Response>> respond,int requests=1){listener=new TcpListener(IPAddress.Loopback,0);listener.Start();Url="http://127.0.0.1:"+((IPEndPoint)listener.LocalEndpoint).Port;Completion=Task.Run(async delegate{for(int i=0;i<requests&&!disposed;i++)await Serve(respond);});}
  async Task Serve(Func<Request,Task<Response>> respond){try{using(TcpClient client=await listener.AcceptTcpClientAsync()){active=client;using(NetworkStream stream=client.GetStream()){
   stream.ReadTimeout=10000;MemoryStream headers=new MemoryStream();int a=0,b=0,c=0,d=0;while(true){int next=stream.ReadByte();if(next<0)throw new EndOfStreamException("Brak nagłówka HTTP");headers.WriteByte((byte)next);a=b;b=c;c=d;d=next;if(a==13&&b==10&&c==13&&d==10)break;if(headers.Length>32768)throw new IOException("Nagłówek testu jest zbyt duży");}
   string text=Encoding.ASCII.GetString(headers.ToArray());string[] lines=text.Split(new[]{"\r\n"},StringSplitOptions.None);string[] first=lines[0].Split(' ');int length=0;foreach(string line in lines){if(line.StartsWith("Content-Length:",StringComparison.OrdinalIgnoreCase))length=int.Parse(line.Substring(15).Trim());if(line.StartsWith("Expect:",StringComparison.OrdinalIgnoreCase)&&line.IndexOf("100-continue",StringComparison.OrdinalIgnoreCase)>=0){byte[] interim=Encoding.ASCII.GetBytes("HTTP/1.1 100 Continue\r\n\r\n");await stream.WriteAsync(interim,0,interim.Length);}}
   if(length>4000000)throw new IOException("Żądanie testu jest zbyt duże");byte[] body=new byte[length];int offset=0;while(offset<body.Length){int read=await stream.ReadAsync(body,offset,body.Length-offset);if(read==0)throw new EndOfStreamException();offset+=read;}
   Request request=new Request{Method=first[0],Path=first[1],Body=Encoding.UTF8.GetString(body)};Received.TrySetResult(request);Response response=await respond(request);if(disposed)return;byte[] bytes=Encoding.UTF8.GetBytes(response.Body);byte[] prefix=Encoding.ASCII.GetBytes("HTTP/1.1 "+response.Status+" "+(response.Status==200?"OK":"Error")+"\r\nContent-Type: application/json; charset=utf-8\r\nContent-Length: "+bytes.Length+"\r\nConnection: close\r\n\r\n");await stream.WriteAsync(prefix,0,prefix.Length);await stream.FlushAsync();if(response.BodyDelay>0)await Task.Delay(response.BodyDelay);if(disposed)return;await stream.WriteAsync(bytes,0,bytes.Length);await stream.FlushAsync();
  }}}catch(Exception ex){if(!disposed){Received.TrySetException(ex);throw;}}}
  public void Dispose(){disposed=true;listener.Stop();if(active!=null)active.Close();}
 }
}
}
