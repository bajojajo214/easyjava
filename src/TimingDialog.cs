using System;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
namespace EasyJava2 {
public sealed class TimingDialog:Form {
 public readonly NumericUpDown Seconds;
 public TimingDialog(double current,Action<double> apply){
  Text="Przerwy między bloczkami";ClientSize=new Size(510,320);StartPosition=FormStartPosition.CenterParent;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;MinimizeBox=false;BackColor=Ui.Panel;ForeColor=Ui.Text;Font=new Font("Segoe UI",10);Icon=Program.Icon;
  Label title=Ui.Label("Przerwa między bloczkami",16);title.SetBounds(22,18,465,36);Controls.Add(title);
  Label info=Ui.Label("Pierwszy bloczek rusza od razu.\nKolejne polecenia czekają wybrany czas.",10);info.ForeColor=Ui.Muted;info.SetBounds(24,64,464,48);Controls.Add(info);
  Seconds=new NumericUpDown{Minimum=0,Maximum=3600,DecimalPlaces=2,Increment=.1M,Value=Math.Round((decimal)current,2),BackColor=Ui.Bg,ForeColor=Ui.Text,Font=new Font("Segoe UI",13)};Seconds.SetBounds(24,123,155,34);Controls.Add(Seconds);
  Label unit=Ui.Label("sekund   (0 = bez przerwy)",10);unit.ForeColor=Ui.Muted;unit.SetBounds(191,130,280,29);Controls.Add(unit);
  FlowLayoutPanel presets=new FlowLayoutPanel{Location=new Point(23,173),Size=new Size(462,35),WrapContents=false};foreach(decimal value in new decimal[]{0,.1M,.5M,1,2,5}){decimal selected=value;Button preset=Ui.Button(value.ToString("0.#",CultureInfo.GetCultureInfo("pl-PL"))+" s",Ui.Raised);preset.Size=new Size(69,31);preset.Click+=delegate{Seconds.Value=selected;};presets.Controls.Add(preset);}Controls.Add(presets);
  Label note=Ui.Label("Ustawienie zapisuje się w .js i działa też poza edytorem.\nBloczek „Czekaj” dodaje do niego swój własny czas.",9);note.ForeColor=Ui.Muted;note.SetBounds(24,219,463,40);Controls.Add(note);
  Button cancel=Ui.Button("Anuluj",Ui.Raised);cancel.SetBounds(249,270,111,36);cancel.DialogResult=DialogResult.Cancel;CancelButton=cancel;Controls.Add(cancel);
  Button save=Ui.Button("Zastosuj",Ui.Purple);save.SetBounds(371,270,116,36);save.Click+=delegate{apply((double)Seconds.Value);DialogResult=DialogResult.OK;Close();};AcceptButton=save;Controls.Add(save);
 }
}
}
