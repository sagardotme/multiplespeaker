using System;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using StereoRouter;
class VerifySta {
 [STAThread] static int Main() {
  var devices=new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active).ToArray();
  var outputs=devices.Where(d=>d.FriendlyName.Contains("BenQ EW3270U")).ToArray();
  if(outputs.Length<2) { Console.WriteLine("Two BenQ outputs not found."); return 1; }
  foreach(var output in outputs.Take(2)) {
   string id=output.ID; Exception error=null; using(var done=new ManualResetEvent(false)) {
    ThreadPool.QueueUserWorkItem(delegate { try { MainForm.PlayTestTone(id); } catch(Exception e) { error=e; } finally { done.Set(); } });
    if(!done.WaitOne(5000)) { Console.WriteLine("Test timed out"); return 1; }
    if(error!=null) { Console.WriteLine("Test failed: "+error); return 1; }
    Console.WriteLine("Test tone succeeded: "+output.FriendlyName);
   }
  }
  var source=devices.FirstOrDefault(d=>d.FriendlyName.Contains("MAG 342"));
  if(source==null) { Console.WriteLine("Separate test source not found."); return 1; }
  using(var router=new Router()) {
   string error=null; router.Failed+=s=>error=s;
   router.Start(source,outputs[0],outputs[1]);
   using(var done=new ManualResetEvent(false)) {
    string id=source.ID; Exception playbackError=null;
    ThreadPool.QueueUserWorkItem(delegate { try { MainForm.PlayTestTone(id); } catch(Exception e) { playbackError=e; } finally { done.Set(); } });
    if(!done.WaitOne(5000) || playbackError!=null) { Console.WriteLine("Source playback failed: "+playbackError); return 1; }
   }
   Thread.Sleep(300);
   if(error!=null) { Console.WriteLine("Routing failed: "+error); return 1; }
   Console.WriteLine("Routing started successfully from an STA UI thread.");
  }
  return 0;
 }
}
