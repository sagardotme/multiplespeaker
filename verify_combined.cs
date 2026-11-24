using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StereoRouter;
class VerifyCombined {
 static TrackBar Slider(MainForm f,string name) { return (TrackBar)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f); }
 static void Check(bool ok,string message) { if(!ok) throw new Exception(message); }
 [STAThread] static void Main() {
  var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StereoRouter","audio-controls.txt");
  bool existed=File.Exists(path); string original=existed?File.ReadAllText(path):null;
  Application.EnableVisualStyles();
  try {
   using(var form=new MainForm()) {
    form.ShowInTaskbar=false; form.Opacity=0; form.Show();
    var left=Slider(form,"leftVolume"); var right=Slider(form,"rightVolume"); var combined=Slider(form,"combinedVolume");
    combined.Value=137;
    Check(left.Value==137 && right.Value==137,"Combined slider did not set both outputs.");
    left.Value=40; right.Value=80;
    Check(combined.Value==60,"Combined value did not follow the average.");
    var eq=(TrackBar[])typeof(MainForm).GetField("equalizer",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
    eq[5].Value=6;
    form.Controls.OfType<Button>().Single(b=>b.Text=="Reset volume").PerformClick();
    Check(left.Value==100 && right.Value==100 && combined.Value==100 && eq[5].Value==6,"Volume reset changed EQ or did not reset volumes.");
    combined.Value=137;
    using(var bitmap=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height)); bitmap.Save("combined-preview.png"); }
    form.Close();
   }
   using(var restored=new MainForm()) {
    Check(Slider(restored,"leftVolume").Value==137 && Slider(restored,"rightVolume").Value==137 && Slider(restored,"combinedVolume").Value==137,"Saved volume was not restored.");
    Console.WriteLine("Combined adjustment, individual average and saved settings passed.");
   }
  } finally { if(existed) File.WriteAllText(path,original); else if(File.Exists(path)) File.Delete(path); }
 }
}
