using System;
using System.Linq;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using StereoRouter;
class Verify {
 static double[] Capture(MMDevice d, Action play) {
  double[] energy=new double[4]; long frame=0;
  using(var cap=new WasapiLoopbackCapture(d)) {
   var buf=new BufferedWaveProvider(cap.WaveFormat) { DiscardOnBufferOverflow=true, BufferDuration=TimeSpan.FromSeconds(3) };
   var samples=buf.ToSampleProvider(); int channels=cap.WaveFormat.Channels;
   cap.DataAvailable+=(s,e)=>{ buf.AddSamples(e.Buffer,0,e.BytesRecorded); float[] f=new float[(e.BytesRecorded/cap.WaveFormat.BlockAlign)*channels]; int n=samples.Read(f,0,f.Length); for(int i=0;i+1<n;i+=channels){ double t=2*Math.PI*frame++/cap.WaveFormat.SampleRate; energy[0]+=f[i]*Math.Sin(330*t); energy[1]+=f[i]*Math.Cos(330*t); energy[2]+=f[i]*Math.Sin(660*t); energy[3]+=f[i]*Math.Cos(660*t); } };
   cap.StartRecording(); play(); cap.StopRecording(); Thread.Sleep(300);
  } return energy;
 }
 static int Main() {
  var e=new MMDeviceEnumerator(); var ds=e.EnumerateAudioEndPoints(DataFlow.Render,DeviceState.Active).ToArray();
  var monitors=ds.Where(d=>d.FriendlyName.Contains("BenQ EW3270U")).ToArray(); var source=ds.FirstOrDefault(d=>d.FriendlyName.Contains("CABLE Input"));
  if(monitors.Length<2||source==null) { Console.WriteLine("Devices missing"); return 1; }
  using(var router=new Router()) {
   string error=null; router.Failed+=s=>error=s; router.Start(source,monitors[0],monitors[1]);
   int rate=48000; var format=new WaveFormat(rate,16,2); short[] v=new short[rate*2*2];
   for(int i=0;i<rate*2;i++){ v[2*i]=(short)(655*Math.Sin(2*Math.PI*330*i/rate)); v[2*i+1]=(short)(1310*Math.Sin(2*Math.PI*660*i/rate)); }
   byte[] bytes=new byte[v.Length*2]; Buffer.BlockCopy(v,0,bytes,0,bytes.Length);
   double[] a=null,b=null;
   int dev=-1; for(int i=0;i<WaveOut.DeviceCount;i++) if(WaveOut.GetCapabilities(i).ProductName.StartsWith("CABLE Input")) dev=i;
   if(dev<0) throw new Exception("Cable playback device not found");
   b=Capture(monitors[1],()=>{ a=Capture(monitors[0],()=>{ using(var o=new WaveOutEvent {DeviceNumber=dev}) { var buf=new BufferedWaveProvider(format){BufferDuration=TimeSpan.FromSeconds(3)};buf.AddSamples(bytes,0,bytes.Length);o.Init(buf);o.Play();Thread.Sleep(2600);o.Stop(); } }); });
   double al=Math.Sqrt(a[0]*a[0]+a[1]*a[1]), ar=Math.Sqrt(a[2]*a[2]+a[3]*a[3]);
   double bl=Math.Sqrt(b[0]*b[0]+b[1]*b[1]), br=Math.Sqrt(b[2]*b[2]+b[3]*b[3]);
   Console.WriteLine("Left monitor: left frequency {0:F3}, right frequency {1:F3}; right monitor: left frequency {2:F3}, right frequency {3:F3}; error: {4}",al,ar,bl,br,error??"none");
   if(error!=null || al<1 || br<1 || al<ar*3 || br<bl*3) return 1;
  } return 0;
 }
}

