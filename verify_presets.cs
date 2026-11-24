using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using StereoRouter;
class VerifyPresets {
 static T Field<T>(MainForm f,string name) { return (T)typeof(MainForm).GetField(name,BindingFlags.Instance|BindingFlags.NonPublic).GetValue(f); }
 static void Check(bool ok,string error) { if(!ok) throw new Exception(error); }
 [STAThread] static void Main() {
  var path=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StereoRouter","audio-controls.txt");
  bool exists=File.Exists(path); string original=exists?File.ReadAllText(path):null;
  Application.EnableVisualStyles();
  try {
   using(var f=new MainForm()) {
    f.ShowInTaskbar=false; f.Opacity=0; f.Show();
    var preset=Field<ComboBox>(f,"eqPreset"); var bands=Field<TrackBar[]>(f,"equalizer");
    var left=Field<TrackBar>(f,"leftVolume"); var right=Field<TrackBar>(f,"rightVolume");
    left.Value=91; right.Value=83;
    preset.SelectedIndex=2;
    Check(bands.Select(b=>b.Value).SequenceEqual(new[]{-6,-5,-4,-2,0,1,1,1,0,0}),"Clean preset values wrong");
    Check(left.Value==91 && right.Value==83,"Preset changed volume");
    bands[0].Value=-7; Check(preset.SelectedIndex==0,"Manual EQ adjustment did not switch to Custom");
    preset.SelectedIndex=2; f.Close();
   }
   using(var f=new MainForm()) {
    Check(Field<ComboBox>(f,"eqPreset").SelectedIndex==2,"Preset was not restored");
    Console.WriteLine("Clean preset, custom detection, volume preservation and persistence passed.");
   }
  } finally { if(exists) File.WriteAllText(path,original); else if(File.Exists(path)) File.Delete(path); }
 }
}
