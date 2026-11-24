using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using NAudio.CoreAudioApi;
using NAudio.Wave;

namespace StereoRouter {
  sealed class DeviceItem {
    public MMDevice Device;
    public override string ToString() { return Device.FriendlyName; }
  }

  sealed class Router : IDisposable {
    WasapiLoopbackCapture capture;
    WasapiOut leftOutput, rightOutput;
    BufferedWaveProvider input, leftBuffer, rightBuffer;
    ISampleProvider samples;
    AudioProcessor processor;
    int leftPercent=100, rightPercent=100;
    readonly int[] eqBands=new int[10];
    readonly object gate = new object();
    bool stopped;
    public event Action<string> Failed;

    public void Start(MMDevice source, MMDevice left, MMDevice right) {
      try {
        capture = new WasapiLoopbackCapture(source);
        WaveFormat format = capture.WaveFormat;
        if (format.Channels < 2) throw new Exception("The source must be configured for stereo audio in Windows Sound settings.");
        lock(gate) {
          processor=new AudioProcessor(format.SampleRate);
          processor.SetVolumes(leftPercent,rightPercent);
          for(int i=0;i<eqBands.Length;i++) processor.SetBand(i,eqBands[i]);
        }
        input = new BufferedWaveProvider(format) { BufferDuration = TimeSpan.FromSeconds(1), DiscardOnBufferOverflow = true };
        samples = input.ToSampleProvider();
        WaveFormat outputFormat = WaveFormat.CreateIeeeFloatWaveFormat(format.SampleRate, 2);
        leftBuffer = NewBuffer(outputFormat); rightBuffer = NewBuffer(outputFormat);
        leftOutput = new WasapiOut(left, AudioClientShareMode.Shared, false, 100);
        rightOutput = new WasapiOut(right, AudioClientShareMode.Shared, false, 100);
        leftOutput.Init(leftBuffer); rightOutput.Init(rightBuffer);
        capture.DataAvailable += OnData;
        capture.RecordingStopped += OnStopped;
        leftOutput.Play(); rightOutput.Play(); capture.StartRecording();
      } catch { Dispose(); throw; }
    }
    static BufferedWaveProvider NewBuffer(WaveFormat f) {
      return new BufferedWaveProvider(f) { BufferDuration = TimeSpan.FromMilliseconds(500), DiscardOnBufferOverflow = true };
    }
    void OnData(object sender, WaveInEventArgs e) {
      try {
        lock (gate) {
          if (stopped) return;
          input.AddSamples(e.Buffer, 0, e.BytesRecorded);
          int frames = e.BytesRecorded / capture.WaveFormat.BlockAlign;
          int channels = capture.WaveFormat.Channels;
          float[] data = new float[frames * channels];
          int read = samples.Read(data, 0, data.Length);
          int actualFrames = read / channels;
          byte[] l = new byte[actualFrames * 8], r = new byte[actualFrames * 8];
          processor.Process(data, channels, actualFrames, l, r);
          // Separate audio devices have independent clocks. Bound latency if one falls behind.
          if (leftBuffer.BufferedDuration.TotalMilliseconds > 300) leftBuffer.ClearBuffer();
          if (rightBuffer.BufferedDuration.TotalMilliseconds > 300) rightBuffer.ClearBuffer();
          leftBuffer.AddSamples(l, 0, l.Length); rightBuffer.AddSamples(r, 0, r.Length);
        }
      } catch (Exception ex) { if (Failed != null) Failed(ex.Message); }
    }
    public void SetAudioControls(int leftVolume,int rightVolume,int[] bands) {
      lock(gate) {
        leftPercent=leftVolume; rightPercent=rightVolume;
        for(int i=0;i<eqBands.Length;i++) eqBands[i]=bands[i];
        if(processor!=null) {
          processor.SetVolumes(leftPercent,rightPercent);
          for(int i=0;i<eqBands.Length;i++) processor.SetBand(i,eqBands[i]);
        }
      }
    }
    public static void Split(float[] data, int channels, int frames, byte[] left, byte[] right) {
      for (int i = 0; i < frames; i++) {
        byte[] l = BitConverter.GetBytes(data[i * channels]);
        byte[] r = BitConverter.GetBytes(data[i * channels + 1]);
        Buffer.BlockCopy(l, 0, left, i * 8, 4); Buffer.BlockCopy(l, 0, left, i * 8 + 4, 4);
        Buffer.BlockCopy(r, 0, right, i * 8, 4); Buffer.BlockCopy(r, 0, right, i * 8 + 4, 4);
      }
    }
    void OnStopped(object sender, StoppedEventArgs e) { if (!stopped && e.Exception != null && Failed != null) Failed(e.Exception.Message); }
    public void Dispose() {
      lock (gate) { stopped = true; }
      if (capture != null) { capture.DataAvailable -= OnData; capture.RecordingStopped -= OnStopped; capture.StopRecording(); capture.Dispose(); capture = null; }
      if (leftOutput != null) { leftOutput.Stop(); leftOutput.Dispose(); leftOutput = null; }
      if (rightOutput != null) { rightOutput.Stop(); rightOutput.Dispose(); rightOutput = null; }
    }
  }

