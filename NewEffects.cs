using System;
using System.Collections.Generic;
using System.Drawing;

static partial class Effects {
    static Color MixColor(Color a,Color b,double amount) {
        amount=Math.Max(0,Math.Min(1,amount));
        return Color.FromArgb((int)Math.Round(a.R+(b.R-a.R)*amount),(int)Math.Round(a.G+(b.G-a.G)*amount),(int)Math.Round(a.B+(b.B-a.B)*amount));
    }
    public static bool HasFixedPalette(int mode) {
        return mode==4 || mode==5 || mode==10 || mode==11 || mode==12 || mode==22 || mode==24 || mode==25 || mode==26 || mode==27 || mode==28 || mode==31 || mode==32 || mode==37 || mode==40;
    }
    static double Spot(double x,double width) { return Math.Exp(-x*x/width); }
    public static Color ExtendedSample(int mode,int z,double t,Color chosen,bool rainbow) {
        Color tint=rainbow?Ripple.Rainbow(t*.07+z*.12):chosen;
        switch(mode) {
            case 27: // Two slowly drifting curtains of aurora.
                double curtain=.5+.5*Math.Sin(t*.8+z*.9+Math.Sin(t*.31));
                Color aurora=MixColor(Color.FromArgb(0,240,125),Color.FromArgb(151,50,255),.5+.5*Math.Sin(t*.37-z*.6));
                return Scale(aurora,.12+.88*curtain*curtain);
            case 28:
                double beat=t%1.8;
                return Scale(Color.Red,Math.Min(1,Spot(beat-.18,.004)+.72*Spot(beat-.43,.008)));
            case 29:
                double scanner=(t*1.7)%6; bool forward=scanner<3;
                double head=forward?scanner:6-scanner,behind=forward?head-z:z-head;
                return Scale(tint,behind>=0?Math.Exp(-behind*1.7):Spot(behind,.08));
            case 30:
                double travel=(t%2)/2*1.5;
                double collision=Spot(z-travel,.16)+Spot(z-(3-travel),.16);
                return Scale(tint,Math.Min(1,collision));
            case 31:
                double lava=.5+.28*Math.Sin(t*.9+z*1.7)+.22*Math.Sin(t*1.37-z*.8);
                Color molten=MixColor(Color.FromArgb(100,0,4),Color.FromArgb(255,135,0),lava);
                return molten;
            case 32:
                int storm=(int)Math.Floor(t/3.7); double phase=t%3.7;
                double flashTime=.5+Noise(storm*17)*1.7;
                double lightning=phase>=flashTime && phase<flashTime+.07?1:phase>=flashTime+.18 && phase<flashTime+.25?.7:0;
                Color cloud=Scale(Color.FromArgb(8,35,110),.15+.15*(.5+.5*Math.Sin(t+z)));
                return MixColor(cloud,Color.FromArgb(190,220,255),lightning*(.7+.3*Noise(storm+z)));
            case 33:
                double radius=1.65*(.5+.5*Math.Sin(t*1.2));
                double distance=Math.Abs(z-1.5);
                return Scale(tint,1/(1+Math.Exp((distance-radius)*8)));
            case 37:
                double coal=.16+.08*Math.Sin(t*.6+z*1.9);
                double emberPhase=t*.35+z*.73;
                int emberTick=(int)Math.Floor(emberPhase);
                double hot=Math.Pow(Math.Sin(Math.PI*(emberPhase-emberTick)),4)*Noise(emberTick+z*83);
                return MixColor(Scale(Color.FromArgb(180,8,0),coal),Color.FromArgb(255,95,0),hot*.8);
            case 38:
                double star=t*(.25+Noise(z*71)*.22)+z*.61;
                int cycle=(int)Math.Floor(star);
                double glow=Math.Pow(Math.Sin(Math.PI*(star-cycle)),2);
                return Scale(rainbow?MixColor(Color.White,Ripple.Rainbow(Noise(cycle+z*97)),.25):chosen,glow*(.35+.65*Noise(cycle+z*43)));
            case 41:
                double alternating=.5+.5*Math.Cos(t*Math.PI*2);
                return Scale(tint,z==0 || z==3?alternating:1-alternating);
            default: return Color.Black;
        }
    }
}

