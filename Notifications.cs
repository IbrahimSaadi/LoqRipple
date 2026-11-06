using System;
using System.Drawing;
using System.Management;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

[ComImport, Guid("5CDF2C82-841E-4546-9722-0CF74078229A"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface IEndpointLevel {
    void RegisterControlChangeNotify(IntPtr callback);
    void UnregisterControlChangeNotify(IntPtr callback);
    void GetChannelCount(out uint count);
    void SetMasterVolumeLevel(float value,IntPtr context);
    void SetMasterVolumeLevelScalar(float value,IntPtr context);
    void GetMasterVolumeLevel(out float value);
    void GetMasterVolumeLevelScalar(out float value);
    void SetChannelVolumeLevel(uint channel,float value,IntPtr context);
    void SetChannelVolumeLevelScalar(uint channel,float value,IntPtr context);
    void GetChannelVolumeLevel(uint channel,out float value);
    void GetChannelVolumeLevelScalar(uint channel,out float value);
    void SetMute([MarshalAs(UnmanagedType.Bool)] bool value,IntPtr context);
    void GetMute([MarshalAs(UnmanagedType.Bool)] out bool value);
}
sealed class VolumeState { public float Level; public bool Muted; }
static class SystemIndicators {
    public static VolumeState Volume(int flow=0) {
        IMMEnumerator enumerator=null; IMMDevice device=null; IEndpointLevel endpoint=null;
        try {
            enumerator=(IMMEnumerator)new MMEnumerator(); enumerator.GetDefaultAudioEndpoint(flow,1,out device);
            Guid id=typeof(IEndpointLevel).GUID; object obj; device.Activate(ref id,23,IntPtr.Zero,out obj); endpoint=(IEndpointLevel)obj;
            float level; bool muted; endpoint.GetMasterVolumeLevelScalar(out level); endpoint.GetMute(out muted);
            return new VolumeState { Level=level,Muted=muted };
        } finally {
            if(endpoint!=null) Marshal.ReleaseComObject(endpoint);
            if(device!=null) Marshal.ReleaseComObject(device);
            if(enumerator!=null) Marshal.ReleaseComObject(enumerator);
        }
    }
    public static int? ScreenBrightness() {
        try {
            using(var query=new ManagementObjectSearcher("root\\WMI","SELECT CurrentBrightness FROM WmiMonitorBrightness WHERE Active=TRUE")) {
                query.Options.Timeout=TimeSpan.FromSeconds(2);
                using(var rows=query.Get()) foreach(ManagementObject row in rows) using(row) {
                    int value=Convert.ToInt32(row["CurrentBrightness"]); if(value>=0 && value<=100) return value;
                }
            }
        } catch(ManagementException) {} catch(COMException) {} catch(UnauthorizedAccessException) {}
        return null;
    }
}
sealed class IndicatorNotice {
    public string Kind; public double Level,FromLevel,TransitionStart,RevealStart,Start,Duration; public Color Color; public int Priority;
}
sealed partial class IndicatorState {
    public IndicatorNotice Current;
    int lastVolume=-1,lastBrightness=-1,lastAC=-1; bool lastMute,lowWarned;
    public void Reset() { ResetHealth(); Current=null; lastVolume=lastBrightness=lastAC=-1; lowWarned=false; }
    public void Show(string kind,double level,Color color,int priority,double now,double duration) {
        if(Current!=null && now<Current.Start+Current.Duration && Current.Priority>priority) return;
        bool continuation=Current!=null && Current.Kind==kind && now<Current.Start+Current.Duration;
        double from=continuation?PresentedLevel(Current,now):level;
        double reveal=continuation?Current.RevealStart:now;
        Current=new IndicatorNotice { Kind=kind,Level=Math.Max(0,Math.Min(1,level)),FromLevel=from,TransitionStart=now,RevealStart=reveal,Color=color,Priority=priority,Start=now,Duration=duration };
    }
    public void Volume(VolumeState sample,double now,double duration) {
        int value=(int)Math.Round(sample.Level*100);
        bool muteNotice=sample.Muted && value>0;
        if(lastVolume>=0 && (value!=lastVolume || sample.Muted!=lastMute)) {
            var previous=Current;
            bool continuing=previous!=null && previous.Kind=="Volume" && now<previous.Start+previous.Duration;
            Show(muteNotice?"Muted":"Volume",muteNotice?1:sample.Level,muteNotice?Color.Red:Color.DeepSkyBlue,1,now,duration);
            if(Current!=previous && !muteNotice && !continuing && !lastMute) Current.FromLevel=lastVolume/100.0;
        }
        lastVolume=value; lastMute=sample.Muted;
    }
    public void Brightness(int? value,double now,double duration) {
        if(!value.HasValue) { lastBrightness=-1; return; }
        if(lastBrightness>=0 && value.Value!=lastBrightness) {
            var previous=Current;
            bool continuing=previous!=null && previous.Kind=="Screen brightness" && now<previous.Start+previous.Duration;
            Show("Screen brightness",value.Value/100.0,Color.Gold,1,now,duration);
            if(Current!=previous && !continuing) Current.FromLevel=lastBrightness/100.0;
        }
        lastBrightness=value.Value;
    }
    public void Battery(int ac,int percent,int threshold,double now,double duration) {
        if(ac!=0 && ac!=1) { lastAC=-1; return; }
        if(lastAC>=0 && ac!=lastAC) Show(ac==1?"Charger connected":"Charger disconnected",1,ac==1?Color.LimeGreen:Color.Orange,3,now,duration);
        lastAC=ac;
        if(ac==1 || percent>threshold+5) lowWarned=false;
        if(ac==0 && percent>=0 && percent<=threshold && !lowWarned) {
            Show("Low battery",1,Color.OrangeRed,4,now,duration); lowWarned=true;
        }
    }
    public static double PresentedLevel(IndicatorNotice notice,double now) {
        double t=Math.Max(0,Math.Min(1,(now-notice.TransitionStart)/.18));
        t=t*t*(3-2*t);
        return notice.FromLevel+(notice.Level-notice.FromLevel)*t;
    }
    public Color Overlay(Color original,int zone,double now) {
        var n=Current; if(n==null || now>=n.Start+n.Duration || now<n.Start) return original;
        double age=now-n.Start;
        double level=(n.Kind=="Volume" || n.Kind=="Screen brightness" || n.Kind=="Battery level")?Math.Max(0,Math.Min(1,PresentedLevel(n,now)*4-zone)):1;
        if(n.Kind=="Muted") level=(age<.18 || age>=.35 && age<.53)?1:.03;
        if(n.Kind=="Low battery" || n.Kind=="High temperature" || n.Kind=="Focus complete") level=.25+.75*(.5+.5*Math.Sin(age*7));
        Color color=Effects.Scale(n.Color,level);
        double fade=Math.Max(0,Math.Min(1,(n.Duration-age)/.3));
        if(n.Kind=="Volume" || n.Kind=="Screen brightness") fade*=Math.Max(0,Math.Min(1,(now-n.RevealStart)/.10));
        return RippleForm.Blend(original,color,(int)(fade*100));
    }
    public static void Test() {
        var state=new IndicatorState();
        state.Volume(new VolumeState { Level=.5f },0,2); if(state.Current!=null) throw new Exception("Initial volume must not alert");
        state.Volume(new VolumeState { Level=.75f },1,2);
        if(state.Current.Kind!="Volume" || state.Overlay(Color.Black,2,1.15).B==0 || state.Overlay(Color.Black,3,1).ToArgb()!=Color.Black.ToArgb()) throw new Exception("Volume bar incorrect");
        if(state.Overlay(Color.Purple,0,3).ToArgb()!=Color.Purple.ToArgb()) throw new Exception("Effect did not return after notice");
        state.Volume(new VolumeState { Level=.75f,Muted=true },4,2); if(state.Current.Kind!="Muted") throw new Exception("Mute change missed");
        state.Volume(new VolumeState { Level=0,Muted=true },4.2,2);
        if(state.Current.Kind!="Volume" || state.Current.Level!=0) throw new Exception("Zero volume produced mute notice");
        for(int zone=0;zone<4;zone++) if(state.Overlay(Color.White,zone,4.4).ToArgb()!=Color.Black.ToArgb()) throw new Exception("Zero volume bar is not dark");
        state.Volume(new VolumeState { Level=0,Muted=false },4.3,2);
        if(state.Current.Kind!="Volume") throw new Exception("Unmuted zero volume produced mute notice");
        state.Reset(); state.Volume(new VolumeState { Level=.2f },0,2); state.Volume(new VolumeState { Level=.8f },1,2);
        double halfway=PresentedLevel(state.Current,1.09);
        if(halfway<=.2 || halfway>=.8) throw new Exception("Volume did not interpolate");
        state.Volume(new VolumeState { Level=.4f },1.09,2);
        if(Math.Abs(PresentedLevel(state.Current,1.09)-halfway)>.0001) throw new Exception("Repeated volume update jumped");
        if(Math.Abs(PresentedLevel(state.Current,1.4)-.4)>.0001) throw new Exception("Volume did not settle");
        state.Reset(); state.Brightness(20,0,2); state.Brightness(80,1,2);
        if(PresentedLevel(state.Current,1.09)<=.2 || PresentedLevel(state.Current,1.09)>=.8) throw new Exception("Brightness did not interpolate");
        state.Reset(); state.Brightness(50,0,2); state.Brightness(65,1,2); if(state.Current.Kind!="Screen brightness") throw new Exception("Brightness change missed");
        state.Reset(); state.Battery(1,50,20,0,2); state.Battery(0,50,20,1,2); if(state.Current.Kind!="Charger disconnected") throw new Exception("Charger change missed");
        state.Battery(0,20,20,4,2); if(state.Current.Kind!="Low battery") throw new Exception("Low battery missed");
        state.Volume(new VolumeState { Level=.5f },4.1,2); state.Volume(new VolumeState { Level=.9f },4.2,2); if(state.Current.Kind!="Low battery") throw new Exception("Priority overwritten");
        state.Battery(0,19,20,8,2); if(state.Current.Start!=4) throw new Exception("Battery warning repeated");
        state.Battery(1,19,20,10,2); state.Battery(0,18,20,13,2); if(state.Current.Kind!="Low battery" || state.Current.Start!=13) throw new Exception("Battery warning did not rearm");
    }
}

sealed partial class RippleForm {
    readonly CheckBox notifyVolume=new CheckBox { Text="Volume changes and mute",Checked=true,AutoSize=true };
    readonly CheckBox notifyBrightness=new CheckBox { Text="Screen brightness changes",Checked=true,AutoSize=true };
    readonly CheckBox notifyBattery=new CheckBox { Text="Low battery and charger changes",Checked=true,AutoSize=true };
    readonly NumericUpDown noticeDuration=new NumericUpDown { Minimum=1,Maximum=5,Value=2,Width=100 };
    readonly NumericUpDown batteryThreshold=new NumericUpDown { Minimum=5,Maximum=50,Value=20,Increment=5,Width=100 };
    readonly Label notificationSources=new Label { AutoSize=false,Size=new Size(350,65),Text="Sources are checked while lighting is running." };
    readonly IndicatorState indicators=new IndicatorState();
    Task<VolumeState> volumeRead; Task<int?> brightnessRead;
    double nextVolume,nextBrightness; int noticeGeneration,volumeGeneration,brightnessGeneration;
    string volumeSource="waiting",brightnessSource="waiting";
    Control BuildNotifications() {
        var panel=new FlowLayoutPanel { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18) };
        panel.Controls.Add(new Label { AutoSize=true,MaximumSize=new Size(350,0),Text="Temporary indicators appear over the current effect, then return to it automatically. They respect the Brightness limit and manual Stop." });
        panel.Controls.Add(notifyVolume); panel.Controls.Add(notifyBrightness); panel.Controls.Add(notifyBattery);
        panel.Controls.Add(new Label { Text="Indicator duration (seconds)",AutoSize=true }); panel.Controls.Add(noticeDuration);
        panel.Controls.Add(new Label { Text="Low battery warning at (%)",AutoSize=true }); panel.Controls.Add(batteryThreshold);
        panel.Controls.Add(notificationSources);
        AddHealthControls(panel);
        var previewNotice=new Button { Text="Preview indicators",AutoSize=true };
        int demo=0;
        previewNotice.Click+=delegate {
            if(keyboard==null) { MessageBox.Show("Start lighting first."); return; }
            string[] names={"Volume","Screen brightness","Muted","Charger connected","Low battery","Internet disconnected","Internet restored","High temperature"};
            Color[] colors={Color.DeepSkyBlue,Color.Gold,Color.Red,Color.LimeGreen,Color.OrangeRed,Color.Red,Color.LimeGreen,Color.OrangeRed};
            int i=demo++%names.Length; indicators.Current=null;
            indicators.Show(names[i],i<2?.65:1,colors[i],1,clock.Elapsed.TotalSeconds,(double)noticeDuration.Value);
        };
        panel.Controls.Add(previewNotice);
        foreach(var check in new CheckBox[]{notifyVolume,notifyBrightness,notifyBattery}) check.CheckedChanged+=delegate { ResetIndicators(); QueueSave(); };
        noticeDuration.ValueChanged+=delegate { QueueSave(); };
        batteryThreshold.ValueChanged+=delegate { ResetIndicators(); QueueSave(); };
        return panel;
    }
    void ResetIndicators() { noticeGeneration++; ResetUtilitySources(); indicators.Reset(); nextVolume=nextBrightness=nextInternet=nextTemperature=0; }
    void PollIndicators(double now) {
        bool active=keyboard!=null;
        PollHealth(now,active); PollUtility(now,active);
        if(volumeRead!=null && volumeRead.IsCompleted) {
            if(volumeRead.IsFaulted) { var error=volumeRead.Exception; volumeSource="unavailable; retrying"; }
            else if(active && notifyVolume.Checked && volumeGeneration==noticeGeneration) {
                indicators.Volume(volumeRead.Result,now,(double)noticeDuration.Value); volumeSource="available";
            }
            volumeRead=null;
        }
        if(brightnessRead!=null && brightnessRead.IsCompleted) {
            if(brightnessRead.IsFaulted) { var error=brightnessRead.Exception; brightnessSource="unavailable"; }
            else if(active && notifyBrightness.Checked && brightnessGeneration==noticeGeneration) {
                indicators.Brightness(brightnessRead.Result,now,(double)noticeDuration.Value);
                brightnessSource=brightnessRead.Result.HasValue?"available":"not supported by this display";
            }
            brightnessRead=null;
        }
        if(active && !testing) {
            if(notifyVolume.Checked && volumeRead==null && now>=nextVolume) {
                nextVolume=now+.10; volumeGeneration=noticeGeneration; volumeRead=Task.Factory.StartNew(()=>SystemIndicators.Volume());
            }
            if(notifyBrightness.Checked && brightnessRead==null && now>=nextBrightness) {
                nextBrightness=now+.25; brightnessGeneration=noticeGeneration; brightnessRead=Task.Factory.StartNew(()=>SystemIndicators.ScreenBrightness());
            }
        }
        string text="Windows volume: "+(notifyVolume.Checked?volumeSource:"disabled")+"\nScreen brightness: "+(notifyBrightness.Checked?brightnessSource:"disabled");
        if(notificationSources.Text!=text) notificationSources.Text=text;
    }
    void BatteryIndicator() {
        if(keyboard!=null && notifyBattery.Checked) indicators.Battery(power.AC,power.Percent,(int)batteryThreshold.Value,clock.Elapsed.TotalSeconds,(double)noticeDuration.Value);
    }
}
