using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace EasyJava2 {
public static class TerminalTests {
 static void Check(bool condition,string message){if(!condition)throw new Exception("Terminal ANSI: "+message);}
 static string Normal(TerminalScreen screen){return screen.PlainText.Replace("\r\n","\n").TrimEnd('\n');}

 public static void Run(List<string> results){
  using(TerminalScreen screen=new TerminalScreen()){
   screen.Feed("pierwszy\r\ndrugi");
   Check(Normal(screen)=="pierwszy\ndrugi","CR/LF nie tworzy osobnych wierszy");
   screen.ResetScreen();screen.Feed("12345");screen.Feed("\x1b[");screen.Feed("Hcat");
   Check(Normal(screen)=="cat45","podzielone CSI H nie nadpisuje poprzedniej klatki");
   screen.Feed("\x1b[2J\x1b[HXY");
   Check(Normal(screen)=="XY"&&!screen.PlainText.Contains("\x1b"),"czyszczenie i nowa klatka zachowują stary tekst albo sekwencje ANSI");
   results.Add("PASS terminal ANSI: CR/LF, podzielone sekwencje oraz powrót kursora nadpisują i odświeżają klatkę");

   screen.ResetScreen();for(int line=0;line<18;line++)screen.Feed(("klatka "+line).PadRight(50,' ')+"\n");
   Check(Normal(screen).StartsWith("klatka 0\n")&&Normal(screen).Split('\n').Length==18&&Normal(screen).EndsWith("klatka 17"),"pomiar szerokości czcionki przy skalowaniu Windows ucina lub zawija klatkę 50x18");
   results.Add("PASS terminal ANSI: pełna klatka 50x18 mieści się bez obcięcia przy skalowaniu Windows");

   screen.ResetScreen();screen.Feed("ABCDE\rXY\x1b[K");
   Check(Normal(screen)=="XY","CSI K nie usuwa końcówki wiersza");
   screen.ResetScreen();screen.Feed("top\r\nbottom\x1b[1;2H\x1b[J");
   Check(Normal(screen)=="t","CSI J nie usuwa tekstu od kursora do końca ekranu");
   screen.ResetScreen();screen.Feed("ABCDE\x1b[1;3H\x1b[1K");
   Check(Normal(screen)=="   DE","CSI 1K nie usuwa początku wiersza razem z pozycją kursora");
   screen.Feed("\x1b[2K");Check(Normal(screen)=="","CSI 2K nie czyści całego wiersza");
   results.Add("PASS terminal ANSI: pozycjonowanie oraz czyszczenie J i K usuwają właściwe fragmenty ekranu");

   screen.ResetScreen();screen.Feed("\x1b[38;2;12;");screen.Feed("34;56mA\x1b[48;2;65;43;21mB");
   Check(screen.ForegroundAt(0,0).ToArgb()==Color.FromArgb(12,34,56).ToArgb(),"kolor RGB po sekwencji podzielonej między fragmenty");
   Check(screen.BackgroundAt(0,1).ToArgb()==Color.FromArgb(65,43,21).ToArgb(),"kolor RGB tła");
   screen.Feed("\x1b[38;5;196mC\x1b[48;5;21mD");
   Check(screen.ForegroundAt(0,2).ToArgb()==Color.FromArgb(255,0,0).ToArgb(),"kolor 196 z palety 256 kolorów");
   Check(screen.BackgroundAt(0,3).ToArgb()==Color.FromArgb(0,0,255).ToArgb(),"kolor 21 tła z palety 256 kolorów");
   screen.Feed("\x1b[0mE");
   Check(screen.ForegroundAt(0,4).ToArgb()!=Color.FromArgb(255,0,0).ToArgb(),"SGR 0 nie przywraca domyślnego koloru");
   Check(Normal(screen)=="ABCDE","kolory ANSI pojawiają się jako tekst");
   results.Add("PASS terminal ANSI: kolory RGB, paleta 256 kolorów, tło i reset stylu zachowują właściwe komórki");

   screen.ResetScreen();screen.Feed("A\r\nB\x1b[1A\x1b[2CZ");
   Check(Normal(screen)=="A  Z\nB","ruch kursora względem bieżącej pozycji");
   screen.Feed("\x1b]0;tytuł");screen.Feed(" okna\x07Q\x1b[?25l");
   Check(Normal(screen)=="A  ZQ\nB"&&!screen.PlainText.Contains("tytuł"),"sekwencja OSC tytułu albo prywatna sekwencja kursora jest widoczna w tekście");
   results.Add("PASS terminal ANSI: ruch kursora i podzielone OSC działają bez wypisywania kodów sterujących");

   screen.ResetScreen();for(int i=0;i<1500;i++)screen.Feed("wiersz "+i+"\r\n");
   Check(screen.Rows>0&&screen.Rows<=200&&screen.Columns>0&&screen.Columns<=300,"rozmiar siatki przekracza ograniczenie");
   Check(screen.CursorRow>=0&&screen.CursorRow<screen.Rows&&screen.CursorColumn>=0&&screen.CursorColumn<screen.Columns,"kursor wychodzi poza siatkę po przewijaniu");
   Check(screen.PlainText.Length<=screen.Rows*(screen.Columns+2)&&screen.PlainText.Contains("1499")&&!screen.PlainText.Contains("wiersz 0\r\n"),"bufor nie przewija się lub gromadzi całą historię");
   screen.ResetScreen();Check(screen.PlainText==""&&screen.CursorRow==0&&screen.CursorColumn==0,"reset nie czyści ekranu i kursora");
   results.Add("PASS terminal ANSI: przewijanie i reset zachowują ograniczony rozmiar bufora ekranu");
  }
 }
}
}
