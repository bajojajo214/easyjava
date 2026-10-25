using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
namespace EasyJava2 {
public static class OllamaUiTests {
 static void Check(bool value,string message){if(!value)throw new Exception("Ollama GUI: "+message);}
 public static void Run(List<string> results){
  using(OllamaDialog dialog=new OllamaDialog(new Project(),false,false)){
   Check(!dialog.ModifyCurrent.Checked&&!dialog.UseButton.Enabled&&dialog.Result==null,"nowy projekt bez gotowej propozycji");
   Check(dialog.EndpointInput.Text==OllamaClient.DefaultEndpoint&&dialog.ModelInput.Text.Length>0,"domyślne lokalne połączenie");
   dialog.GenerateButton.PerformClick();Check(dialog.Result==null,"pusty prompt nie generuje");
  }
  using(OllamaDialog dialog=new OllamaDialog(Examples.Counter(),false,false))Check(dialog.ModifyCurrent.Checked,"istniejący projekt domyślnie do poprawy");
  using(Editor editor=new Editor("")){
   editor.Project=Examples.Counter();editor.Project.SourcePath=@"C:\projekty\licznik.js";typeof(Editor).GetField("filePath",BindingFlags.Instance|BindingFlags.NonPublic).SetValue(editor,editor.Project.SourcePath);string before=ProjectIO.Json(editor.Project);
   Project generated=Examples.Todo();editor.ApplyAiProject(generated);
   Check(editor.Project.Name==generated.Name&&editor.Project!=generated,"wstawienie kopii wygenerowanych bloczków");
   Check(editor.Project.SourcePath==@"C:\projekty\licznik.js","zachowanie folderu projektu");
   Check(editor.Preview.Stage.Elements.Count==0,"wstawienie AI nie uruchamia projektu");
   typeof(Editor).GetMethod("Undo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);
   Check(ProjectIO.Json(editor.Project)==before,"Cofnij odtwarza poprzedni projekt");
   typeof(Editor).GetMethod("Redo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);
   Check(editor.Project.Name==generated.Name,"Ponów odtwarza projekt AI");
   editor.ApplyAiProject(Examples.Gui(),false);
   Check(editor.Project.SourcePath==null&&typeof(Editor).GetField("filePath",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor)==null,"nowy projekt AI nie nadpisuje wcześniejszego pliku");
   typeof(Editor).GetMethod("Undo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);
   Check(editor.Project.Name==generated.Name&&editor.Project.SourcePath==@"C:\projekty\licznik.js"&&(string)typeof(Editor).GetField("filePath",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(editor)==editor.Project.SourcePath,"Cofnij nowy projekt AI odtwarza ścieżki dokumentu");
   typeof(Editor).GetMethod("Redo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(editor,null);
   Check(editor.Project.SourcePath==null,"Ponów nowy projekt AI odtwarza osobny dokument");
  }
  results.Add("PASS Ollama GUI: lokalne ustawienia, tryb poprawy, wstawianie bez uruchamiania, Cofnij i Ponów");
 }
}
}
