using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;
using System.Xml.Serialization;

public enum ScreenColorStyle { Accurate, Vivid }
public enum WaveDirection { Both, Right, Left }
public class EffectDetails {
    [Category("Layer"),DisplayName("Speed (%)")] public int Speed { get; set; }
    [Category("Layer"),DisplayName("Brightness (%)")] public int Brightness { get; set; }
    [Category("Layer"),DisplayName("Custom color")] public bool CustomColor { get; set; }
    [Category("Layer"),DisplayName("Rainbow colors")] public bool Rainbow { get; set; }
    [Browsable(false)] public int ColorArgb { get; set; }
    [XmlIgnore,Category("Layer")] public Color Color { get { return Color.FromArgb(ColorArgb); } set { ColorArgb=value.ToArgb(); } }
    [Category("Motion")] public WaveDirection Direction { get; set; }
    [Category("Wave"),DisplayName("Wave width / fade (ms)")] public int WaveWidth { get; set; }
    [Category("Wave"),DisplayName("Tail loss per zone (%)")] public int TailLoss { get; set; }
    [Category("Wave"),DisplayName("Repeats per press")] public int Repeats { get; set; }
    [Category("Pointer"),DisplayName("Motion sensitivity (%)")] public int Sensitivity { get; set; }
    [Category("Pointer"),DisplayName("Follow smoothing (ms)")] public int FollowSmoothing { get; set; }
    [Category("Pointer"),DisplayName("Fade after movement (ms)")] public int PointerFade { get; set; }
    [Category("Pointer"),DisplayName("Light width (%)")] public int PointerWidth { get; set; }
    [Category("Audio"),DisplayName("Bass sensitivity (%)")] public int BassGain { get; set; }
    [Category("Audio"),DisplayName("Mid sensitivity (%)")] public int MidGain { get; set; }
    [Category("Audio"),DisplayName("Treble sensitivity (%)")] public int TrebleGain { get; set; }
    [Category("Audio"),DisplayName("Volume sensitivity (%)")] public int AudioGain { get; set; }
    [Category("Audio"),DisplayName("Attack (ms)")] public int Attack { get; set; }
    [Category("Audio"),DisplayName("Release (ms)")] public int Release { get; set; }
    [Browsable(false)] public int BassArgb { get; set; }
    [Browsable(false)] public int MidArgb { get; set; }
    [Browsable(false)] public int TrebleArgb { get; set; }
    [XmlIgnore,Category("Audio")] public Color BassColor { get { return Color.FromArgb(BassArgb); } set { BassArgb=value.ToArgb(); } }
    [XmlIgnore,Category("Audio")] public Color MidColor { get { return Color.FromArgb(MidArgb); } set { MidArgb=value.ToArgb(); } }
    [XmlIgnore,Category("Audio")] public Color TrebleColor { get { return Color.FromArgb(TrebleArgb); } set { TrebleArgb=value.ToArgb(); } }
    [Category("Screen"),DisplayName("Color rendering")] public ScreenColorStyle ScreenStyle { get; set; }
    [Category("Screen"),DisplayName("Color change sensitivity (%)")] public int ScreenSensitivity { get; set; }
    [Category("Screen"),DisplayName("Transition smoothing (ms)")] public int ScreenSmoothing { get; set; }
    [Category("Screen"),DisplayName("Ignore near black")] public bool IgnoreBlack { get; set; }
    [Category("Screen"),DisplayName("Ignore neutral / white")] public bool IgnoreNeutral { get; set; }
    public EffectDetails() {
        Speed=Brightness=100; Rainbow=true; ColorArgb=Color.DeepSkyBlue.ToArgb();
        WaveWidth=420; TailLoss=18; Repeats=1;
        Sensitivity=100; FollowSmoothing=140; PointerFade=400; PointerWidth=100;
        BassGain=MidGain=TrebleGain=AudioGain=100; Attack=40; Release=180;
        BassArgb=Color.OrangeRed.ToArgb(); MidArgb=Color.LimeGreen.ToArgb(); TrebleArgb=Color.DeepSkyBlue.ToArgb();
        ScreenSensitivity=100; ScreenSmoothing=180; IgnoreBlack=IgnoreNeutral=true;
    }
    static int Limit(int value,int min,int max) { return Math.Max(min,Math.Min(max,value)); }
    public void Validate() {
        if(!Enum.IsDefined(typeof(ScreenColorStyle),ScreenStyle)) ScreenStyle=ScreenColorStyle.Accurate;
        Speed=Limit(Speed,10,400); Brightness=Limit(Brightness,0,100);
        if(!Enum.IsDefined(typeof(WaveDirection),Direction)) Direction=WaveDirection.Both;
        WaveWidth=Limit(WaveWidth,50,3000); TailLoss=Limit(TailLoss,0,100); Repeats=Limit(Repeats,1,5);
        Sensitivity=Limit(Sensitivity,10,400); FollowSmoothing=Limit(FollowSmoothing,0,2000); PointerFade=Limit(PointerFade,50,5000); PointerWidth=Limit(PointerWidth,20,300);
        BassGain=Limit(BassGain,0,400); MidGain=Limit(MidGain,0,400); TrebleGain=Limit(TrebleGain,0,400); AudioGain=Limit(AudioGain,0,400);
        Attack=Limit(Attack,0,3000); Release=Limit(Release,0,5000);
        ScreenSensitivity=Limit(ScreenSensitivity,10,300); ScreenSmoothing=Limit(ScreenSmoothing,0,3000);
    }
    public EffectDetails Clone() { return (EffectDetails)MemberwiseClone(); }
}
sealed class DetailView : ICustomTypeDescriptor {
    readonly EffectDetails value; readonly int mode;
    public DetailView(EffectDetails value,int mode) { this.value=value; this.mode=mode; }
    public PropertyDescriptorCollection GetProperties(Attribute[] attributes) {
        var properties=new List<PropertyDescriptor>();
        foreach(PropertyDescriptor p in TypeDescriptor.GetProperties(value,attributes)) {
            if(!p.IsBrowsable) continue;
            string category=p.Category;
            if(category=="Layer" && Effects.HasFixedPalette(mode) && (p.Name=="CustomColor" || p.Name=="Rainbow" || p.Name=="Color")) continue;
            if(mode==13 && p.Name=="Rainbow") continue;
            if(category=="Layer" && p.Name=="Speed" && (mode==13 || mode==22 || mode==25)) continue;
            bool audio=mode==21 || mode==26 || mode==35 || mode==40, pointer=mode==14;
            if(category=="Wave" && !(mode<4 || mode==15 || mode==17 || mode==20 || mode==23 || mode==24 || mode==34 || mode==39)) continue;
            if(category=="Motion" && (audio && mode!=35 && mode!=40 || mode==13 || mode==22 || mode==25)) continue;
            if(category=="Pointer" && !pointer) continue;
            if(category=="Audio" && !audio) continue;
            if(category=="Audio" && (mode==21 || mode==40) && (p.Name=="BassGain" || p.Name=="MidGain" || p.Name=="TrebleGain" || p.Name.EndsWith("Color"))) continue;
            if(mode==26 && p.Name=="AudioGain") continue;
            if(mode==35 && category=="Audio" && p.Name!="BassGain" && p.Name!="Attack" && p.Name!="Release") continue;
            if(category=="Screen" && mode!=22) continue;
            properties.Add(p);
        }
        return new PropertyDescriptorCollection(properties.ToArray());
    }
    public PropertyDescriptorCollection GetProperties() { return GetProperties(new Attribute[0]); }
    public object GetPropertyOwner(PropertyDescriptor p) { return value; }
    public AttributeCollection GetAttributes() { return TypeDescriptor.GetAttributes(value); }
    public string GetClassName() { return "Effect settings"; }
    public string GetComponentName() { return null; }
    public TypeConverter GetConverter() { return TypeDescriptor.GetConverter(value); }
    public EventDescriptor GetDefaultEvent() { return null; }
    public PropertyDescriptor GetDefaultProperty() { return null; }
    public object GetEditor(Type type) { return null; }
    public EventDescriptorCollection GetEvents(Attribute[] attributes) { return EventDescriptorCollection.Empty; }
    public EventDescriptorCollection GetEvents() { return EventDescriptorCollection.Empty; }
}

