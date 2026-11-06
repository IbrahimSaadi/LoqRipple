param([switch]$Restore, [switch]$Maximized, [ValidateSet("Lighting","Effect settings","Indicators")][string]$Page="Lighting")
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public class UiCapture {
 [DllImport("user32.dll")] public static extern bool SetForegroundWindow(IntPtr h);
 [DllImport("user32.dll")] public static extern bool ShowWindow(IntPtr h,int n);
 [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] public static extern IntPtr FindWindow(string cls, string title);
 [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, int message, IntPtr w, IntPtr l);
 [DllImport("user32.dll")] public static extern bool PrintWindow(IntPtr h, IntPtr dc, uint flags);
 [DllImport("user32.dll")] public static extern bool GetWindowRect(IntPtr h, out R r);
 delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc e,IntPtr p);
 [DllImport("user32.dll")] static extern uint GetWindowThreadProcessId(IntPtr h,out uint p);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetWindowText(IntPtr h,System.Text.StringBuilder s,int n);
 public static IntPtr FindForProcess(int id) { IntPtr result=IntPtr.Zero; EnumWindows(delegate(IntPtr h,IntPtr p) { uint pid; GetWindowThreadProcessId(h,out pid); if(pid==id) { var s=new System.Text.StringBuilder(256); GetWindowText(h,s,256); if(s.ToString()=="LOQ Key Ripple") { result=h; return false; } } return true; },IntPtr.Zero); return result; }
 [DllImport("user32.dll")] static extern bool EnumChildWindows(IntPtr h, EnumProc callback, IntPtr data);
 [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h,System.Text.StringBuilder s,int n);
 public static void SelectPage(IntPtr window,string name) { EnumChildWindows(window,delegate(IntPtr h,IntPtr p) { var text=new System.Text.StringBuilder(256); GetWindowText(h,text,256); var cls=new System.Text.StringBuilder(256); GetClassName(h,cls,256); R rect; GetWindowRect(h,out rect); if(text.ToString()==name && rect.Rt-rect.L<220 && rect.B-rect.T<65) { SendMessage(h,0x201,new IntPtr(1),new IntPtr(0x000A000A)); SendMessage(h,0x202,IntPtr.Zero,new IntPtr(0x000A000A)); return false; } return true; },IntPtr.Zero); }
 public struct R { public int L,T,Rt,B; }
}
'@
$null=[UiCapture]::SetProcessDPIAware()
$p=Get-Process LoqRipple | Select-Object -First 1
$window=[UiCapture]::FindForProcess($p.Id)
if($window -eq [IntPtr]::Zero) { throw "Application window not ready" }
if($Restore) { $null=[UiCapture]::ShowWindow($window,9); $null=[UiCapture]::SetForegroundWindow($window); Start-Sleep -Milliseconds 300 }
[UiCapture]::SelectPage($window,$Page)
Start-Sleep -Milliseconds 200
if($Maximized) { $null=[UiCapture]::SendMessage($window,0x112,[IntPtr]0xF030,[IntPtr]::Zero); Start-Sleep -Milliseconds 250 }
$r=New-Object UiCapture+R
$null=[UiCapture]::GetWindowRect($window,[ref]$r)
$b=New-Object Drawing.Bitmap(($r.Rt-$r.L),($r.B-$r.T))
$g=[Drawing.Graphics]::FromImage($b)
$dc=$g.GetHdc()
try { $null=[UiCapture]::PrintWindow($window,$dc,2) } finally { $g.ReleaseHdc($dc) }
$b.Save((Join-Path $PSScriptRoot 'studio-preview.png'))
$g.Dispose(); $b.Dispose()
if($Maximized) { $null=[UiCapture]::SendMessage($window,0x112,[IntPtr]0xF120,[IntPtr]::Zero) }
