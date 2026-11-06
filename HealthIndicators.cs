using System;
using System.Drawing;
using System.Management;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;

sealed class ThermalState {
    public double? CPU,GPU;
    public string GPUProvider="";
    public bool Available { get { return CPU.HasValue || GPU.HasValue; } }
    public double? Maximum {
        get { return CPU.HasValue && GPU.HasValue?Math.Max(CPU.Value,GPU.Value):CPU??GPU; }
    }
}
static class SystemHealth {
    [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlInit_v2();
    [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlShutdown();
    [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetCount_v2(out uint count);
    [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetHandleByIndex_v2(uint index,out IntPtr device);
    [DllImport("nvml.dll",CallingConvention=CallingConvention.Cdecl)] static extern int nvmlDeviceGetTemperature(IntPtr device,uint sensor,out uint temperature);
    public static bool? Internet() {
        object manager=null;
        try {
            var type=Type.GetTypeFromCLSID(new Guid("DCB00C01-570F-4A9B-8D69-199FDBA5723B"));
            manager=Activator.CreateInstance(type);
            return Convert.ToBoolean(type.InvokeMember("IsConnectedToInternet",BindingFlags.GetProperty,null,manager,null));
        } catch(COMException) {} catch(TargetInvocationException) {} catch(MissingMethodException) {}
        finally { if(manager!=null && Marshal.IsComObject(manager)) Marshal.ReleaseComObject(manager); }
        return null;
    }
    static bool Valid(double value) { return !double.IsNaN(value) && !double.IsInfinity(value) && value>=1 && value<=125; }
    static double? Highest(double? old,double value) { return Valid(value)?(old.HasValue?Math.Max(old.Value,value):value):old; }
    public static ThermalState Temperature() {
        var result=new ThermalState();
        // WMI providers expose identified CPU/GPU sensors; ACPI zones are not CPU sensors.
        foreach(string ns in new string[]{"root\\LibreHardwareMonitor","root\\OpenHardwareMonitor"}) {
            try {
                using(var query=new ManagementObjectSearcher(ns,"SELECT Identifier, Value FROM Sensor WHERE SensorType='Temperature'")) {
                    query.Options.Timeout=TimeSpan.FromSeconds(2);
                    using(var rows=query.Get()) foreach(ManagementObject row in rows) using(row) {
                        if(row["Value"]==null) continue;
                        string id=Convert.ToString(row["Identifier"]).ToLowerInvariant();
                        double value=Convert.ToDouble(row["Value"]);
                        if(id.StartsWith("/intelcpu/") || id.StartsWith("/amdcpu/")) result.CPU=Highest(result.CPU,value);
                        if(id.StartsWith("/gpu-")) { result.GPU=Highest(result.GPU,value); result.GPUProvider=ns.Substring(5); }
                    }
                }
            } catch(ManagementException) {} catch(COMException) {} catch(UnauthorizedAccessException) {}
        }
        if(!result.GPU.HasValue) {
            try {
                if(nvmlInit_v2()==0) try {
                    uint count;
                    if(nvmlDeviceGetCount_v2(out count)==0) for(uint i=0;i<Math.Min(count,8);i++) {
                        IntPtr device; uint value;
                        if(nvmlDeviceGetHandleByIndex_v2(i,out device)==0 && nvmlDeviceGetTemperature(device,0,out value)==0) result.GPU=Highest(result.GPU,value);
                    }
                    if(result.GPU.HasValue) result.GPUProvider="NVIDIA";
                } finally { nvmlShutdown(); }
            } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {} catch(BadImageFormatException) {}
        }
        return result;
    }
    public static void TestDetectors() {
        var state=new IndicatorState();
        state.Internet(true,0,2); if(state.Current!=null) throw new Exception("Initial network should not notify");
        state.Internet(false,3,2); if(state.Current!=null) throw new Exception("Transient internet failure notified");
        state.Internet(null,6,2); state.Internet(false,9,2); if(state.Current!=null) throw new Exception("Unknown network treated as down");
        state.Internet(false,12,2); if(state.Current==null || state.Current.Kind!="Internet disconnected") throw new Exception("Internet loss missed");
        state.Internet(false,15,2); if(state.Current.Start!=12) throw new Exception("Repeated network warning");
        state.Internet(true,18,2); state.Internet(true,21,2); if(state.Current.Kind!="Internet restored") throw new Exception("Internet recovery missed");
        state.Reset();
        state.Temperature(new ThermalState { GPU=90 },85,0,2); if(state.Current!=null) throw new Exception("Transient heat notified");
        state.Temperature(new ThermalState { GPU=91 },85,5,2); if(state.Current==null || state.Current.Kind!="High temperature") throw new Exception("Heat alert missed");
        state.Temperature(new ThermalState { GPU=92 },85,10,2); if(state.Current.Start!=5) throw new Exception("Heat alert flooding");
        state.Volume(new VolumeState { Level=.5f },5.1,2); state.Volume(new VolumeState { Level=.9f },5.2,2); if(state.Current.Kind!="High temperature") throw new Exception("Heat priority lost");
        state.Temperature(new ThermalState { GPU=90 },85,66,2); if(state.Current.Start!=66) throw new Exception("Persistent heat reminder missed");
        state.Temperature(new ThermalState { GPU=79 },85,70,2);
        state.Temperature(new ThermalState { CPU=90 },85,75,2); state.Temperature(new ThermalState { CPU=91 },85,80,2); if(state.Current.Start!=80) throw new Exception("Heat rearm failed");
        state.Reset(); state.Temperature(new ThermalState { GPU=90 },85,0,2);
        state.Temperature(new ThermalState(),85,5,2); state.Temperature(new ThermalState { GPU=90 },85,10,2);
        if(state.Current!=null) throw new Exception("Missing sensor did not reset heat debounce");
        state.Reset(); state.Temperature(new ThermalState(),85,0,2); if(state.Current!=null) throw new Exception("Missing sensor produced heat alert");
    }
}
sealed partial class IndicatorState {
    bool? stableInternet,candidateInternet; int internetCount,hotCount;
    bool heatActive; double lastHeat=-1000;
    void ResetHealth() { stableInternet=candidateInternet=null; internetCount=hotCount=0; heatActive=false; lastHeat=-1000; }
    public void Internet(bool? available,double now,double duration) {
        if(!available.HasValue) { candidateInternet=null; internetCount=0; return; }
        if(!stableInternet.HasValue) { stableInternet=available; return; }
        if(available==stableInternet) { candidateInternet=null; internetCount=0; return; }
        if(candidateInternet!=available) { candidateInternet=available; internetCount=1; return; }
        if(++internetCount<2) return;
        stableInternet=available; candidateInternet=null; internetCount=0;
        Show(available.Value?"Internet restored":"Internet disconnected",1,available.Value?Color.LimeGreen:Color.Red,2,now,duration);
    }
    public void Temperature(ThermalState sample,int threshold,double now,double duration) {
        var max=sample.Maximum;
        if(!max.HasValue) { hotCount=0; return; }
        if(max.Value<=threshold-5) { heatActive=false; hotCount=0; }
        if(max.Value<threshold) { hotCount=0; return; }
        hotCount++;
        if(hotCount>=2 && (!heatActive || now-lastHeat>=60)) {
            Show("High temperature",1,Color.OrangeRed,5,now,duration); heatActive=true; lastHeat=now;
        }
    }
}
sealed partial class RippleForm {
    readonly CheckBox notifyInternet=new CheckBox { Text="Internet disconnect / reconnect (Windows status)",Checked=true,AutoSize=true };
    readonly CheckBox notifyHeat=new CheckBox { Text="CPU / GPU temperature warning",Checked=true,AutoSize=true };
    readonly NumericUpDown heatThreshold=new NumericUpDown { Minimum=60,Maximum=110,Value=85,Width=100 };
    readonly Label healthSources=new Label { AutoSize=false,Size=new Size(350,100),Text="Internet and temperature sources: waiting" };
    Task<bool?> internetRead; Task<ThermalState> temperatureRead;
    int internetGeneration,temperatureGeneration;
    double nextInternet,nextTemperature;
    string internetStatus="waiting",thermalStatus="waiting";
    void AddHealthControls(FlowLayoutPanel panel) {
        panel.Controls.Add(notifyInternet); panel.Controls.Add(notifyHeat);
        panel.Controls.Add(new Label { Text="Temperature warning threshold (C)",AutoSize=true }); panel.Controls.Add(heatThreshold);
        panel.Controls.Add(healthSources);
        notifyInternet.CheckedChanged+=delegate { ResetIndicators(); QueueSave(); };
        notifyHeat.CheckedChanged+=delegate { ResetIndicators(); QueueSave(); };
        heatThreshold.ValueChanged+=delegate { ResetIndicators(); QueueSave(); };
    }
    void PollHealth(double now,bool active) {
        if(internetRead!=null && internetRead.IsCompleted) {
            if(internetRead.IsFaulted) { var error=internetRead.Exception; internetStatus="unavailable"; if(active && internetGeneration==noticeGeneration) indicators.Internet(null,now,(double)noticeDuration.Value); }
            else if(active && notifyInternet.Checked && internetGeneration==noticeGeneration) {
                bool? value=internetRead.Result;
                indicators.Internet(value,now,(double)noticeDuration.Value);
                internetStatus=value.HasValue?(value.Value?"connected":"no Internet"):"Windows status unavailable";
            }
            internetRead=null;
        }
        if(temperatureRead!=null && temperatureRead.IsCompleted) {
            if(temperatureRead.IsFaulted) { var error=temperatureRead.Exception; thermalStatus="temperature unavailable"; nextTemperature=now+30; if(active && temperatureGeneration==noticeGeneration) indicators.Temperature(new ThermalState(),(int)heatThreshold.Value,now,(double)noticeDuration.Value); }
            else if(active && notifyHeat.Checked && temperatureGeneration==noticeGeneration) {
                var value=temperatureRead.Result;
                indicators.Temperature(value,(int)heatThreshold.Value,now,(double)noticeDuration.Value);
                thermalStatus="CPU: "+(value.CPU.HasValue?value.CPU.Value.ToString("F0")+" C":"unavailable")+"\nGPU: "+(value.GPU.HasValue?value.GPU.Value.ToString("F0")+" C ("+value.GPUProvider+")":"unavailable");
                if(!value.Available) { thermalStatus+="\nOptional: run Libre Hardware Monitor for sensors."; nextTemperature=now+30; }
            }
            temperatureRead=null;
        }
        if(active && !testing) {
            if(notifyInternet.Checked && internetRead==null && now>=nextInternet) {
                nextInternet=now+3; internetGeneration=noticeGeneration; internetRead=Task.Factory.StartNew(()=>SystemHealth.Internet());
            }
            if(notifyHeat.Checked && temperatureRead==null && now>=nextTemperature) {
                nextTemperature=now+5; temperatureGeneration=noticeGeneration; temperatureRead=Task.Factory.StartNew(()=>SystemHealth.Temperature());
            }
        }
        string text="Internet: "+(notifyInternet.Checked?internetStatus:"disabled")+"\n"+(notifyHeat.Checked?thermalStatus:"Temperature: disabled");
        if(healthSources.Text!=text) healthSources.Text=text;
    }
}
