$ErrorActionPreference = 'Stop'
Add-Type -ReferencedAssemblies System.Drawing -TypeDefinition @'
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
public static class RippleIconArt {
    static GraphicsPath Round(float x,float y,float w,float h,float r) {
        var p=new GraphicsPath(); float d=r*2;
        p.AddArc(x,y,d,d,180,90); p.AddArc(x+w-d,y,d,d,270,90);
        p.AddArc(x+w-d,y+h-d,d,d,0,90); p.AddArc(x,y+h-d,d,d,90,90); p.CloseFigure(); return p;
    }
    static void Box(Graphics g,Brush brush,float x,float y,float w,float h,float r) {
        using(var p=Round(x,y,w,h,r)) g.FillPath(brush,p);
    }
    static Bitmap Draw(int size) {
        var b=new Bitmap(size,size,PixelFormat.Format32bppArgb);
        using(var g=Graphics.FromImage(b)) {
            g.SmoothingMode=SmoothingMode.AntiAlias;
            g.ScaleTransform(size/256f,size/256f);
            using(var bg=new LinearGradientBrush(new Point(0,0),new Point(256,256),Color.FromArgb(28,36,58),Color.FromArgb(9,13,25))) Box(g,bg,8,8,240,240,52);
            using(var edge=new Pen(Color.FromArgb(72,111,147),2)) using(var shape=Round(9,9,238,238,51)) g.DrawPath(edge,shape);
            using(var path=new GraphicsPath()) {
                path.AddBezier(35,73,55,73,54,43,76,43);
                path.AddBezier(76,43,98,43,96,88,120,88);
                path.AddBezier(120,88,145,88,139,47,163,47);
                path.AddBezier(163,47,184,47,187,73,221,61);
                using(var colors=new LinearGradientBrush(new Point(35,0),new Point(221,0),Color.Cyan,Color.Magenta)) {
                    colors.InterpolationColors=new ColorBlend { Colors=new Color[]{Color.FromArgb(28,216,255),Color.FromArgb(95,130,255),Color.FromArgb(185,95,255),Color.FromArgb(255,79,160)},Positions=new float[]{0,.34f,.67f,1} };
                    for(int width=21;width>=9;width-=6) using(var glow=new Pen(Color.FromArgb(13,100,155,255),width)) { glow.StartCap=glow.EndCap=LineCap.Round; g.DrawPath(glow,path); }
                    using(var pen=new Pen(colors,7)) { pen.StartCap=pen.EndCap=LineCap.Round; g.DrawPath(pen,path); }
                }
            }
            using(var shell=new SolidBrush(Color.FromArgb(39,49,70))) Box(g,shell,24,106,208,111,19);
            using(var inner=new SolidBrush(Color.FromArgb(12,18,32))) Box(g,inner,30,112,196,99,14);
            Color[] palette={Color.FromArgb(34,213,249),Color.FromArgb(89,139,255),Color.FromArgb(179,103,255),Color.FromArgb(255,91,163)};
            for(int col=0;col<4;col++) {
                int rows=size<=24?2:3;
                for(int row=0;row<rows;row++) using(var fill=new SolidBrush(palette[col])) Box(g,fill,39+col*46,122+row*(size<=24?27:22),40,size<=24?21:16,5);
            }
            if(size>24) using(var space=new LinearGradientBrush(new Point(68,0),new Point(188,0),palette[0],palette[3])) Box(g,space,69,191,118,9,4);
        }
        return b;
    }
    public static void Build(string folder) {
        int[] sizes={16,20,24,32,48,64,128,256}; var data=new byte[sizes.Length][];
        for(int i=0;i<sizes.Length;i++) using(var b=Draw(sizes[i])) using(var ms=new MemoryStream()) { b.Save(ms,ImageFormat.Png); data[i]=ms.ToArray(); }
        using(var writer=new BinaryWriter(File.Create(Path.Combine(folder,"LoqRipple.ico")))) {
            writer.Write((ushort)0); writer.Write((ushort)1); writer.Write((ushort)sizes.Length);
            int offset=6+16*sizes.Length;
            for(int i=0;i<sizes.Length;i++) {
                writer.Write((byte)(sizes[i]==256?0:sizes[i])); writer.Write((byte)(sizes[i]==256?0:sizes[i]));
                writer.Write((byte)0); writer.Write((byte)0); writer.Write((ushort)1); writer.Write((ushort)32);
                writer.Write(data[i].Length); writer.Write(offset); offset+=data[i].Length;
            }
            foreach(var bytes in data) writer.Write(bytes);
        }
        using(var preview=Draw(512)) preview.Save(Path.Combine(folder,"LoqRipple-preview.png"),ImageFormat.Png);
    }
}
'@
[RippleIconArt]::Build($PSScriptRoot)
Write-Host 'Created ICO (16-256 px) and PNG preview.'
