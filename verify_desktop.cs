using System;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using StereoRouter;
class VerifyDesktop {
 [STAThread] static int Main() {
  var roles=new[]{Role.Console,Role.Multimedia,Role.Communications};
  var enumerator=new MMDeviceEnumerator();
  var before=roles.Select(r=>enumerator.GetDefaultAudioEndpoint(DataFlow.Render,r).ID).ToArray();
  var alternative=enumerator.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active).First(d=>!before.Contains(d.ID));
  try {
   DesktopSource.Activate(alternative.ID,before[0]);
   foreach(var role in roles) if(enumerator.GetDefaultAudioEndpoint(DataFlow.Render,role).ID!=alternative.ID) throw new Exception("Did not switch role "+role);
   Console.WriteLine("All three desktop output roles switched successfully.");
  } finally { DesktopSource.Restore(); }
  for(int i=0;i<roles.Length;i++) if(enumerator.GetDefaultAudioEndpoint(DataFlow.Render,roles[i]).ID!=before[i]) throw new Exception("Did not restore role "+roles[i]);
  Console.WriteLine("Original output roles restored successfully.");
  return 0;
 }
}
