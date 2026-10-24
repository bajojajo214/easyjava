using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace EasyJava2 {
public static class DragDropTests {
 static IEnumerable<Control> Desc(Control p){foreach(Control c in p.Controls){yield return c;foreach(Control d in Desc(c))yield return d;}}
 static void Check(bool b,string text){if(!b)throw new Exception("Drag-and-drop: "+text);}
 static DragEventArgs Args(Editor e,DragInfo d,Point screen){DataObject data=new DataObject();data.SetData(typeof(DragInfo),d);return new DragEventArgs(data,1,screen.X,screen.Y,d.Block==null?DragDropEffects.Copy:DragDropEffects.Move,DragDropEffects.None);}
 static void Drop(Editor e,DragInfo d,List<Block> target,int index){e.Drop(Args(e,d,e.PointToScreen(new Point(200,200))),target,index);e.Render();}
 static void Event(Control target,string name,EventArgs e){typeof(Control).GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(target,new object[]{e});}
 public static void Run(List<string> results){using(Editor editor=new Editor("")){
  IntPtr handle=editor.Handle;editor.PerformLayout();editor.Render();editor.RenderPalette();
  Button palette=Desc(editor).OfType<Button>().Single(x=>x.Name=="palette_repeat");
  Event(palette,"OnClick",EventArgs.Empty);Event(palette,"OnDoubleClick",EventArgs.Empty);Check(editor.Project.Blocks.Count==0,"kliknięcie lub dwuklik dodał bloczek");
  editor.CreatePaletteBlock("repeat");Check(editor.Project.Blocks.Count==0,"utworzenie danych przeciągania zmieniło projekt przed upuszczeniem");results.Add("PASS DnD: kliknięcie i dwuklik nie dodają; brak upuszczenia nie zmienia projektu");
  Drop(editor,new DragInfo{Kind="ui_button"},editor.Project.Blocks,0);Drop(editor,new DragInfo{Kind="ui_button"},editor.Project.Blocks,1);Check(editor.Project.Blocks[0].A=="button1"&&editor.Project.Blocks[1].A=="button2","unikalne identyfikatory GUI");results.Add("PASS DnD: kolejne elementy GUI mają unikalne identyfikatory");
  editor.Project=new Project();Block a=Block.Make("comment","A"),b=Block.Make("comment","B"),c=Block.Make("comment","C");editor.Project.Blocks.AddRange(new[]{a,b,c});editor.Render();
  Drop(editor,new DragInfo{Block=c},editor.Project.Blocks,0);Check(editor.Project.Blocks.SequenceEqual(new[]{c,a,b}),"przeniesienie w górę tej samej listy");Drop(editor,new DragInfo{Block=c},editor.Project.Blocks,3);Check(editor.Project.Blocks.SequenceEqual(new[]{a,b,c}),"przeniesienie w dół tej samej listy");
  NodeList root=Desc(editor).OfType<NodeList>().Single(x=>x.Nodes==editor.Project.Blocks);NodeCard first=root.Controls.OfType<NodeCard>().First();int gap=first.Bottom+3;Check(root.IndexAt(gap)==1,"indeks odstępu między bloczkami");Event(root,"OnDragDrop",Args(editor,new DragInfo{Kind="wait"},root.PointToScreen(new Point(15,gap))));editor.Render();Check(editor.Project.Blocks[1].Kind=="wait"&&editor.Project.Blocks.Count==4,"upuszczenie w odstępie wstawiło na końcu");results.Add("PASS DnD: przenoszenie w obie strony i wstawianie pomiędzy bloczkami");
  editor.Project=new Project();Drop(editor,new DragInfo{Kind="repeat"},editor.Project.Blocks,0);Block loop=editor.Project.Blocks[0];Drop(editor,new DragInfo{Kind="say"},loop.Children,0);Block say=loop.Children[0];Check(loop.Children.Count==1,"upuszczenie do pętli");Drop(editor,new DragInfo{Block=say},editor.Project.Blocks,1);Check(loop.Children.Count==0&&editor.Project.Blocks[1]==say,"przenoszenie z wnętrza pętli na główny poziom");
  Drop(editor,new DragInfo{Kind="if_else"},editor.Project.Blocks,2);Block condition=editor.Project.Blocks[2];Drop(editor,new DragInfo{Block=say},condition.Else,0);Check(condition.Else.Single()==say,"upuszczenie do drugiej gałęzi warunku");Drop(editor,new DragInfo{Block=say},condition.Children,0);Check(condition.Else.Count==0&&condition.Children.Single()==say,"przenoszenie pomiędzy gałęziami");results.Add("PASS DnD: zagnieżdżanie, wyjmowanie i obie gałęzie warunku");
  Check(!editor.CanDrop(new DragInfo{Block=loop},loop.Children),"rodzic we własnym wnętrzu");Check(!editor.CanDrop(new DragInfo{Kind="event_button"},loop.Children),"zagnieżdżona deklaracja zdarzenia");Check(editor.CanDrop(new DragInfo{Kind="event_button"},editor.Project.Blocks),"zdarzenie na głównym poziomie");Check(editor.DragEffect(Args(editor,new DragInfo{Kind="wait"},Point.Empty),editor.Project.Blocks)==DragDropEffects.Copy,"paleta musi dawać Copy");Check(editor.DragEffect(Args(editor,new DragInfo{Block=loop},Point.Empty),editor.Project.Blocks)==DragDropEffects.Move,"istniejący bloczek musi dawać Move");results.Add("PASS DnD: odrzucanie własnych potomków i zagnieżdżonych zdarzeń; poprawne Copy/Move");
  editor.Project=new Project();Block comment=Block.Make("comment","Tekst");editor.Project.Blocks.Add(comment);editor.Render();NodeCard card=editor.Cards[comment];TextBox field=card.Controls.OfType<TextBox>().First();Check(field.AllowDrop,"pole blokuje upuszczenie");Event(field,"OnDragDrop",Args(editor,new DragInfo{Kind="wait"},card.PointToScreen(new Point(150,card.Height-3))));editor.Render();Check(editor.Project.Blocks.Count==2&&editor.Project.Blocks[1].Kind=="wait","upuszczenie przez pole tekstowe");results.Add("PASS DnD: nagłówki i pola przekazują upuszczenie do karty");
 }}
}
}
