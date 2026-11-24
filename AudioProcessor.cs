using System;

namespace StereoRouter {
  // Ten independent peaking filters for each stereo channel. Caller serializes
  // changes with audio processing so coefficients and filter state stay in sync.
  internal sealed class AudioProcessor {
    internal static readonly double[] Frequencies={31,62,125,250,500,1000,2000,4000,8000,16000};
    readonly PeakFilter[,] filters=new PeakFilter[2,10];
    readonly int[] bandDb=new int[10];
    readonly int sampleRate;
    float leftGain=1, rightGain=1;
    internal AudioProcessor(int sampleRate) {
      this.sampleRate=sampleRate;
      for(int channel=0;channel<2;channel++) for(int i=0;i<10;i++) filters[channel,i]=new PeakFilter();
    }
    internal void SetVolumes(int leftPercent,int rightPercent) {
      leftGain=Math.Max(0,Math.Min(200,leftPercent))/100f;
      rightGain=Math.Max(0,Math.Min(200,rightPercent))/100f;
    }
    internal void SetBand(int index,int db) {
      if(index<0 || index>=10) throw new ArgumentOutOfRangeException("index");
      db=Math.Max(-12,Math.Min(12,db));
      if(bandDb[index]==db) return;
      bandDb[index]=db;
      for(int channel=0;channel<2;channel++) filters[channel,index].Configure(sampleRate,Frequencies[index],db);
    }
    internal void Process(float[] input,int channels,int frames,byte[] left,byte[] right) {
      float[] l=new float[frames*2],r=new float[frames*2];
      for(int frame=0;frame<frames;frame++) {
        double lv=input[frame*channels],rv=input[frame*channels+1];
        for(int band=0;band<10;band++) if(bandDb[band]!=0) {
          lv=filters[0,band].Process(lv); rv=filters[1,band].Process(rv);
        }
        float lout=Clamp(lv*leftGain),rout=Clamp(rv*rightGain);
        l[frame*2]=lout; l[frame*2+1]=lout; r[frame*2]=rout; r[frame*2+1]=rout;
      }
      Buffer.BlockCopy(l,0,left,0,left.Length); Buffer.BlockCopy(r,0,right,0,right.Length);
    }
    static float Clamp(double value) { if(double.IsNaN(value)) return 0; return (float)Math.Max(-1.0,Math.Min(1.0,value)); }
  }
  internal sealed class PeakFilter {
    double b0,b1,b2,a1,a2,z1,z2;
    internal void Configure(int sampleRate,double frequency,int gainDb) {
      z1=z2=0;
      if(frequency>=sampleRate*0.49 || gainDb==0) { b0=1; b1=b2=a1=a2=0; return; }
      double a=Math.Pow(10,gainDb/40.0), omega=2*Math.PI*frequency/sampleRate;
      double alpha=Math.Sin(omega)/(2*1.41421356237), cos=Math.Cos(omega), a0=1+alpha/a;
      b0=(1+alpha*a)/a0; b1=-2*cos/a0; b2=(1-alpha*a)/a0;
      a1=-2*cos/a0; a2=(1-alpha/a)/a0;
    }
    internal double Process(double x) {
      double y=b0*x+z1; z1=b1*x-a1*y+z2; z2=b2*x-a2*y; return y;
    }
  }
}
