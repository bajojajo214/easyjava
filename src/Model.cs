using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Text.RegularExpressions;

namespace EasyJava2 {
[DataContract] public class Block {
 [DataMember] public string Kind="say";
 [DataMember] public string A="",B="",C="",D="",E="",F="",Op="==";
 [DataMember] public bool Disabled,Collapsed;
 [DataMember] public List<Block> Children=new List<Block>();
 [DataMember] public List<Block> Else=new List<Block>();
 public string Get(string key) { switch(key) {case "A":return A;case "B":return B;case "C":return C;case "D":return D;case "E":return E;case "F":return F;default:return Op;} }
 public void Set(string key,string val) {switch(key){case "A":A=val;break;case "B":B=val;break;case "C":C=val;break;case "D":D=val;break;case "E":E=val;break;case "F":F=val;break;default:Op=val;break;} }
 public static Block Make(string kind,params string[] args) {Block b=new Block{Kind=kind};Spec s=Catalog.All[kind];foreach(Field f in s.Fields)b.Set(f.Key,f.Default);for(int i=0;i<args.Length&&i<6;i++)b.Set(((char)('A'+i)).ToString(),args[i]);return b;}
}
[DataContract] public class Project {
 [DataMember] public int Version=2;
 [DataMember] public string Name="mojprojekt";
 [DataMember] public double StepDelaySeconds;
 [DataMember] public List<Block> Blocks=new List<Block>();
 [DataMember] public Dictionary<string,bool> Features=Catalog.Groups.ToDictionary(x=>x,x=>x!="Pliki");
 [DataMember] public Dictionary<string,string> Assets=new Dictionary<string,string>();
 public bool Allows(string group){bool on;return Features.TryGetValue(group,out on)?on:group!="Pliki";}
}
public class Field {
 public string Key,Label,Default;public string[] Options;
 public Field(string key,string label,string def,string options=null){Key=key;Label=label;Default=def;Options=options==null?null:options.Split('|');}
}
public class Spec {
 public string Kind,Name,Group,Hint;public bool Container,Event,HasElse;public Field[] Fields;
 public Spec(string kind,string name,string group,string hint,bool container,bool evt,bool els,params Field[] fields){Kind=kind;Name=name;Group=group;Hint=hint;Container=container;Event=evt;HasElse=els;Fields=fields;}
}
public static class Catalog {
 public static readonly string[] Groups={"Zdarzenia","Okna i GUI","Ruch","Wygląd","Sterowanie","Zmienne","Listy","Czujniki","Pisak","Dźwięk","Pliki"};
 public static Dictionary<string,Spec> All=new Dictionary<string,Spec>();
 static Field F(string k,string l,string d,string o=null){return new Field(k,l,d,o);}
 static void Add(string k,string n,string g,string h,params Field[] f){All[k]=new Spec(k,n,g,h,false,false,false,f);}
 static void Nest(string k,string n,string g,string h,bool evt,bool els,params Field[] f){All[k]=new Spec(k,n,g,h,true,evt,els,f);}
 static void Ui(string k,string n,string text,string width,string height){Add(k,n,"Okna i GUI","Identyfikator musi być unikalny. Pozycja jest liczona od lewego górnego rogu okna.",F("A","Identyfikator",k.Replace("ui_","")+"1"),F("B","Tekst / wartość",text),F("C","X","30"),F("D","Y","40"),F("E","Szerokość",width),F("F","Wysokość",height));}
 static Catalog(){
  Nest("event_start","Kiedy kliknięto flagę","Zdarzenia","Wykonaj po uruchomieniu projektu.",true,false);
  Nest("event_button","Kiedy kliknięto przycisk","Zdarzenia","Podaj identyfikator przycisku utworzonego w GUI.",true,false,F("A","Identyfikator","button1"));
  Nest("event_change","Kiedy zmieni się pole","Zdarzenia","Działa dla pól tekstowych, suwaków, list i pól wyboru.",true,false,F("A","Identyfikator","input1"));
  Nest("event_key","Kiedy naciśnięto klawisz","Zdarzenia","Kliknij scenę, aby kierować do niej klawiaturę. Np. Left, Right, Space, A.",true,false,F("A","Klawisz","Right","Right|Left|Up|Down|Space|Enter|A|D|W|S|Escape"));
  Nest("event_sprite","Kiedy kliknięto duszka","Zdarzenia","Kliknięcie rysunku duszka uruchamia ten skrypt.",true,false,F("A","Duszek","robot"));
  Nest("event_message","Kiedy otrzymam wiadomość","Zdarzenia","Uruchamiane przez bloczek Nadaj wiadomość.",true,false,F("A","Wiadomość","start"));
  Nest("event_tick","Co określony czas","Zdarzenia","Wywołuje skrypt cyklicznie; minimalny odstęp to 0,02 s.",true,false,F("A","Sekundy","1"));
  Nest("event_click","Kiedy kliknięto tło sceny","Zdarzenia","Pozycję kliknięcia odczytasz bloczkiem Pozycja myszy.",true,false);
  Add("broadcast","Nadaj wiadomość","Zdarzenia","Uruchamia odbiorców wiadomości.",F("A","Nazwa","start"));
  Add("window","Ustaw okno i scenę","Okna i GUI","Rozmiar sceny jest taki sam w podglądzie i gotowym programie.",F("A","Tytuł","Mój program"),F("B","Szerokość","640"),F("C","Wysokość","420"),F("D","Kolor tła","#f3f5fb"));
  Ui("ui_label","Dodaj napis","Witaj w mojej aplikacji","300","35");
  Ui("ui_button","Dodaj przycisk","Kliknij mnie","150","42");
  Ui("ui_input","Dodaj pole tekstowe","","220","32");
  Ui("ui_textarea","Dodaj duże pole tekstowe","Wpisz coś...","300","120");
  Ui("ui_checkbox","Dodaj pole wyboru","Włącz opcję","190","32");
  Ui("ui_combo","Dodaj listę rozwijaną","Pierwszy|Drugi|Trzeci","220","32");
  Ui("ui_listbox","Dodaj listę elementów","Jabłko|Banan|Gruszka","220","120");
  Ui("ui_slider","Dodaj suwak","50","220","45");
  Ui("ui_progress","Dodaj pasek postępu","50","220","25");
  Ui("ui_image","Dodaj obraz","","160","120");
  Add("ui_text","Zmień tekst elementu","Okna i GUI","Zmienna w tekście: {punkty}.",F("A","Identyfikator","label1"),F("B","Nowy tekst","Wynik: {punkty}"));
  Add("ui_get","Odczytaj wartość elementu","Okna i GUI","Dla pola: tekst; pola wyboru: 1/0; suwaka: liczba; listy: zaznaczony tekst.",F("A","Identyfikator","input1"),F("B","Zapisz w","imie"));
  Add("ui_set","Ustaw właściwość elementu","Okna i GUI","Możesz zmienić widoczność, aktywność, kolor, tekst, wartość i rozmiar czcionki.",F("A","Identyfikator","button1"),F("B","Właściwość","Text","Text|Value|Checked|Enabled|Visible|BackColor|ForeColor|FontSize"),F("C","Wartość","Nowy tekst"));
  Add("ui_move","Przesuń element GUI","Okna i GUI","Ustaw pozycję w pikselach sceny.",F("A","Identyfikator","button1"),F("B","X","30"),F("C","Y","100"));
  Add("ui_size","Zmień rozmiar elementu","Okna i GUI","Szerokość i wysokość w pikselach.",F("A","Identyfikator","button1"),F("B","Szerokość","200"),F("C","Wysokość","45"));
  Add("ui_focus","Ustaw kursor w polu","Okna i GUI","Przenosi aktywne pole do wskazanego elementu.",F("A","Identyfikator","input1"));
  Add("ui_item","Dodaj element do listy GUI","Okna i GUI","Dotyczy listy rozwijanej i listy elementów.",F("A","Identyfikator","listbox1"),F("B","Tekst","Nowy element"));
  Add("ui_clear","Wyczyść element GUI","Okna i GUI","Czyści tekst albo wszystkie pozycje listy.",F("A","Identyfikator","input1"));
  Add("ui_remove","Usuń element GUI","Okna i GUI","Usuwa element z okna.",F("A","Identyfikator","button1"));
  Add("dialog","Pokaż okno dialogowe","Okna i GUI","Wyświetla okno z przyciskiem OK.",F("A","Wiadomość","Gotowe!"),F("B","Tytuł","Informacja"));
  Add("ask_text","Zapytaj o tekst","Okna i GUI","Wyświetla okno z polem do wpisania odpowiedzi.",F("A","Pytanie","Jak masz na imię?"),F("B","Zapisz w","imie"),F("C","Domyślnie",""));
  Add("ask","Zapytaj TAK / NIE","Okna i GUI","Zapisuje 1 dla TAK, 0 dla NIE.",F("A","Pytanie","Czy kontynuować?"),F("B","Zapisz w","odpowiedz"));
  Add("sprite_add","Utwórz duszka","Wygląd","Dodaj duszka i wybierz go jako aktywnego w tym skrypcie.",F("A","Nazwa","robot"),F("B","Kostium","robot","robot|kot|rakieta|koło|kwadrat|gwiazda"),F("C","Kolor","#8b68f6"));
  Add("sprite_select","Wybierz duszka","Wygląd","Dalsze bloczki ruchu i wyglądu dotyczą tego duszka.",F("A","Nazwa","robot"));
  Add("costume","Zmień kostium","Wygląd","Możesz wpisać nazwę wbudowanego kostiumu lub identyfikator zaimportowanego obrazu.",F("A","Kostium","kot"));
  Add("sprite_color","Zmień kolor duszka","Wygląd","Kolor HTML, np. #ff9944.",F("A","Kolor","#ff9944"));
  Add("sprite_size","Ustaw rozmiar duszka","Wygląd","100% to rozmiar podstawowy.",F("A","Procent","100"));
  Add("sprite_show","Pokaż duszka","Wygląd","Pokazuje aktywnego duszka.");
  Add("sprite_hide","Ukryj duszka","Wygląd","Ukrywa aktywnego duszka.");
  Add("say","Powiedz / pokaż tekst","Wygląd","Dymek przy aktywnym duszku i wpis w dzienniku podglądu.",F("A","Tekst","Cześć!"));
  Add("say_for","Powiedz przez chwilę","Wygląd","Pokazuje dymek przez podaną liczbę sekund.",F("A","Tekst","Cześć!"),F("B","Sekundy","2"));
  Add("background","Zmień tło sceny","Wygląd","Kolor tła sceny.",F("A","Kolor","#dfeaff"));
  Add("move","Przesuń o kroki","Ruch","Porusza w aktualnym kierunku.",F("A","Kroki","10"));
  Add("turn","Obróć o stopnie","Ruch","Dodatnie wartości obracają przeciwnie do wskazówek zegara.",F("A","Stopnie","15"));
  Add("direction","Ustaw kierunek","Ruch","0 = prawo, 90 = góra, 180 = lewo, 270 = dół.",F("A","Stopnie","0"));
  Add("goto","Idź do pozycji","Ruch","Środek sceny to x=0, y=0; dodatnie Y kieruje w górę.",F("A","X","0"),F("B","Y","0"));
  Add("change_x","Zmień X o","Ruch","Przesunięcie poziome.",F("A","O ile","10"));
  Add("change_y","Zmień Y o","Ruch","Przesunięcie pionowe.",F("A","O ile","10"));
  Add("glide","Leć do pozycji","Ruch","Płynny ruch w podanym czasie.",F("A","Sekundy","1"),F("B","X","100"),F("C","Y","0"));
  Add("bounce","Odbij od krawędzi","Ruch","Odbija duszka od brzegu sceny.");
  Nest("repeat","Powtórz","Sterowanie","Wykonuje bloczki w środku określoną liczbę razy.",false,false,F("A","Ile razy","10"));
  Nest("forever","Zawsze","Sterowanie","Powtarza do zatrzymania programu; nie blokuje okna.",false,false);
  Nest("if","Jeżeli","Sterowanie","Porównuje liczby lub tekst. Możesz wpisać =wyrażenie w wartości A.",false,false,F("A","Wartość A","{punkty}"),F("B","Wartość B","10"),F("Op","Porównanie",">=","==|!=|>|<|>=|<="));
  Nest("if_else","Jeżeli / w przeciwnym razie","Sterowanie","Wykonuje jedną z dwóch gałęzi.",false,true,F("A","Wartość A","{punkty}"),F("B","Wartość B","10"),F("Op","Porównanie",">=","==|!=|>|<|>=|<="));
  Nest("until","Powtarzaj aż","Sterowanie","Powtarza, dopóki wyrażenie nie jest prawdziwe. Np. =punkty >= 10.",false,false,F("A","Warunek","=punkty >= 10"));
  Nest("for","Dla zmiennej od / do","Sterowanie","Zakres włącznie; krok może być ujemny.",false,false,F("A","Zmienna","i"),F("B","Od","1"),F("C","Do","10"),F("D","Krok","1"));
  Add("wait","Czekaj","Sterowanie","Czeka bez blokowania interfejsu.",F("A","Sekundy","1"));
  Add("wait_until","Czekaj aż","Sterowanie","Np. =punkty > 5.",F("A","Warunek","=punkty > 5"));
  Add("break","Przerwij pętlę","Sterowanie","Kończy najbliższą otaczającą pętlę.");
  Add("stop_this","Zatrzymaj ten skrypt","Sterowanie","Pozostałe zdarzenia działają dalej.");
  Add("stop","Zatrzymaj wszystko","Sterowanie","Zatrzymuje skrypty i zdarzenia. Okno pozostaje otwarte.");
  Add("comment","Komentarz","Sterowanie","Notatka autora.",F("A","Opis","Mój pomysł"));
  Add("set","Ustaw zmienną","Zmienne","Wpisz liczbę, tekst, {zmienna} lub =wyrażenie.",F("A","Zmienna","punkty"),F("B","Wartość","0"));
  Add("change","Zmień zmienną o","Zmienne","Dodaje do zmiennej liczbę.",F("A","Zmienna","punkty"),F("B","Dodaj","1"));
  Add("random","Losuj liczbę","Zmienne","Losuje liczbę całkowitą z zakresu włącznie.",F("A","Zapisz w","los"),F("B","Od","1"),F("C","Do","6"));
  Add("math","Oblicz działanie","Zmienne","Wynik działania trafia do zmiennej.",F("A","Zapisz w","wynik"),F("B","Liczba A","5"),F("C","Działanie","+","+|-|*|/|%|^"),F("D","Liczba B","2"));
  Add("function","Oblicz funkcję","Zmienne","Funkcje trygonometryczne używają stopni.",F("A","Zapisz w","wynik"),F("B","Funkcja","sqrt","sqrt|abs|round|floor|ceil|sin|cos|tan|log"),F("C","Wartość","25"));
  Add("join","Połącz teksty","Zmienne","Łączy dwa teksty w jeden.",F("A","Zapisz w","tekst"),F("B","Tekst A","Cześć "),F("C","Tekst B","{imie}"));
  Add("text_length","Długość tekstu","Zmienne","Zwraca liczbę znaków.",F("A","Tekst","Cześć"),F("B","Zapisz w","dlugosc"));
  Add("text_replace","Zamień fragment tekstu","Zmienne","Zamienia wszystkie wystąpienia.",F("A","Zapisz w","tekst"),F("B","Tekst","Ala ma kota"),F("C","Szukaj","kota"),F("D","Zamień na","psa"));
  Add("watch","Pokaż zmienną na scenie","Zmienne","Włącza lub wyłącza monitor wartości.",F("A","Zmienna","punkty"),F("B","Widoczna","1","1|0"));
  Add("list_create","Utwórz listę","Listy","Opcjonalne wartości początkowe rozdziel znakiem |.",F("A","Lista","lista"),F("B","Elementy",""));
  Add("list_add","Dodaj do listy","Listy","Dodaje element na końcu.",F("A","Lista","lista"),F("B","Wartość","Jabłko"));
  Add("list_get","Odczytaj element listy","Listy","Indeksy zaczynają się od 1.",F("A","Lista","lista"),F("B","Numer","1"),F("C","Zapisz w","element"));
  Add("list_set","Zmień element listy","Listy","Zastępuje wskazany element.",F("A","Lista","lista"),F("B","Numer","1"),F("C","Wartość","Gruszka"));
  Add("list_remove","Usuń element listy","Listy","Usuwa wskazany element.",F("A","Lista","lista"),F("B","Numer","1"));
  Add("list_length","Długość listy","Listy","Zapisuje liczbę elementów.",F("A","Lista","lista"),F("B","Zapisz w","ile"));
  Add("list_contains","Czy lista zawiera","Listy","Zapisuje 1 lub 0.",F("A","Lista","lista"),F("B","Wartość","Jabłko"),F("C","Zapisz w","zawiera"));
  Add("list_clear","Wyczyść listę","Listy","Usuwa zawartość listy programu.",F("A","Lista","lista"));
  Add("mouse","Pozycja myszy","Czujniki","Zapisuje współrzędne względem środka sceny.",F("A","Zmienna X","mysz_x"),F("B","Zmienna Y","mysz_y"));
  Add("key_pressed","Czy klawisz wciśnięty","Czujniki","Zapisuje 1 albo 0.",F("A","Klawisz","Space"),F("B","Zapisz w","wcisniety"));
  Add("sprite_position","Odczytaj pozycję duszka","Czujniki","Zapisuje X i Y aktywnego duszka.",F("A","Zmienna X","x"),F("B","Zmienna Y","y"));
  Add("distance","Odległość między duszkami","Czujniki","Odległość między środkami w pikselach sceny.",F("A","Duszek A","robot"),F("B","Duszek B","cel"),F("C","Zapisz w","odleglosc"));
  Add("touch_edge","Czy dotyka krawędzi","Czujniki","Zapisuje 1 albo 0.",F("A","Zapisz w","krawedz"));
  Add("timer","Odczytaj stoper","Czujniki","Liczba sekund od startu lub wyzerowania.",F("A","Zapisz w","czas"));
  Add("timer_reset","Wyzeruj stoper","Czujniki","Uruchamia stoper od zera.");
  Add("pen_down","Przyłóż pisak","Pisak","Duszek rysuje podczas ruchu.");
  Add("pen_up","Podnieś pisak","Pisak","Ruch nie zostawia śladu.");
  Add("pen_color","Kolor pisaka","Pisak","Kolor linii rysowanych przez aktywnego duszka.",F("A","Kolor","#8b68f6"));
  Add("pen_size","Grubość pisaka","Pisak","Od 1 do 50 pikseli.",F("A","Grubość","3"));
  Add("pen_clear","Wyczyść rysunek","Pisak","Usuwa linie i stemple ze sceny.");
  Add("stamp","Stempluj duszka","Pisak","Zostawia kopię duszka na scenie.");
  Add("beep","Zagraj ton","Dźwięk","Częstotliwość w Hz, czas w sekundach.",F("A","Częstotliwość","660"),F("B","Sekundy","0,15"));
  Add("sound","Dźwięk systemowy","Dźwięk","Odtwarza krótki dźwięk Windows.",F("A","Dźwięk","Asterisk","Asterisk|Beep|Exclamation|Hand|Question"));
  Add("file_open","Wybierz plik do odczytu","Pliki","Otwiera systemowy wybór pliku. Najpierw włącz kategorię Pliki.",F("A","Zapisz ścieżkę w","plik"));
  Add("file_save","Wybierz plik do zapisu","Pliki","Otwiera systemowy wybór miejsca zapisu.",F("A","Zapisz ścieżkę w","plik"),F("B","Proponowana nazwa","notatka.txt"));
  Add("file_read","Odczytaj plik tekstowy","Pliki","Czyta plik UTF-8. Limit 5 MB.",F("A","Ścieżka","{plik}"),F("B","Zapisz tekst w","zawartosc"));
  Add("file_write","Zapisz plik tekstowy","Pliki","Przed zastąpieniem istniejącego pliku pyta użytkownika.",F("A","Ścieżka","{plik}"),F("B","Tekst","{zawartosc}"));
  Add("file_append","Dopisz do pliku","Pliki","Dopisuje tekst na końcu pliku UTF-8.",F("A","Ścieżka","{plik}"),F("B","Tekst","Nowy wiersz"));
  Add("file_exists","Czy plik istnieje","Pliki","Zapisuje 1 lub 0.",F("A","Ścieżka","{plik}"),F("B","Zapisz w","istnieje"));
 }
 public static Color ColorFor(string group){int i=Array.IndexOf(Groups,group);Color[] c={Color.FromArgb(213,166,43),Color.FromArgb(108,102,235),Color.FromArgb(64,139,221),Color.FromArgb(166,94,214),Color.FromArgb(219,143,47),Color.FromArgb(30,159,152),Color.FromArgb(204,102,81),Color.FromArgb(46,160,190),Color.FromArgb(39,157,111),Color.FromArgb(184,83,154),Color.FromArgb(113,126,153)};return c[Math.Max(0,i)];}
}
public static class ProjectIO {
 const string Marker="// EASYJAVA_PROJECT_V2:";
 public static string Json(Project p){using(MemoryStream m=new MemoryStream()){new DataContractJsonSerializer(typeof(Project)).WriteObject(m,p);return Encoding.UTF8.GetString(m.ToArray());}}
 public static Project FromJson(string text){if(text.Length>40000000)throw new Exception("Plik projektu jest za duży.");using(MemoryStream m=new MemoryStream(Encoding.UTF8.GetBytes(text))){Project p=(Project)new DataContractJsonSerializer(typeof(Project)).ReadObject(m);Normalize(p);return p;}}
 public static Project Read(string path){string t=File.ReadAllText(path,Encoding.UTF8);return Parse(t);}
 public static Project Parse(string t){if(t.TrimStart('\uFEFF',' ','\r','\n','\t').StartsWith("{"))return FromJson(t.TrimStart('\uFEFF'));int start=t.IndexOf(Marker,StringComparison.Ordinal);if(start<0)throw new Exception("Ten plik .js nie zawiera projektu EasyJava. Otwórz plik zapisany przez EasyJava.");start+=Marker.Length;int end=t.IndexOf('\n',start);string raw=(end<0?t.Substring(start):t.Substring(start,end-start)).Trim();return FromJson(Encoding.UTF8.GetString(Convert.FromBase64String(raw)));}
 public static void Normalize(Project p){if(p==null||p.Blocks==null||p.Version<1||p.Version>2)throw new Exception("Nieobsługiwany projekt.");p.Version=2;p.Name=p.Name??"mojprojekt";if(double.IsNaN(p.StepDelaySeconds)||double.IsInfinity(p.StepDelaySeconds)||p.StepDelaySeconds<0||p.StepDelaySeconds>3600)throw new Exception("Odstęp między bloczkami musi wynosić od 0 do 3600 sekund.");if(p.Features==null)p.Features=Catalog.Groups.ToDictionary(x=>x,x=>x!="Pliki");if(p.Assets==null)p.Assets=new Dictionary<string,string>();int count=0;Check(p.Blocks,0,ref count);if(p.Assets.Sum(x=>(long)x.Value.Length)>28000000)throw new Exception("Obrazy w projekcie są za duże (limit około 20 MB).");}
 static void Check(List<Block> list,int depth,ref int count){if(depth>20)throw new Exception("Limit 20 poziomów zagnieżdżenia.");foreach(Block b in list){if(++count>2000)throw new Exception("Limit 2000 bloczków.");if(b==null||b.Kind==null||!Catalog.All.ContainsKey(b.Kind))throw new Exception("Nieznany bloczek w projekcie.");b.A=b.A??"";b.B=b.B??"";b.C=b.C??"";b.D=b.D??"";b.E=b.E??"";b.F=b.F??"";b.Op=b.Op??"==";if(b.Children==null)b.Children=new List<Block>();if(b.Else==null)b.Else=new List<Block>();if(new[]{b.A,b.B,b.C,b.D,b.E,b.F}.Any(x=>x.Length>200000))throw new Exception("Zbyt długi tekst w bloczku.");Check(b.Children,depth+1,ref count);Check(b.Else,depth+1,ref count);}}
 public static int Count(List<Block> list){return list.Sum(b=>1+Count(b.Children)+Count(b.Else));}
 public static string Safe(string name){name=(name??"").Trim();foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');name=name.TrimEnd(' ','.');if(Regex.IsMatch(name,@"^(CON|PRN|AUX|NUL|COM[0-9]|LPT[0-9])(?:\.|$)",RegexOptions.IgnoreCase))name="projekt_"+name;if(name.Length>70)name=name.Substring(0,70);return name.Length==0?"mojprojekt":name;}
 static string Q(string s){return "\""+s.Replace("\\","\\\\").Replace("\"","\\\"").Replace("\r","\\r").Replace("\n","\\n")+"\"";}
 public static string Export(Project p,string runtimeExe){
  Normalize(p);StringBuilder b=new StringBuilder();
  b.AppendLine("// EasyJava 2 - program okienkowy Windows, uruchamiany bez Node.js.");
  b.AppendLine(Marker+Convert.ToBase64String(Encoding.UTF8.GetBytes(Json(p))));
  b.AppendLine("// Ten plik jest jednoczesnie programem i edytowalnym projektem EasyJava.");
  b.AppendLine("var shell = new ActiveXObject(\"WScript.Shell\");");
  b.AppendLine("var fs = new ActiveXObject(\"Scripting.FileSystemObject\");");
  b.AppendLine("var dir = null, exitCode = 0;");
  b.AppendLine("var testing = WScript.Arguments.Named.Exists(\"easyjava-test\");");
  b.AppendLine("try {");
  b.AppendLine("  dir = fs.BuildPath(fs.GetSpecialFolder(2), \"EasyJava_\" + fs.GetTempName());");
  b.AppendLine("  fs.CreateFolder(dir);");
  b.AppendLine("  var exe = fs.BuildPath(dir, \"easyjava-player.exe\");");
  b.AppendLine("  var xml = new ActiveXObject(\"Msxml2.DOMDocument.6.0\");");
  b.AppendLine("  var data = xml.createElement(\"binary\"); data.dataType = \"bin.base64\";");
  b.AppendLine("  data.text = \""+Convert.ToBase64String(File.ReadAllBytes(runtimeExe))+"\";");
  b.AppendLine("  var stream = new ActiveXObject(\"ADODB.Stream\"); stream.Type = 1; stream.Open();");
  b.AppendLine("  stream.Write(data.nodeTypedValue); stream.SaveToFile(exe, 2); stream.Close();");
  b.AppendLine("  var mode = testing ? \"--smoke\" : \"--player\";");
  b.AppendLine("  exitCode = shell.Run('\"' + exe + '\" ' + mode + ' \"' + WScript.ScriptFullName + '\"', testing ? 0 : 1, true);");
  b.AppendLine("} catch (e) { exitCode = 1; if (testing) WScript.Echo(e.message); else shell.Popup(\"Nie mozna uruchomic programu EasyJava:\\n\" + e.message, 0, \"EasyJava\", 16); }");
  b.AppendLine("finally { if (dir != null && fs.FolderExists(dir)) { try { fs.DeleteFolder(dir, true); } catch (ignored) {} } }");
  b.AppendLine("WScript.Quit(exitCode);");return b.ToString();
 }
 public static void Write(string path,Project p,string runtime){string text=Export(p,runtime);string temp=path+".tmp-"+Guid.NewGuid().ToString("N");try{File.WriteAllText(temp,text,Encoding.ASCII);if(File.Exists(path)){string backup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EasyJava","Kopie");Directory.CreateDirectory(backup);File.Copy(path,Path.Combine(backup,Safe(Path.GetFileNameWithoutExtension(path))+"-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".js"),false);File.Replace(temp,path,null);}else File.Move(temp,path);}finally{if(File.Exists(temp))File.Delete(temp);}}
 public static void Shortcut(string path,string script,string icon){Type t=Type.GetTypeFromProgID("WScript.Shell");object s=null,l=null;try{s=Activator.CreateInstance(t);l=t.InvokeMember("CreateShortcut",System.Reflection.BindingFlags.InvokeMethod,null,s,new object[]{path});Type z=l.GetType();Action<string,object> set=(k,v)=>z.InvokeMember(k,System.Reflection.BindingFlags.SetProperty,null,l,new[]{v});set("TargetPath",Path.Combine(Environment.SystemDirectory,"wscript.exe"));set("Arguments","//E:JScript //nologo \""+script+"\"");set("WorkingDirectory",Path.GetDirectoryName(script));set("IconLocation",icon+",0");z.InvokeMember("Save",System.Reflection.BindingFlags.InvokeMethod,null,l,null);}finally{if(l!=null)Marshal.FinalReleaseComObject(l);if(s!=null)Marshal.FinalReleaseComObject(s);}}
}
}
