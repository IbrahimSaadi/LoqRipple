using System;
using System.Runtime.InteropServices;

[ComImport, Guid("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ILoopAudioClient {
    void Initialize(int mode,uint flags,long duration,long periodicity,IntPtr format,IntPtr session);
    void GetBufferSize(out uint frames);
    void GetStreamLatency(out long latency);
    void GetCurrentPadding(out uint padding);
    [PreserveSig] int IsFormatSupported(int mode,IntPtr format,out IntPtr closest);
    void GetMixFormat(out IntPtr format);
    void GetDevicePeriod(out long normal,out long minimum);
    void Start(); void Stop(); void Reset(); void SetEventHandle(IntPtr handle);
    void GetService(ref Guid iid,[MarshalAs(UnmanagedType.IUnknown)] out object service);
}
[ComImport, Guid("C8ADBD64-E71E-48A0-A4DE-185C395CD317"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
interface ILoopCapture {
    void GetBuffer(out IntPtr data,out uint frames,out uint flags,out ulong position,out ulong qpc);
    void ReleaseBuffer(uint frames);
    void GetNextPacketSize(out uint frames);
}
sealed class AudioSpectrum : IDisposable {
    ILoopAudioClient client; ILoopCapture capture;
    int rate,channels,bits,align; bool floating;
    readonly double[] samples=new double[2048]; int write,available;
    byte[] packet=new byte[0];
    void Open() {
        IMMEnumerator enumerator=null; IMMDevice device=null; IntPtr format=IntPtr.Zero;
        try {
            enumerator=(IMMEnumerator)new MMEnumerator(); enumerator.GetDefaultAudioEndpoint(0,1,out device);
            Guid id=typeof(ILoopAudioClient).GUID; object obj; device.Activate(ref id,23,IntPtr.Zero,out obj); client=(ILoopAudioClient)obj;
            client.GetMixFormat(out format);
            int tag=(ushort)Marshal.ReadInt16(format,0);
            channels=(ushort)Marshal.ReadInt16(format,2); rate=Marshal.ReadInt32(format,4);
            align=(ushort)Marshal.ReadInt16(format,12); bits=(ushort)Marshal.ReadInt16(format,14);
            if(tag==0xfffe) tag=Marshal.ReadInt32(format,24);
            floating=tag==3;
            if(channels<1 || rate<8000 || !(floating && bits==32 || tag==1 && (bits==16 || bits==24 || bits==32))) throw new Exception("Unsupported audio mix format");
            client.Initialize(0,0x20000,1000000,0,format,IntPtr.Zero);
            id=typeof(ILoopCapture).GUID; client.GetService(ref id,out obj); capture=(ILoopCapture)obj; client.Start();
        } catch { Dispose(); throw; }
        finally {
            if(format!=IntPtr.Zero) Marshal.FreeCoTaskMem(format);
            if(device!=null) Marshal.ReleaseComObject(device);
            if(enumerator!=null) Marshal.ReleaseComObject(enumerator);
        }
    }
    public double[] Read() {
        if(client==null) Open();
        uint next; capture.GetNextPacketSize(out next); bool received=false;
        for(int packets=0;next>0 && packets<50;packets++) {
            IntPtr data; uint frames,flags; ulong pos,qpc;
            capture.GetBuffer(out data,out frames,out flags,out pos,out qpc);
            try {
                int size=checked((int)frames*align); if(packet.Length<size) packet=new byte[size];
                bool silent=(flags&2)!=0;
                if(!silent) Marshal.Copy(data,packet,0,size);
                for(int i=0;i<(int)frames;i++) {
                    double value=0;
                    if(!silent) for(int c=0;c<channels;c++) {
                        int p=i*align+c*(bits/8);
                        if(floating) value+=BitConverter.ToSingle(packet,p);
                        else if(bits==16) value+=BitConverter.ToInt16(packet,p)/32768.0;
                        else if(bits==32) value+=BitConverter.ToInt32(packet,p)/2147483648.0;
                        else { int v=packet[p]|packet[p+1]<<8|packet[p+2]<<16; if((v&0x800000)!=0) v|=unchecked((int)0xff000000); value+=v/8388608.0; }
                    }
                    samples[write]=value/channels; write=(write+1)%samples.Length; available=Math.Min(samples.Length,available+1);
                }
                received|=frames>0;
            } finally { capture.ReleaseBuffer(frames); }
            capture.GetNextPacketSize(out next);
        }
        if(!received) return new double[3];
        var ordered=new double[available];
        for(int i=0;i<available;i++) ordered[i]=samples[(write-available+i+samples.Length)%samples.Length];
        return Analyze(ordered,rate);
    }
    // Hann-windowed FFT; bass 40-250 Hz, mid 250-2000 Hz, treble 2-12 kHz.
    public static double[] Analyze(double[] input,int rate) {
        int n=1; while(n*2<=input.Length && n<2048) n*=2;
        if(n<64) return new double[3];
        var re=new double[n]; var im=new double[n];
        for(int i=0;i<n;i++) re[i]=input[input.Length-n+i]*(.5-.5*Math.Cos(2*Math.PI*i/(n-1)));
        for(int i=1,j=0;i<n;i++) { int bit=n>>1; for(;(j&bit)!=0;bit>>=1) j^=bit; j^=bit; if(i<j) { double t=re[i];re[i]=re[j];re[j]=t; } }
        for(int len=2;len<=n;len*=2) {
            double angle=-2*Math.PI/len;
            for(int block=0;block<n;block+=len) for(int k=0;k<len/2;k++) {
                double c=Math.Cos(angle*k),s=Math.Sin(angle*k);
                int a=block+k,b=a+len/2;
                double r=re[b]*c-im[b]*s, v=re[b]*s+im[b]*c;
                re[b]=re[a]-r; im[b]=im[a]-v; re[a]+=r; im[a]+=v;
            }
        }
        var energy=new double[3];
        for(int k=1;k<n/2;k++) {
            double hz=k*(double)rate/n; if(hz<40 || hz>12000) continue;
            int band=hz<250?0:hz<2000?1:2;
            energy[band]+=re[k]*re[k]+im[k]*im[k];
        }
        for(int i=0;i<3;i++) energy[i]=Math.Min(1,Math.Sqrt(energy[i])*4/n);
        return energy;
    }
    public static void Test() {
        foreach(int hz in new int[]{125,1000,6000}) {
            var signal=new double[2048]; for(int i=0;i<signal.Length;i++) signal[i]=.5*Math.Sin(2*Math.PI*hz*i/48000);
            var bands=Analyze(signal,48000); int expected=hz<250?0:hz<2000?1:2;
            for(int i=0;i<3;i++) if(i!=expected && bands[expected]<bands[i]*10) throw new Exception("Frequency bands not separated");
            if(bands[expected]<.1) throw new Exception("Tone not detected");
        }
        foreach(double value in Analyze(new double[2048],48000)) if(value!=0) throw new Exception("Silence not dark");
    }
    public void Dispose() {
        if(client!=null) { try { client.Stop(); } catch(COMException) {} }
        if(capture!=null) { Marshal.ReleaseComObject(capture);capture=null; }
        if(client!=null) { Marshal.ReleaseComObject(client);client=null; }
        Array.Clear(samples,0,samples.Length); available=write=0;
    }
}
