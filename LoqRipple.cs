using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using System.Threading.Tasks;
using System.IO;
using System.Xml.Serialization;
using Microsoft.Win32.SafeHandles;

static class Native {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("hid.dll")] public static extern void HidD_GetHidGuid(out Guid g);
    [DllImport("setupapi.dll", SetLastError=true)] public static extern IntPtr SetupDiGetClassDevs(ref Guid g, IntPtr e, IntPtr p, uint f);
    [DllImport("setupapi.dll", SetLastError=true)] public static extern bool SetupDiEnumDeviceInterfaces(IntPtr s, IntPtr d, ref Guid g, uint i, ref InterfaceData data);
    [DllImport("setupapi.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern bool SetupDiGetDeviceInterfaceDetail(IntPtr s, ref InterfaceData d, IntPtr detail, uint size, out uint required, IntPtr dev);
    [DllImport("setupapi.dll")] public static extern bool SetupDiDestroyDeviceInfoList(IntPtr s);
    [StructLayout(LayoutKind.Sequential)] public struct InterfaceData { public int size; public Guid guid; public uint flags; public IntPtr reserved; }
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern SafeFileHandle CreateFile(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_GetPreparsedData(SafeFileHandle h, out IntPtr p);
    [DllImport("hid.dll")] public static extern bool HidD_FreePreparsedData(IntPtr p);
    [DllImport("hid.dll")] public static extern int HidP_GetCaps(IntPtr p, out Caps caps);
    [StructLayout(LayoutKind.Sequential)] public struct Caps { public ushort usage, page, input, output, feature; [MarshalAs(UnmanagedType.ByValArray, SizeConst=17)] public ushort[] reserved; public ushort a,b,c,d,e,f,g,h,i,j; }
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_SetFeature(SafeFileHandle h, byte[] data, int len);
    [DllImport("hid.dll", SetLastError=true)] public static extern bool HidD_GetFeature(SafeFileHandle h, byte[] data, int len);
    public delegate IntPtr HookProc(int code, IntPtr w, IntPtr l);
    [DllImport("user32.dll", SetLastError=true)] public static extern IntPtr SetWindowsHookEx(int id, HookProc proc, IntPtr module, uint thread);
    [DllImport("user32.dll")] public static extern bool UnhookWindowsHookEx(IntPtr h);
    [DllImport("user32.dll")] public static extern IntPtr CallNextHookEx(IntPtr h, int c, IntPtr w, IntPtr l);
    [DllImport("kernel32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr GetModuleHandle(string name);
}

sealed class Keyboard : IDisposable {
    SafeFileHandle handle;
    byte[] saved;
    bool changed;
    public Keyboard() {
        Guid guid; Native.HidD_GetHidGuid(out guid);
        IntPtr set=Native.SetupDiGetClassDevs(ref guid,IntPtr.Zero,IntPtr.Zero,18);
        if(set==new IntPtr(-1)) throw new Exception("Cannot enumerate HID devices.");
        try {
            for(uint i=0;;i++) {
                var entry=new Native.InterfaceData(); entry.size=Marshal.SizeOf(entry);
                if(!Native.SetupDiEnumDeviceInterfaces(set,IntPtr.Zero,ref guid,i,ref entry)) break;
                uint size; Native.SetupDiGetDeviceInterfaceDetail(set,ref entry,IntPtr.Zero,0,out size,IntPtr.Zero);
                IntPtr detail=Marshal.AllocHGlobal((int)size);
                try {
                    Marshal.WriteInt32(detail,IntPtr.Size==8?8:6);
                    if(!Native.SetupDiGetDeviceInterfaceDetail(set,ref entry,detail,size,out size,IntPtr.Zero)) continue;
                    string path=Marshal.PtrToStringUni(IntPtr.Add(detail,4));
                    if(!path.ToLowerInvariant().Contains("vid_048d&pid_c993")) continue;
                    var h=Native.CreateFile(path,0,3,IntPtr.Zero,3,0,IntPtr.Zero);
                    if(h.IsInvalid) { h.Dispose(); continue; }
                    IntPtr pp; Native.Caps caps=new Native.Caps();
                    bool match=false;
                    if(Native.HidD_GetPreparsedData(h,out pp)) {
                        try { match=Native.HidP_GetCaps(pp,out caps)>=0 && caps.page==0xff89 && caps.usage==0xcc && caps.feature==33; }
                        finally { Native.HidD_FreePreparsedData(pp); }
                    }
                    if(!match) { h.Dispose(); continue; }
                    handle=h;
                    var old=new byte[33]; old[0]=0xcc;
                    if(Native.HidD_GetFeature(h,old,old.Length) && old[0]==0xcc && old[1]==0x16) saved=old;
                    return;
                } finally { Marshal.FreeHGlobal(detail); }
            }
        } finally { Native.SetupDiDestroyDeviceInfoList(set); }
        throw new Exception("LOQ 15IRX9 RGB interface not found (048D:C993, FF89:CC). Close other RGB controllers and retry.");
    }
    public static byte[] Packet(byte[] colors) {
        if(colors.Length!=12) throw new ArgumentException("Four RGB zones required.");
        var p=new byte[33]; p[0]=0xcc; p[1]=0x16; p[2]=1; p[3]=1; p[4]=2;
        Array.Copy(colors,0,p,5,12); return p;
    }
    public void Write(byte[] colors) { changed=true; var p=Packet(colors); if(!Native.HidD_SetFeature(handle,p,p.Length)) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error(),"RGB write failed"); }
    public void Dispose() {
        if(handle==null) return;
        // Some firmware cannot read back the active effect. Use steady white then.
        var p=saved??Packet(new byte[]{90,90,90,90,90,90,90,90,90,90,90,90});
        if(changed) Native.HidD_SetFeature(handle,p,p.Length); handle.Dispose(); handle=null;
    }
}

static class Ripple {
    public static Color Rainbow(double phase) {
        double h=(phase-Math.Floor(phase))*6;
        int sector=(int)h;
        int up=(int)(255*(h-sector)), down=255-up;
        switch(sector) {
            case 0: return Color.FromArgb(255,up,0);
            case 1: return Color.FromArgb(down,255,0);
            case 2: return Color.FromArgb(0,255,up);
            case 3: return Color.FromArgb(0,down,255);
            case 4: return Color.FromArgb(up,0,255);
            default: return Color.FromArgb(255,0,down);
        }
    }
    public static int Zone(int vk) {
        if(vk>=96 && vk<=111) return 3;
        string left="QWASZX"; string middle="ERTDFGCVB"; string right="YUIOPHJKLNM";
        char c=(char)vk;
        if(left.IndexOf(c)>=0) return 0;
        if(middle.IndexOf(c)>=0) return 1;
        if(right.IndexOf(c)>=0) return 2;
        if(vk>=48 && vk<=57) return vk==48?2:(vk<=51?0:(vk<=54?1:2));
        if(vk>=112 && vk<=123) return Math.Min(2,(vk-112)/4);
        if(vk==32) return 1;
        if(vk>=33 && vk<=46) return 3;
        if(vk==13 || vk==8 || vk>=186 && vk<=222) return 2;
        return 0;
    }
    public static double Level(int origin,int zone,double age,double speed) {
        double t=age-Math.Abs(origin-zone)*speed;
        return t<0 || t>.42?0:Math.Sin(Math.PI*t/.42)*Math.Exp(-Math.Abs(origin-zone)*.18);
    }
}

static partial class Effects {
    public static readonly string[] Names={
        "Key Ripple", "Key Flash", "Key Trail", "Typing Heat",
        "Rainbow Wave", "Color Cycle", "Breathing", "Meteor",
        "Rain", "Sparkle", "Lightning", "Fire", "Ocean", "Static", "Touchpad Follow",
        "Drag Light", "Scroll Wave", "Double Tap", "Bounce", "Orbit",
        "Hold Charge", "Music Pulse", "Screen Colors", "Idle Breathing", "Key Groups", "Battery", "Music Spectrum",
        "Aurora", "Heartbeat", "Scanner", "Colliding Waves", "Lava", "Thunderstorm", "Tides", "Color Key Ripple", "Music Echo", "Typing Build",
        "Embers", "Twinkling Stars", "Typing Echo", "Music Rainbow Wave", "Alternating Zones"
    };
    public static Color KeyColor(int vk) {
        return vk==13?Color.White:((vk>=48 && vk<=57)||(vk>=96 && vk<=105))?Color.Orange:(vk>=65 && vk<=90)?Color.DeepSkyBlue:Color.Magenta;
    }
    public static Color AudioColor(int zone,double peak,double time) {
        return Scale(Ripple.Rainbow(time*.08+zone*.15),Math.Max(0,Math.Min(1,Math.Sqrt(Math.Max(0,peak))*4-zone*.7)));
    }
    public static int PointerZone(int x,int left,int width) { return Math.Max(0,Math.Min(3,(int)(((double)x-left)*4/Math.Max(1,width)))); }
    public static int ReactiveMode(int selected,bool overlay) { return selected<4?selected:(overlay?0:-1); }
    public static double ReactivePower(int mode,int origin,int zone,double age,int delay) {
        if(age<0) return 0;
        double pace=140.0/delay;
        if(mode==1) return age<.8?Math.Exp(-age*pace*7):0;
        if(mode==2) {
            double tail=age*pace-((zone-origin+4)%4)*.12;
            return tail>=0 && tail<.7?Math.Exp(-tail*7):0;
        }
        if(mode==3) return zone==origin?Math.Exp(-age*pace*1.8):0;
        return Ripple.Level(origin,zone,age,delay/1000.0);
    }
    public static Color Follow(int zone,double position,double motion,Color chosen,bool rainbow) {
        position=Math.Max(0,Math.Min(3,position));
        double distance=zone-position;
        double strength=Math.Exp(-distance*distance/0.48)*Math.Max(0,Math.Min(1,motion));
        return Scale(rainbow?Ripple.Rainbow(position/3.0*.8):chosen,strength);
    }
    static double Noise(int n) { double x=Math.Sin(n*127.1+311.7)*43758.5453; return x-Math.Floor(x); }
    public static Color Scale(Color c,double v) {
        v=Math.Max(0,Math.Min(1,v));
        return Color.FromArgb((int)(c.R*v),(int)(c.G*v),(int)(c.B*v));
    }
    public static Color Sample(int mode,int zone,double time,Color chosen,bool rainbow) {
        if(mode>=27) return ExtendedSample(mode,zone,time,chosen,rainbow);
        double pulse=.5+.5*Math.Sin(time*2*Math.PI);
        Color tint=rainbow?Ripple.Rainbow(time*.15+zone*.18):chosen;
        switch(mode) {
            case 4: return Ripple.Rainbow(zone*.22-time*.25);
            case 5: return Ripple.Rainbow(time*.18);
            case 6: return Scale(tint,.03+.97*pulse*pulse);
            case 7:
                double head=(time*2)%6;
                double distance=head-zone;
                return Scale(tint,distance>=0 && distance<2.4?Math.Exp(-distance*1.8):0);
            case 8:
                double fall=time*2+zone*.77;
                int drop=(int)Math.Floor(fall);
                return Scale(tint,Noise(drop+zone*41)>.42?Math.Exp(-(fall-drop)*5):0);
            case 9:
                double tick=time*3;
                return Scale(tint,Math.Pow(Noise((int)tick+zone*101),4)*Math.Pow(1-(tick-Math.Floor(tick)),2));
            case 10:
                double storm=time%4;
                double flash=storm<.10?1:(storm>.23 && storm<.31?.8:0);
                return Scale(Color.FromArgb(180,210,255),flash*(.7+.3*Noise((int)(time/4)+zone)));
            case 11:
                double heat=.5+.25*Math.Sin(time*7+zone*2)+.2*Math.Sin(time*11-zone);
                return Color.FromArgb((int)(170+85*heat),(int)(15+105*heat*heat),0);
            case 12:
                double water=.5+.5*Math.Sin(zone*1.4-time*2);
                return Color.FromArgb(0,(int)(30+140*water),(int)(90+165*water));
            case 13: return chosen;
            case 18: return Follow(zone,3*(.5+.5*Math.Sin(time*2)),1,chosen,rainbow);
            case 19:
                double point=(time*1.8)%4;
                double gap=Math.Abs(zone-point); gap=Math.Min(gap,4-gap);
                return Scale(tint,Math.Exp(-gap*gap*3));
            default: return Color.Black;
        }
    }
}

[ComImport, Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")] class MMEnumerator {}
[ComImport, Guid("A95664D2-9614-4F35-A746-DE8DB63617E6"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMEnumerator {
    void EnumAudioEndpoints(int flow,uint mask,out IntPtr collection);
    void GetDefaultAudioEndpoint(int flow,int role,out IMMDevice device);
}
[ComImport, Guid("D666063F-1587-4E43-81F1-B948E807363F"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IMMDevice { void Activate(ref Guid iid,uint context,IntPtr parameters,[MarshalAs(UnmanagedType.IUnknown)] out object result); }
[ComImport, Guid("C02216F6-8C67-4B5B-9D00-D008E73E0064"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IAudioMeter { void GetPeakValue(out float peak); }
sealed class AudioPeak : IDisposable {
    IAudioMeter meter;
    public float Read() {
        if(meter==null) {
            IMMEnumerator enumerator=null; IMMDevice device=null;
            try {
                enumerator=(IMMEnumerator)new MMEnumerator();
                enumerator.GetDefaultAudioEndpoint(0,1,out device);
                Guid id=typeof(IAudioMeter).GUID; object obj;
                device.Activate(ref id,23,IntPtr.Zero,out obj); meter=(IAudioMeter)obj;
            } finally {
                if(device!=null) Marshal.ReleaseComObject(device);
                if(enumerator!=null) Marshal.ReleaseComObject(enumerator);
            }
        }
        float value; meter.GetPeakValue(out value); return value;
    }
    public void Dispose() { if(meter!=null) { Marshal.ReleaseComObject(meter); meter=null; } }
}

public class LightingProfile {
    public string Name="Custom";
    public int Effect=14, Delay=140, Brightness=75, ColorArgb=Color.FromArgb(30,170,255).ToArgb();
    public bool Rainbow=true, KeyRipple=true, ClickRipple=true, PureScreen=true;
    public int Secondary=-1, Mix=40, ScreenRefresh=250, Saturation=110;
    public string Monitor="";
    public List<EffectDetails> PrimaryDetails=new List<EffectDetails>(), SecondaryDetails=new List<EffectDetails>();
    public override string ToString() { return Name; }
}
public class UserSettings {
    public AppAppearance Appearance=AppAppearance.System;
    public bool PauseLock=true, PauseSaver=true;
    public bool NotifyVolume=true, NotifyBrightness=true, NotifyBattery=true, NotifyInternet=true, NotifyHeat=true;
    public bool MicIndicator,CapsIndicator,LanguageIndicator,BatteryOnDemand,FocusIndicator,CpuIndicator,ChargingIndicator,IdleIndicator;
    public int FocusMinutes=25,IdleSeconds=60;
    public int HeatThreshold=85;
    public int NoticeDuration=2, BatteryThreshold=20;
    public LightingProfile Current=new LightingProfile();
    public List<LightingProfile> Profiles=new List<LightingProfile>();
    public static UserSettings Load(string path) {
        try { using(var file=File.OpenRead(path)) return (UserSettings)new XmlSerializer(typeof(UserSettings)).Deserialize(file); }
        catch(IOException) { return new UserSettings(); }
        catch(InvalidOperationException) { return new UserSettings(); }
        catch(UnauthorizedAccessException) { return new UserSettings(); }
    }
    public void Save(string path) {
        Directory.CreateDirectory(Path.GetDirectoryName(path));
        string temp=path+".tmp";
        using(var file=File.Create(temp)) new XmlSerializer(typeof(UserSettings)).Serialize(file,this);
        if(File.Exists(path)) File.Replace(temp,path,null); else File.Move(temp,path);
    }
}

sealed partial class RippleForm : Form {
    sealed class Wave { public int zone; public double time; public bool click; public int kind; public int settingsMode=-1,settingsLayer; public Color tint=Color.Empty; }
    readonly List<Wave> waves=new List<Wave>();
    readonly HashSet<int> down=new HashSet<int>();
    readonly Stopwatch clock=Stopwatch.StartNew();
    readonly Timer timer=new Timer();
    readonly StudioSlider speed=new StudioSlider { Minimum=60,Maximum=300,Value=140,TickFrequency=40,Width=360 };
    readonly StudioSlider brightness=new StudioSlider { Minimum=10,Maximum=100,Value=75,TickFrequency=10,Width=360 };
    readonly GroupedEffectCombo effect=new GroupedEffectCombo { DropDownStyle=ComboBoxStyle.DropDownList,Width=360,MaxDropDownItems=15,IntegralHeight=true };
    readonly Label description=new Label { AutoSize=false,Size=new Size(370,48) };
    readonly Panel[] preview=new Panel[4];
    readonly Label status=new Label { AutoSize=false,Size=new Size(370,32),Text="Stopped" };
    readonly CheckBox pureScreen=new StudioSwitch { Text="Screen: pure colors (no input overlays)",Checked=true,Enabled=false,AutoSize=true };
    readonly CheckBox clickRipple=new StudioSwitch { Text="Touchpad / mouse click ripple",Checked=true,AutoSize=true };
    readonly CheckBox rippleOverlay=new StudioSwitch { Text="Add Key Ripple over this effect",Checked=true,AutoSize=true };
    readonly CheckBox rainbow=new StudioSwitch { Text="Use rainbow colors",Checked=true,AutoSize=true };
    Color color=Color.FromArgb(30,170,255);
    Keyboard keyboard; IntPtr hook, mouseHook; Native.HookProc callback, mouseCallback;
    byte[] previous;
    Point lastPointer;
    double pointerZone, pointerMotion, pointerFrameTime;
    double activity, holdStart, scrollTime=-100, scrollPhase, lastClick=-100, lastDrag=-100, screenTime=-100, audioRetry;
    int scrollDirection=1, lastClickButton; Point lastClickPoint;
    bool dragging;
    readonly AudioPeak audio=new AudioPeak();
    Task<Color[]> screenCapture;
    readonly ScreenSampler screenSampler=new ScreenSampler();
    string pendingScreenKey;
    double screenInterval=.25;
    readonly Color[] screenColors={Color.Black,Color.Black,Color.Black,Color.Black};
    string sourceError="";
    bool testing, loading, exitRequested;
    readonly NotifyIcon tray=new NotifyIcon();
    readonly ContextMenuStrip trayMenu=new ContextMenuStrip();
    readonly Timer saveTimer=new Timer { Interval=500 };
    readonly StudioCombo profiles=new StudioCombo { DropDownStyle=ComboBoxStyle.DropDownList,Width=235 };
    UserSettings settings;
    readonly string settingsPath=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"LoqRipple","settings.xml");
    LightingProfile Snapshot(string name) {
        return new LightingProfile { Name=name,Effect=activeMode,Delay=speed.Value,Brightness=brightness.Value,ColorArgb=color.ToArgb(),Rainbow=rainbow.Checked,KeyRipple=rippleOverlay.Checked,ClickRipple=clickRipple.Checked,PureScreen=pureScreen.Checked,Secondary=secondaryMode,Mix=mix.Value,ScreenRefresh=(int)refreshMs.Value,Saturation=saturation.Value,Monitor=monitor.SelectedItem as string??"",PrimaryDetails=CopyDetails(primaryDetails),SecondaryDetails=CopyDetails(secondaryDetails) };
    }
    void QueueSave() { if(loading || testing) return; saveTimer.Stop(); saveTimer.Start(); }
    void SaveSettings() {
        saveTimer.Stop(); if(testing || loading || settings==null) return;
        settings.Current=Snapshot("Last used");
        SaveUtilitySettings();
        settings.Appearance=(AppAppearance)Math.Max(0,appearance.SelectedIndex);
        settings.NotifyInternet=notifyInternet.Checked; settings.NotifyHeat=notifyHeat.Checked; settings.HeatThreshold=(int)heatThreshold.Value;
        settings.NotifyVolume=notifyVolume.Checked; settings.NotifyBrightness=notifyBrightness.Checked; settings.NotifyBattery=notifyBattery.Checked;
        settings.NoticeDuration=(int)noticeDuration.Value; settings.BatteryThreshold=(int)batteryThreshold.Value;
        settings.PauseLock=pauseLock.Checked; settings.PauseSaver=pauseSaver.Checked;
        try { settings.Save(settingsPath); }
        catch(Exception ex) { if(ex is IOException || ex is UnauthorizedAccessException) status.Text="Settings could not be saved"; else throw; }
    }
    void RestoreProfile(LightingProfile p) {
        if(p==null) return;
        loading=true;
        try {
            primaryDetails=CopyDetails(p.PrimaryDetails); secondaryDetails=CopyDetails(p.SecondaryDetails);
            effect.SelectedIndex=Math.Max(0,Math.Min(Effects.Names.Length-1,p.Effect));
            speed.Value=Math.Max(speed.Minimum,Math.Min(speed.Maximum,p.Delay));
            brightness.Value=Math.Max(brightness.Minimum,Math.Min(brightness.Maximum,p.Brightness));
            color=Color.FromArgb(p.ColorArgb); rainbow.Checked=p.Rainbow;
            rippleOverlay.Checked=p.KeyRipple; clickRipple.Checked=p.ClickRipple; pureScreen.Checked=p.PureScreen;
            secondary.SelectedIndex=Math.Max(0,Math.Min(Effects.Names.Length,p.Secondary+1)); secondaryMode=secondary.SelectedIndex-1;
            mix.Value=Math.Max(0,Math.Min(100,p.Mix));
            refreshMs.Value=Math.Max(refreshMs.Minimum,Math.Min(refreshMs.Maximum,p.ScreenRefresh));
            saturation.Value=Math.Max(saturation.Minimum,Math.Min(saturation.Maximum,p.Saturation));
            if(monitor.Items.Contains(p.Monitor??"")) monitor.SelectedItem=p.Monitor;
            ApplyEffect();
        } finally { loading=false; }
        UpdateContextControls(); RefreshDetails(); ResetDetailAnimation(); QueueSave();
    }
    void RefreshProfiles() {
        profiles.Items.Clear();
        profiles.Items.Add(new LightingProfile { Name="Work",Effect=23,Brightness=30,Rainbow=false,ClickRipple=false });
        profiles.Items.Add(new LightingProfile { Name="Gaming",Effect=14,Brightness=80 });
        profiles.Items.Add(new LightingProfile { Name="Music",Effect=21,Brightness=70,KeyRipple=false,ClickRipple=false });
        profiles.Items.Add(new LightingProfile { Name="Screen",Effect=22,Brightness=85,KeyRipple=false,ClickRipple=false });
        foreach(var p in settings.Profiles) if(p!=null) profiles.Items.Add(p);
    }
    void SaveProfile() {
        StudioTextBox name;
        using(var dialog=CreateProfileDialog(out name)) {
            if(dialog.ShowDialog(this)!=DialogResult.OK || string.IsNullOrWhiteSpace(name.Text)) return;
            string label=name.Text.Trim();
            settings.Profiles.RemoveAll(p=>p!=null && string.Equals(p.Name,label,StringComparison.OrdinalIgnoreCase));
            settings.Profiles.Add(Snapshot(label)); RefreshProfiles(); SaveSettings();
        }
    }
    void ShowWindow() { Show(); WindowState=FormWindowState.Normal; Activate(); }
    void HideToTray() { SaveSettings(); if(settingsWindow!=null) settingsWindow.Hide(); Hide(); }
    void SetupTray() {
        tray.Icon=Icon??SystemIcons.Application; tray.Text="LOQ Key Ripple"; tray.ContextMenuStrip=trayMenu;
        trayMenu.Items.Add("Show window",null,delegate { ShowWindow(); });
        trayMenu.Items.Add("Settings...",null,delegate { ShowGeneralSettings(); });
        trayMenu.Items.Add("Start / Resume",null,delegate { Start(); });
        trayMenu.Items.Add("Stop / Restore light",null,delegate { Stop(); });
        trayMenu.Items.Add(new ToolStripSeparator());
        trayMenu.Items.Add("Exit",null,delegate { exitRequested=true; Close(); });
        tray.DoubleClick+=delegate { ShowWindow(); }; tray.Visible=true;
    }
    int activeMode;
    void AddWave(int zone,double time,int kind,Color tint,int settingsMode=-1,int settingsLayer=0) {
        if(waves.Count>=40) waves.RemoveAt(0);
        waves.Add(new Wave { zone=zone,time=time,click=true,kind=kind,tint=tint,settingsMode=settingsMode,settingsLayer=settingsLayer });
    }
    static string Describe(int mode) {
        string[] extra={"Hold left click and move to draw a light trail.","Scroll with two fingers or a mouse wheel; direction reverses the wave.","Double click or double tap for two pulses.","Light bounces between the edges.","Light orbits through the four zones.","Hold a key to charge; release the last held key to launch a wave.","Reacts to default playback audio level, without recording.","Dominant colors from all four full-screen quarters; reduced neutral dilution.","After 8 seconds idle, typing waves become breathing light.","Letters: blue. Numbers: orange. Enter: white. Other keys: magenta.","Battery level by zone, with charging pulses.","Playback frequencies: bass, mid, treble, combined."};
        string[] added={"Slow green and purple aurora curtains.","A double heartbeat, followed by a quiet pause.","A scanning light with a fading tail.","Two waves meet in the middle.","Slowly flowing red and orange lava.","Dim storm clouds with irregular lightning flashes.","Light expands from the center and recedes.","Each key launches a colored wave; overlapping waves mix.","Bass pulses travel across the keyboard with a fading echo.","Typing fills the keyboard; pausing lets the light fade.","Quiet red embers with occasional orange glows.","Independent stars fade in and out smoothly.","Each key launches a wave and progressively weaker echoes.","A rainbow wave accelerates with playback audio level.","Outer zones and inner zones alternate with a smooth beat."};
        if(mode>=27) return added[mode-27];
        return mode>=15?extra[mode-15]:mode==14?"Light follows the pointer; enable Add Key Ripple for typing waves.":mode<4?"Reactive: type anywhere to trigger this effect.":"Continuous: animates without typing.";
    }
    internal static Color[] AverageScreen(Bitmap image) {
        var result=new Color[4];
        for(int zone=0;zone<4;zone++) {
            long r=0,g=0,b=0,count=0;
            int left=zone*image.Width/4, right=(zone+1)*image.Width/4;
            for(int y=0;y<image.Height;y++) for(int x=left;x<right;x++) {
                Color c=image.GetPixel(x,y); r+=c.R; g+=c.G; b+=c.B; count++;
            }
            result[zone]=Color.FromArgb((int)(r/count),(int)(g/count),(int)(b/count));
        }
        return result;
    }
    internal static Color[] DominantScreen(Bitmap image,bool ignoreBlack=true,bool ignoreNeutral=true,bool vivid=true,Color[] previousColors=null) {
        Color[] result=AverageScreen(image);
        for(int zone=0;zone<4;zone++) {
            var weight=new double[13]; var red=new double[13]; var green=new double[13]; var blue=new double[13];
            int left=zone*image.Width/4, right=(zone+1)*image.Width/4, colorful=0;
            for(int y=0;y<image.Height;y++) for(int x=left;x<right;x++) {
                Color c=image.GetPixel(x,y);
                double max=Math.Max(c.R,Math.Max(c.G,c.B)), min=Math.Min(c.R,Math.Min(c.G,c.B));
                double saturation=max==0?0:(max-min)/max;
                if(max<30 && ignoreBlack) continue;
                bool neutral=saturation<.18;
                if(neutral && max>=30 && ignoreNeutral) continue;
                colorful++;
                int bin=neutral?12:Math.Min(11,(int)(c.GetHue()/30));
                double w=neutral?.5+max/510:saturation*max/255.0;
                weight[bin]+=w; red[bin]+=c.R*w; green[bin]+=c.G*w; blue[bin]+=c.B*w;
            }
            // A tiny colored icon must not turn an otherwise neutral quarter neon.
            if(colorful<(right-left)*image.Height*.04) continue;
            int best=0;
            for(int i=1;i<13;i++) if(weight[i]>weight[best]) best=i;
            if(previousColors!=null && previousColors.Length==4) {
                Color old=previousColors[zone]; double high=Math.Max(old.R,Math.Max(old.G,old.B)),low=Math.Min(old.R,Math.Min(old.G,old.B));
                int oldBin=high==0 || (high-low)/high<.18?12:Math.Min(11,(int)(old.GetHue()/30));
                // Keep the previous winner only while it remains competitive; scene cuts switch immediately.
                if(weight[oldBin]>0 && weight[oldBin]>=weight[best]*.85) best=oldBin;
            }
            double total=0,r=0,g=0,b=0;
            for(int offset=best==12?0:-1;offset<=(best==12?0:1);offset++) {
                int i=best==12?12:(best+offset+12)%12;
                total+=weight[i]; r+=red[i]; g+=green[i]; b+=blue[i];
            }
            if(total==0) continue;
            r/=total; g/=total; b/=total;
            double floor=!vivid || best==12?0:Math.Min(r,Math.Min(g,b))*.2;
            r-=floor; g-=floor; b-=floor;
            double lift=!vivid || best==12?1:Math.Max(1,Math.Min(1.35,150/Math.Max(1,Math.Max(r,Math.Max(g,b)))));
            result[zone]=Color.FromArgb((int)Math.Round(Math.Min(255,r*lift)),(int)Math.Round(Math.Min(255,g*lift)),(int)Math.Round(Math.Min(255,b*lift)));
        }
        return result;
    }
    static Color[] CaptureScreen(Rectangle bounds,EffectDetails primary=null,EffectDetails secondaryConfig=null) {
        using(var sampler=new ScreenSampler()) return sampler.Capture(bounds,primary??new EffectDetails(),secondaryConfig);
    }
    string CurrentScreenKey() {
        return ScreenSampler.Key(CaptureBounds(),Details(22,0),secondaryMode==22?Details(22,1):null);
    }
    void UpdateScreenCapture(double now,bool active) {
        if(screenCapture!=null && screenCapture.IsCompleted) {
            if(screenCapture.IsFaulted) {
                // Observe the exception even after leaving the effect.
                var error=screenCapture.Exception;
                if(active) sourceError="Screen capture unavailable";
                screenInterval=2;
            } else if(active && pendingScreenKey==CurrentScreenKey()) {
                Array.Copy(screenCapture.Result,screenColors,4);
                if(screenCapture.Result.Length>=8) Array.Copy(screenCapture.Result,4,secondaryScreenColors,0,4);
                else Array.Copy(screenColors,secondaryScreenColors,4);
                sourceError="";
                screenInterval=Math.Max((double)refreshMs.Value/1000,Math.Min(3,screenSampler.LastCaptureSeconds*1.5));
            }
            screenCapture=null;
        }
        if(active && screenCapture==null && now-screenTime>=screenInterval) {
            Rectangle bounds=CaptureBounds();
            screenTime=now;
            var first=Details(22,0).Clone(); var second=secondaryMode==22?Details(22,1).Clone():null;
            pendingScreenKey=ScreenSampler.Key(bounds,first,second);
            screenCapture=Task.Factory.StartNew(()=>screenSampler.Capture(bounds,first,second));
        }
    }
    void ApplyEffect() {
        if(effect.SelectedIndex<0) return;
        activeMode=effect.SelectedIndex;
        waves.Clear(); previous=null;
        rippleOverlay.Enabled=activeMode>=4; pureScreen.Enabled=UsesMode(22);
        description.Text=Describe(activeMode);
        spectrum.Dispose(); spectrumRetry=0;
        audio.Dispose(); audioRetry=0; if(screenCapture==null) screenTime=-100;
        screenInterval=(double)refreshMs.Value/1000; sourceError=""; dragging=false; scrollTime=-100; lastClick=-100;
        activity=clock.Elapsed.TotalSeconds; holdStart=activity;
        UpdateContextControls(); RefreshDetails(); ResetDetailAnimation(); QueueSave();
    }
    public RippleForm(bool testMode=false,bool background=false) {
        AutoScaleMode=AutoScaleMode.Dpi; Font=new Font("Segoe UI",9.5f);
        testing=testMode; startHidden=background; loading=true;
        settings=testing?new UserSettings():UserSettings.Load(settingsPath);
        if(settings.Profiles==null) settings.Profiles=new List<LightingProfile>();
        Icon=Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        Text="LOQ Key Ripple"; ClientSize=new Size(1140,780); MinimumSize=new Size(1080,800); FormBorderStyle=FormBorderStyle.Sizable; MaximizeBox=true; StartPosition=FormStartPosition.CenterScreen;
        var layout=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,Padding=new Padding(18),WrapContents=false,AutoScroll=true };
        var profileRow=new FlowLayoutPanel { Width=375,Height=36 };
        profileRow.Controls.Add(profiles);
        var saveProfile=new Button { Text="Save profile",AutoSize=true };
        saveProfile.Click+=delegate { SaveProfile(); }; profileRow.Controls.Add(saveProfile);
        RefreshProfiles(); profiles.SelectionChangeCommitted+=delegate { RestoreProfile(profiles.SelectedItem as LightingProfile); };
        layout.Controls.Add(profileRow);
        layout.Controls.Add(new Label { Text="Lighting effect",AutoSize=true });
        effect.Populate(false); effect.SelectedIndex=0;
        // Browsing the popup must not rebuild the layout or switch effects.
        effect.SelectedIndexChanged+=delegate { if(!effect.DroppedDown) ApplyEffect(); };
        effect.SelectionChangeCommitted+=delegate { ApplyEffect(); };
        effect.DropDownClosed+=delegate {
            BeginInvoke((Action)delegate { if(!IsDisposed && !effect.DroppedDown && effect.SelectedIndex>=0 && effect.SelectedIndex!=activeMode) ApplyEffect(); });
        };
        layout.Controls.Add(effect); description.Text="Reactive: type anywhere to trigger this effect."; layout.Controls.Add(description);
        var strip=new FlowLayoutPanel { Width=370,Height=40 };
        for(int i=0;i<4;i++) { preview[i]=new Panel { Width=82,Height=28,BackColor=Color.Black,Tag="rgb-preview",BorderStyle=BorderStyle.FixedSingle }; strip.Controls.Add(preview[i]); }
        layout.Controls.Add(strip);
        layout.Controls.Add(new Label { Text="Animation delay (left = faster)",AutoSize=true }); layout.Controls.Add(speed);
        layout.Controls.Add(new Label { Text="Brightness",AutoSize=true }); layout.Controls.Add(brightness);
        layout.Controls.Add(rainbow);
        layout.Controls.Add(rippleOverlay);
        layout.Controls.Add(clickRipple);
        layout.Controls.Add(pureScreen);
        clickRipple.CheckedChanged+=delegate { waves.RemoveAll(x=>x.click); };
        rippleOverlay.CheckedChanged+=delegate { waves.Clear(); previous=null; };
        var choose=new Button { Text="Choose color",AutoSize=true }; choose.Click+=delegate { using(var d=new ColorDialog { Color=color,FullOpen=true }) if(d.ShowDialog()==DialogResult.OK) { color=d.Color; rainbow.Checked=false; QueueSave(); } }; layout.Controls.Add(choose);
        var start=new Button { Text="Start / Resume",AutoSize=true }; start.Click+=delegate { Start(); }; layout.Controls.Add(start);
        var stop=new Button { Text="Stop / Restore light",AutoSize=true }; stop.Click+=delegate { Stop(); }; layout.Controls.Add(stop);
        var hide=new Button { Text="Hide to tray",AutoSize=true }; hide.Click+=delegate { HideToTray(); }; layout.Controls.Add(hide);
        layout.Controls.Add(status); layout.Controls.Add(new Label { Text="Emergency stop: Ctrl + Alt + F12\nNo keystrokes are recorded or sent anywhere.",AutoSize=true });
        effect.SelectedIndex=14;
        var tabs=new StudioTabs { Dock=DockStyle.Fill };
        var mainTab=new TabPage("Lighting"); mainTab.Controls.Add(BuildLightingStudio(layout)); tabs.TabPages.Add(mainTab);
        var detailsTab=new TabPage("Effect settings"); detailsTab.Controls.Add(BuildCombinedEffectSettings()); tabs.TabPages.Add(detailsTab);
        var noticesTab=new TabPage("Indicators"); noticesTab.Controls.Add(BuildIndicatorStudio((FlowLayoutPanel)BuildNotifications())); tabs.TabPages.Add(noticesTab);
        CreateGeneralSettings();
        Controls.Add(BuildTabHost(tabs)); Controls.Add(BuildMacNavigation(tabs)); Controls.Add(BuildStudioHeader()); timer.Interval=33; timer.Tick+=delegate { Frame(); };
        InitializeAppearance();
        saveTimer.Tick+=delegate { SaveSettings(); };
        speed.ValueChanged+=delegate { QueueSave(); }; brightness.ValueChanged+=delegate { QueueSave(); };
        foreach(var check in new CheckBox[]{rainbow,rippleOverlay,clickRipple,pureScreen}) check.CheckedChanged+=delegate { QueueSave(); };
        notifyInternet.Checked=settings.NotifyInternet; notifyHeat.Checked=settings.NotifyHeat; heatThreshold.Value=Math.Max(60,Math.Min(110,settings.HeatThreshold));
        notifyVolume.Checked=settings.NotifyVolume; notifyBrightness.Checked=settings.NotifyBrightness; notifyBattery.Checked=settings.NotifyBattery;
        noticeDuration.Value=Math.Max(1,Math.Min(5,settings.NoticeDuration)); batteryThreshold.Value=Math.Max(5,Math.Min(50,settings.BatteryThreshold));
        pauseLock.Checked=settings.PauseLock; pauseSaver.Checked=settings.PauseSaver;
        LoadUtilitySettings();
        loading=false; RestoreProfile(settings.Current);
        InitAdvanced();
        if(!testing) SetupTray();
        Resize+=delegate { if(!testing && WindowState==FormWindowState.Minimized) HideToTray(); };
        FormClosing+=delegate(object sender,FormClosingEventArgs e) {
            if(!testing && !exitRequested && e.CloseReason==CloseReason.UserClosing) { e.Cancel=true; HideToTray(); return; }
            SaveSettings(); Stop();
        };
        FormClosed+=delegate { DisposeAdvanced(); tray.Visible=false; tray.Dispose(); trayMenu.Dispose(); saveTimer.Dispose(); timer.Dispose(); };
        Shown+=delegate { Start(); };
    }
    void Start() {
        desiredRunning=true;
        string reason=PauseReason(); if(reason!=null && !testing) { status.Text=reason; return; }
        if(keyboard!=null) return;
        try {
            keyboard=new Keyboard(); callback=OnKey;
            hook=Native.SetWindowsHookEx(13,callback,Native.GetModuleHandle(null),0);
            if(hook==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            mouseCallback=OnMouse;
            mouseHook=Native.SetWindowsHookEx(14,mouseCallback,Native.GetModuleHandle(null),0);
            if(mouseHook==IntPtr.Zero) throw new System.ComponentModel.Win32Exception(Marshal.GetLastWin32Error());
            waves.Clear(); down.Clear(); previous=null;
            lastPointer=Cursor.Position;
            Rectangle desktop=SystemInformation.VirtualScreen;
            pointerZone=Math.Max(0,Math.Min(3,(lastPointer.X-desktop.Left)*3.0/Math.Max(1,desktop.Width-1)));
            pointerMotion=0; pointerFrameTime=clock.Elapsed.TotalSeconds; activity=pointerFrameTime; holdStart=activity; dragging=false;
            timer.Start(); status.Text="Running";
        } catch(Exception ex) { Stop(); MessageBox.Show(ex.Message,"Cannot start"); }
    }
    IntPtr OnKey(int code,IntPtr w,IntPtr l) {
        if(code>=0) {
            int msg=w.ToInt32(), vk=Marshal.ReadInt32(l);
            double now=clock.Elapsed.TotalSeconds;
            activity=now;
            if(msg==0x101 || msg==0x105) {
                bool released=down.Remove(vk);
                if(released && down.Count==0 && UsesMode(20)) AddWave(Ripple.Zone(vk),now,0,Color.Empty,20,activeMode==20?0:1);
            }
            if(msg==0x100 || msg==0x104) {
                if(vk==123 && (Control.ModifierKeys & (Keys.Control|Keys.Alt))==(Keys.Control|Keys.Alt)) BeginInvoke((Action)Stop);
                else if(down.Add(vk)) {
                    RecordColorKey(Ripple.Zone(vk),now);
                    if(down.Count==1) holdStart=now;
                    if(UsesMode(24)) AddWave(Ripple.Zone(vk),now,vk==13?3:0,Effects.KeyColor(vk),24,activeMode==24?0:1);
                    else if(!UsesMode(20)) { if(waves.Count>=40) waves.RemoveAt(0); waves.Add(new Wave { zone=Ripple.Zone(vk),time=now }); }
                }
            }
        }
        return Native.CallNextHookEx(hook,code,w,l);
    }
    IntPtr OnMouse(int code,IntPtr w,IntPtr l) {
        int msg=w.ToInt32();
        if(code>=0) {
            double now=clock.Elapsed.TotalSeconds;
            int x=Marshal.ReadInt32(l), y=Marshal.ReadInt32(l,4);
            int zone=Effects.PointerZone(x,SystemInformation.VirtualScreen.Left,SystemInformation.VirtualScreen.Width);
            if(msg!=0x200) activity=now;
            if(msg==0x201) dragging=true;
            if(msg==0x202) dragging=false;
            if(msg==0x20a || msg==0x20e) {
                int delta=(short)((uint)Marshal.ReadInt32(l,8)>>16);
                if(delta!=0) { scrollPhase+=delta/120.0*.35; scrollDirection=Math.Sign(delta); scrollTime=now; }
            }
            if(msg==0x201 || msg==0x204 || msg==0x207) {
                bool twice=lastClickButton==msg && (now-lastClick)*1000<=SystemInformation.DoubleClickTime && Math.Abs(x-lastClickPoint.X)<=SystemInformation.DoubleClickSize.Width && Math.Abs(y-lastClickPoint.Y)<=SystemInformation.DoubleClickSize.Height;
                if(UsesMode(17)) {
                    if(twice) { AddWave(zone,now,3,Color.Empty,17,activeMode==17?0:1); AddWave(zone,now+.24,3,Color.Empty,17,activeMode==17?0:1); }
                } else if(clickRipple.Checked) AddWave(zone,now,0,Color.Empty);
                lastClick=twice?-100:now; lastClickButton=msg; lastClickPoint=new Point(x,y);
            }
        }
        return Native.CallNextHookEx(mouseHook,code,w,l);
    }
    void Frame() {
        try {
            double now=clock.Elapsed.TotalSeconds; PollIndicators(now); double lifetime=WaveLifetime(); waves.RemoveAll(x=>now-x.time>lifetime);
            int mode=activeMode; double pace=140.0/speed.Value;
            Point pointer=Cursor.Position;
            double dt=Math.Max(.001,Math.Min(.2,now-pointerFrameTime)); pointerFrameTime=now;
            Rectangle desktop=SystemInformation.VirtualScreen;
            double target=Math.Max(0,Math.Min(3,(pointer.X-desktop.Left)*3.0/Math.Max(1,desktop.Width-1)));
            double dx=(double)pointer.X-lastPointer.X, dy=(double)pointer.Y-lastPointer.Y;
            double movement=Math.Sqrt(dx*dx+dy*dy);
            double smooth=1-Math.Exp(-dt*1000.0/speed.Value);
            pointerZone+=(target-pointerZone)*smooth;
            rawPointerMotion=movement>0?Math.Min(1,.25+movement/dt/900.0):0;
            if(movement>0) pointerMotion=Math.Min(1,.25+movement/dt/900.0);
            else pointerMotion*=Math.Exp(-dt*2.5);
            if(movement>0) activity=now;
            if(UsesMode(15) && dragging && movement>0 && now-lastDrag>.045) {
                AddWave(Effects.PointerZone(pointer.X,desktop.Left,desktop.Width),now,2,Color.Empty,15,activeMode==15?0:1); lastDrag=now;
            }
            lastPointer=pointer;
            double audioLevel=0;
            if((UsesMode(21) || UsesMode(40)) && now>=audioRetry) {
                try { audioLevel=audio.Read(); sourceError=""; }
                catch(Exception) { audio.Dispose(); audioRetry=now+2; sourceError="Audio unavailable - retrying"; }
            }
            if((UsesMode(26) || UsesMode(35)) && now>=spectrumRetry) {
                try { audioBands=spectrum.Read(); sourceError=""; }
                catch(Exception) { spectrum.Dispose(); audioBands=new double[3]; spectrumRetry=now+2; sourceError="Audio spectrum unavailable - retrying"; }
            }
            UpdateScreenCapture(now,UsesMode(22));
            string statusText=sourceError.Length>0?sourceError:"Running - "+Effects.Names[mode];
            if(status.Text!=statusText) status.Text=statusText;
            double keyHistoryLife=Math.Max(30,WaveLifetime());
            colorKeyWaves.RemoveAll(key=>now-key.time>keyHistoryLife);
            int reactiveMode=(mode==23)?0:Effects.ReactiveMode(mode,rippleOverlay.Checked);
            if(mode==34 || mode==36 || mode==39 || secondaryMode==34 || secondaryMode==36 || secondaryMode==39 || mode>=4 && secondaryMode>=0 && secondaryMode<4) reactiveMode=-1;
            var rgb=new byte[12];
            for(int z=0;z<4;z++) {
                Color ambient=Ambient(mode,z,now,pace,audioLevel,0,dt);
                if(secondaryMode>=0) ambient=Blend(ambient,Ambient(secondaryMode,z,now,pace,audioLevel,1,dt),mix.Value);
                double red=ambient.R,green=ambient.G,blue=ambient.B;
                foreach(var wave in waves) {
                    if(UsesMode(22) && pureScreen.Checked) continue;
                    if(!wave.click && (reactiveMode<0 || mode<4)) continue;
                    double age=now-wave.time;
                    var waveDetails=Details(wave.settingsMode>=0?wave.settingsMode:mode,wave.settingsLayer);
                    double power=DetailedWave(wave.click?(wave.kind==3?1:wave.kind):reactiveMode,wave.zone,z,age,(int)(speed.Value*100.0/waveDetails.Speed),waveDetails);
                    Color tint=wave.settingsMode==24 && !wave.tint.IsEmpty?wave.tint:waveDetails.CustomColor?(waveDetails.Rainbow?Ripple.Rainbow(wave.time*.23+z*.18+age*.35):waveDetails.Color):!wave.tint.IsEmpty?wave.tint:rainbow.Checked?Ripple.Rainbow(wave.time*.23+z*.18+age*.35):color;
                    power*=waveDetails.Brightness/100.0;
                    red+=tint.R*power; green+=tint.G*power; blue+=tint.B*power;
                }
                Color baseLight=Color.FromArgb((int)Math.Min(255,red),(int)Math.Min(255,green),(int)Math.Min(255,blue));
                baseLight=UtilityOverlay(baseLight,z,now,dt);
                Color indicated=indicators.Overlay(baseLight,z,now); red=indicated.R; green=indicated.G; blue=indicated.B;
                double gain=brightness.Value/100.0;
                rgb[z*3]=(byte)(Math.Min(255,red)*gain);
                rgb[z*3+1]=(byte)(Math.Min(255,green)*gain);
                rgb[z*3+2]=(byte)(Math.Min(255,blue)*gain);
                preview[z].BackColor=Color.FromArgb(rgb[z*3],rgb[z*3+1],rgb[z*3+2]);
            }
            bool changed=previous==null; for(int i=0;i<12 && !changed;i++) changed=rgb[i]!=previous[i];
            if(changed) { keyboard.Write(rgb); previous=rgb; }
        } catch(Exception ex) { Stop(); if(testing) throw; MessageBox.Show(ex.Message,"Lighting stopped"); }
    }
    public static void ScreenTest() {
        using(var test=new Bitmap(96,54)) using(var g=Graphics.FromImage(test)) {
            Color[] expected={Color.Red,Color.Lime,Color.Blue,Color.White};
            for(int z=0;z<4;z++) using(var brush=new SolidBrush(expected[z])) g.FillRectangle(brush,z*24,0,24,54);
            Color[] actual=DominantScreen(test);
            for(int z=0;z<4;z++) if(actual[z].ToArgb()!=expected[z].ToArgb()) throw new Exception("Screen quarter color mismatch");
            g.Clear(Color.Black);
            g.FillRectangle(Brushes.Red,0,0,96,27);
            actual=AverageScreen(test);
            foreach(Color c in actual) if(c.R!=127 || c.G!=0 || c.B!=0) throw new Exception("Full-height averaging failed");
            actual=DominantScreen(test);
            foreach(Color c in actual) if(c.R!=255 || c.G!=0 || c.B!=0) throw new Exception("Black background diluted dominant red");
            g.Clear(Color.White); g.FillRectangle(Brushes.Blue,0,0,96,18);
            actual=DominantScreen(test);
            foreach(Color c in actual) if(c.B!=255 || c.R!=0 || c.G!=0) throw new Exception("White background diluted dominant blue");
            actual=DominantScreen(test,true,false);
            foreach(Color c in actual) if(c.ToArgb()!=Color.White.ToArgb()) throw new Exception("Include-neutral option ignored");
            g.Clear(Color.Black); g.FillRectangle(Brushes.Red,0,0,96,5);
            actual=DominantScreen(test,false,true);
            foreach(Color c in actual) if(c.ToArgb()!=Color.Black.ToArgb()) throw new Exception("Include-black option ignored");
            g.Clear(Color.Gray); actual=DominantScreen(test);
            foreach(Color c in actual) if(c.ToArgb()!=Color.Gray.ToArgb()) throw new Exception("Neutral screen changed hue");
            g.Clear(Color.Black); actual=DominantScreen(test);
            foreach(Color c in actual) if(c.ToArgb()!=Color.Black.ToArgb()) throw new Exception("Black screen was brightened");
        }
        using(var sample=new Bitmap(96,54)) using(var g=Graphics.FromImage(sample)) {
            Color dim=Color.FromArgb(48,82,110); g.Clear(dim);
            foreach(Color c in DominantScreen(sample,true,true,false)) if(c.ToArgb()!=dim.ToArgb()) throw new Exception("Accurate mode altered RGB");
            foreach(Color c in DominantScreen(sample,true,true,true)) if(c.ToArgb()==dim.ToArgb()) throw new Exception("Vivid mode did not boost color");
            Color[] previous={Color.Red,Color.Red,Color.Red,Color.Red};
            for(int z=0;z<4;z++) { g.FillRectangle(Brushes.Red,z*24,0,12,54); g.FillRectangle(Brushes.Blue,z*24+12,0,12,54); }
            foreach(Color c in DominantScreen(sample,true,true,false,previous)) if(c.R!=255) throw new Exception("Tied colors flickered");
            g.Clear(Color.Blue);
            foreach(Color c in DominantScreen(sample,true,true,false,previous)) if(c.B!=255 || c.R!=0) throw new Exception("Scene cut retained stale color");
        }
        using(var sampler=new ScreenSampler()) {
            var config=new EffectDetails();
            for(int i=0;i<3;i++) if(sampler.Capture(Screen.PrimaryScreen.Bounds,config,null).Length!=4) throw new Exception("Invalid reusable capture");
            if(sampler.BufferAllocations!=1) throw new Exception("Screen buffers not reused");
            if(sampler.Capture(Screen.PrimaryScreen.Bounds,config,config).Length!=8) throw new Exception("Independent screen layers missing");
            Console.WriteLine("PASS: accurate RGB, vivid boost, stable tied colors, immediate scene cuts, reused capture buffers; latest capture "+(sampler.LastCaptureSeconds*1000).ToString("F1")+" ms");
        }
        var watch=Stopwatch.StartNew();
        for(int i=0;i<3;i++) {
            Color[] colors=CaptureScreen(Screen.PrimaryScreen.Bounds);
            if(colors.Length!=4) throw new Exception("Invalid screen sample");
        }
        Console.WriteLine("PASS: dominant quarter colors, neutral backgrounds, full-height coverage; three full-screen captures in "+watch.ElapsedMilliseconds+" ms");
    }
    public void SmokeTest() {
        testing=true;
        Show(); Start(); timer.Stop(); TestAppearance(); TestUnifiedControls(); GroupedEffectCombo.Test(); TestNewEffects(); TestUtilityIndicators();
        if(keyboard==null) throw new Exception("RGB device was not opened");
        try {
            TabControl tabs=FindStudioTabs(this);
            if(tabs!=null && !tabs.TabPages[0].Contains(secondary)) throw new Exception("Second effect is not in Lighting");
            if(tabs==null || tabs.TabPages.Count!=3 || tabs.TabPages[1].Text!="Effect settings") throw new Exception("Three-tab layout not applied");
            ShowGeneralSettings(); Application.DoEvents(); settingsWindow.Close(); Application.DoEvents();
            if(settingsWindow.IsDisposed || settingsWindow.Visible) throw new Exception("Settings window did not hide for reuse");
            if(pauseLock.Parent.FindForm()!=settingsWindow || secondary.Parent.FindForm()!=this) throw new Exception("Settings controls moved incorrectly");
            Activate();
            Console.WriteLine("PASS: three tabs, merged effect controls and reusable general settings window.");
            IndicatorState.Test(); SystemHealth.TestDetectors(); DetailTests();
            AdvancedTest();
            var original=Snapshot("Test original");
            RestoreProfile(new LightingProfile { Effect=21,Delay=999,Brightness=-10,KeyRipple=false });
            if(activeMode!=21 || speed.Value!=300 || brightness.Value!=10 || rippleOverlay.Checked) throw new Exception("Profile restore failed");
            RestoreProfile(original);
            SetupTray(); HideToTray();
            if(Visible || !tray.Visible || keyboard==null) throw new Exception("Tray hide stopped lighting");
            ShowWindow(); if(!Visible) throw new Exception("Tray restore failed");
            Console.WriteLine("PASS: profile restore, range validation, tray hide/show without stopping lighting.");
            Activate(); effect.Focus(); effect.DroppedDown=true;
            int originalMode=activeMode;
            for(int index=0;index<Effects.Names.Length;index++) {
                effect.SelectedIndex=index; Frame(); Application.DoEvents();
                if(activeMode!=originalMode || !effect.DroppedDown) throw new Exception("Effect popup changed or closed during browsing");
            }
            int finalChoice=(originalMode+1)%Effects.Names.Length;
            effect.SelectedIndex=finalChoice; effect.DroppedDown=false; Application.DoEvents();
            if(activeMode!=finalChoice || description.Text!=Describe(finalChoice)) throw new Exception("Closed selection did not match active effect");
            effect.SelectedIndex=originalMode;
            Console.WriteLine("PASS: popup stayed open while browsing all "+Effects.Names.Length+" effects.");
            for(int mode=0;mode<Effects.Names.Length;mode++) {
                effect.SelectedIndex=mode;
                double now=clock.Elapsed.TotalSeconds;
                AddWave(1,now-.12,0,Color.Empty);
                AddWave(2,now-.2,3,Effects.KeyColor(13));
                if(mode==20) { down.Add(65); holdStart=now-1; }
                if(mode==16) { scrollTime=now; scrollPhase=.5; }
                if(mode==23) activity=now-10;
                for(int frame=0;frame<5;frame++) { Frame(); Application.DoEvents(); System.Threading.Thread.Sleep(35); }
                if(mode==22 && screenCapture!=null) {
                    if(!screenCapture.Wait(10000)) throw new Exception("Screen capture timed out");
                    UpdateScreenCapture(clock.Elapsed.TotalSeconds,true);
                }
                if(sourceError.Length>0) throw new Exception(sourceError);
                down.Clear();
            }
            effect.SelectedIndex=13; secondary.SelectedIndex=0; AdvancedChanged();
            waves.Clear(); color=Color.White; brightness.Value=50;
            foreach(string kind in new string[]{"Volume","Screen brightness","Muted","Charger connected","Low battery","Internet disconnected","Internet restored","High temperature"}) {
                indicators.Current=null;
                indicators.Show(kind,.65,Color.Gold,1,clock.Elapsed.TotalSeconds,2);
                Frame(); if(previous==null) throw new Exception("Indicator not sent to hardware");
            }
            indicators.Current.Start=clock.Elapsed.TotalSeconds-3; Frame();
            for(int i=0;i<12;i++) if(previous[i]!=127) throw new Exception("Previous effect did not return after indicator; mode="+activeMode+", selection="+effect.SelectedIndex+", secondary="+secondaryMode+", RGB="+string.Join(",",previous));
            Console.WriteLine("PASS: eight indicators sent to RGB hardware; original effect restored after expiry.");
            Console.WriteLine("PASS: rendered and sent all "+Effects.Names.Length+" effects to RGB hardware.");
        } finally { Stop(); Close(); }
    }
    void Stop() {
        desiredRunning=false; ResetIndicators();
        timer.Stop(); if(hook!=IntPtr.Zero) { Native.UnhookWindowsHookEx(hook); hook=IntPtr.Zero; }
        if(mouseHook!=IntPtr.Zero) { Native.UnhookWindowsHookEx(mouseHook); mouseHook=IntPtr.Zero; }
        if(keyboard!=null) { keyboard.Dispose(); keyboard=null; }
        audio.Dispose(); spectrum.Dispose(); dragging=false;
        waves.Clear(); colorKeyWaves.Clear(); bassHistory[0].Clear(); bassHistory[1].Clear(); down.Clear(); status.Text="Stopped - lighting restored (or steady white)";
    }
}

static class Program {
    [STAThread] static int Main(string[] args) {
        Native.SetProcessDPIAware();
        if(args.Length>0 && args[0]=="--self-test") {
            var p=Keyboard.Packet(new byte[]{1,2,3,4,5,6,7,8,9,10,11,12});
            if(p.Length!=33 || p[0]!=0xcc || p[4]!=2 || p[5]!=1 || p[16]!=12) return 1;
            if(Ripple.Zone('A')!=0 || Ripple.Zone('G')!=1 || Ripple.Zone('L')!=2 || Ripple.Zone(97)!=3) return 2;
            if(Ripple.Level(0,1,.1,.14)!=0 || Ripple.Level(0,1,.35,.14)<=0 || Ripple.Level(0,1,2,.14)!=0) return 3;
            if(Ripple.Rainbow(0).ToArgb()!=Color.Red.ToArgb() || Ripple.Rainbow(1).ToArgb()!=Ripple.Rainbow(0).ToArgb() || Ripple.Rainbow(1.0/3).G!=255 || Ripple.Rainbow(-.2).ToArgb()!=Ripple.Rainbow(.8).ToArgb()) return 4;
            if(Effects.ReactivePower(1,0,3,0,140)!=1 || Effects.ReactivePower(1,0,3,1,140)!=0) return 31;
            if(Effects.ReactivePower(2,3,0,.05,140)!=0 || Effects.ReactivePower(2,3,0,.2,140)<=0) return 32;
            if(Effects.ReactivePower(3,2,1,.1,140)!=0 || Effects.ReactivePower(3,2,2,.1,140)<=0) return 33;
            for(int mode=4;mode<14;mode++) {
                bool lit=false,animated=false; Color first=Effects.Sample(mode,0,0,Color.Cyan,true);
                for(int frame=0;frame<300;frame++) for(int zone=0;zone<4;zone++) {
                    Color sample=Effects.Sample(mode,zone,frame/30.0,Color.Cyan,true);
                    lit|=sample.R+sample.G+sample.B>0;
                    if(zone==0 && sample.ToArgb()!=first.ToArgb()) animated=true;
                }
                if(!lit || (mode!=13 && !animated)) return 10+mode;
            }
            if(Effects.Sample(13,2,5,Color.Cyan,false).ToArgb()!=Color.Cyan.ToArgb() || Effects.Sample(0,0,1,Color.Red,true).ToArgb()!=Color.Black.ToArgb()) return 30;
            if(Effects.Follow(0,0,1,Color.Red,false).R!=255 || Effects.Follow(3,0,1,Color.Red,false).R!=0 || Effects.Follow(2,2,0,Color.Red,false).R!=0) return 34;
            if(Effects.Follow(1,1.5,1,Color.Cyan,false).ToArgb()!=Effects.Follow(2,1.5,1,Color.Cyan,false).ToArgb()) return 35;
            if(Effects.ReactiveMode(14,true)!=0 || Effects.ReactiveMode(14,false)!=-1 || Effects.ReactiveMode(0,true)!=0 || Effects.ReactiveMode(2,true)!=2) return 36;
            if(Effects.PointerZone(-100,-100,400)!=0 || Effects.PointerZone(0,-100,400)!=1 || Effects.PointerZone(299,-100,400)!=3 || Effects.PointerZone(999,-100,400)!=3) return 37;
            if(Effects.Names.Length!=42 || Effects.KeyColor(65)!=Color.DeepSkyBlue || Effects.KeyColor(49)!=Color.Orange || Effects.KeyColor(13)!=Color.White) return 38;
            if(Effects.AudioColor(0,0,1).ToArgb()!=Color.Black.ToArgb() || Effects.AudioColor(0,1,1).ToArgb()==Color.Black.ToArgb()) return 39;
            foreach(int mode in new int[]{18,19}) {
                bool lit=false,changed=false; Color initial=Effects.Sample(mode,0,0,Color.Red,false);
                for(int i=0;i<120;i++) { Color c=Effects.Sample(mode,0,i/30.0,Color.Red,false); lit|=c.R>0; changed|=c.ToArgb()!=initial.ToArgb(); }
                if(!lit || !changed) return 40+mode;
            }
            Console.WriteLine("PASS: HID report layout, key zones, wave timing."); return 0;
        }
        if(args.Length>0 && args[0]=="--health-test") {
            try {
                SystemHealth.TestDetectors();
                bool? internet=SystemHealth.Internet(); var heat=SystemHealth.Temperature();
                Console.WriteLine("PASS: network transitions, unknown state, heat debounce, hysteresis, priority and reminders.");
                Console.WriteLine("Windows Internet="+(internet.HasValue?internet.Value.ToString():"unavailable"));
                Console.WriteLine("CPU="+(heat.CPU.HasValue?heat.CPU.Value.ToString("F1")+" C":"unavailable")+"; GPU="+(heat.GPU.HasValue?heat.GPU.Value.ToString("F1")+" C ("+heat.GPUProvider+")":"unavailable")); return 0;
            } catch(Exception ex) { Console.Error.WriteLine(ex); return 67; }
        }
        if(args.Length>0 && args[0]=="--indicators-test") {
            try {
                IndicatorState.Test();
                var volume=SystemIndicators.Volume(); int? light=SystemIndicators.ScreenBrightness();
                Console.WriteLine("PASS: indicator baselines, bars, expiry, mute, charger, threshold, hysteresis, priority.");
                Console.WriteLine("Windows volume="+Math.Round(volume.Level*100)+"%, muted="+volume.Muted+"; screen brightness="+(light.HasValue?light.Value+"%":"unsupported")); return 0;
            } catch(Exception ex) { Console.Error.WriteLine(ex); return 66; }
        }
        if(args.Length>0 && args[0]=="--spectrum-test") {
            try {
                AudioSpectrum.Test();
                using(var capture=new AudioSpectrum()) for(int i=0;i<12;i++) { capture.Read(); System.Threading.Thread.Sleep(35); }
                Console.WriteLine("PASS: synthetic bass/mid/treble and actual WASAPI loopback."); return 0;
            } catch(Exception ex) { Console.Error.WriteLine(ex); return 65; }
        }
        if(args.Length>0 && args[0]=="--appearance-preview") {
            try { Application.EnableVisualStyles(); using(var form=new RippleForm(true)) form.AppearancePreviews(); return 0; }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 67; }
        }
        if(args.Length>0 && args[0]=="--utility-test") {
            try { UtilitySources.TestActualSources(); return 0; }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 66; }
        }
        if(args.Length>0 && args[0]=="--settings-test") {
            string dir=Path.Combine(Path.GetTempPath(),"LoqRipple-test-"+Guid.NewGuid().ToString("N"));
            string path=Path.Combine(dir,"settings.xml");
            try {
                var state=new UserSettings(); state.Current=new LightingProfile { Effect=22,Brightness=42,ColorArgb=Color.Gold.ToArgb(),KeyRipple=false,Secondary=14,Mix=37,ScreenRefresh=1100,Saturation=125,Monitor="test-display" };
                state.Current.PrimaryDetails.Add(new EffectDetails { WaveWidth=900,Repeats=3,ColorArgb=Color.Gold.ToArgb(),ScreenStyle=ScreenColorStyle.Vivid });
                state.Current.SecondaryDetails.Add(new EffectDetails { BassGain=180,Attack=100 });
                state.MicIndicator=state.CapsIndicator=state.LanguageIndicator=state.BatteryOnDemand=true;
                state.FocusIndicator=state.CpuIndicator=state.ChargingIndicator=state.IdleIndicator=true;
                state.Appearance=AppAppearance.Dark;
                state.FocusMinutes=37; state.IdleSeconds=120;
                state.NotifyInternet=false; state.NotifyHeat=false; state.HeatThreshold=95;
                state.NotifyVolume=false; state.NotifyBrightness=false; state.NoticeDuration=4; state.BatteryThreshold=15;
                state.Profiles.Add(new LightingProfile { Name="My Music",Effect=21 }); state.Save(path);
                state.Current.Brightness=63; state.Save(path);
                var loaded=UserSettings.Load(path);
                if(loaded.Current.PrimaryDetails[0].ScreenStyle!=ScreenColorStyle.Vivid || loaded.Current.PrimaryDetails[0].WaveWidth!=900 || loaded.Current.PrimaryDetails[0].Repeats!=3 || loaded.Current.SecondaryDetails[0].BassGain!=180 || loaded.Current.Brightness!=63 || loaded.Current.Effect!=22 || loaded.Current.KeyRipple || loaded.Current.ColorArgb!=Color.Gold.ToArgb() || loaded.Profiles[0].Name!="My Music" || loaded.Current.Secondary!=14 || loaded.Current.Mix!=37 || loaded.Current.ScreenRefresh!=1100 || loaded.Current.Saturation!=125 || loaded.Current.Monitor!="test-display" || loaded.NotifyInternet || loaded.NotifyHeat || loaded.HeatThreshold!=95 || loaded.NotifyVolume || loaded.NotifyBrightness || loaded.NoticeDuration!=4 || loaded.BatteryThreshold!=15) return 63;
                if(loaded.Appearance!=AppAppearance.Dark || !loaded.MicIndicator || !loaded.CapsIndicator || !loaded.LanguageIndicator || !loaded.BatteryOnDemand || !loaded.FocusIndicator || !loaded.CpuIndicator || !loaded.ChargingIndicator || !loaded.IdleIndicator || loaded.FocusMinutes!=37 || loaded.IdleSeconds!=120) throw new Exception("Utility settings did not persist");
                File.WriteAllText(path,"invalid XML"); if(UserSettings.Load(path).Current.Effect!=14) return 64;
                Console.WriteLine("PASS: settings roundtrip, replacement, custom profiles, corrupt-file fallback."); return 0;
            } finally { if(Directory.Exists(dir)) Directory.Delete(dir,true); }
        }
        if(args.Length>0 && args[0]=="--screen-test") {
            try { RippleForm.ScreenTest(); return 0; }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 62; }
        }
        if(args.Length>0 && args[0]=="--smoke-test") {
            try { Application.EnableVisualStyles(); using(var form=new RippleForm(true)) form.SmokeTest(); return 0; }
            catch(Exception ex) { Console.Error.WriteLine(ex); return 61; }
        }
        if(args.Length>0 && args[0]=="--sources-test") {
            try {
                using(var a=new AudioPeak()) Console.WriteLine("Audio meter opened; peak="+a.Read());
                using(var b=new Bitmap(1,1)) using(var g=Graphics.FromImage(b)) { g.CopyFromScreen(0,0,0,0,new Size(1,1)); Console.WriteLine("Screen sampling available"); }
                return 0;
            } catch(Exception ex) { Console.Error.WriteLine(ex); return 60; }
        }
        if(args.Length>0 && args[0]=="--probe") {
            try { using(var k=new Keyboard()) { Console.WriteLine("Compatible 048D:C993 RGB interface opened."); } return 0; }
            catch(Exception e) { Console.Error.WriteLine(e.Message); return 1; }
        }
        bool created;
        using(var mutex=new System.Threading.Mutex(true,"Local\\LoqKeyRipple",out created)) {
            if(!created) { MessageBox.Show("LOQ Key Ripple is already running."); return 1; }
            Application.EnableVisualStyles(); Application.Run(new RippleForm(false,Array.IndexOf(args,"--background")>=0));
        }
        return 0;
    }
}