sealed partial class RippleForm {
    List<EffectDetails> primaryDetails=new List<EffectDetails>(),secondaryDetails=new List<EffectDetails>();
    readonly StudioCombo detailLayer=new StudioCombo { DropDownStyle=ComboBoxStyle.DropDownList,Width=250 };
    readonly Label detailTitle=new Label { AutoSize=false,Size=new Size(450,35) };
    bool detailsReady;
    readonly double[,,] screenEnvelope=new double[2,4,3];
    readonly Color[,] acceptedScreens=new Color[2,4];
    double rawPointerMotion;
    readonly Color[] secondaryScreenColors={Color.Black,Color.Black,Color.Black,Color.Black};
    readonly double[,,] envelopes=new double[2,Effects.Names.Length,4];
    readonly double[,] pointerPositions=new double[2,2];
    readonly Color[,] smoothScreens=new Color[2,4];
    static EffectDetails DefaultDetails(int mode) { return new EffectDetails { Repeats=mode==39?3:1 }; }
    static List<EffectDetails> CopyDetails(List<EffectDetails> source) {
        var result=new List<EffectDetails>();
        for(int i=0;i<Effects.Names.Length;i++) { var d=source!=null && i<source.Count && source[i]!=null?source[i].Clone():DefaultDetails(i); d.Validate(); result.Add(d); }
        return result;
    }
    EffectDetails Details(int mode,int layer) {
        var list=layer==0?primaryDetails:secondaryDetails;
        while(list.Count<Effects.Names.Length) list.Add(DefaultDetails(list.Count));
        return list[Math.Max(0,Math.Min(Effects.Names.Length-1,mode))];
    }
    Control BuildEffectDetails() {
        var outer=new Panel { Dock=DockStyle.Fill };
        var top=new TableLayoutPanel { Dock=DockStyle.Top,Tag="whitepanel",Height=88,ColumnCount=3,RowCount=2,Padding=new Padding(12) };
        top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,220)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); top.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,160));
        detailLayer.Width=210; detailLayer.Items.AddRange(new object[]{"Primary layer","Secondary layer"}); detailLayer.SelectedIndex=0;
        detailLayer.SelectionChangeCommitted+=delegate { RefreshDetails(); };
        top.Controls.Add(detailLayer,0,0);
        detailTitle.Dock=DockStyle.Fill; detailTitle.Font=new Font("Segoe UI",10,FontStyle.Bold); top.Controls.Add(detailTitle,1,0);
        var reset=new Button { Text="Reset effect",Dock=DockStyle.Fill };
        reset.Click+=delegate {
            int layer=detailLayer.SelectedIndex, mode=layer==0?activeMode:secondaryMode; if(mode<0) return;
            (layer==0?primaryDetails:secondaryDetails)[mode]=DefaultDetails(mode);
            RefreshDetails(); ResetDetailAnimation(); QueueSave();
        };
        top.Controls.Add(reset,2,0);
        var hint=new Label { Text="Each layer keeps its own settings. Changes apply instantly and save automatically.",AutoSize=true,Tag="muted" };
        top.Controls.Add(hint,0,1); top.SetColumnSpan(hint,3);
        detailFields.Resize+=delegate { FitDetailCards(); };
        var shared=new TableLayoutPanel { Dock=DockStyle.Top,Height=148,ColumnCount=2,RowCount=3,Padding=new Padding(16,8,16,8),Tag="whitepanel",Visible=false };
        shared.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); shared.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50));
        shared.RowStyles.Add(new RowStyle(SizeType.Absolute,26)); shared.RowStyles.Add(new RowStyle(SizeType.Absolute,48)); shared.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
        shared.Controls.Add(new Label { Text="Global animation delay",AutoSize=true },0,0);
        shared.Controls.Add(new Label { Text="Default color palette",AutoSize=true },1,0);
        speed.Dock=DockStyle.Fill; shared.Controls.Add(speed,0,1);
        var palette=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false };
        rainbow.AutoSize=true; palette.Controls.Add(rainbow);
        sharedColorButton.Dock=DockStyle.None; sharedColorButton.Size=new Size(125,36); palette.Controls.Add(sharedColorButton); shared.Controls.Add(palette,1,1);
        var sharedHint=new Label { Text="Shared defaults for both layers. Each effect can override its color and adjust its speed below.",Dock=DockStyle.Fill,Tag="muted" };
        shared.Controls.Add(sharedHint,0,2); shared.SetColumnSpan(sharedHint,2);
        var expand=new Button { Text="Shared defaults: speed and colors  +",Dock=DockStyle.Top,Height=40,TextAlign=ContentAlignment.MiddleLeft };
        expand.Click+=delegate { shared.Visible=!shared.Visible; expand.Text="Shared defaults: speed and colors  "+(shared.Visible?"−":"+"); };
        outer.Controls.Add(detailFields); outer.Controls.Add(top); outer.Controls.Add(shared); outer.Controls.Add(expand);
        detailsReady=true; RefreshDetails(); return outer;
    }
    void RefreshDetails() {
        if(!detailsReady) return;
        int layer=detailLayer.SelectedIndex,mode=layer==0?activeMode:secondaryMode;
        detailTitle.Text=mode<0?"No second effect selected":Effects.Names[mode];
        RenderDetailFields(mode,layer);
    }
    void ResetDetailAnimation() {
        Array.Clear(envelopes,0,envelopes.Length);
        bassHistory[0].Clear(); bassHistory[1].Clear(); Array.Clear(musicRainbowPhase,0,musicRainbowPhase.Length);
        for(int layer=0;layer<2;layer++) for(int z=0;z<4;z++) {
            Color c=(layer==0?screenColors:secondaryScreenColors)[z];
            smoothScreens[layer,z]=acceptedScreens[layer,z]=c;
            screenEnvelope[layer,z,0]=c.R; screenEnvelope[layer,z,1]=c.G; screenEnvelope[layer,z,2]=c.B;
        }
        for(int layer=0;layer<2;layer++) { pointerPositions[layer,0]=pointerZone; pointerPositions[layer,1]=pointerMotion; }
    }
    static double Ease(double old,double target,double dt,int attack,int release) {
        int ms=target>old?attack:release;
        return ms<=0?target:old+(target-old)*(1-Math.Exp(-dt*1000/ms));
    }
    static double DetailedWave(int mode,int origin,int zone,double age,int delay,EffectDetails d) {
        if(age<0) return 0;
        if(d.Direction==WaveDirection.Right && zone<origin || d.Direction==WaveDirection.Left && zone>origin) return 0;
        double duration=d.WaveWidth/1000.0, travel=delay/1000.0, total=0;
        double repeatGap=duration+travel*3;
        for(int repeat=0;repeat<d.Repeats;repeat++) {
            double t=age-repeat*repeatGap;
            if(mode==0 || mode==23 || mode==24 || mode==20 || mode==17) {
                t-=Math.Abs(origin-zone)*travel;
                if(t>=0 && t<duration) total+=Math.Sin(Math.PI*t/duration)*Math.Exp(-Math.Abs(origin-zone)*d.TailLoss/100.0);
            } else if(mode==1) { if(t>=0 && t<duration*2) total+=Math.Exp(-t*3/duration); }
            else if(mode==3) { if(origin==zone && t>=0 && t<duration*8) total+=Math.Exp(-t/duration); }
            else {
                int distance=d.Direction==WaveDirection.Left?(origin-zone+4)%4:(zone-origin+4)%4;
                t-=distance*travel;
                if(t>=0 && t<duration*2) total+=Math.Exp(-t*3/duration);
            }
        }
        return Math.Min(1,total);
    }
    static double DetailLifetime(EffectDetails d,int delay) { return (d.WaveWidth/1000.0+delay/1000.0*3)*d.Repeats+d.WaveWidth/1000.0*8; }
    double WaveLifetime() {
        double life=4;
        int[] modes={activeMode,secondaryMode};
        for(int layer=0;layer<2;layer++) if(modes[layer]>=0) {
            var d=Details(modes[layer],layer); life=Math.Max(life,DetailLifetime(d,(int)(speed.Value*100.0/d.Speed)));
        }
        return life;
    }
    void DetailTests() {
        var d=new EffectDetails();
        if(DetailedWave(0,1,0,.3,140,new EffectDetails { Direction=WaveDirection.Right })!=0) throw new Exception("Wave direction ignored");
        double first=DetailedWave(0,0,0,.2,140,d);
        d.Repeats=2;
        if(first<=0 || DetailedWave(0,0,0,1.04,140,d)<=0) throw new Exception("Wave repetition failed");
        if(Ease(0,1,.1,0,100)!=1 || Ease(1,0,.1,100,0)!=0) throw new Exception("Envelope settings ignored");
        var copy=CopyDetails(primaryDetails); copy[0].Brightness=1;
        if(Details(0,0).Brightness==1) throw new Exception("Profile details share references");
        if(new DetailView(d,14).GetProperties()["BassGain"]!=null || new DetailView(d,26).GetProperties()["BassGain"]==null) throw new Exception("Effect-specific settings filter failed");
        var oldPrimary=Details(13,0).Clone(); var oldSecondary=Details(13,1).Clone();
        try {
            primaryDetails[13]=new EffectDetails { CustomColor=true,Rainbow=false,ColorArgb=Color.Red.ToArgb(),Brightness=50 };
            secondaryDetails[13]=new EffectDetails { CustomColor=true,Rainbow=false,ColorArgb=Color.Blue.ToArgb(),Brightness=25 };
            Color a=Ambient(13,0,1,1,0,0,.033), b=Ambient(13,0,1,1,0,1,.033);
            if(a.R!=127 || a.B!=0 || b.B!=63 || b.R!=0) throw new Exception("Independent layer color/brightness ignored");
        } finally { primaryDetails[13]=oldPrimary; secondaryDetails[13]=oldSecondary; }
        var profile=Snapshot("detail test");
        primaryDetails[0].WaveWidth=700; secondaryDetails[0].Repeats=4;
        var saved=Snapshot("saved detail test"); primaryDetails[0].WaveWidth=100; secondaryDetails[0].Repeats=1;
        RestoreProfile(saved);
        if(Details(0,0).WaveWidth!=700 || Details(0,1).Repeats!=4) throw new Exception("Profile did not restore independent effect details");
        RestoreProfile(profile);
        Console.WriteLine("PASS: per-effect settings, wave direction/repeats, envelopes, profile copies, contextual controls and independent layer rendering.");
    }
}
