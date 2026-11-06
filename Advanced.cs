using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

sealed partial class RippleForm {
    readonly GroupedEffectCombo secondary=new GroupedEffectCombo { DropDownStyle=ComboBoxStyle.DropDownList,Width=320,MaxDropDownItems=12 };
    readonly StudioSlider mix=new StudioSlider { Minimum=0,Maximum=100,Value=40,Width=320,TickFrequency=10 };
    readonly StudioCombo monitor=new StudioCombo { DropDownStyle=ComboBoxStyle.DropDownList,Width=320 };
    readonly NumericUpDown refreshMs=new NumericUpDown { Minimum=100,Maximum=3000,Increment=50,Value=250,Width=120 };
    readonly StudioSlider saturation=new StudioSlider { Minimum=50,Maximum=180,Value=110,Width=320,TickFrequency=20 };
    readonly CheckBox pauseLock=new StudioSwitch { Text="Pause when locked / suspended",Checked=true,AutoSize=true };
    readonly CheckBox pauseSaver=new StudioSwitch { Text="Pause in Windows battery saver",Checked=true,AutoSize=true };
    readonly CheckBox autoStart=new StudioSwitch { Text="Start with Windows (in tray)",AutoSize=true };
    readonly Timer supervisor=new Timer { Interval=1000 };
    readonly AudioSpectrum spectrum=new AudioSpectrum();
    double[] audioBands=new double[3];
    double spectrumRetry, chargingPulse=-100;
    int secondaryMode=-1;
    bool locked,suspended,desiredRunning,advancedReady;
    bool startHidden;
    byte previousAC=255;
    const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    [StructLayout(LayoutKind.Sequential)] struct PowerInfo { public byte AC,Flags,Percent,Saver; public uint Life,FullLife; }
    [DllImport("kernel32.dll")] static extern bool GetSystemPowerStatus(out PowerInfo info);
    [DllImport("user32.dll")] static extern bool RegisterHotKey(IntPtr window,int id,uint modifiers,uint key);
    [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr window,int id);
    PowerInfo power;
    bool UsesMode(int mode) { return activeMode==mode || secondaryMode==mode; }
    string PauseReason() {
        if(pauseLock.Checked && (locked || suspended)) return "Paused - session locked / suspended";
        if(pauseSaver.Checked && power.Saver==1) return "Paused - battery saver";
        return null;
    }
    void CheckPower() {
        GetSystemPowerStatus(out power);
        if(previousAC!=255 && power.AC!=previousAC) chargingPulse=clock.Elapsed.TotalSeconds;
        previousAC=power.AC;
        BatteryIndicator(); PollIndicators(clock.Elapsed.TotalSeconds);
        string reason=PauseReason();
        if(reason!=null && desiredRunning) {
            if(keyboard!=null) { Stop(); desiredRunning=true; }
            status.Text=reason;
        } else if(reason==null && desiredRunning && keyboard==null) Start();
    }
    void SessionChanged(object sender,SessionSwitchEventArgs e) {
        if(IsDisposed || !IsHandleCreated) return;
        BeginInvoke((Action)delegate {
            if(e.Reason==SessionSwitchReason.SessionLock) locked=true;
            if(e.Reason==SessionSwitchReason.SessionUnlock) locked=false;
            CheckPower();
        });
    }
    void PowerChanged(object sender,PowerModeChangedEventArgs e) {
        if(IsDisposed || !IsHandleCreated) return;
        BeginInvoke((Action)delegate { if(e.Mode==PowerModes.Suspend) suspended=true; if(e.Mode==PowerModes.Resume) suspended=false; CheckPower(); });
    }
    void SetAutoStart() {
        if(loading || testing) return;
        try {
            using(var key=Registry.CurrentUser.CreateSubKey(RunKey)) {
                if(autoStart.Checked) key.SetValue("LoqRipple","\""+Application.ExecutablePath+"\" --background");
                else key.DeleteValue("LoqRipple",false);
            }
        } catch(Exception ex) {
            MessageBox.Show("Could not change startup: "+ex.Message);
            loading=true; autoStart.Checked=!autoStart.Checked; loading=false;
        }
    }
    FlowLayoutPanel mixOptions;
    TableLayoutPanel screenOptions;
    void UpdateContextControls() {
        if(mixOptions!=null) mixOptions.Visible=secondaryMode>=0;
        if(primaryControls!=null) primaryControls.RowStyles[7].Height=secondaryMode>=0?70:0;
        if(screenOptions!=null) screenOptions.Visible=UsesMode(22);
        pureScreen.Visible=UsesMode(22);
        if(lightingLayout!=null && lightingLayout.RowStyles.Count>1) lightingLayout.RowStyles[1].Height=UsesMode(22)?174:154;
    }
    void AdvancedChanged() {
        if(loading) return;
        secondaryMode=secondary.SelectedIndex-1;
        pureScreen.Enabled=UsesMode(22);
        spectrum.Dispose(); spectrumRetry=0; audio.Dispose(); audioRetry=0;
        sourceError=""; previous=null; UpdateContextControls(); RefreshDetails(); ResetDetailAnimation(); QueueSave();
    }
    Control BuildAdvanced() {
        var panel=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Width=370,Margin=Padding.Empty };
        panel.Controls.Add(new Label { Text="SECOND EFFECT · OPTIONAL",AutoSize=true });
        secondary.Populate(true); secondary.SelectedIndex=0;
        secondary.SelectionChangeCommitted+=delegate { AdvancedChanged(); }; panel.Controls.Add(secondary);
        mixOptions=new FlowLayoutPanel { AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,FlowDirection=FlowDirection.TopDown,WrapContents=false,Width=325,Margin=Padding.Empty };
        mixOptions.Controls.Add(new Label { Text="Second effect strength: 0% main / 100% second",AutoSize=true,MaximumSize=new Size(310,0) }); mixOptions.Controls.Add(mix);
        panel.Controls.Add(mixOptions);
        screenOptions=new TableLayoutPanel { Height=100,Width=800,ColumnCount=3,RowCount=2,Margin=Padding.Empty,Tag="whitepanel" };
        screenOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,40));
        screenOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25));
        screenOptions.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,35));
        screenOptions.RowStyles.Add(new RowStyle(SizeType.Absolute,28));
        screenOptions.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
        screenOptions.Controls.Add(new Label { Text="Screen monitor",AutoSize=true },0,0);
        screenOptions.Controls.Add(new Label { Text="Refresh interval (ms)",AutoSize=true },1,0);
        screenOptions.Controls.Add(new Label { Text="Vivid saturation (%)",AutoSize=true },2,0);
        foreach(var screen in Screen.AllScreens) monitor.Items.Add(screen.DeviceName);
        monitor.SelectedIndex=0; monitor.Dock=DockStyle.Fill; screenOptions.Controls.Add(monitor,0,1);
        refreshMs.AccessibleName="Screen refresh interval in milliseconds"; screenOptions.Controls.Add(new StudioNumberView(refreshMs) { Dock=DockStyle.Fill },1,1);
        saturation.Dock=DockStyle.Fill; screenOptions.Controls.Add(saturation,2,1);
        panel.Controls.Add(screenOptions); UpdateContextControls();
        mix.ValueChanged+=delegate { QueueSave(); };
        monitor.SelectionChangeCommitted+=delegate { if(screenCapture==null) screenTime=-100; QueueSave(); };
        refreshMs.ValueChanged+=delegate { screenInterval=(double)refreshMs.Value/1000; QueueSave(); };
        saturation.ValueChanged+=delegate { if(screenCapture==null) screenTime=-100; QueueSave(); };
        return panel;
    }
    Form settingsWindow;
    void CreateGeneralSettings() {
        settingsWindow=new Form { Text="LOQ Key Ripple - Settings",ClientSize=new Size(480,560),Font=Font,Padding=new Padding(12),FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ShowInTaskbar=false,StartPosition=FormStartPosition.CenterParent };
        var panel=StudioCard("General settings","Startup, power behavior and keyboard shortcuts.");
        BuildAppearanceControls(panel);
        Section(panel,"POWER & STARTUP");
        panel.Controls.Add(pauseLock); panel.Controls.Add(pauseSaver); panel.Controls.Add(autoStart);
        panel.Controls.Add(new Label { AutoSize=true,MaximumSize=new Size(350,0),Text="Global shortcuts:\nCtrl + Alt + Left / Right: previous / next effect\nCtrl + Alt + Up / Down: brightness\nCtrl + Alt + F10: start / stop\nCtrl + Alt + F11: show / hide\nCtrl + Alt + F12: stop\n\nBattery shows charge by zone; Music Spectrum splits bass, mid and treble." });
        pauseLock.CheckedChanged+=delegate { QueueSave(); CheckPower(); };
        pauseSaver.CheckedChanged+=delegate { QueueSave(); CheckPower(); };
        autoStart.CheckedChanged+=delegate { SetAutoStart(); };
        settingsWindow.Controls.Add(panel);
        settingsWindow.FormClosing+=delegate(object sender,FormClosingEventArgs e) {
            if(e.CloseReason==CloseReason.UserClosing && !exitRequested) { e.Cancel=true; settingsWindow.Hide(); SaveSettings(); }
        };
    }
    void ShowGeneralSettings() {
        if(settingsWindow.Visible) settingsWindow.Activate(); else settingsWindow.Show(this);
        SetDarkTitle(settingsWindow);
    }
    Control BuildCombinedEffectSettings() {
        var outer=new Panel { Dock=DockStyle.Fill,Padding=new Padding(8) };
        outer.Controls.Add(BuildEffectDetails());
        screenOptions.Dock=DockStyle.Top; screenOptions.Padding=new Padding(16,8,16,8);
        outer.Controls.Add(screenOptions); UpdateContextControls(); return outer;
    }
    void InitAdvanced() {
        advancedReady=true;
        GetSystemPowerStatus(out power); previousAC=power.AC;
        if(testing) return;
        using(var key=Registry.CurrentUser.OpenSubKey(RunKey)) { loading=true; autoStart.Checked=key!=null && key.GetValue("LoqRipple")!=null; loading=false; }
        SystemEvents.SessionSwitch+=SessionChanged; SystemEvents.PowerModeChanged+=PowerChanged;
        supervisor.Tick+=delegate { CheckPower(); }; supervisor.Start();
        Shown+=delegate {
            uint[] keys={37,39,38,40,121,122,123};
            for(int i=0;i<keys.Length;i++) if(!RegisterHotKey(Handle,700+i,0x4003,keys[i])) status.Text="Some global shortcuts are in use";
            UpdateBatteryShortcut();
            if(startHidden) HideToTray();
        };
    }
    void DisposeAdvanced() {
        DisposeAppearance();
        supervisor.Stop(); supervisor.Dispose(); spectrum.Dispose();
        if(screenCapture!=null) screenCapture.ContinueWith(t=>{ var observed=t.Exception; screenSampler.Dispose(); });
        else screenSampler.Dispose();
        if(settingsWindow!=null) settingsWindow.Dispose();
        if(!testing) {
            SystemEvents.SessionSwitch-=SessionChanged; SystemEvents.PowerModeChanged-=PowerChanged;
            for(int i=0;i<8;i++) UnregisterHotKey(Handle,700+i);
        }
    }
    protected override void WndProc(ref Message m) {
        if(m.Msg==0x312 && advancedReady) {
            switch(m.WParam.ToInt32()) {
                case 700: effect.SelectedIndex=(activeMode+Effects.Names.Length-1)%Effects.Names.Length; break;
                case 701: effect.SelectedIndex=(activeMode+1)%Effects.Names.Length; break;
                case 702: brightness.Value=Math.Min(100,brightness.Value+5); break;
                case 703: brightness.Value=Math.Max(10,brightness.Value-5); break;
                case 704: if(desiredRunning) Stop(); else Start(); break;
                case 705: if(Visible) HideToTray(); else ShowWindow(); break;
                case 706: Stop(); break;
                case 707: ShowBatteryLevel(clock.Elapsed.TotalSeconds); break;
            }
        }
        base.WndProc(ref m);
        if(m.Msg==0x84 && FormBorderStyle==FormBorderStyle.None && WindowState==FormWindowState.Normal) {
            long point=m.LParam.ToInt64(); Point client=PointToClient(new Point((short)(point&0xffff),(short)((point>>16)&0xffff)));
            if(client.X>=ClientSize.Width-12 && client.Y>=ClientSize.Height-12) m.Result=new IntPtr(17);
        }
    }
    Rectangle CaptureBounds() {
        string name=monitor.SelectedItem as string;
        foreach(var screen in Screen.AllScreens) if(screen.DeviceName==name) return screen.Bounds;
        return Screen.PrimaryScreen.Bounds;
    }
    static Color BoostSaturation(Color c,int amount) {
        double gray=.2126*c.R+.7152*c.G+.0722*c.B, gain=amount/100.0;
        return Color.FromArgb((int)Math.Max(0,Math.Min(255,gray+(c.R-gray)*gain)),(int)Math.Max(0,Math.Min(255,gray+(c.G-gray)*gain)),(int)Math.Max(0,Math.Min(255,gray+(c.B-gray)*gain)));
    }
    internal static Color Blend(Color a,Color b,int percent) {
        double t=Math.Max(0,Math.Min(100,percent))/100.0;
        return Color.FromArgb((int)Math.Round(a.R*(1-t)+b.R*t),(int)Math.Round(a.G*(1-t)+b.G*t),(int)Math.Round(a.B*(1-t)+b.B*t));
    }
    static Color BatteryColor(int zone,double level,bool charging,double now) {
        Color c=level<.2?Color.OrangeRed:level<.5?Color.Gold:Color.LimeGreen;
        double strength=Math.Max(0,Math.Min(1,level*4-zone));
        if(charging) strength*=.55+.45*(.5+.5*Math.Sin(now*3));
        return Effects.Scale(c,strength);
    }
    Color Ambient(int mode,int z,double now,double pace,double audioLevel,int layer,double dt) {
        var d=Details(mode,layer); pace*=d.Speed/100.0;
        Color layerColor=d.CustomColor?d.Color:color; bool layerRainbow=d.CustomColor?d.Rainbow:rainbow.Checked;
        int mappedZone=d.Direction==WaveDirection.Left?3-z:z;
        Color c=Effects.Sample(mode,mappedZone,now*pace,layerColor,layerRainbow);
        Color tint=layerRainbow?Ripple.Rainbow(now*.12+mappedZone*.15):layerColor;
        if(mode==14) {
            if(z==0) {
                Rectangle screen=SystemInformation.VirtualScreen; Point mouse=Cursor.Position;
                double target=Math.Max(0,Math.Min(3,(mouse.X-screen.Left)*3.0/Math.Max(1,screen.Width-1)));
                pointerPositions[layer,0]=Ease(pointerPositions[layer,0],target,dt*d.Speed/100.0,d.FollowSmoothing,d.FollowSmoothing);
                double motion=rawPointerMotion*d.Sensitivity/100;
                pointerPositions[layer,1]=Ease(pointerPositions[layer,1],motion,dt*d.Speed/100.0,0,d.PointerFade);
            }
            double width=d.PointerWidth/100.0;
            double distance=(mappedZone-pointerPositions[layer,0])/width;
            c=Effects.Scale(tint,Math.Exp(-distance*distance/.48)*Math.Min(1,pointerPositions[layer,1]));
        }
        if(mode==16) c=Effects.Scale(layerRainbow?Ripple.Rainbow(z*.22-scrollPhase-(now-scrollTime)*scrollDirection*pace):layerColor,Math.Exp(-Math.Max(0,now-scrollTime)*1.8));
        if(mode==20 && down.Count>0) c=Effects.Scale(tint,Math.Max(0,Math.Min(1,(now-holdStart)*pace-z*.35)));
        if(mode==21) {
            double target=Math.Min(1,audioLevel*d.AudioGain/100);
            envelopes[layer,mode,z]=Ease(envelopes[layer,mode,z],target,dt*d.Speed/100.0,d.Attack,d.Release);
            c=d.CustomColor?Effects.Scale(tint,Math.Sqrt(envelopes[layer,mode,z])):layerRainbow?Effects.AudioColor(z,envelopes[layer,mode,z],now):Effects.Scale(layerColor,Math.Max(0,Math.Min(1,Math.Sqrt(envelopes[layer,mode,z])*4-z*.7)));
        }
        if(mode==40) {
            envelopes[layer,mode,z]=Ease(envelopes[layer,mode,z],Math.Min(1,audioLevel*d.AudioGain/100),dt,d.Attack,d.Release);
            if(z==0) musicRainbowPhase[layer]=(musicRainbowPhase[layer]+dt*pace*(.05+1.8*Math.Sqrt(envelopes[layer,mode,z])))%1;
            c=Ripple.Rainbow(mappedZone*.22-musicRainbowPhase[layer]);
        }
        if(mode==22) {
            Color target=(layer==0?screenColors:secondaryScreenColors)[mappedZone];
            Color old=acceptedScreens[layer,z];
            int difference=Math.Max(Math.Abs(target.R-old.R),Math.Max(Math.Abs(target.G-old.G),Math.Abs(target.B-old.B)));
            if(difference>=12*100.0/d.ScreenSensitivity) acceptedScreens[layer,z]=target;
            target=acceptedScreens[layer,z];
            int[] values={target.R,target.G,target.B};
            for(int channel=0;channel<3;channel++) screenEnvelope[layer,z,channel]=Ease(screenEnvelope[layer,z,channel],values[channel],dt,d.ScreenSmoothing,d.ScreenSmoothing);
            smoothScreens[layer,z]=Color.FromArgb((int)Math.Round(screenEnvelope[layer,z,0]),(int)Math.Round(screenEnvelope[layer,z,1]),(int)Math.Round(screenEnvelope[layer,z,2]));
            c=d.ScreenStyle==ScreenColorStyle.Accurate?smoothScreens[layer,z]:BoostSaturation(smoothScreens[layer,z],(int)saturation.Value);
        }
        if(mode==23 && now-activity>8) c=Effects.Sample(6,z,(now-activity-8)*pace,layerColor,layerRainbow);
        if(mode==25) {
            c=power.Percent>100?Color.DimGray:BatteryColor(z,power.Percent/100.0,power.AC==1,now);
            if(now-chargingPulse<1) c=Blend(c,Color.White,(int)(50*Math.Max(0,1-(now-chargingPulse))));
        }
        if(mode==26) {
            double band=z<3?audioBands[z]:(audioBands[0]+audioBands[1]+audioBands[2])/3;
            int[] gains={d.BassGain,d.MidGain,d.TrebleGain,(d.BassGain+d.MidGain+d.TrebleGain)/3};
            band=Math.Min(1,band*gains[z]/100.0);
            envelopes[layer,mode,z]=Ease(envelopes[layer,mode,z],band,dt*d.Speed/100.0,d.Attack,d.Release);
            Color[] colors={d.BassColor,d.MidColor,d.TrebleColor,Color.MediumPurple};
            c=Effects.Scale(colors[z],Math.Sqrt(envelopes[layer,mode,z])*1.8);
        }
        if(mode==34 || mode==35 || mode==36 || mode==39) c=InputEffect(mode,z,now,pace,layer,dt,d,tint,layerRainbow);
        if(mode>=0 && mode<4) {
            double r=0,g=0,b=0;
            foreach(var wave in waves) if(!wave.click) {
                double v=DetailedWave(mode,wave.zone,z,now-wave.time,(int)(speed.Value*100.0/d.Speed),d);
                Color wc=layerRainbow?Ripple.Rainbow(wave.time*.23+z*.18):layerColor;
                r+=wc.R*v;g+=wc.G*v;b+=wc.B*v;
            }
            c=Color.FromArgb((int)Math.Min(255,r),(int)Math.Min(255,g),(int)Math.Min(255,b));
        }
        return Effects.Scale(c,d.Brightness/100.0);
    }
    void AdvancedTest() {
        if(Blend(Color.Red,Color.Blue,0).ToArgb()!=Color.Red.ToArgb() || Blend(Color.Red,Color.Blue,100).ToArgb()!=Color.Blue.ToArgb()) throw new Exception("Blend endpoints failed");
        Color middle=Blend(Color.Red,Color.Blue,50); if(middle.R!=128 || middle.B!=128) throw new Exception("Blend weights failed");
        if(BatteryColor(3,.1,false,0).ToArgb()!=Color.Black.ToArgb() || BatteryColor(0,1,false,0).G==0) throw new Exception("Battery display failed");
        AudioSpectrum.Test();
        int oldBrightness=brightness.Value;
        var message=Message.Create(Handle,0x312,new IntPtr(702),IntPtr.Zero); WndProc(ref message);
        if(brightness.Value!=Math.Min(100,oldBrightness+5)) throw new Exception("Brightness shortcut failed");
        brightness.Value=oldBrightness;
        int oldEffect=activeMode;
        message=Message.Create(Handle,0x312,new IntPtr(701),IntPtr.Zero); WndProc(ref message);
        if(activeMode!=(oldEffect+1)%Effects.Names.Length) throw new Exception("Effect shortcut failed");
        effect.SelectedIndex=oldEffect;
        int original=secondary.SelectedIndex; secondary.SelectedIndex=15; AdvancedChanged();
        if(!UsesMode(14)) throw new Exception("Second effect not active");
        secondary.SelectedIndex=original; AdvancedChanged();
        for(int i=0;i<Effects.Names.Length;i++) {
            secondary.SelectedIndex=i+1; AdvancedChanged();
            if(secondaryMode!=i) throw new Exception("Secondary selection mismatch");
            Frame();
        }
        secondary.SelectedIndex=original; AdvancedChanged();
        locked=true; desiredRunning=true; CheckPower();
        if(keyboard!=null || !desiredRunning) throw new Exception("Lock pause failed");
        locked=false; CheckPower(); if(keyboard==null) throw new Exception("Unlock resume failed");
        Stop(); CheckPower(); if(keyboard!=null) throw new Exception("Manual stop was overridden"); Start();
        Console.WriteLine("PASS: layer blending, battery zones, frequency separation, all secondary effects, lock pause/resume and manual stop.");
    }
}
