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
  Project original=Examples.Counter();string originalJson=ProjectIO.Json(original);
  using(OllamaDialog dialog=new OllamaDialog(original,false,false)){
   dialog.ShowInTaskbar=false;dialog.WindowState=FormWindowState.Minimized;dialog.Show();dialog.EndpointInput.Text="http://127.0.0.1:1";
   Check(dialog.ModifyCurrent.Checked&&dialog.Result==null,"brak oryginalnego projektu przed wyborem wzorca");
   dialog.CalculatorButton.PerformClick();
   Check(dialog.Result!=null&&dialog.UseButton.Enabled&&!dialog.ResultModifiesCurrent&&!dialog.ModifyCurrent.Checked,"gotowy kalkulator nie tworzy osobnej propozycji możliwej do użycia");
   Check(dialog.Result.Summary.Contains("wzorzec")&&dialog.Result.Summary.Contains("bez generowania AI")&&dialog.State.Text.Contains("bez Ollamy"),"wzorzec kalkulatora jest przedstawiony jako wynik modelu AI");
   Check(ProjectIO.Json(original)==originalJson&&dialog.Result.Project!=original,"sam wybór gotowego kalkulatora zmienia oryginalny projekt przed Użyj projektu");
   Check(ProjectIO.Json(dialog.Result.Project)==ProjectIO.Json(Examples.Calculator()),"przycisk kalkulatora nie zwraca sprawdzonego wzorca");
   dialog.UseButton.PerformClick();Check(dialog.DialogResult==DialogResult.OK&&ProjectIO.Json(original)==originalJson,"Użyj projektu nie oddaje wzorca do edytora lub samodzielnie zmienia oryginał");
  }
  results.Add("PASS Ollama kalkulator: przycisk działa bez połączenia i generowania, jawnie oznacza wzorzec i chroni bieżący projekt do momentu wstawienia");
  List<OllamaModelChoice> choices=OllamaModelDialog.Choices("qwen2.5-coder:3b",new[]{"qwen2.5-coder:3b","lokalny:latest"});
  Check(choices[0].Name=="lokalny:latest"&&choices[0].Installed,"najpierw inny model już pobrany");
  Check(!choices.Any(x=>x.Name=="qwen2.5-coder:3b")&&choices.Any(x=>x.Name=="qwen2.5-coder:7b"&&!x.Installed&&x.Detail.Contains("4,7")),"propozycja innego modelu z rozmiarem pobierania");
  using(OllamaModelDialog advice=new OllamaModelDialog("qwen2.5-coder:3b",new[]{"lokalny:latest"}))Check(advice.Models.Items.Count>=2&&advice.SelectedModel==null&&advice.Details.Text.Length>0,"wybór i pobranie wymagają działania użytkownika");
  results.Add("PASS Ollama modele: propozycja innego pobranego modelu, alternatywy z rozmiarem, wybór bez automatycznego pobierania");
 }
}
}
