using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;

// Used by a single background capture task. Reuse GDI resources until display size changes.
sealed class ScreenSampler : IDisposable {
    Bitmap full,small;
    Graphics capture,scale;
    string lastKey;
    Color[] firstHistory,secondHistory;
    public double LastCaptureSeconds { get; private set; }
    public int BufferAllocations { get; private set; }
    public static string Key(Rectangle bounds,EffectDetails first,EffectDetails second) {
        return bounds.ToString()+ConfigKey(first)+ConfigKey(second);
    }
    static string ConfigKey(EffectDetails config) {
        return config==null?"none":":"+config.ScreenStyle+":"+config.IgnoreBlack+":"+config.IgnoreNeutral;
    }
    public Color[] Capture(Rectangle bounds,EffectDetails first,EffectDetails second) {
        var watch=Stopwatch.StartNew();
        if(full==null || full.Size!=bounds.Size) {
            ReleaseBuffers();
            full=new Bitmap(bounds.Width,bounds.Height); capture=Graphics.FromImage(full);
            small=new Bitmap(96,54); scale=Graphics.FromImage(small);
            scale.InterpolationMode=InterpolationMode.HighQualityBilinear;
            scale.PixelOffsetMode=PixelOffsetMode.HighQuality;
            BufferAllocations++;
        }
        string key=Key(bounds,first,second);
        if(key!=lastKey) { firstHistory=secondHistory=null; lastKey=key; }
        capture.CopyFromScreen(bounds.Left,bounds.Top,0,0,full.Size);
        scale.DrawImage(full,new Rectangle(0,0,small.Width,small.Height),new Rectangle(Point.Empty,full.Size),GraphicsUnit.Pixel);
        firstHistory=RippleForm.DominantScreen(small,first.IgnoreBlack,first.IgnoreNeutral,first.ScreenStyle==ScreenColorStyle.Vivid,firstHistory);
        Color[] result=firstHistory;
        if(second!=null) {
            secondHistory=RippleForm.DominantScreen(small,second.IgnoreBlack,second.IgnoreNeutral,second.ScreenStyle==ScreenColorStyle.Vivid,secondHistory);
            result=new Color[8]; Array.Copy(firstHistory,result,4); Array.Copy(secondHistory,0,result,4,4);
        }
        LastCaptureSeconds=watch.Elapsed.TotalSeconds;
        return result;
    }
    void ReleaseBuffers() {
        if(scale!=null) scale.Dispose(); if(capture!=null) capture.Dispose();
        if(small!=null) small.Dispose(); if(full!=null) full.Dispose();
        scale=capture=null; small=full=null;
    }
    public void Dispose() { ReleaseBuffers(); firstHistory=secondHistory=null; }
}
