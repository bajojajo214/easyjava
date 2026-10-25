using System;
using System.IO;
using System.Windows.Forms;

namespace EasyJava2 {
public class RuntimeDialogs {
 public virtual void Inform(IWin32Window owner,string text,string title){MessageBox.Show(owner,text,title,MessageBoxButtons.OK,MessageBoxIcon.Information);}
 public virtual bool Confirm(IWin32Window owner,string text,string title){return MessageBox.Show(owner,text,title,MessageBoxButtons.YesNo,MessageBoxIcon.Question)==DialogResult.Yes;}
 public virtual string ChooseFile(IWin32Window owner,bool save,string initial,string folder){
  using(FileDialog dialog=save?(FileDialog)new SaveFileDialog{DefaultExt="txt",AddExtension=true,OverwritePrompt=false}:new OpenFileDialog()){
   dialog.Filter=save?"Plik tekstowy|*.txt|Wszystkie pliki|*.*":"Pliki tekstowe|*.txt;*.csv;*.json;*.js;*.md|Wszystkie pliki|*.*";
   dialog.FileName=initial;dialog.InitialDirectory=folder;dialog.RestoreDirectory=true;dialog.CheckPathExists=true;
   return dialog.ShowDialog(owner)==DialogResult.OK?dialog.FileName:"";
  }
 }
 public virtual string AskText(IWin32Window owner,string title,string question,string initial){
  using(Form form=new Form{Text=title,ClientSize=new System.Drawing.Size(420,165),StartPosition=FormStartPosition.CenterParent,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false}){
   Label label=new Label{Text=question,Left=18,Top=18,Width=385,Height=45};TextBox input=new TextBox{Text=initial,Left=18,Top=70,Width=385};
   Button ok=new Button{Text="OK",DialogResult=DialogResult.OK,Left=220,Top=112,Width=85},cancel=new Button{Text="Anuluj",DialogResult=DialogResult.Cancel,Left=315,Top=112,Width=85};
   form.Controls.AddRange(new Control[]{label,input,ok,cancel});form.AcceptButton=ok;form.CancelButton=cancel;
   return form.ShowDialog(owner)==DialogResult.OK?input.Text:"";
  }
 }
}
public static class RuntimeFiles {
 public static string ProjectFolder(Project p){return p!=null&&!string.IsNullOrEmpty(p.SourcePath)?Path.GetDirectoryName(p.SourcePath):Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);}
 public static string CommandFolder(Project p,string folder){if(string.IsNullOrWhiteSpace(folder))return ProjectFolder(p);folder=Environment.ExpandEnvironmentVariables(folder.Trim());if(folder.Length>=2&&folder[0]=='"'&&folder[folder.Length-1]=='"')folder=folder.Substring(1,folder.Length-2);return Path.GetFullPath(Path.IsPathRooted(folder)?folder:Path.Combine(ProjectFolder(p),folder));}
 public static string Resolve(Project p,string path){
  path=Environment.ExpandEnvironmentVariables((path??"").Trim());
  if(path.Length>=2&&path[0]=='"'&&path[path.Length-1]=='"')path=path.Substring(1,path.Length-2);
  if(string.IsNullOrWhiteSpace(path))throw new IOException("Nie wybrano pliku. Podaj pełną ścieżkę, np. {pulpit}\\notatka.txt, albo użyj bloczka Wybierz plik do zapisu.");
  string full=Path.GetFullPath(Path.IsPathRooted(path)?path:Path.Combine(ProjectFolder(p),path));
  if(Directory.Exists(full)||full.EndsWith("\\")||full.EndsWith("/"))throw new IOException("To jest folder, a potrzebna jest nazwa pliku: "+full+"\nDopisz np. \\notatka.txt.");
  return full;
 }
 public static string ForWrite(Project p,string path){string full=Resolve(p,path);if(!Directory.Exists(Path.GetDirectoryName(full)))throw new DirectoryNotFoundException("Nie istnieje folder zapisu: "+Path.GetDirectoryName(full)+". Wybierz istniejący folder i nazwę pliku .txt.");return full;}
}
}
