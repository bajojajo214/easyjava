using System.Drawing;
using System.Windows.Forms;
namespace EasyJava2 {
public sealed class DragGhost:Form {
 public DragGhost(string title,Color accent){FormBorderStyle=FormBorderStyle.None;ShowInTaskbar=false;StartPosition=FormStartPosition.Manual;TopMost=true;Size=new Size(265,49);Opacity=.88;BackColor=accent;Label text=new Label{Text="⠿  "+title,ForeColor=Color.White,Font=new Font("Segoe UI",11,FontStyle.Bold),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,Padding=new Padding(12,0,8,0)};Controls.Add(text);}
 protected override bool ShowWithoutActivation{get{return true;}}
 protected override CreateParams CreateParams{get{CreateParams p=base.CreateParams;p.ExStyle|=0x08000000|0x00000020|0x00000080;return p;}}
 public void FollowCursor(){Point p=Cursor.Position;Location=new Point(p.X+18,p.Y+16);}
}
}
