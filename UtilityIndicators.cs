using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class UtilitySources {
    [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low,High; public ulong Value { get { return ((ulong)High<<32)|Low; } } }
    [StructLayout(LayoutKind.Sequential)] struct LastInput { public uint Size,Tick; }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FileTime idle,out FileTime kernel,out FileTime user);
    [DllImport("user32.dll")] static extern bool GetLastInputInfo(ref LastInput info);
    [DllImport("user32.dll")] static extern short GetKeyState(int key);
    [DllImport("user32.dll")] static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr window,IntPtr process);
    [DllImport("user32.dll")] static extern IntPtr GetKeyboardLayout(uint thread);
    ulong oldIdle,oldTotal; bool cpuReady;
    public static bool CapsLock() { return (GetKeyState(0x14)&1)!=0; }
    public static int Language() {
        IntPtr window=GetForegroundWindow(); if(window==IntPtr.Zero) return 0;
        uint thread=GetWindowThreadProcessId(window,IntPtr.Zero); if(thread==0) return 0;
        return (int)(GetKeyboardLayout(thread).ToInt64()&0xffff);
    }
    public static double? IdleSeconds() {
        var info=new LastInput { Size=(uint)Marshal.SizeOf(typeof(LastInput)) };
        if(!GetLastInputInfo(ref info)) return null;
        uint elapsed=unchecked((uint)Environment.TickCount-info.Tick);
        return elapsed/1000.0;
    }
    public static void TestActualSources() {
        var reader=new UtilitySources(); reader.Cpu(); System.Threading.Thread.Sleep(150);
        double? cpu=reader.Cpu(); double? idle=IdleSeconds();
        Console.WriteLine("CPU="+(cpu.HasValue?(cpu.Value*100).ToString("F1")+"%":"unavailable")+"; idle="+(idle.HasValue?idle.Value.ToString("F1")+"s":"unavailable")+"; layout=0x"+Language().ToString("X4")+"; caps="+CapsLock());
        try { Console.WriteLine("Default microphone muted="+SystemIndicators.Volume(1).Muted); }
        catch(COMException ex) { Console.WriteLine("Default microphone unavailable: 0x"+ex.ErrorCode.ToString("X8")); }
    }
    public void ResetCpu() { cpuReady=false; }
    public double? Cpu() {
        FileTime idle,kernel,user; if(!GetSystemTimes(out idle,out kernel,out user)) { cpuReady=false; return null; }
        ulong total=kernel.Value+user.Value; double? result=null;
        if(cpuReady && total>oldTotal && idle.Value>=oldIdle) result=CpuFraction(total-oldTotal,idle.Value-oldIdle);
        oldTotal=total; oldIdle=idle.Value; cpuReady=true; return result;
    }
    public static double CpuFraction(ulong total,ulong idle) { return total==0?0:Math.Max(0,Math.Min(1,1-idle/(double)total)); }
}