  sealed class MainForm : Form {
    ComboBox source, left, right;
    ComboBox eqPreset;
    Button start, stop, refresh, testLeft, testRight;
    Label status;
    TrackBar leftVolume, rightVolume, combinedVolume;
    readonly TrackBar[] equalizer=new TrackBar[10];
    readonly Label[] equalizerValues=new Label[10];
    static readonly string[] PresetNames={"Custom", "Flat", "Clean / less bass", "Voice clarity", "Warm / gentle"};
    static readonly int[][] PresetBands={
      null,
      new[]{0,0,0,0,0,0,0,0,0,0},
      new[]{-6,-5,-4,-2,0,1,1,1,0,0},
      new[]{-5,-4,-3,-2,0,2,3,2,0,-1},
      new[]{-1,0,1,1,1,0,0,0,-1,-2}
    };
    Label leftVolumeValue, rightVolumeValue, combinedVolumeValue;
    System.Windows.Forms.Timer saveTimer;
    bool changingControls;
    Router router;
    readonly string config = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StereoRouter", "devices.txt");
    readonly string controlConfig = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "StereoRouter", "audio-controls.txt");
    public MainForm() {
      Text = "Stereo Router"; ClientSize = new Size(780, 820); MinimumSize = new Size(796, 859);
      StartPosition = FormStartPosition.CenterScreen; BackColor = Color.FromArgb(246,248,252); Font = new Font("Segoe UI", 10f);
      var heading = LabelAt("STEREO ROUTER", 26, 18, 700, 28, 17f, true); heading.ForeColor = Color.FromArgb(27,39,62);
      LabelAt("Desktop audio, routed to your left and right speakers.", 28, 53, 700, 25, 10f, false);
      source = ComboAt(30, 0, 1); source.Visible = false;
      LabelAt("LEFT SPEAKER", 30, 95, 260, 20, 9f, true);
      LabelAt("RIGHT SPEAKER", 400, 95, 260, 20, 9f, true);
      left = ComboAt(30, 120, 350); right = ComboAt(400, 120, 350);
      testLeft = ButtonAt("Test left", 30, 158, 142, 34); testRight = ButtonAt("Test right", 400, 158, 142, 34);
      testLeft.Click += delegate { Test(left); }; testRight.Click += delegate { Test(right); };
      LabelAt("LEFT VOLUME",30,212,190,20,9f,true); leftVolumeValue=LabelAt("100%",320,210,60,25,10f,true);
      LabelAt("RIGHT VOLUME",400,212,190,20,9f,true); rightVolumeValue=LabelAt("100%",690,210,60,25,10f,true);
      leftVolume=VolumeAt(25,235); rightVolume=VolumeAt(395,235);
      leftVolume.ValueChanged+=delegate { UpdateAudioControls(); }; rightVolume.ValueChanged+=delegate { UpdateAudioControls(); };
      LabelAt("COMBINED VOLUME  (SETS BOTH SPEAKERS)",30,295,450,20,9f,true);
      combinedVolumeValue=LabelAt("100%",670,293,80,25,10f,true); combinedVolumeValue.TextAlign=ContentAlignment.MiddleRight;
      combinedVolume=VolumeAt(25,318); combinedVolume.Width=725;
      combinedVolume.ValueChanged+=delegate { SetCombinedVolume(); };
      LabelAt("10-BAND GRAPHIC EQUALIZER",30,375,430,22,10f,true);
      LabelAt("PRESET",435,376,65,22,9f,true);
      eqPreset=ComboAt(500,370,250); eqPreset.Items.AddRange(PresetNames); eqPreset.SelectedIndex=0;
      eqPreset.SelectedIndexChanged+=delegate { ApplyPreset(); };
      string[] frequencies={"31","62","125","250","500","1k","2k","4k","8k","16k"};
      for(int i=0;i<10;i++) {
        int index=i, x=29+i*72;
        equalizerValues[i]=LabelAt("0 dB",x,406,61,22,9f,false); equalizerValues[i].TextAlign=ContentAlignment.MiddleCenter;
        equalizer[i]=new TrackBar { Left=x+8,Top=426,Width=45,Height=166,Orientation=Orientation.Vertical,Minimum=-12,Maximum=12,Value=0,TickFrequency=6,SmallChange=1,LargeChange=3,AutoSize=false };
        equalizer[i].ValueChanged+=delegate { UpdateAudioControls(); }; Controls.Add(equalizer[i]);
        var freq=LabelAt(frequencies[index]+" Hz",x,593,61,22,8.5f,true); freq.TextAlign=ContentAlignment.MiddleCenter;
      }
      var resetVolume=ButtonAt("Reset volume",30,630,135,36); resetVolume.Click+=delegate { ResetVolumes(); };
      var reset=ButtonAt("Reset volumes + EQ",175,630,185,36); reset.Click+=delegate { ResetControls(); };
      LabelAt("Above 100% may cause distortion.",385,638,365,22,9f,false);
      start = ButtonAt("Start stereo", 30, 684, 158, 42); start.BackColor = Color.FromArgb(38,91,205); start.ForeColor = Color.White; start.FlatStyle = FlatStyle.Flat;
      stop = ButtonAt("Stop", 198, 684, 105, 42); refresh = ButtonAt("Refresh devices", 569, 684, 181, 42);
      start.Click += delegate { StartRouting(); }; stop.Click += delegate { StopRouting(); }; refresh.Click += delegate { LoadDevices(true); };
      status = LabelAt("Ready", 30, 743, 720, 43, 10f, true); status.ForeColor = Color.FromArgb(26,100,77);
      var settings = new LinkLabel { Left = 30, Top = 794, Width = 210, Text = "Open Windows sound settings", AutoSize = true };
      settings.LinkClicked += delegate { System.Diagnostics.Process.Start("ms-settings:sound"); }; Controls.Add(settings);
      var help = new LinkLabel { Left = 305, Top = 794, Width = 320, Text = "Install VB-CABLE (one-time setup)", AutoSize = true };
      help.LinkClicked += delegate { System.Diagnostics.Process.Start("https://vb-audio.com/Cable/"); }; Controls.Add(help);
      try { DesktopSource.Restore(); } catch(Exception ex) { MessageBox.Show("Could not restore Windows audio output: "+ex.Message+"\nPlease check Windows sound settings."); }
      saveTimer=new System.Windows.Forms.Timer { Interval=500 }; saveTimer.Tick+=delegate { saveTimer.Stop(); SaveControls(); };
      LoadControls();
      FormClosing += delegate { saveTimer.Stop(); SaveControls(); StopRouting(); }; LoadDevices(false); SetRunning(false);
    }
    Label LabelAt(string t,int x,int y,int w,int h,float size,bool bold) { var v = new Label { Text=t, Left=x, Top=y, Width=w, Height=h, Font=new Font("Segoe UI",size,bold?FontStyle.Bold:FontStyle.Regular), ForeColor=Color.FromArgb(65,78,99) }; Controls.Add(v); return v; }
    ComboBox ComboAt(int x,int y,int w) { var c=new ComboBox { Left=x,Top=y,Width=w, DropDownStyle=ComboBoxStyle.DropDownList }; Controls.Add(c); return c; }
    TrackBar VolumeAt(int x,int y) { var t=new TrackBar { Left=x,Top=y,Width=350,Height=45,Minimum=0,Maximum=200,Value=100,TickFrequency=25,SmallChange=1,LargeChange=10,AutoSize=false }; Controls.Add(t); return t; }
    Button ButtonAt(string text,int x,int y,int w,int h) { var b=new Button { Text=text,Left=x,Top=y,Width=w,Height=h,Cursor=Cursors.Hand }; Controls.Add(b); return b; }
    int[] CurrentBands() { return equalizer.Select(t=>t.Value).ToArray(); }
    void UpdateAudioControls() {
      if(changingControls || equalizer.Any(t=>t==null)) return;
      leftVolumeValue.Text=leftVolume.Value+"%"; rightVolumeValue.Text=rightVolume.Value+"%";
      int average=(leftVolume.Value+rightVolume.Value)/2;
      changingControls=true; combinedVolume.Value=average; changingControls=false;
      combinedVolumeValue.Text=average+"%"+(leftVolume.Value==rightVolume.Value?"":" avg");
      for(int i=0;i<10;i++) equalizerValues[i].Text=(equalizer[i].Value>0?"+":"")+equalizer[i].Value+" dB";
      int presetIndex=0; int[] current=CurrentBands();
      for(int i=1;i<PresetBands.Length;i++) if(current.SequenceEqual(PresetBands[i])) { presetIndex=i; break; }
      changingControls=true; eqPreset.SelectedIndex=presetIndex; changingControls=false;
      if(router!=null) router.SetAudioControls(leftVolume.Value,rightVolume.Value,CurrentBands());
      if(saveTimer!=null) { saveTimer.Stop(); saveTimer.Start(); }
    }
    void SetCombinedVolume() {
      if(changingControls) return;
      int value=combinedVolume.Value;
      changingControls=true; leftVolume.Value=value; rightVolume.Value=value; changingControls=false;
      UpdateAudioControls();
    }
    void ApplyPreset() {
      if(changingControls || eqPreset.SelectedIndex<=0) return;
      int[] values=PresetBands[eqPreset.SelectedIndex];
      changingControls=true;
      for(int i=0;i<equalizer.Length;i++) equalizer[i].Value=values[i];
      changingControls=false; UpdateAudioControls();
    }
    void ResetVolumes() {
      changingControls=true; leftVolume.Value=100; rightVolume.Value=100; changingControls=false;
      UpdateAudioControls();
    }
    void ResetControls() {
      changingControls=true; leftVolume.Value=100; rightVolume.Value=100;
      foreach(var band in equalizer) band.Value=0;
      changingControls=false; UpdateAudioControls();
    }
    void LoadControls() {
      try {
        if(File.Exists(controlConfig)) {
          string[] saved=File.ReadAllLines(controlConfig);
          if(saved.Length==12) {
            int[] v=saved.Select(s=>int.Parse(s)).ToArray();
            if(v[0]>=0 && v[0]<=200 && v[1]>=0 && v[1]<=200 && v.Skip(2).All(n=>n>=-12 && n<=12)) {
              changingControls=true; leftVolume.Value=v[0]; rightVolume.Value=v[1];
              for(int i=0;i<10;i++) equalizer[i].Value=v[i+2]; changingControls=false;
            }
          }
        }
      } catch { changingControls=false; }
      UpdateAudioControls();
    }
    void SaveControls() {
      try { Directory.CreateDirectory(Path.GetDirectoryName(controlConfig)); File.WriteAllLines(controlConfig,new[]{leftVolume.Value,rightVolume.Value}.Concat(CurrentBands()).Select(v=>v.ToString()).ToArray()); } catch {}
    }
    string Id(ComboBox c) { DeviceItem i=c.SelectedItem as DeviceItem; return i==null?"":i.Device.ID; }
    void SelectId(ComboBox c,string id) { for(int i=0;i<c.Items.Count;i++) if(((DeviceItem)c.Items[i]).Device.ID==id) { c.SelectedIndex=i; return; } }
    void LoadDevices(bool preserve) {
      string[] ids = new string[3];
      if (preserve) { ids[0]=Id(source); ids[1]=Id(left); ids[2]=Id(right); }
      else try { if (File.Exists(config)) { string[] saved=File.ReadAllLines(config); for(int i=0;i<Math.Min(3,saved.Length);i++) ids[i]=saved[i]; } } catch {}
      try {
        var devices=new MMDeviceEnumerator().EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active).ToArray();
        foreach (ComboBox box in new[]{source,left,right}) { box.Items.Clear(); foreach (MMDevice d in devices) box.Items.Add(new DeviceItem { Device=d }); }
        SelectId(left,ids[1]); SelectId(right,ids[2]);
        for(int i=0;i<source.Items.Count;i++) if(source.Items[i].ToString().IndexOf("CABLE Input",StringComparison.OrdinalIgnoreCase)>=0 && source.Items[i].ToString().IndexOf("VB-Audio",StringComparison.OrdinalIgnoreCase)>=0) { source.SelectedIndex=i; break; }
        int[] benq=Enumerable.Range(0,devices.Length).Where(i=>devices[i].FriendlyName.IndexOf("BenQ",StringComparison.OrdinalIgnoreCase)>=0).ToArray();
        if(left.SelectedIndex<0 && benq.Length>=2) left.SelectedIndex=benq[0];
        if(right.SelectedIndex<0 && benq.Length>=2) right.SelectedIndex=benq[1];
        status.Text=source.SelectedItem==null ? "One-time setup needed: install VB-CABLE, then refresh devices." : "Ready. Use Test left / Test right to identify your speakers.";
      } catch(Exception ex) { MessageBox.Show("Could not list audio devices: "+ex.Message); }
    }
    void SetRunning(bool running) { start.Enabled=!running; stop.Enabled=running; refresh.Enabled=!running; source.Enabled=!running; left.Enabled=!running; right.Enabled=!running; testLeft.Enabled=!running; testRight.Enabled=!running; }
    void StartRouting() {
      if(source.SelectedItem==null) { MessageBox.Show("Desktop audio needs the VB-CABLE driver once. Click the install link below, install it, restart Windows if prompted, then click Refresh devices. Stereo Router will select it automatically.","One-time setup"); return; }
      if(left.SelectedItem==null || right.SelectedItem==null) { MessageBox.Show("Please choose a left speaker and a right speaker.","Select devices"); return; }
      if(Id(source)==Id(left) || Id(source)==Id(right) || Id(left)==Id(right)) { MessageBox.Show("Choose three different devices. The source must be separate to prevent feedback.","Select different devices"); return; }
      try {
        router=new Router(); router.Failed += delegate(string message) { try { BeginInvoke((Action)delegate { StopRouting(); MessageBox.Show("Audio stopped: "+message); }); } catch {} };
        router.SetAudioControls(leftVolume.Value,rightVolume.Value,CurrentBands());
        router.Start(((DeviceItem)source.SelectedItem).Device,((DeviceItem)left.SelectedItem).Device,((DeviceItem)right.SelectedItem).Device);
        DesktopSource.Activate(Id(source),Id(left));
        try { Directory.CreateDirectory(Path.GetDirectoryName(config)); File.WriteAllLines(config,new[]{Id(source),Id(left),Id(right)}); } catch {}
        SetRunning(true); status.Text="Playing desktop audio through your left and right speakers.";
      } catch(Exception ex) { if(router!=null) { router.Dispose(); router=null; } MessageBox.Show("Could not start audio: "+ex.Message,"Stereo Router"); }
    }
    void StopRouting() {
      if(router!=null) {
        try { DesktopSource.Restore(); } catch(Exception ex) { MessageBox.Show("Could not restore Windows audio output: "+ex.Message+"\nPlease check Windows sound settings."); }
        router.Dispose(); router=null;
      }
      SetRunning(false); if(status!=null) status.Text="Stopped.";
    }
    void Test(ComboBox box) {
      DeviceItem item=box.SelectedItem as DeviceItem; if(item==null) { MessageBox.Show("Choose a speaker first."); return; }
      string deviceId=item.Device.ID;
      testLeft.Enabled=false; testRight.Enabled=false; start.Enabled=false; refresh.Enabled=false; source.Enabled=false; left.Enabled=false; right.Enabled=false; status.Text="Playing a short test tone...";
      ThreadPool.QueueUserWorkItem(delegate {
        try {
          PlayTestTone(deviceId);
          BeginInvoke((Action)delegate { SetRunning(false); status.Text="Test finished. Swap the selections if the sound came from the wrong side."; });
        } catch(Exception ex) { try { BeginInvoke((Action)delegate { SetRunning(false); status.Text="Test failed."; MessageBox.Show("Could not play test: "+ex.Message); }); } catch {} }
      });
    }
    internal static void PlayTestTone(string deviceId) {
      // MMDevice wraps a COM interface bound to the thread on which it was created.
      // Look it up on this worker thread, instead of sharing the UI's device object.
      using(var enumerator=new MMDeviceEnumerator())
      using(var device=enumerator.GetDevice(deviceId))
      using(var playback=new WasapiOut(device,AudioClientShareMode.Shared,false,100)) {
        int rate=48000, frames=rate*3/4; float[] values=new float[frames*2];
        for(int i=0;i<frames;i++) { float envelope=(float)Math.Min(1.0,Math.Min(i/1200.0,(frames-i)/1200.0)); float v=(float)(0.035*envelope*Math.Sin(2*Math.PI*440*i/rate)); values[i*2]=v; values[i*2+1]=v; }
        byte[] bytes=new byte[values.Length*4]; Buffer.BlockCopy(values,0,bytes,0,bytes.Length);
        var buf=new BufferedWaveProvider(WaveFormat.CreateIeeeFloatWaveFormat(rate,2)); buf.AddSamples(bytes,0,bytes.Length); playback.Init(buf); playback.Play(); Thread.Sleep(1100); playback.Stop();
      }
    }
  }
  static class Program {
    [STAThread] static void Main(string[] args) {
      if(args.Length>0 && args[0]=="--self-test") { float[] samples={.25f,-.5f,.75f,-1f}; byte[] l=new byte[16],r=new byte[16]; Router.Split(samples,2,2,l,r); if(BitConverter.ToSingle(l,0)!=.25f || BitConverter.ToSingle(l,4)!=.25f || BitConverter.ToSingle(l,8)!=.75f || BitConverter.ToSingle(r,0)!=-.5f || BitConverter.ToSingle(r,12)!=-1f) Environment.Exit(1); return; }
      bool created;
      using(var mutex=new Mutex(true,"Local\\StereoRouterDesktopAudio",out created)) {
        if(!created) { MessageBox.Show("Stereo Router is already open."); return; }
        Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false); Application.Run(new MainForm());
      }
    }
  }
}
