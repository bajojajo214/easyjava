using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;

namespace EasyJava2 {
public static class PauseUiTests {
 static readonly BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
 static void Check(bool condition,string message){if(!condition)throw new Exception("Przerwy GUI: "+message);}
 static IEnumerable<Control> Desc(Control parent){foreach(Control child in parent.Controls){yield return child;foreach(Control nested in Desc(child))yield return nested;}}
 static T Field<T>(Editor editor,string name){FieldInfo field=typeof(Editor).GetField(name,Private);Check(field!=null,"brak pola "+name);return (T)field.GetValue(editor);}
 static void Call(Editor editor,string name){MethodInfo method=typeof(Editor).GetMethod(name,Private);Check(method!=null,"brak polecenia "+name);method.Invoke(editor,null);}
 static void LabelIs(Button button,decimal seconds){string value=seconds.ToString("0.##",CultureInfo.GetCultureInfo("pl-PL"));Check(button.Text.Contains("Przerw")&&button.Text.Contains(value+" s"),"przycisk nie pokazuje aktualnej przerwy: "+button.Text);}
 static void Inside(Control child,Panel panel){Rectangle bounds=panel.RectangleToClient(child.Parent.RectangleToScreen(child.Bounds));Check(panel.ClientRectangle.Contains(bounds),"pole lub jednostka wychodzi poza panel przy minimalnym oknie: "+child.Name);}

 public static void Run(List<string> results){using(Editor editor=new Editor("")){
  try{
   IntPtr handle=editor.Handle;editor.PerformLayout();editor.Render();
   NumericUpDown seconds=Desc(editor).OfType<NumericUpDown>().Single(x=>x.Name=="stepDelaySeconds");
   Panel panel=Desc(editor).OfType<Panel>().Single(x=>x.Name=="stepDelayPanel");
   Button button=Desc(editor).OfType<Button>().Single(x=>x.Name=="stepDelayButton");
   Label unit=Desc(editor).OfType<Label>().Single(x=>x.Name=="stepDelayUnit");
   Check(seconds.Minimum==0&&seconds.Maximum==3600&&seconds.DecimalPlaces==2,"nieprawidłowy zakres pola sekund");
   seconds.Value=.75M;Check(editor.Project.StepDelaySeconds==.75,"zmiana pola nie zmienia projektu");LabelIs(button,.75M);
   Call(editor,"Undo");Check(editor.Project.StepDelaySeconds==0&&seconds.Value==0,"cofanie nie synchronizuje pola");LabelIs(button,0);
   Call(editor,"Redo");Check(editor.Project.StepDelaySeconds==.75&&seconds.Value==.75M,"ponawianie nie synchronizuje pola");LabelIs(button,.75M);
   results.Add("PASS przerwy GUI: pole sekund zmienia projekt, przycisk oraz cofanie i ponawianie");

   Stack<string> undo=Field<Stack<string>>(editor,"undo"),redo=Field<Stack<string>>(editor,"redo");undo.Clear();redo.Clear();
   typeof(Editor).GetField("dirty",Private).SetValue(editor,false);editor.Project=new Project();editor.Render();
   Check(seconds.Value==0&&editor.Project.StepDelaySeconds==0,"nowy projekt nie zeruje pola przerwy");
   Check(undo.Count==0&&redo.Count==0&&!Field<bool>(editor,"dirty"),"synchronizacja nowego projektu tworzy pozorną zmianę");LabelIs(button,0);
   results.Add("PASS przerwy GUI: odświeżenie pustego projektu zeruje ustawienie bez zmian i wpisów cofania");

   editor.Size=editor.MinimumSize;editor.PerformLayout();foreach(Control control in Desc(editor).ToArray())control.PerformLayout();editor.Render();
   Check(panel.Width>0&&panel.Height>0,"panel przerw nie ma miejsca przy minimalnym oknie");Inside(seconds,panel);Inside(unit,panel);
   results.Add("PASS przerwy GUI: pole sekund i jednostka mieszczą się przy minimalnym rozmiarze okna");

   TextBox search=Field<TextBox>(editor,"search");foreach(string term in new[]{"przerwa","pauza","odstęp"}){
    search.Text=term;Button wait=Desc(editor).OfType<Button>().SingleOrDefault(x=>x.Name=="palette_wait");
    Check(wait!=null,"wyszukiwanie nie znajduje bloczka Czekaj: "+term);
    typeof(Control).GetMethod("OnClick",Private).Invoke(wait,new object[]{EventArgs.Empty});
    Check(editor.Project.Blocks.Count==0,"kliknięcie wyniku wyszukiwania dodało bloczek");
   }
   results.Add("PASS przerwy GUI: przerwa, pauza i odstęp znajdują Czekaj; kliknięcie nadal nie dodaje bloczka");

   seconds.Text=(1.25M).ToString(CultureInfo.CurrentCulture);Call(editor,"CommitTimingInput");
   Check(editor.Project.StepDelaySeconds==1.25&&seconds.Value==1.25M,"wpisany tekst nie jest zatwierdzany przed poleceniem");LabelIs(button,1.25M);
   results.Add("PASS przerwy GUI: wpisany czas jest zatwierdzany przed uruchomieniem lub zapisem");
  }finally{
   System.Windows.Forms.Timer timer=Field<System.Windows.Forms.Timer>(editor,"previewTimer");if(timer!=null){timer.Stop();timer.Dispose();}editor.Preview.Stop();
  }
 }}
}
}
