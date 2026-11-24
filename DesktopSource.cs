using System;
using System.IO;
using System.Runtime.InteropServices;
using NAudio.CoreAudioApi;

namespace StereoRouter {
  [ComImport, Guid("870af99c-171d-4f9e-af0d-e63df40c2bc9")]
  internal class PolicyConfigClient { }
  [ComImport, Guid("f8679f50-850a-41cf-9c72-430f290290c8"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
  internal interface IPolicyConfig {
    [PreserveSig] int GetMixFormat();
    [PreserveSig] int GetDeviceFormat();
    [PreserveSig] int ResetDeviceFormat();
    [PreserveSig] int SetDeviceFormat();
    [PreserveSig] int GetProcessingPeriod();
    [PreserveSig] int SetProcessingPeriod();
    [PreserveSig] int GetShareMode();
    [PreserveSig] int SetShareMode();
    [PreserveSig] int GetPropertyValue();
    [PreserveSig] int SetPropertyValue();
    [PreserveSig] int SetDefaultEndpoint([MarshalAs(UnmanagedType.LPWStr)] string deviceId, Role role);
    [PreserveSig] int SetEndpointVisibility();
  }
  internal static class DesktopSource {
    static readonly Role[] Roles={Role.Console,Role.Multimedia,Role.Communications};
    static readonly string StateFile=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"StereoRouter","desktop-restore.txt");
    static string Current(Role role) {
      using(var enumerator=new MMDeviceEnumerator()) using(var device=enumerator.GetDefaultAudioEndpoint(DataFlow.Render,role)) return device.ID;
    }
    static void Set(string id,Role role) {
      object client=new PolicyConfigClient();
      try { Marshal.ThrowExceptionForHR(((IPolicyConfig)client).SetDefaultEndpoint(id,role)); }
      finally { Marshal.ReleaseComObject(client); }
    }
    internal static void Activate(string sourceId, string fallbackId) {
      if(File.Exists(StateFile)) throw new InvalidOperationException("A previous audio session still needs restoration. Restart the app to recover it.");
      string[] state=new string[4]; state[0]=sourceId;
      // An audio driver installer may have already made the virtual cable
      // the default. In that case, restore to the selected speaker on stop.
      for(int i=0;i<Roles.Length;i++) { string current=Current(Roles[i]); state[i+1]=current==sourceId ? fallbackId : current; }
      Directory.CreateDirectory(Path.GetDirectoryName(StateFile));
      File.WriteAllLines(StateFile,state);
      try { foreach(Role role in Roles) Set(sourceId,role); }
      catch { Restore(); throw; }
    }
    internal static bool Restore() {
      if(!File.Exists(StateFile)) return false;
      string[] state=File.ReadAllLines(StateFile);
      if(state.Length!=4) throw new InvalidOperationException("Saved Windows audio settings are invalid. Open Windows Sound settings to choose an output.");
      for(int i=0;i<Roles.Length;i++) {
        // Don't undo an output change made by the user while routing was active.
        if(Current(Roles[i])==state[0] && state[i+1]!=state[0]) Set(state[i+1],Roles[i]);
      }
      File.Delete(StateFile); return true;
    }
  }
}
