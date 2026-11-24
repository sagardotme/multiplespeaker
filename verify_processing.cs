using System;
using StereoRouter;
class VerifyProcessing {
 static void Check(bool condition,string message) { if(!condition) throw new Exception(message); }
 static double Rms(byte[] data) { double sum=0; int frames=data.Length/8; for(int i=frames/4;i<frames;i++){ double x=BitConverter.ToSingle(data,i*8); sum+=x*x; } return Math.Sqrt(sum/(frames-frames/4)); }
 static void Main() {
  int rate=48000, frames=rate; float[] input=new float[frames*2];
  for(int i=0;i<frames;i++) { input[2*i]=(float)(0.04*Math.Sin(2*Math.PI*1000*i/rate)); input[2*i+1]=(float)(0.02*Math.Sin(2*Math.PI*1000*i/rate)); }
  var processor=new AudioProcessor(rate); byte[] left=new byte[frames*8],right=new byte[frames*8];
  processor.Process(input,2,frames,left,right); double flatL=Rms(left),flatR=Rms(right);
  Check(Math.Abs(flatL/flatR-2)<0.001,"Stereo channel separation failed");
  processor.SetVolumes(200,50); processor.Process(input,2,frames,left,right);
  Check(Math.Abs(Rms(left)/flatL-2)<0.001 && Math.Abs(Rms(right)/flatR-.5)<0.001,"Independent volume failed");
  processor.SetVolumes(100,100); processor.SetBand(5,6); processor.Process(input,2,frames,left,right);
  Check(Math.Abs(Rms(left)/flatL-Math.Pow(10,6.0/20))<0.01,"1 kHz equalizer boost failed");
  processor.SetBand(5,-6); processor.Process(input,2,frames,left,right);
  Check(Math.Abs(Rms(left)/flatL-Math.Pow(10,-6.0/20))<0.01,"1 kHz equalizer cut failed");
  processor.SetBand(5,0); processor.SetVolumes(100,100); processor.Process(input,2,frames,left,right);
  Check(Math.Abs(Rms(left)/flatL-1)<0.001,"Reset to flat failed");
  processor.SetVolumes(200,200); float[] peak={0.8f,-0.8f}; byte[] l=new byte[8],r=new byte[8]; processor.Process(peak,2,1,l,r);
  Check(BitConverter.ToSingle(l,0)==1f && BitConverter.ToSingle(r,0)==-1f,"Output clipping bounds failed");
  Console.WriteLine("Independent gain, 200% boost, stereo separation, EQ boost/cut, reset and output bounds passed.");
 }
}
