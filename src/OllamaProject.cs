using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Xml;

namespace EasyJava2 {
public sealed class OllamaGeneration {
 public Project Project;
 public string Summary;
}

// The model returns data describing blocks, never executable source. Only Catalog
// entries accepted here can become part of an EasyJava project.
public static class OllamaProject {
 public const int MaxBlocks=500,MaxDepth=12,MaxFieldLength=32768,MaxJsonLength=1048576;
 static readonly HashSet<string> RootKeys=new HashSet<string>(new[]{"name","summary","stepDelaySeconds","blocks"},StringComparer.Ordinal);
 static readonly HashSet<string> BlockKeys=new HashSet<string>(new[]{"kind","A","B","C","D","E","F","Op","background","disabled","collapsed","children","otherwise"},StringComparer.Ordinal);

 public static OllamaGeneration Parse(string json,Project context=null) {
  if(string.IsNullOrWhiteSpace(json))throw new Exception("AI zwróciło pusty projekt. Spróbuj ponownie lub wybierz inny model.");
  if(json.Length>MaxJsonLength)throw new Exception("Projekt AI jest za duży (limit 1 MB tekstu).");
  XmlElement root=OllamaJson.Read(json);
  Dictionary<string,XmlElement> map=OllamaJson.Object(root,"projekt");CheckKeys(map,RootKeys,"projekcie");
  string name=StringRequired(map,"name",120),summary=StringRequired(map,"summary",12000);
  if(string.IsNullOrWhiteSpace(name))throw new Exception("AI nie podało nazwy projektu.");
  Project project=new Project{Name=ProjectIO.Safe(name)};
  if(context!=null){
   project.SourcePath=context.SourcePath;
   project.StepDelaySeconds=context.StepDelaySeconds;
   project.Assets=context.Assets==null?new Dictionary<string,string>():new Dictionary<string,string>(context.Assets);
   if(context.Features!=null)project.Features=new Dictionary<string,bool>(context.Features);
  }
  XmlElement step;
  if(map.TryGetValue("stepDelaySeconds",out step)){
   if(OllamaJson.Type(step)!="number"||!double.TryParse(step.InnerText,NumberStyles.Float,CultureInfo.InvariantCulture,out project.StepDelaySeconds)||double.IsNaN(project.StepDelaySeconds)||double.IsInfinity(project.StepDelaySeconds)||project.StepDelaySeconds<0||project.StepDelaySeconds>3600)
    throw new Exception("AI podało nieprawidłową przerwę między bloczkami (0–3600 s).");
  }
  XmlElement blocks;if(!map.TryGetValue("blocks",out blocks))throw new Exception("AI nie zwróciło listy bloczków.");
  int count=0;project.Blocks=ParseBlocks(blocks,0,ref count,project,true);
  if(count==0)throw new Exception("AI zwróciło projekt bez bloczków. Doprecyzuj, co ma robić program.");
  ProjectIO.Normalize(project);
  return new OllamaGeneration{Project=project,Summary=summary};
 }