sealed partial class RippleForm {
    readonly CheckBox notifyMic=new CheckBox { Text="Default microphone mute · red zone 1",AutoSize=true };
    readonly CheckBox notifyCaps=new CheckBox { Text="Caps Lock · amber zone 4",AutoSize=true };
    readonly CheckBox notifyLanguage=new CheckBox { Text="Typing language change",AutoSize=true };
    readonly CheckBox batteryOnDemand=new CheckBox { Text="Battery shortcut · Ctrl + Alt + B",AutoSize=true };
    readonly CheckBox focusEnabled=new CheckBox { Text="Focus timer progress",AutoSize=true };
    readonly CheckBox cpuEnabled=new CheckBox { Text="CPU usage colors",AutoSize=true };
    readonly CheckBox chargingEnabled=new CheckBox { Text="Charging progress / full battery",AutoSize=true };
    readonly CheckBox idleEnabled=new CheckBox { Text="Dim lighting when idle",AutoSize=true };
    readonly NumericUpDown focusMinutes=new NumericUpDown { Minimum=1,Maximum=180,Value=25,Width=90 };
    readonly NumericUpDown idleAfter=new NumericUpDown { Minimum=10,Maximum=3600,Increment=10,Value=60,Width=100 };
    readonly Label utilitySources=new Label { AutoSize=true,MaximumSize=new Size(350,0),Text="Optional indicators are off until enabled." };
    readonly Label focusStatus=new Label { AutoSize=true,Text="Timer stopped" };
    readonly UtilitySources utilityReader=new UtilitySources();
    Task<bool> micRead;
    int micGeneration,utilityGeneration,lastLanguage; bool? micMuted;
    bool capsActive,batteryShortcutReady;
    double nextUtility,nextMic,nextCpu,idleElapsed,idleGain=1,cpuLevel,cpuTarget,focusEnd,focusLength;
    bool cpuAvailable;
    void LoadUtilitySettings() {
        notifyMic.Checked=settings.MicIndicator; notifyCaps.Checked=settings.CapsIndicator; notifyLanguage.Checked=settings.LanguageIndicator;
        batteryOnDemand.Checked=settings.BatteryOnDemand; focusEnabled.Checked=settings.FocusIndicator;
        cpuEnabled.Checked=settings.CpuIndicator; chargingEnabled.Checked=settings.ChargingIndicator; idleEnabled.Checked=settings.IdleIndicator;
        focusMinutes.Value=Math.Max(1,Math.Min(180,settings.FocusMinutes)); idleAfter.Value=Math.Max(10,Math.Min(3600,settings.IdleSeconds));
    }
    void SaveUtilitySettings() {
        settings.MicIndicator=notifyMic.Checked; settings.CapsIndicator=notifyCaps.Checked; settings.LanguageIndicator=notifyLanguage.Checked;
        settings.BatteryOnDemand=batteryOnDemand.Checked; settings.FocusIndicator=focusEnabled.Checked;
        settings.CpuIndicator=cpuEnabled.Checked; settings.ChargingIndicator=chargingEnabled.Checked; settings.IdleIndicator=idleEnabled.Checked;
        settings.FocusMinutes=(int)focusMinutes.Value; settings.IdleSeconds=(int)idleAfter.Value;
    }
    void BuildUtilityControls(FlowLayoutPanel sources,FlowLayoutPanel preferences) {
        Section(sources,"OPTIONAL STATUS LIGHTS");
        string[] titles={"Microphone mute","Caps Lock","Typing language","Battery on demand","Focus timer","CPU activity","Charging status","Idle dimming"};
        string[] descriptions={"Red marker in the first zone","Amber marker in the fourth zone","A brief color signal when switching","Show charge with Ctrl + Alt + B","Let the light count down with you","Colors reflect processor usage","Progress while charging, green when full","Soften the light when you step away"};
        int utilityIndex=0;
        foreach(var check in new[]{notifyMic,notifyCaps,notifyLanguage,batteryOnDemand,focusEnabled,cpuEnabled,chargingEnabled,idleEnabled}) {
            AddIndicatorSwitch(sources,check,titles[utilityIndex],descriptions[utilityIndex]); utilityIndex++;
            check.CheckedChanged+=delegate { ResetUtilitySources(); if(!focusEnabled.Checked) { focusEnd=0; focusStatus.Text="Timer stopped"; } if(check==batteryOnDemand && advancedReady && !testing && IsHandleCreated) UpdateBatteryShortcut(); QueueSave(); };
        }
        sources.Controls.Add(new Label { Text="Temporary alerts have priority. Timer progress overrides charging and CPU colors; mute and Caps Lock mark their own zones.",AutoSize=true,MaximumSize=new Size(330,0),Tag="muted" });
        sources.Controls.Add(utilitySources);
        AddIndicatorNumber(preferences,focusMinutes,"FOCUS SESSION · MINUTES");
        var row=new FlowLayoutPanel { Width=330,Height=46,WrapContents=false };
        var startFocus=new Button { Text="Start timer",Width=145,Height=36 };
        var cancelFocus=new Button { Text="Cancel timer",Width=145,Height=36 };
        startFocus.Click+=delegate { if(keyboard==null) { MessageBox.Show("Start lighting first."); return; } focusEnabled.Checked=true; focusLength=(double)focusMinutes.Value*60; focusEnd=clock.Elapsed.TotalSeconds+focusLength; };
        cancelFocus.Click+=delegate { focusEnd=0; focusStatus.Text="Timer stopped"; };
        row.Controls.Add(startFocus); row.Controls.Add(cancelFocus); preferences.Controls.Add(row); preferences.Controls.Add(focusStatus);
        AddIndicatorNumber(preferences,idleAfter,"DIM AFTER IDLE · SECONDS");
        preferences.Controls.Add(new Label { Text="Fades to 10% over five seconds and returns smoothly on input. CPU is measured across the system. Microphone status refers to the default Windows input device, not individual app mute buttons.",AutoSize=true,MaximumSize=new Size(330,0),Tag="muted" });
        focusMinutes.ValueChanged+=delegate { QueueSave(); }; idleAfter.ValueChanged+=delegate { QueueSave(); };
    }
    void ResetUtilitySources() { utilityGeneration++; utilityReader.ResetCpu(); micMuted=null; lastLanguage=0; capsActive=false; nextUtility=nextMic=nextCpu=0; cpuAvailable=false; idleElapsed=0; }
    void UpdateBatteryShortcut() {
        UnregisterHotKey(Handle,707); batteryShortcutReady=false;
        if(batteryOnDemand.Checked) batteryShortcutReady=RegisterHotKey(Handle,707,0x4003,66);
    }
    void ShowBatteryLevel(double now) {
        if(keyboard==null || !batteryOnDemand.Checked) return;
        GetSystemPowerStatus(out power);
        if(power.Percent>100) { utilitySources.Text="Battery level unavailable"; return; }
        Color color=power.Percent<=20?Color.OrangeRed:power.Percent<=50?Color.Gold:Color.LimeGreen;
        indicators.Show("Battery level",power.Percent/100.0,color,2,now,(double)noticeDuration.Value);
    }
    void ObserveLanguage(int language,double now) {
        if(language==0) { lastLanguage=0; return; }
        if(lastLanguage!=0 && language!=lastLanguage) {
            int primary=language&0x3ff;
            indicators.Show("Typing language",1,primary==1?Color.LimeGreen:primary==9?Color.DeepSkyBlue:Color.MediumPurple,1,now,(double)noticeDuration.Value);
        }
        lastLanguage=language;
    }
    void PollUtility(double now,bool active) {
        if(focusEnd>0) {
            double left=focusEnd-now;
            if(left<=0) { focusEnd=0; focusStatus.Text="Focus complete"; if(active) indicators.Show("Focus complete",1,Color.LimeGreen,3,now,3); }
            else focusStatus.Text="Remaining: "+TimeSpan.FromSeconds(left).ToString(@"hh\:mm\:ss");
        }
        if(micRead!=null && micRead.IsCompleted) {
            if(micRead.IsFaulted) { var observed=micRead.Exception; micMuted=null; }
            else if(active && notifyMic.Checked && micGeneration==utilityGeneration) micMuted=micRead.Result;
            micRead=null;
        }
        if(!active || testing) return;
        if(now>=nextUtility) {
            nextUtility=now+.25;
            capsActive=notifyCaps.Checked && UtilitySources.CapsLock();
            if(notifyLanguage.Checked) ObserveLanguage(UtilitySources.Language(),now);
            idleElapsed=idleEnabled.Checked?(UtilitySources.IdleSeconds()??0):0;
        }
        if(notifyMic.Checked && micRead==null && now>=nextMic) {
            nextMic=now+1; micGeneration=utilityGeneration;
            micRead=Task.Factory.StartNew(()=>SystemIndicators.Volume(1).Muted);
        }
        if(cpuEnabled.Checked && now>=nextCpu) {
            nextCpu=now+1; double? value=utilityReader.Cpu(); cpuAvailable=value.HasValue; cpuTarget=value??0;
        }
        string text="Microphone: "+(!notifyMic.Checked?"disabled":!micMuted.HasValue?"checking / unavailable":micMuted.Value?"muted":"unmuted")+
            "\nCPU: "+(!cpuEnabled.Checked?"disabled":cpuAvailable?(cpuTarget*100).ToString("F0")+"%":"checking")+
            "\nBattery shortcut: "+(!batteryOnDemand.Checked?"disabled":batteryShortcutReady?"Ctrl + Alt + B":"unavailable / not registered");
        if(utilitySources.Text!=text) utilitySources.Text=text;
    }
    static double FocusFraction(double now,double end,double duration) { return duration<=0?0:Math.Max(0,Math.Min(1,(end-now)/duration)); }
    static double IdleTarget(double elapsed,double after) { return 1-.9*Math.Max(0,Math.Min(1,(elapsed-after)/5)); }
    Color UtilityOverlay(Color original,int zone,double now,double dt) {
        if(zone==0) {
            cpuLevel=Ease(cpuLevel,cpuTarget,dt,400,400);
            idleGain=Ease(idleGain,idleEnabled.Checked?IdleTarget(idleElapsed,(double)idleAfter.Value):1,dt,180,180);
        }
        Color c=original;
        if(cpuEnabled.Checked && cpuAvailable) c=Blend(Color.LimeGreen,Color.OrangeRed,(int)(cpuLevel*100));
        if(chargingEnabled.Checked && power.AC==1 && power.Percent<=100) c=power.Percent==100?Color.LimeGreen:BatteryColor(zone,power.Percent/100.0,true,now);
        if(focusEnabled.Checked && focusEnd>0) c=Effects.Scale(Color.DeepSkyBlue,Math.Max(0,Math.Min(1,FocusFraction(now,focusEnd,focusLength)*4-zone)));
        if(notifyMic.Checked && micMuted==true && zone==0) c=Effects.Scale(Color.Red,.35);
        if(notifyCaps.Checked && capsActive && zone==3) c=Color.Gold;
        return Effects.Scale(c,idleGain);
    }
    void TestUtilityIndicators() {
        if(Math.Abs(UtilitySources.CpuFraction(100,25)-.75)>.001 || UtilitySources.CpuFraction(0,0)!=0 || UtilitySources.CpuFraction(10,20)!=0) throw new Exception("CPU load calculation failed");
        if(FocusFraction(0,60,60)!=1 || FocusFraction(30,60,60)!=.5 || FocusFraction(61,60,60)!=0) throw new Exception("Focus progress failed");
        if(IdleTarget(30,60)!=1 || Math.Abs(IdleTarget(65,60)-.1)>.001) throw new Exception("Idle dimming failed");
        indicators.Reset(); lastLanguage=0; ObserveLanguage(0x409,0); if(indicators.Current!=null) throw new Exception("Initial language must not alert");
        ObserveLanguage(0x401,1); if(indicators.Current==null || indicators.Current.Color!=Color.LimeGreen) throw new Exception("Arabic language notice failed");
        indicators.Reset(); lastLanguage=0;
        focusEnabled.Checked=true; focusLength=60; focusEnd=60;
        if(UtilityOverlay(Color.Red,3,30,.033).ToArgb()!=Color.Black.ToArgb()) throw new Exception("Focus zones did not count down");
        PollUtility(61,true);
        if(focusEnd!=0 || indicators.Current==null || indicators.Current.Kind!="Focus complete") throw new Exception("Focus completion notice failed");
        indicators.Current=null; PollUtility(62,true); if(indicators.Current!=null) throw new Exception("Focus completion repeated");
        focusEnabled.Checked=false;
        var oldPower=power; power.Percent=50; batteryOnDemand.Checked=true;
        // The same notice shape used by the battery hotkey fills exactly two zones at 50%.
        indicators.Show("Battery level",.5,Color.LimeGreen,2,100,2);
        if(indicators.Overlay(Color.Black,1,100.2).G==0 || indicators.Overlay(Color.White,2,100.2).ToArgb()!=Color.Black.ToArgb()) throw new Exception("On-demand battery bar failed");
        indicators.Reset(); batteryOnDemand.Checked=false;
        chargingEnabled.Checked=true; power.AC=1; power.Percent=100;
        if(UtilityOverlay(Color.Black,2,0,.033).ToArgb()!=Color.LimeGreen.ToArgb()) throw new Exception("Full battery status failed");
        chargingEnabled.Checked=false; power=oldPower;
        notifyMic.Checked=notifyCaps.Checked=true; micMuted=true; capsActive=true; idleGain=1;
        if(UtilityOverlay(Color.Black,0,0,.033).R==0 || UtilityOverlay(Color.Black,3,0,.033).G==0) throw new Exception("Mute / Caps state markers failed");
        notifyMic.Checked=notifyCaps.Checked=false; ResetUtilitySources();
        Console.WriteLine("PASS: CPU accounting, focus countdown/completion, idle fade, language, microphone/Caps markers, on-demand battery and full charging status.");
    }
}
