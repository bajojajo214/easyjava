using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
namespace EasyJava2 {
public sealed class OllamaModelChoice {
 public string Name,Detail;public bool Installed;
 public override string ToString(){return Name+(Installed?"  • pobrany":"  • do pobrania");}
}
public sealed class OllamaModelDialog:Form {
 public readonly ListBox Models;public readonly Label Details;public string SelectedModel {get;private set;}
 public static List<OllamaModelChoice> Choices(string current,IEnumerable<string> installed){
  HashSet<string> local=new HashSet<string>(installed,StringComparer.OrdinalIgnoreCase);List<OllamaModelChoice> choices=new List<OllamaModelChoice>();
  foreach(string model in local.Where(x=>!string.Equals(x,current,StringComparison.OrdinalIgnoreCase)))choices.Add(new OllamaModelChoice{Name=model,Installed=true,Detail="Ten model jest już na wybranym serwerze. Możesz od razu spróbować tego samego polecenia."});
  foreach(string[] suggestion in new[]{new[]{"qwen2.5-coder:7b","Większy model do kodu • pobranie około 4,7 GB. Może lepiej poradzić sobie z trudniejszym projektem, ale potrzebuje więcej pamięci i może działać wolniej."},new[]{"qwen2.5-coder:3b","Mniejszy model do kodu • pobranie około 1,9 GB. Kompromis między rozmiarem a czasem generowania."},new[]{"qwen2.5-coder:1.5b","Lekki model do prostych zadań • pobranie około 1 GB. Zużywa mniej pamięci, lecz może częściej popełniać błędy."}}){if(string.Equals(current,suggestion[0],StringComparison.OrdinalIgnoreCase)||choices.Any(x=>string.Equals(x.Name,suggestion[0],StringComparison.OrdinalIgnoreCase)))continue;choices.Add(new OllamaModelChoice{Name=suggestion[0],Installed=local.Contains(suggestion[0]),Detail=suggestion[1]});}
  return choices;
 }
 public OllamaModelDialog(string current,IEnumerable<string> installed){
  Text="Wybierz inny model Ollama";Icon=Program.Icon;BackColor=Ui.Panel;ForeColor=Ui.Text;Font=new Font("Segoe UI",10);ClientSize=new Size(655,435);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;
  Label title=Ui.Label("Spróbuj innego modelu",19);title.Font=new Font("Segoe UI",19,FontStyle.Bold);title.SetBounds(23,17,610,40);Controls.Add(title);
  Label info=Ui.Label("Obecny: "+current+"\nZmiana modelu zachowuje opis zadania i Twój projekt.",10);info.ForeColor=Ui.Muted;info.SetBounds(25,65,600,51);Controls.Add(info);
  Models=new ListBox{Name="ollamaSuggestedModels",BackColor=Ui.Bg,ForeColor=Ui.Text,BorderStyle=BorderStyle.FixedSingle,Font=new Font("Segoe UI",11),Location=new Point(25,125),Size=new Size(602,125)};foreach(OllamaModelChoice choice in Choices(current,installed))Models.Items.Add(choice);Controls.Add(Models);
  Details=Ui.Label("",10);Details.ForeColor=Ui.Muted;Details.SetBounds(26,260,600,80);Controls.Add(Details);Models.SelectedIndexChanged+=delegate{OllamaModelChoice choice=Models.SelectedItem as OllamaModelChoice;Details.Text=choice==null?"":choice.Detail;};if(Models.Items.Count>0)Models.SelectedIndex=0;
  Label note=Ui.Label("Model zostanie pobrany dopiero po kliknięciu Pobierz model w panelu AI.",9);note.ForeColor=Ui.Muted;note.SetBounds(26,339,600,26);Controls.Add(note);
  Button cancel=Ui.Button("Anuluj",Ui.Raised);cancel.SetBounds(363,382,120,35);cancel.DialogResult=DialogResult.Cancel;CancelButton=cancel;Controls.Add(cancel);
  Button use=Ui.Button("Wybierz model",Ui.Purple);use.SetBounds(493,382,135,35);use.Click+=delegate{OllamaModelChoice choice=Models.SelectedItem as OllamaModelChoice;if(choice==null)return;SelectedModel=choice.Name;DialogResult=DialogResult.OK;Close();};AcceptButton=use;Controls.Add(use);
 }
}
}