 static List<Block> ParseBlocks(XmlElement node,int depth,ref int count,Project project,bool active) {
  if(depth>MaxDepth)throw new Exception("Projekt AI przekracza limit 12 poziomów zagnieżdżenia.");
  if(OllamaJson.Type(node)!="array")throw new Exception("Lista bloczków AI musi być tablicą JSON.");
  List<Block> blocks=new List<Block>();
  foreach(XmlNode child in node.ChildNodes){
   XmlElement item=child as XmlElement;if(item==null)continue;
   if(++count>MaxBlocks)throw new Exception("Projekt AI przekracza limit 500 bloczków. Poproś o prostszy program.");
   Dictionary<string,XmlElement> map=OllamaJson.Object(item,"bloczek");CheckKeys(map,BlockKeys,"bloczku");
   string kind=StringRequired(map,"kind",100);Spec spec;
   if(!Catalog.All.TryGetValue(kind,out spec))throw new Exception("AI użyło nieznanego bloczka: "+kind+". Spróbuj wygenerować projekt ponownie.");
   if(spec.Event&&depth!=0)throw new Exception("Zdarzenia AI muszą znajdować się na najwyższym poziomie projektu.");
   Block block=Block.Make(kind);
   foreach(string key in new[]{"A","B","C","D","E","F","Op"}){
    XmlElement value;if(!map.TryGetValue(key,out value))continue;
    string text=OllamaJson.String(value,key,MaxFieldLength);
    // Empty unused fields often appear in otherwise valid schema responses.
    if(!spec.Fields.Any(f=>f.Key==key)&&text.Length!=0)throw new Exception("Bloczek "+spec.Name+" nie obsługuje pola "+key+".");
    if(spec.Fields.Any(f=>f.Key==key))block.Set(key,text);
   }
   block.Disabled=BoolOptional(map,"disabled");block.Collapsed=BoolOptional(map,"collapsed");
   block.RunInBackground=BoolOptional(map,"background");
   if(block.RunInBackground&&kind!="cmd"&&kind!="powershell")throw new Exception("Tryb W tle dotyczy tylko bloczków CMD i PowerShell.");
   if(spec.Fields.Any(f=>f.Key=="Op")&&!spec.Fields.First(f=>f.Key=="Op").Options.Contains(block.Op))throw new Exception("AI podało nieprawidłowy znak porównania: "+block.Op+".");
   XmlElement children;if(map.TryGetValue("children",out children))block.Children=ParseBlocks(children,depth+1,ref count,project,active&&!block.Disabled);
   XmlElement otherwise;if(map.TryGetValue("otherwise",out otherwise))block.Else=ParseBlocks(otherwise,depth+1,ref count,project,active&&!block.Disabled);
   if(block.Children.Count>0&&!spec.Container)throw new Exception("Bloczek "+spec.Name+" nie może zawierać innych bloczków.");
   if(block.Else.Count>0&&!spec.HasElse)throw new Exception("Bloczek "+spec.Name+" nie ma gałęzi w przeciwnym razie.");
   if(active&&!block.Disabled)project.Features[spec.Group]=true;
   blocks.Add(block);
  }
  return blocks;
 }

 static string StringRequired(Dictionary<string,XmlElement> map,string key,int maximum){XmlElement value;if(!map.TryGetValue(key,out value))throw new Exception("Brak pola "+key+" w odpowiedzi AI.");return OllamaJson.String(value,key,maximum);}
 static bool BoolOptional(Dictionary<string,XmlElement> map,string key){XmlElement value;if(!map.TryGetValue(key,out value))return false;if(OllamaJson.Type(value)!="boolean")throw new Exception("Pole "+key+" w projekcie AI musi być true lub false.");return value.InnerText=="true";}
 static void CheckKeys(Dictionary<string,XmlElement> map,HashSet<string> allowed,string where){foreach(string key in map.Keys)if(!allowed.Contains(key))throw new Exception("Nieobsługiwane pole "+key+" w "+where+" AI.");}

 public static string CreateSchema(){
  string text="{\"type\":\"string\",\"maxLength\":"+MaxFieldLength+"}";
  string boolean="{\"type\":\"boolean\"}";
  StringBuilder schema=new StringBuilder("{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"name\",\"summary\",\"blocks\"],\"properties\":{\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":70},\"summary\":{\"type\":\"string\",\"maxLength\":12000},\"stepDelaySeconds\":{\"type\":\"number\",\"minimum\":0,\"maximum\":3600},\"blocks\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":500,\"items\":{\"$ref\":\"#/$defs/block\"}}},\"$defs\":{\"block\":{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"kind\"],\"properties\":{\"kind\":{\"type\":\"string\",\"enum\":[");
  schema.Append(string.Join(",",Catalog.All.Keys.Select(OllamaJson.Quote)));schema.Append("]}");
  foreach(string key in new[]{"A","B","C","D","E","F","Op"})schema.Append(","+OllamaJson.Quote(key)+":"+text);
  foreach(string key in new[]{"background","disabled","collapsed"})schema.Append(","+OllamaJson.Quote(key)+":"+boolean);
  foreach(string key in new[]{"children","otherwise"})schema.Append(","+OllamaJson.Quote(key)+":{\"type\":\"array\",\"maxItems\":500,\"items\":{\"$ref\":\"#/$defs/block\"}}");
  return schema.Append("}}}}").ToString();
 }

