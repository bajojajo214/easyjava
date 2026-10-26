using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;
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
  NormalizeNumericReferences(project.Blocks);
  ProjectIO.Normalize(project);
  return new OllamaGeneration{Project=project,Summary=summary};
 }

 // Models sometimes use a bare variable name in an operand even though the
 // runtime requires {name}. Only numeric operands and exact, declared variable
 // names are canonicalized here; text, commands and paths remain literal.
 static void NormalizeNumericReferences(List<Block> blocks){
  List<Block> all=Flatten(blocks).ToList();HashSet<string> known=new HashSet<string>(StringComparer.Ordinal);
  foreach(Block block in all){
   string[] names;
   switch(block.Kind){
    case "set":case "change":case "random":case "math":case "function":case "join":case "text_replace":case "timer":case "touch_edge":case "for":case "file_open":case "file_save":names=new[]{block.A};break;
    case "ui_get":case "ask":case "ask_text":case "text_length":case "list_length":case "key_pressed":case "file_read":case "file_exists":names=new[]{block.B};break;
    case "list_get":case "list_contains":case "distance":names=new[]{block.C};break;
    case "mouse":case "sprite_position":names=new[]{block.A,block.B};break;
    case "cmd":case "powershell":names=new[]{block.B,block.C,block.D};break;
    default:names=new string[0];break;
   }
   foreach(string name in names)if(Regex.IsMatch(name??"",@"^[\p{L}_][\p{L}\p{N}_]{0,79}$"))known.Add(name);
  }
  foreach(Block block in all){if(block.Kind=="math"){block.B=NumericReference(block.B,known);block.D=NumericReference(block.D,known);if(block.C=="\\/")block.C="/";}else if(block.Kind=="change")block.B=NumericReference(block.B,known);}
 }
 static string NumericReference(string value,HashSet<string> known){return known.Contains(value??"")?"{"+value+"}":value;}
 static IEnumerable<Block> Flatten(List<Block> blocks){foreach(Block block in blocks){yield return block;foreach(Block child in Flatten(block.Children))yield return child;foreach(Block child in Flatten(block.Else))yield return child;}}

 // Structural JSON is not enough: reject references to missing controls and
 // obvious numeric/layout errors before offering a generated project to use.
 // This never executes the project and never changes a user's opened project.
 public static void ValidateGenerated(Project project){
  ProjectIO.Normalize(project);List<Block> active=ActiveBlocks(project.Blocks,project).ToList();List<string> errors=new List<string>();
  HashSet<string> controls=new HashSet<string>(active.Where(b=>IsUiDefinition(b)&&LiteralId(b.A)).Select(b=>b.A),StringComparer.Ordinal);
  HashSet<string> buttons=new HashSet<string>(active.Where(b=>b.Kind=="event_button"&&LiteralId(b.A)&&b.Children.Count>0).Select(b=>b.A),StringComparer.Ordinal);
  foreach(Block button in active.Where(b=>b.Kind=="ui_button"&&LiteralId(b.A)))if(!buttons.Contains(button.A))errors.Add("Przycisk GUI „"+button.A+"” nie ma działającej obsługi kliknięcia. Dodaj event_button z tym ID i potrzebnymi bloczkami children albo usuń zbędny przycisk.");
  foreach(Block button in active.Where(b=>b.Kind=="ui_button"&&LiteralId(b.A))){
   string symbol=(button.B??"").Trim().Replace('−','-').Replace('×','*').Replace('÷','/');if(!new[]{"+","-","*","/"}.Contains(symbol))continue;
   List<Block> arithmetic=active.Where(b=>b.Kind=="event_button"&&b.A==button.A).SelectMany(b=>ActiveBlocks(b.Children,project)).Where(b=>b.Kind=="math").ToList();
   if(arithmetic.Count==1&&arithmetic[0].C!=symbol)errors.Add("Przycisk „"+button.B+"” ma działanie „"+arithmetic[0].C+"”, zamiast „"+symbol+"”. Popraw math.C w event_button „"+button.A+"” zgodnie ze znakiem na przycisku.");
  }
  string[] references={"ui_get","ui_text","ui_set","ui_move","ui_size","ui_focus","ui_item","ui_clear","ui_remove","event_button","event_change"};
  foreach(Block block in active){
   if(references.Contains(block.Kind)&&LiteralId(block.A)&&!controls.Contains(block.A))errors.Add("Bloczek „"+Catalog.All[block.Kind].Name+"” odwołuje się do nieistniejącego elementu GUI „"+block.A+"”. Dodaj odpowiedni element z tym identyfikatorem albo popraw identyfikator w zdarzeniu/bloczku.");
   try{if(block.Kind=="math"){ValidateNumeric(block.B,block.Kind,"B");ValidateNumeric(block.D,block.Kind,"D");if(!new[]{"+","-","*","/","%","^"}.Contains(block.C))throw new Exception("Bloczek math ma nieprawidłowy znak działania „"+block.C+"”. Użyj +, -, *, /, % albo ^.");}else if(block.Kind=="change")ValidateNumeric(block.B,block.Kind,"B");}catch(Exception error){errors.Add(error.Message);}
   try{if(block.Kind=="window"){ValidateNumeric(block.B,block.Kind,"B");ValidateNumeric(block.C,block.Kind,"C");}else if(IsUiDefinition(block)){ValidateNumeric(block.C,block.Kind,"C");ValidateNumeric(block.D,block.Kind,"D");ValidateNumeric(block.E,block.Kind,"E");ValidateNumeric(block.F,block.Kind,"F");}}catch(Exception error){errors.Add(error.Message);}
  }
  ValidateCoverage(project.Blocks,project,errors);
  if(errors.Count>0)throw new Exception("Wygenerowany projekt ma błędy:\n"+string.Join("\n",errors.Distinct().Take(12)));
 }
 static bool IsUiDefinition(Block block){return block.Kind.StartsWith("ui_",StringComparison.Ordinal)&&Catalog.All[block.Kind].Fields.Length==6;}
 static bool LiteralId(string value){return !string.IsNullOrWhiteSpace(value)&&value.IndexOf('{')<0&&!value.TrimStart().StartsWith("=",StringComparison.Ordinal);}
 static IEnumerable<Block> ActiveBlocks(List<Block> blocks,Project project){foreach(Block block in blocks){if(block.Disabled||!project.Allows(Catalog.All[block.Kind].Group))continue;yield return block;foreach(Block child in ActiveBlocks(block.Children,project))yield return child;foreach(Block child in ActiveBlocks(block.Else,project))yield return child;}}
 static void ValidateNumeric(string value,string kind,string field){
  string text=(value??"").Trim();double number;
  if(double.TryParse(text.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out number)&&!double.IsInfinity(number)&&!double.IsNaN(number))return;
  if(text.StartsWith("=",StringComparison.Ordinal)&&text.Length>1||Regex.IsMatch(text,@"^\{[^{}]+\}$"))return;
  Field definition=Catalog.All[kind].Fields.FirstOrDefault(f=>f.Key==field);string label=definition==null?field:definition.Label;
  throw new Exception("Bloczek „"+Catalog.All[kind].Name+"”, pole „"+label+"” oczekuje liczby, {zmiennej} lub wyrażenia z =. Wartość „"+text+"” nie jest liczbą. Obliczenia wpisz np. =liczba1 + liczba2 albo użyj bloczka math z osobnymi operandami.");
 }
 static bool TryBounds(Block block,out double x,out double y,out double width,out double height){
  return double.TryParse(block.C.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out x)&double.TryParse(block.D.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out y)&double.TryParse(block.E.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out width)&double.TryParse(block.F.Replace(',','.'),NumberStyles.Float,CultureInfo.InvariantCulture,out height)&&width>0&&height>0&&!new[]{x,y,width,height}.Any(v=>double.IsNaN(v)||double.IsInfinity(v));
 }
 static void ValidateCoverage(List<Block> blocks,Project project,List<string> errors){
  List<Block> earlierControls=new List<Block>();
  foreach(Block block in blocks){
   if(block.Disabled||!project.Allows(Catalog.All[block.Kind].Group))continue;
   if(block.Kind=="ui_input"||block.Kind=="ui_textarea"||block.Kind=="ui_button"){
    double x,y,w,h;if(TryBounds(block,out x,out y,out w,out h))foreach(Block cover in earlierControls){double lx,ly,lw,lh;if(TryBounds(cover,out lx,out ly,out lw,out lh)&&lx<=x&&ly<=y&&lx+lw>=x+w&&ly+lh>=y+h)errors.Add("Element GUI „"+block.A+"” jest całkowicie zasłonięty wcześniejszym elementem „"+cover.A+"”. Rozmieść pola, przyciski i napisy oddzielnie, bez nakładania ich pełnych obszarów.");}
   }
   if(block.Kind=="ui_label"||block.Kind=="ui_input"||block.Kind=="ui_textarea"||block.Kind=="ui_button"||block.Kind=="ui_listbox"||block.Kind=="ui_combo")earlierControls.Add(block);
   // Keep mutually exclusive child branches separate: their controls need not
   // coexist. The conservative coverage check operates within each list.
   ValidateCoverage(block.Children,project,errors);ValidateCoverage(block.Else,project,errors);
  }
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

 public static string CreateSchema(bool preserveState=true){
  string text="{\"type\":\"string\",\"maxLength\":"+MaxFieldLength+"}";
  string boolean="{\"type\":\"boolean\"}";
  string state=preserveState?boolean:"{\"type\":\"boolean\",\"const\":false}";
  string nested="{\"type\":\"array\",\"maxItems\":500,\"items\":{\"$ref\":\"#/$defs/block\"}}";
  StringBuilder schema=new StringBuilder("{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"name\",\"summary\",\"blocks\"],\"properties\":{\"name\":{\"type\":\"string\",\"minLength\":1,\"maxLength\":70},\"summary\":{\"type\":\"string\",\"maxLength\":600},\"stepDelaySeconds\":{\"type\":\"number\",\"minimum\":0,\"maximum\":3600},\"blocks\":{\"type\":\"array\",\"minItems\":1,\"maxItems\":500,\"items\":{\"$ref\":\"#/$defs/block\"}}},\"$defs\":{\"block\":{\"oneOf\":[");
  bool first=true;
  foreach(Spec spec in Catalog.All.Values){
   if(!first)schema.Append(',');first=false;
   // A separate alternative for every kind prevents the model from emitting
   // fields copied from another block (for example file_open.B or change.C).
   schema.Append("{\"type\":\"object\",\"additionalProperties\":false,\"required\":[\"kind\"],\"properties\":{\"kind\":{\"type\":\"string\",\"const\":"+OllamaJson.Quote(spec.Kind)+"}");
   foreach(Field field in spec.Fields)if(field.Key!="G")schema.Append(","+OllamaJson.Quote(field.Key)+":"+(field.Options!=null&&(field.Key=="Op"||spec.Kind=="math"&&field.Key=="C")?"{\"type\":\"string\",\"enum\":["+string.Join(",",field.Options.Select(OllamaJson.Quote))+"]}":text));
   foreach(string key in new[]{"disabled","collapsed"})schema.Append(","+OllamaJson.Quote(key)+":"+state);
   if(spec.Kind=="cmd"||spec.Kind=="powershell")schema.Append(",\"background\":"+boolean);
   if(spec.Container)schema.Append(",\"children\":"+nested);
   if(spec.HasElse)schema.Append(",\"otherwise\":"+nested);
   schema.Append("}}");
  }
  return schema.Append("]}}}").ToString();
 }

 public static string CreateSystemPrompt(string request=null){
  StringBuilder prompt=new StringBuilder();
  prompt.AppendLine("Jesteś programistą EasyJava, wizualnego edytora programów Windows. Użytkownik opisuje program po polsku; zwracasz kompletny działający projekt jako obiekt JSON zgodny ze schematem. Nie zwracaj JavaScriptu, Markdown ani tekstu poza JSON. summary ma krótko po polsku wyjaśnić działanie oraz ewentualne ograniczenia.");
  prompt.AppendLine("Odpowiadaj zwięźle: summary najwyżej 2 zdania / 600 znaków. Podawaj tylko potrzebne pola, pomijaj disabled/collapsed=false, puste children i otherwise. Cały projekt musi się zmieścić w odpowiedzi. Nie twórz dekoracyjnych ani pustych przycisków. KAŻDY przycisk musi mieć event_button z rzeczywistymi instrukcjami children. Pola, napisy i przyciski muszą mieć oddzielne prostokąty, bez nakładania.");
  prompt.AppendLine("KRYTYCZNE DLA DZIAŁANIA GUI: pole tekstowe w oknie NIE JEST zmienną programu. Sam set nie odczytuje tekstu użytkownika. W obsłudze przycisku najpierw użyj ui_get dla KAŻDEGO potrzebnego pola, a dopiero potem obliczaj lub zapisuj. Kalkulator: ui_get pierwszego pola, ui_get drugiego pola, math, ui_text. Notatnik Zapisz: ui_get dużego pola tekstowego PRZED file_write. Bez tych odczytów program nie działa, nawet jeśli wygląda poprawnie.");
  prompt.AppendLine("Projekt: {name,summary,stepDelaySeconds:0,blocks:[...]}. Każdy bloczek: {kind,A,B,C,D,E,F,Op,background,disabled,collapsed,children,otherwise}. Podawaj tylko potrzebne pola. A–F oraz Op są zawsze TEKSTAMI, także liczby. children zawiera treść pętli lub zdarzenia. otherwise tylko dla if_else. background=true tylko CMD/PowerShell. Nie wymyślaj typów bloczków ani pól. Puste brakujące pola otrzymują domyślne wartości z katalogu. Maksymalnie 500 bloczków i 12 poziomów.");
  prompt.AppendLine("Top-level bloczki niebędące zdarzeniami wykonują się po uruchomieniu projektu. Zdarzenia event_* rejestrują dzieci, ale nie wykonują ich od razu (wyjątek event_start). Zdarzenia umieszczaj WYŁĄCZNIE na najwyższym poziomie, bez zagnieżdżania. GUI utwórz przed obsługującymi je zdarzeniami. Każdy element GUI ma unikalne ID w polu A. event_button.A to ID przycisku. ui_get.A to ID elementu, ui_get.B to nazwa zmiennej docelowej. ui_text.A to ID, B to nowy tekst.");
  prompt.AppendLine("Wartości zmiennych w tekście: {punkty}. Wyrażenie matematyczne zaczyna się od =, np. =a + b; w wyrażeniu nazwy zmiennych BEZ klamer. Obsługiwane + - * / % ^, porównania, && || !, funkcje sqrt abs round floor ceil sin cos tan log min max. Najpierw zainicjuj zmienne bloczkiem set. ui_get pobiera tekst pola; działania liczbowe konwertują tekst. Indeksy list od 1. Współrzędne GUI od lewego górnego rogu, duszki od środka sceny z dodatnim Y do góry. Wielkość okna window.B i C musi pomieścić GUI.");
  prompt.AppendLine("WAŻNE: bloczek change ma TYLKO A=nazwa zmiennej i B=o ile dodać. NIGDY nie dodawaj mu C ani D. Do kalkulatora użyj math: A=wynik, B={liczba1}, C=+, D={liczba2}; lub set: A=wynik, B==liczba1 + liczba2. Przykład po odczycie pól: [{\"kind\":\"ui_get\",\"A\":\"input1\",\"B\":\"liczba1\"},{\"kind\":\"ui_get\",\"A\":\"input2\",\"B\":\"liczba2\"},{\"kind\":\"math\",\"A\":\"wynik\",\"B\":\"{liczba1}\",\"C\":\"+\",\"D\":\"{liczba2}\"},{\"kind\":\"ui_text\",\"A\":\"label1\",\"B\":\"Wynik: {wynik}\"}].");
  prompt.AppendLine("WAŻNE DLA PLIKÓW: file_open ma TYLKO A=nazwa zmiennej ze ścieżką, żadnego B ani C. file_save ma A=nazwa zmiennej, B=proponowana nazwa pliku. file_read ma A=ścieżka, B=zmienna tekstu. file_write ma A=ścieżka, B=tekst. Po file_open/file_save sprawdź if: A={plik}, B=pusty tekst, Op=!=. Anulowanie okna zostawia pustą ścieżkę; wtedy pomiń odczyt/zapis. Nie wymyślaj tytułu lub filtra pliku w dodatkowych polach.");
  prompt.AppendLine("Pliki: {pulpit}\\notatka.txt, {dokumenty}, {folder_projektu}; sama nazwa zapisuje obok projektu, nie w Program Files. Nie nadpisuj ani nie usuwaj plików bez wyraźnej potrzeby użytkownika. CMD/PowerShell to dosłowna komenda, klamry nie interpolują się. Dla ciągłego strumienia jak curl parrot.live ustaw background=true, by następne bloczki działały. GUI, matematyka i animacje powinny używać natywnych bloczków zamiast komend, chyba że użytkownik prosi o komendę.");
  prompt.AppendLine("Gdy podany jest aktualny projekt, zwróć CAŁY poprawiony projekt, zachowując potrzebne wcześniejsze funkcje i identyfikatory. Obrazy już istniejące zostają zachowane; ich identyfikatory są dostępne w kontekście. Nie generuj danych obrazów ani kodu wykonywalnego.");
  prompt.AppendLine("Przykład licznika z przyciskiem: {\"name\":\"licznik\",\"summary\":\"Przycisk dodaje punkt.\",\"blocks\":[{\"kind\":\"window\",\"A\":\"Licznik\",\"B\":\"600\",\"C\":\"360\",\"D\":\"#eef2ff\"},{\"kind\":\"set\",\"A\":\"punkty\",\"B\":\"0\"},{\"kind\":\"ui_label\",\"A\":\"wynik\",\"B\":\"Wynik: 0\",\"C\":\"30\",\"D\":\"30\",\"E\":\"400\",\"F\":\"45\"},{\"kind\":\"ui_button\",\"A\":\"plus\",\"B\":\"Dodaj punkt\",\"C\":\"30\",\"D\":\"100\",\"E\":\"200\",\"F\":\"45\"},{\"kind\":\"event_button\",\"A\":\"plus\",\"children\":[{\"kind\":\"change\",\"A\":\"punkty\",\"B\":\"1\"},{\"kind\":\"ui_text\",\"A\":\"wynik\",\"B\":\"Wynik: {punkty}\"}]}]}");
  prompt.AppendLine("KATALOG WSZYSTKICH OBSŁUGIWANYCH BLOCZKÓW (kind | nazwa | pola klucz: opis [domyślna wartość; opcje] | uwagi):");
  foreach(Spec spec in Catalog.All.Values){
   prompt.Append(spec.Kind+" | "+spec.Name+" | ");
   foreach(Field field in spec.Fields)prompt.Append(field.Key+": "+field.Label+" ["+field.Default+(field.Options==null?"":"; "+string.Join("/",field.Options))+"]; ");
   prompt.Append(spec.Container?"children dozwolone; ":"");prompt.Append(spec.HasElse?"otherwise dozwolone; ":"");prompt.AppendLine(spec.Hint);
  }
  prompt.AppendLine("KONTROLA PRZED ODPOWIEDZIĄ: czy każdy przycisk pobiera aktualną treść pól? Dla kalkulatora umieść wewnątrz event_button: [{\"kind\":\"ui_get\",\"A\":\"input1\",\"B\":\"liczba1\"},{\"kind\":\"ui_get\",\"A\":\"input2\",\"B\":\"liczba2\"},{\"kind\":\"math\",\"A\":\"wynik\",\"B\":\"{liczba1}\",\"C\":\"+\",\"D\":\"{liczba2}\"},{\"kind\":\"ui_text\",\"A\":\"label1\",\"B\":\"Wynik: {wynik}\"}]. input1/input2/label1 muszą być identyfikatorami naprawdę utworzonych elementów GUI. Nie zastępuj tych ui_get początkowym set 0.");
  prompt.AppendLine("Dla notatnika wewnątrz przycisku Zapisz wybierz file_save do zmiennej plik, sprawdź if A={plik}, B=\"\", Op=!=, a wewnątrz if umieść ui_get A=editor B=tekst, następnie file_write A={plik} B={tekst}. editor musi być ID utworzonego dużego pola tekstowego. Przycisk Otwórz: file_open A=plik, if A={plik}, B=\"\", Op=!=, w środku file_read A={plik} B=tekst i ui_text A=editor B={tekst}.");
  prompt.AppendLine("Odpowiedź musi odpowiadać schematowi JSON przekazanemu w parametrze format. Każdy kind ma własny zestaw dozwolonych pól z katalogu powyżej; nie dopisuj pól należących do innego bloczka.");
  if((request??"").IndexOf("kalkulator",StringComparison.OrdinalIgnoreCase)>=0||(request??"").IndexOf("calculator",StringComparison.OrdinalIgnoreCase)>=0)prompt.AppendLine("PEŁNY PRZYKŁAD KALKULATORA (dopasuj go do prośby użytkownika): " + ContextJson(Examples.Calculator()).Replace(",\"existingAssetIds\":[]", "").Replace("\"stepDelaySeconds\":0,", "\"summary\":\"Kalkulator z czterema działaniami i obsługą zera.\","));
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
