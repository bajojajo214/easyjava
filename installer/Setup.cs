using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

[assembly:AssemblyTitle("Instalator EasyJava")]
[assembly:AssemblyProduct("EasyJava")]
[assembly:AssemblyVersion("2.0.12.0")]
[assembly:AssemblyFileVersion("2.0.12.0")]
public static class Setup {
 public static Icon AppIcon;
 [DllImport("user32.dll")]static extern bool SetProcessDPIAware();
 public static string DefaultFolder{get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"EasyJava");}}
 public static string Hash(string file){using(SHA256 hash=SHA256.Create())using(FileStream stream=File.OpenRead(file))return BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();}
 public static string Extract(string path){string full=Path.GetFullPath(path);Directory.CreateDirectory(Path.GetDirectoryName(full));string temp=full+"."+Guid.NewGuid().ToString("N")+".tmp";try{using(Stream resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("EasyJava.Setup.Package"))using(FileStream output=new FileStream(temp,FileMode.CreateNew)){if(resource==null)throw new Exception("Brak pakietu aplikacji w instalatorze.");resource.CopyTo(output);}if(Hash(temp)!=PackageInfo.Sha256)throw new Exception("Pakiet instalacyjny jest uszkodzony. Pobierz instalator ponownie.");if(File.Exists(full))File.Delete(full);File.Move(temp,full);return full;}finally{if(File.Exists(temp))File.Delete(temp);}}
 [STAThread]public static int Main(string[] args){try{SetProcessDPIAware();Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);AppIcon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
  if(args.Length==2&&args[0]=="--extract-msi"){Extract(args[1]);return 0;}
  if(args.Length==2&&args[0]=="--snapshot"){using(SetupForm form=new SetupForm()){form.Show();Application.DoEvents();using(Bitmap bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(args[1]);}form.Close();}return 0;}
  using(SetupForm form=new SetupForm())Application.Run(form);return 0;
 }catch(Exception ex){if(args.Length>0){try{File.WriteAllText(Path.Combine(Path.GetTempPath(),"easyjava-setup-error.txt"),ex.ToString());}catch{}}else MessageBox.Show(ex.Message,"Instalator EasyJava",MessageBoxButtons.OK,MessageBoxIcon.Error);return 1;}}
}
public sealed class SetupForm:Form {
 readonly Color bg=Color.FromArgb(17,23,37),panel=Color.FromArgb(29,36,54),purple=Color.FromArgb(133,105,239),muted=Color.FromArgb(163,176,201);
 TextBox folder;Button install,browse,close;Label state;ProgressBar progress;CheckBox launch;bool busy,complete;
 public SetupForm(){Text="Instalator EasyJava 2.0.12";Icon=Setup.AppIcon;Font=new Font("Segoe UI",10);AutoScaleMode=AutoScaleMode.Dpi;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=true;StartPosition=FormStartPosition.CenterScreen;ClientSize=new Size(650,510);BackColor=bg;ForeColor=Color.White;
  Panel header=new Panel{BackColor=panel,Location=new Point(0,0),Size=new Size(650,122)};Controls.Add(header);PictureBox logo=new PictureBox{Location=new Point(29,32),Size=new Size(58,58),SizeMode=PictureBoxSizeMode.Zoom,Image=Setup.AppIcon.ToBitmap()};header.Controls.Add(logo);Label title=Label("Zainstaluj EasyJava",25,99,23,520,48);title.Font=new Font("Segoe UI",25,FontStyle.Bold);header.Controls.Add(title);Label sub=Label("Twoje pomysły. Twoje bloczki. Twoje aplikacje.",10,103,78,510,26);sub.ForeColor=muted;header.Controls.Add(sub);
  Controls.Add(Label("Wszystko gotowe do tworzenia",16,28,143,590,35));Label info=Label("Instalator skopiuje aplikację i przykłady oraz utworzy skrót\nEasyJava na pulpicie i w menu Start.",10,30,190,590,53);info.ForeColor=muted;Controls.Add(info);
  Controls.Add(Label("Folder aplikacji",10,30,263,570,24));folder=new TextBox{Text=Setup.DefaultFolder,Location=new Point(30,294),Size=new Size(478,29),BackColor=panel,ForeColor=Color.White,BorderStyle=BorderStyle.FixedSingle};Controls.Add(folder);browse=Button("Zmień…",519,291,100,33,panel);browse.Click+=delegate{using(FolderBrowserDialog dialog=new FolderBrowserDialog{Description="Wybierz folder dla EasyJava",SelectedPath=folder.Text,ShowNewFolderButton=true})if(dialog.ShowDialog(this)==DialogResult.OK)folder.Text=Path.Combine(dialog.SelectedPath,"EasyJava");};Controls.Add(browse);
  Label rights=Label("Zapis do Program Files wymaga potwierdzenia w oknie Windows.",9,31,335,588,28);rights.ForeColor=muted;Controls.Add(rights);
  state=Label("EasyJava 2.0.12 • Windows 64-bit • instalacja offline",9,30,380,590,29);state.ForeColor=muted;Controls.Add(state);progress=new ProgressBar{Location=new Point(30,416),Size=new Size(590,5),Visible=false,Style=ProgressBarStyle.Marquee};Controls.Add(progress);
  launch=new CheckBox{Text="Uruchom EasyJava",Checked=true,Location=new Point(31,437),Size=new Size(255,30),Visible=false,ForeColor=Color.White};Controls.Add(launch);close=Button("Anuluj",358,441,108,40,panel);close.Click+=delegate{Close();};Controls.Add(close);install=Button("Zainstaluj",478,441,143,40,purple);install.Click+=async delegate{if(complete){if(launch.Checked)Process.Start(new ProcessStartInfo(Path.Combine(folder.Text,"easyjava.exe")){UseShellExecute=true,WorkingDirectory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)});Close();return;}await Install();};Controls.Add(install);AcceptButton=install;CancelButton=close;FormClosing+=delegate(object sender,FormClosingEventArgs e){if(busy)e.Cancel=true;};}
 Label Label(string text,int size,int x,int y,int width,int height){return new Label{Text=text,Font=new Font("Segoe UI",size),ForeColor=Color.White,Location=new Point(x,y),Size=new Size(width,height)};}
 Button Button(string text,int x,int y,int width,int height,Color color){Button b=new Button{Text=text,Location=new Point(x,y),Size=new Size(width,height),BackColor=color,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Cursor=Cursors.Hand};b.FlatAppearance.BorderSize=0;return b;}
 async Task Install(){string target;try{target=Path.GetFullPath(folder.Text.Trim());if(!Environment.Is64BitOperatingSystem)throw new Exception("Ta wersja EasyJava wymaga 64-bitowego Windows.");if(string.IsNullOrWhiteSpace(folder.Text)||target==Path.GetPathRoot(target))throw new Exception("Wybierz folder aplikacji, np. "+Setup.DefaultFolder);if(target.Contains("\""))throw new Exception("Nieprawidłowa ścieżka folderu.");}catch(Exception ex){MessageBox.Show(this,ex.Message,"Folder instalacji",MessageBoxButtons.OK,MessageBoxIcon.Information);return;}
  busy=true;install.Enabled=browse.Enabled=close.Enabled=folder.Enabled=false;progress.Visible=true;state.Text="Trwa instalacja. Postęp pojawi się w oknie Instalatora Windows…";
  try{string cache=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"EasyJava","Instalator","2.0.12");Directory.CreateDirectory(cache);string msi=Setup.Extract(Path.Combine(cache,"EasyJava.msi"));string log=Path.Combine(cache,"instalacja-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".log");using(Process process=Process.Start(new ProcessStartInfo(Path.Combine(Environment.SystemDirectory,"msiexec.exe"),"/i \""+msi+"\" /qb /norestart ALLUSERS=1 INSTALLDIR=\""+target+"\" /L*V \""+log+"\""){UseShellExecute=true})){await Task.Run(()=>process.WaitForExit());int code=process.ExitCode;if(code==1602){state.Text="Instalacja została anulowana.";return;}if(code!=0&&code!=3010){if(code==1618)throw new Exception("Inna instalacja Windows jest aktywna. Poczekaj na jej zakończenie i spróbuj ponownie.");throw new Exception("Instalator Windows zakończył się kodem "+code+".\nSzczegóły: "+log);}if(!File.Exists(Path.Combine(target,"easyjava.exe")))throw new Exception("Nie znaleziono zainstalowanej aplikacji. Sprawdź dziennik: "+log);folder.Text=target;complete=true;launch.Visible=true;install.Text="Gotowe";close.Visible=false;state.Text=code==3010?"Zainstalowano EasyJava. Windows zaleca ponowne uruchomienie.":"Gotowe! Skrót EasyJava jest na pulpicie.";}}
  catch(Exception ex){state.Text="Instalacja nie została ukończona.";MessageBox.Show(this,ex.Message,"Instalator EasyJava",MessageBoxButtons.OK,MessageBoxIcon.Error);}
  finally{busy=false;progress.Visible=false;install.Enabled=true;if(!complete)browse.Enabled=close.Enabled=folder.Enabled=true;}
 }
}