 public static string CreateSystemPrompt(){
  StringBuilder prompt=new StringBuilder();
  prompt.AppendLine("Jesteś programistą EasyJava, wizualnego edytora programów Windows. Użytkownik opisuje program po polsku; zwracasz kompletny działający projekt jako obiekt JSON zgodny ze schematem. Nie zwracaj JavaScriptu, Markdown ani tekstu poza JSON. summary ma krótko po polsku wyjaśnić działanie oraz ewentualne ograniczenia.");
  prompt.AppendLine("Projekt: {name,summary,stepDelaySeconds:0,blocks:[...]}. Każdy bloczek: {kind,A,B,C,D,E,F,Op,background,disabled,collapsed,children,otherwise}. Podawaj tylko potrzebne pola. A–F oraz Op są zawsze TEKSTAMI, także liczby. children zawiera treść pętli lub zdarzenia. otherwise tylko dla if_else. background=true tylko CMD/PowerShell. Nie wymyślaj typów bloczków ani pól. Puste brakujące pola otrzymują domyślne wartości z katalogu. Maksymalnie 500 bloczków i 12 poziomów.");
  prompt.AppendLine("Top-level bloczki niebędące zdarzeniami wykonują się po uruchomieniu projektu. Zdarzenia event_* rejestrują dzieci, ale nie wykonują ich od razu (wyjątek event_start). Zdarzenia umieszczaj WYŁĄCZNIE na najwyższym poziomie, bez zagnieżdżania. GUI utwórz przed obsługującymi je zdarzeniami. Każdy element GUI ma unikalne ID w polu A. event_button.A to ID przycisku. ui_get.A to ID elementu, ui_get.B to nazwa zmiennej docelowej. ui_text.A to ID, B to nowy tekst.");
  prompt.AppendLine("Wartości zmiennych w tekście: {punkty}. Wyrażenie matematyczne zaczyna się od =, np. =a + b; w wyrażeniu nazwy zmiennych BEZ klamer. Obsługiwane + - * / % ^, porównania, && || !, funkcje sqrt abs round floor ceil sin cos tan log min max. Najpierw zainicjuj zmienne bloczkiem set. ui_get pobiera tekst pola; działania liczbowe konwertują tekst. Indeksy list od 1. Współrzędne GUI od lewego górnego rogu, duszki od środka sceny z dodatnim Y do góry. Wielkość okna window.B i C musi pomieścić GUI.");
  prompt.AppendLine("Pliki: {pulpit}\\notatka.txt, {dokumenty}, {folder_projektu}; sama nazwa zapisuje obok projektu, nie w Program Files. Nie nadpisuj ani nie usuwaj plików bez wyraźnej potrzeby użytkownika. CMD/PowerShell to dosłowna komenda, klamry nie interpolują się. Dla ciągłego strumienia jak curl parrot.live ustaw background=true, by następne bloczki działały. GUI, matematyka i animacje powinny używać natywnych bloczków zamiast komend, chyba że użytkownik prosi o komendę.");
  prompt.AppendLine("Gdy podany jest aktualny projekt, zwróć CAŁY poprawiony projekt, zachowując potrzebne wcześniejsze funkcje i identyfikatory. Obrazy już istniejące zostają zachowane; ich identyfikatory są dostępne w kontekście. Nie generuj danych obrazów ani kodu wykonywalnego.");
  prompt.AppendLine("Przykład licznika z przyciskiem: {\"name\":\"licznik\",\"summary\":\"Przycisk dodaje punkt.\",\"blocks\":[{\"kind\":\"window\",\"A\":\"Licznik\",\"B\":\"600\",\"C\":\"360\",\"D\":\"#eef2ff\"},{\"kind\":\"set\",\"A\":\"punkty\",\"B\":\"0\"},{\"kind\":\"ui_label\",\"A\":\"wynik\",\"B\":\"Wynik: 0\",\"C\":\"30\",\"D\":\"30\",\"E\":\"400\",\"F\":\"45\"},{\"kind\":\"ui_button\",\"A\":\"plus\",\"B\":\"Dodaj punkt\",\"C\":\"30\",\"D\":\"100\",\"E\":\"200\",\"F\":\"45\"},{\"kind\":\"event_button\",\"A\":\"plus\",\"children\":[{\"kind\":\"change\",\"A\":\"punkty\",\"B\":\"1\"},{\"kind\":\"ui_text\",\"A\":\"wynik\",\"B\":\"Wynik: {punkty}\"}]}]}");
  prompt.AppendLine("KATALOG WSZYSTKICH OBSŁUGIWANYCH BLOCZKÓW (kind | nazwa | pola klucz: opis [domyślna wartość; opcje] | uwagi):");
  foreach(Spec spec in Catalog.All.Values){
   prompt.Append(spec.Kind+" | "+spec.Name+" | ");
   foreach(Field field in spec.Fields)prompt.Append(field.Key+": "+field.Label+" ["+field.Default+(field.Options==null?"":"; "+string.Join("/",field.Options))+"]; ");
   prompt.Append(spec.Container?"children dozwolone; ":"");prompt.Append(spec.HasElse?"otherwise dozwolone; ":"");prompt.AppendLine(spec.Hint);
  }
  prompt.AppendLine("SCHEMAT JSON odpowiedzi: "+CreateSchema());
  return prompt.ToString();
 }

