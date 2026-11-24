using System;
using System.Linq;
using System.Windows.Forms;
using StereoRouter;
class VerifyGui {
 [STAThread] static int Main() {
  Application.EnableVisualStyles();
  using(var form=new MainForm()) {
   var text=String.Join(" | ",form.Controls.OfType<Label>().Select(x=>x.Text).ToArray());
   if(!text.Contains("Desktop audio will be captured automatically") || !text.Contains("install VB-CABLE")) throw new Exception("First-run guidance is missing");
   Console.WriteLine("Desktop audio UI and first-run setup message verified.");
  }
  return 0;
 }
}
