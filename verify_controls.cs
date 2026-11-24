using System;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StereoRouter;
class VerifyControls {
 [STAThread] static void Main() {
  Application.EnableVisualStyles();
  using(var form=new MainForm()) {
   form.ShowInTaskbar=false; form.Opacity=0; form.Show();
   var tracks=form.Controls.OfType<TrackBar>().ToArray();
   if(tracks.Length!=12 || tracks.Count(t=>t.Maximum==200)!=2 || tracks.Count(t=>t.Minimum==-12 && t.Maximum==12)!=10) throw new Exception("Expected volume and equalizer controls are missing");
   tracks[0].Value=200; tracks[1].Value=45; tracks[2].Value=6;
   var reset=form.Controls.OfType<Button>().Single(b=>b.Text=="Reset volumes + EQ");
   reset.PerformClick();
   if(tracks[0].Value!=100 || tracks[1].Value!=100 || tracks.Skip(2).Any(t=>t.Value!=0)) throw new Exception("Reset did not reset all controls");
   using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(0,0,image.Width,image.Height)); image.Save("ui-preview.png"); }
   Console.WriteLine("UI controls and reset passed.");
  }
 }
}