 public static string ContextJson(Project project){
  if(project==null)return "";
  StringBuilder json=new StringBuilder("{\"name\":"+OllamaJson.Quote(project.Name)+",\"stepDelaySeconds\":"+project.StepDelaySeconds.ToString(CultureInfo.InvariantCulture)+",\"blocks\":");
  WriteBlocks(json,project.Blocks);
  json.Append(",\"existingAssetIds\":["+string.Join(",",(project.Assets??new Dictionary<string,string>()).Keys.Select(OllamaJson.Quote))+"]}");
  if(json.Length>32000)throw new Exception("Aktualny projekt jest za duży dla AI (limit kontekstu 32 tys. znaków). Wygeneruj nowy projekt bez dołączania aktualnego.");
  return json.ToString();
 }
 static void WriteBlocks(StringBuilder json,List<Block> blocks){
  json.Append('[');bool first=true;
  foreach(Block block in blocks){if(!first)json.Append(',');first=false;json.Append("{\"kind\":"+OllamaJson.Quote(block.Kind));
   Spec spec=Catalog.All[block.Kind];foreach(Field field in spec.Fields)if(field.Key!="G")json.Append(","+OllamaJson.Quote(field.Key)+":"+OllamaJson.Quote(block.Get(field.Key)));
   if(block.Disabled)json.Append(",\"disabled\":true");if(block.Collapsed)json.Append(",\"collapsed\":true");if(block.RunInBackground)json.Append(",\"background\":true");
   if(block.Children.Count>0){json.Append(",\"children\":");WriteBlocks(json,block.Children);}if(block.Else.Count>0){json.Append(",\"otherwise\":");WriteBlocks(json,block.Else);}json.Append('}');
  }json.Append(']');
 }
}

// JSON reader is part of .NET Framework; using its XML mapping lets us reject
// duplicate/unknown properties rather than silently ignoring malformed AI data.
internal static class OllamaJson {
 internal static string Type(XmlElement e){return e.GetAttribute("type");}
 internal static XmlElement Read(string json){
  try{XmlDictionaryReaderQuotas quotas=new XmlDictionaryReaderQuotas{MaxDepth=64,MaxStringContentLength=4194304,MaxArrayLength=4194304,MaxBytesPerRead=4096,MaxNameTableCharCount=65536};
   using(XmlDictionaryReader reader=JsonReaderWriterFactory.CreateJsonReader(Encoding.UTF8.GetBytes(json),quotas)){XmlDocument doc=new XmlDocument{XmlResolver=null};doc.Load(reader);return doc.DocumentElement;}
  }catch(Exception e){throw new Exception("AI zwróciło nieprawidłowy JSON. Spróbuj ponownie lub użyj innego modelu.",e);}
 }
 internal static Dictionary<string,XmlElement> Object(XmlElement node,string what){
  if(node==null||Type(node)!="object")throw new Exception("Nieprawidłowy "+what+" w odpowiedzi AI: wymagany obiekt JSON.");
  Dictionary<string,XmlElement> map=new Dictionary<string,XmlElement>(StringComparer.Ordinal);
  foreach(XmlNode child in node.ChildNodes){XmlElement e=child as XmlElement;if(e==null)continue;string key=e.LocalName=="item"&&e.HasAttribute("item")?e.GetAttribute("item"):e.LocalName;if(map.ContainsKey(key))throw new Exception("Powtórzone pole "+key+" w odpowiedzi AI.");map.Add(key,e);}return map;
 }
 internal static string String(XmlElement node,string key,int maximum){if(Type(node)!="string")throw new Exception("Pole "+key+" w odpowiedzi AI musi być tekstem.");string value=node.InnerText;if(value.Length>maximum)throw new Exception("Pole "+key+" w odpowiedzi AI jest za długie (limit "+maximum+" znaków).");return value;}
 internal static string Quote(string value){using(MemoryStream stream=new MemoryStream()){new DataContractJsonSerializer(typeof(string)).WriteObject(stream,value??"");return Encoding.UTF8.GetString(stream.ToArray());}}
}
}