sealed partial class RippleForm {
    // Positions and timestamps only; no key names or text are retained.
    readonly List<Wave> colorKeyWaves=new List<Wave>();
    sealed class EchoPoint { public double Time,Level; }
    readonly List<EchoPoint>[] bassHistory={new List<EchoPoint>(),new List<EchoPoint>()};
    void RecordColorKey(int zone,double now) {
        if(!UsesMode(34) && !UsesMode(36) && !UsesMode(39)) return;
        if(colorKeyWaves.Count>=80) colorKeyWaves.RemoveAt(0);
        colorKeyWaves.Add(new Wave { zone=zone,time=now });
    }
    static double EchoLevel(List<EchoPoint> points,double target) {
        if(points.Count==0 || target<points[0].Time) return 0;
        for(int i=points.Count-1;i>0;i--) if(points[i-1].Time<=target) {
            double span=points[i].Time-points[i-1].Time;
            double fraction=span<=0?1:Math.Max(0,Math.Min(1,(target-points[i-1].Time)/span));
            return points[i-1].Level+(points[i].Level-points[i-1].Level)*fraction;
        }
        return points[0].Level;
    }
    static double TypingEchoPower(int origin,int zone,double age,int delay,EffectDetails d) {
        if(age<0 || d.Direction==WaveDirection.Right && zone<origin || d.Direction==WaveDirection.Left && zone>origin) return 0;
        double duration=d.WaveWidth/1000.0,travel=delay/1000.0,gap=duration+travel*3,total=0;
        for(int echo=0;echo<d.Repeats;echo++) {
            double t=age-echo*gap-Math.Abs(origin-zone)*travel;
            if(t>=0 && t<duration) total+=Math.Sin(Math.PI*t/duration)*Math.Pow(.55,echo)*Math.Exp(-Math.Abs(origin-zone)*d.TailLoss/100.0);
        }
        return Math.Min(1,total);
    }
    readonly double[] musicRainbowPhase=new double[2];
    Color InputEffect(int mode,int zone,double now,double pace,int layer,double dt,EffectDetails d,Color tint,bool rainbowPalette) {
        if(mode==35) {
            var history=bassHistory[layer];
            if(zone==0) {
                history.Add(new EchoPoint { Time=now,Level=Math.Min(1,audioBands[0]*d.BassGain/100.0) });
                while(history.Count>256 || history.Count>0 && now-history[0].Time>10) history.RemoveAt(0);
            }
            int mapped=d.Direction==WaveDirection.Left?3-zone:zone;
            double target=EchoLevel(history,now-mapped*.18/Math.Max(.05,pace));
            envelopes[layer,mode,zone]=Ease(envelopes[layer,mode,zone],target,dt,d.Attack,d.Release);
            return Effects.Scale(tint,Math.Sqrt(envelopes[layer,mode,zone])*Math.Pow(.83,mapped));
        }
        double r=0,g=0,b=0,charge=0;
        foreach(var key in colorKeyWaves) {
            double age=now-key.time; if(age<0) continue;
            if(mode==36) { charge+=Math.Exp(-age*pace/2.5)*.22; continue; }
            double v=mode==39?TypingEchoPower(key.zone,zone,age,(int)(speed.Value*100.0/d.Speed),d):DetailedWave(0,key.zone,zone,age,(int)(speed.Value*100.0/d.Speed),d);
            Color c=rainbowPalette?Ripple.Rainbow(key.time*.31):tint;
            r+=c.R*v; g+=c.G*v; b+=c.B*v;
        }
        if(mode==36) {
            int mapped=d.Direction==WaveDirection.Left?3-zone:zone;
            return Effects.Scale(tint,Math.Max(0,Math.Min(1,charge*4-mapped)));
        }
        return Color.FromArgb((int)Math.Min(255,r),(int)Math.Min(255,g),(int)Math.Min(255,b));
    }
    void TestFiveEffects() {
        foreach(int mode in new[]{37,38,41}) {
            bool lit=false,changed=false; Color first=Effects.Sample(mode,0,0,Color.Cyan,true);
            for(int frame=0;frame<300;frame++) for(int zone=0;zone<4;zone++) {
                Color c=Effects.Sample(mode,zone,frame/30.0,Color.Cyan,true);
                lit|=c.R+c.G+c.B>0; if(zone==0) changed|=c.ToArgb()!=first.ToArgb();
                if(mode==37 && (c.B!=0 || c.R<c.G || c.ToArgb()!=Effects.Sample(37,zone,frame/30.0,Color.Blue,false).ToArgb())) throw new Exception("Embers lost red/orange palette");
                if(mode==38) {
                    Color next=Effects.Sample(mode,zone,(frame+1)/30.0,Color.Cyan,true);
                    if(Math.Max(Math.Abs(c.R-next.R),Math.Max(Math.Abs(c.G-next.G),Math.Abs(c.B-next.B)))>20) throw new Exception("Stars did not fade smoothly");
                }
            }
            if(!lit || !changed) throw new Exception("New effect inactive: "+Effects.Names[mode]);
        }
        if(Effects.Sample(41,0,0,Color.Red,false).R!=255 || Effects.Sample(41,1,0,Color.Red,false).R!=0 || Effects.Sample(41,0,.5,Color.Red,false).R!=0 || Effects.Sample(41,1,.5,Color.Red,false).R!=255) throw new Exception("Outer/inner alternation failed");
        var echo=DefaultDetails(39);
        double gap=echo.WaveWidth/1000.0+.14*3,peak=echo.WaveWidth/2000.0;
        double a=TypingEchoPower(0,0,peak,140,echo),b=TypingEchoPower(0,0,peak+gap,140,echo),cEcho=TypingEchoPower(0,0,peak+2*gap,140,echo);
        if(echo.Repeats!=3 || a<=b || b<=cEcho || cEcho<=0 || TypingEchoPower(0,0,gap*4,140,echo)!=0) throw new Exception("Typing echoes did not attenuate/expire");
        EffectDetails saved=Details(40,0).Clone(); double oldPhase=musicRainbowPhase[0],otherPhase=musicRainbowPhase[1];
        try {
            primaryDetails[40]=DefaultDetails(40); primaryDetails[40].Attack=primaryDetails[40].Release=0;
            musicRainbowPhase[0]=0; Ambient(40,0,100,1,0,0,.1); double quiet=musicRainbowPhase[0];
            musicRainbowPhase[0]=0; Ambient(40,0,100,1,1,0,.1); double loud=musicRainbowPhase[0];
            if(loud<=quiet*5 || musicRainbowPhase[1]!=otherPhase) throw new Exception("Music wave audio speed / independent layers failed");
            Ambient(40,1,100,1,1,0,.1); if(musicRainbowPhase[0]!=loud) throw new Exception("Music phase advanced per-zone");
            if(new DetailView(primaryDetails[40],40).GetProperties()["Color"]!=null) throw new Exception("Music rainbow exposed unused color control");
        } finally { primaryDetails[40]=saved; musicRainbowPhase[0]=oldPhase; Array.Clear(envelopes,0,envelopes.Length); }
        Console.WriteLine("PASS: embers palette, smooth independent stars, fading typing echoes, audio-driven rainbow speed, outer/inner alternation.");
    }
    void TestNewEffects() {
        TestFiveEffects();
        for(int mode=27;mode<=33;mode++) {
            bool lit=false,changed=false; Color first=Effects.Sample(mode,0,0,Color.Cyan,true);
            for(int i=0;i<240;i++) for(int z=0;z<4;z++) {
                Color c=Effects.Sample(mode,z,i/30.0,Color.Cyan,true);
                lit|=c.R+c.G+c.B>0; if(z==0) changed|=c.ToArgb()!=first.ToArgb();
            }
            if(!lit || !changed) throw new Exception("New continuous effect inactive: "+Effects.Names[mode]);
        }
        foreach(int mode in new[]{4,5,10,11,12,27,28,31,32}) {
            var view=new DetailView(new EffectDetails(),mode).GetProperties();
            if(view["CustomColor"]!=null || view["Rainbow"]!=null || view["Color"]!=null) throw new Exception("Unused palette controls exposed for "+Effects.Names[mode]);
            for(int frame=0;frame<180;frame++) for(int z=0;z<4;z++) {
                Color expected=Effects.Sample(mode,z,frame/30.0,Color.Blue,false);
                Color actual=Effects.Sample(mode,z,frame/30.0,Color.Lime,true);
                if(expected.ToArgb()!=actual.ToArgb()) throw new Exception("Fixed palette overridden for "+Effects.Names[mode]);
                if(mode==28 && (actual.G!=0 || actual.B!=0)) throw new Exception("Heartbeat is not red");
                if(mode==32 && actual.B<actual.R) throw new Exception("Storm lost blue palette");
            }
        }
        for(int frame=0;frame<240;frame++) for(int zone=0;zone<4;zone++) foreach(bool palette in new[]{false,true}) {
            Color lava=Effects.Sample(31,zone,frame/30.0,Color.Blue,palette);
            if(lava.R<100 || lava.G>135 || lava.B>4 || lava.R<=lava.B) throw new Exception("Lava palette was overridden");
        }
        var d=new EffectDetails { CustomColor=true,Rainbow=false,Color=Color.Red };
        colorKeyWaves.Clear(); colorKeyWaves.Add(new Wave { zone=0,time=10 });
        if(InputEffect(34,0,10.2,1,0,.033,d,Color.Red,false).R==0 || InputEffect(34,0,30,1,0,.033,d,Color.Red,false).R!=0) throw new Exception("Color ripple timing failed");
        Color one=InputEffect(36,3,10,1,0,.033,d,Color.Red,false);
        for(int i=0;i<8;i++) colorKeyWaves.Add(new Wave { zone=0,time=10 });
        if(one.R!=0 || InputEffect(36,3,10,1,0,.033,d,Color.Red,false).R==0 || InputEffect(36,0,40,1,0,.033,d,Color.Red,false).R!=0) throw new Exception("Typing build accumulation/fade failed");
        colorKeyWaves.Clear();
        var points=new List<EchoPoint> { new EchoPoint { Time=1,Level=0 },new EchoPoint { Time=2,Level=1 },new EchoPoint { Time=3,Level=0 } };
        if(EchoLevel(points,.9)!=0 || Math.Abs(EchoLevel(points,1.5)-.5)>.001 || EchoLevel(points,3)!=0) throw new Exception("Bass echo delay interpolation failed");
        var oldBands=audioBands; audioBands=new double[]{1,0,0}; d.Attack=d.Release=0;
        bassHistory[0].Clear();
        if(InputEffect(35,0,20,1,0,.033,d,Color.Red,false).R==0 || InputEffect(35,3,20,1,0,.033,d,Color.Red,false).R!=0) throw new Exception("Bass echo started without delay");
        audioBands[0]=0;
        if(InputEffect(35,0,20.6,1,0,.033,d,Color.Red,false).R!=0 || InputEffect(35,3,20.6,1,0,.033,d,Color.Red,false).R==0) throw new Exception("Bass echo did not travel");
        audioBands=oldBands; bassHistory[0].Clear(); Array.Clear(envelopes,0,envelopes.Length);
        Color savedColor=color; bool savedRainbow=rainbow.Checked; double savedActivity=activity,savedScroll=scrollTime;
        int[] checkedModes={16,21,23}; var savedDetails=new EffectDetails[checkedModes.Length];
        for(int i=0;i<checkedModes.Length;i++) savedDetails[i]=Details(checkedModes[i],0).Clone();
        try {
            color=Color.Blue; rainbow.Checked=false; activity=90; scrollTime=100;
            foreach(int mode in checkedModes) {
                primaryDetails[mode]=new EffectDetails { CustomColor=true,Rainbow=false,Color=Color.Red,Attack=0,Release=0 };
                Color sample=Ambient(mode,0,100,1,1,0,.033);
                if(sample.R==0 || sample.B!=0 || sample.G!=0) throw new Exception("Custom palette ignored by "+Effects.Names[mode]);
            }
            primaryDetails[21].CustomColor=false;
            Color music=Ambient(21,0,100,1,1,0,.033);
            if(music.B==0 || music.R!=0 || music.G!=0) throw new Exception("Music Pulse ignored global palette");
        } finally {
            color=savedColor; rainbow.Checked=savedRainbow; activity=savedActivity; scrollTime=savedScroll;
            for(int i=0;i<checkedModes.Length;i++) primaryDetails[checkedModes[i]]=savedDetails[i];
            Array.Clear(envelopes,0,envelopes.Length);
        }
        Console.WriteLine("PASS: scroll, music pulse and idle breathing honor custom/global palettes.");
        Console.WriteLine("PASS: fixed palettes and matching controls for rainbow, lightning, fire, ocean, aurora, heartbeat, lava and thunderstorm.");
        Console.WriteLine("PASS: seven continuous effects, typing ripple/build decay, bass echo delay and interpolation.");
    }
}
