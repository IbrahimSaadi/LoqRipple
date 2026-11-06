using System;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Win32;

public enum AppAppearance { System, Light, Dark }
sealed partial class RippleForm {
    readonly StudioCombo appearance=new StudioCombo { Width=350,AccessibleName="Appearance" };
    static bool ResolveDark(AppAppearance mode) {
        if(mode==AppAppearance.Dark) return true;
        if(mode==AppAppearance.Light) return false;
        try {
            using(var key=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize")) return key!=null && Convert.ToInt32(key.GetValue("AppsUseLightTheme",1))==0;
        } catch(UnauthorizedAccessException) { return false; } catch(System.Security.SecurityException) { return false; }
    }
    void BuildAppearanceControls(FlowLayoutPanel panel) {
        Section(panel,"APPEARANCE");
        appearance.Items.AddRange(new object[]{"System (Windows)","Light","Dark"});
        appearance.SelectedIndex=Enum.IsDefined(typeof(AppAppearance),settings.Appearance)?(int)settings.Appearance:0;
        panel.Controls.Add(appearance);
        panel.Controls.Add(new Label { Text="System follows the Windows app theme automatically.",AutoSize=true,MaximumSize=new Size(360,0),Tag="muted" });
        appearance.SelectionChangeCommitted+=delegate {
            settings.Appearance=(AppAppearance)appearance.SelectedIndex; RefreshAppearance(true); QueueSave();
        };
    }
    void InitializeAppearance() {
        StudioTheme.SetDark(ResolveDark(settings.Appearance)); ApplyStudioTheme(); ApplyTitleBars();
        HandleCreated+=delegate { SetDarkTitle(this); };
        settingsWindow.HandleCreated+=delegate { SetDarkTitle(settingsWindow); };
        if(!testing) SystemEvents.UserPreferenceChanged+=WindowsAppearanceChanged;
    }
    void WindowsAppearanceChanged(object sender,UserPreferenceChangedEventArgs e) {
        if(settings.Appearance!=AppAppearance.System || IsDisposed || !IsHandleCreated) return;
        try { BeginInvoke((Action)delegate { if(!IsDisposed) RefreshAppearance(false); }); } catch(InvalidOperationException) {}
    }
    void RefreshAppearance(bool force) {
        bool dark=ResolveDark(settings.Appearance); if(!force && StudioTheme.IsDark==dark) return;
        StudioTheme.SetDark(dark); ApplyStudioTheme();
        foreach(Form owned in OwnedForms) { StudioTheme.Apply(owned); HighlightButtons(owned); SetDarkTitle(owned); owned.Invalidate(true); }
        ApplyTitleBars(); Invalidate(true);
    }
    [DllImport("uxtheme.dll",CharSet=CharSet.Unicode)] static extern int SetWindowTheme(IntPtr window,string app,string classes);
    public static void ThemeNativeControl(Control control) {
        if(!control.IsHandleCreated || !(control is ScrollableControl)) return;
        try { SetWindowTheme(control.Handle,StudioTheme.IsDark?"DarkMode_Explorer":null,StudioTheme.IsDark?"ScrollBar":null); } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
    }
    public static void NativeThemeHandleCreated(object sender,EventArgs e) { ThemeNativeControl((Control)sender); }
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr window,int attribute,ref int value,int size);
    static void SetDarkTitle(Form window) {
        if(window==null || !window.IsHandleCreated) return;
        int value=StudioTheme.IsDark?1:0;
        try { DwmSetWindowAttribute(window.Handle,20,ref value,4); } catch(DllNotFoundException) {} catch(EntryPointNotFoundException) {}
    }
    void ApplyTitleBars() { SetDarkTitle(this); SetDarkTitle(settingsWindow); }
    void DisposeAppearance() { if(!testing) SystemEvents.UserPreferenceChanged-=WindowsAppearanceChanged; }
    [DllImport("user32.dll")] static extern bool PrintWindow(IntPtr window,IntPtr dc,uint flags);
    static void SaveThemePreview(Form window,string path) {
        using(var bitmap=new Bitmap(window.Width,window.Height)) using(var g=Graphics.FromImage(bitmap)) {
            IntPtr dc=g.GetHdc();
            try { if(!PrintWindow(window.Handle,dc,2)) throw new Exception("Could not capture theme preview"); }
            finally { g.ReleaseHdc(dc); }
            bitmap.Save(path,System.Drawing.Imaging.ImageFormat.Png);
        }
    }
    public void AppearancePreviews() {
        try {
            Show(); Stop();
            foreach(var mode in new[]{AppAppearance.Light,AppAppearance.Dark}) {
                settings.Appearance=mode; appearance.SelectedIndex=(int)mode; RefreshAppearance(true);
                FindStudioTabs(this).SelectedIndex=2; Application.DoEvents();
                System.Threading.Thread.Sleep(80);
                SaveThemePreview(this,System.IO.Path.Combine(Application.StartupPath,"assets",mode.ToString().ToLower()+"-mode-preview.png"));
            }
            Console.WriteLine("PASS: saved light and dark Indicators previews.");
        } finally { Close(); }
    }
    void TestAppearance() {
        var saved=settings.Appearance;
        try {
            settings.Appearance=AppAppearance.Dark; RefreshAppearance(true);
            if(!StudioTheme.IsDark || BackColor.GetBrightness()>.25 || StudioTheme.Text.GetBrightness()<.7) throw new Exception("Dark palette failed");
            settings.Appearance=AppAppearance.Light; RefreshAppearance(true);
            if(StudioTheme.IsDark || BackColor.GetBrightness()<.9 || StudioTheme.Text.GetBrightness()>.3) throw new Exception("Light palette failed");
            if(!ResolveDark(AppAppearance.Dark) || ResolveDark(AppAppearance.Light)) throw new Exception("Explicit theme override failed");
            settings.Appearance=AppAppearance.System; RefreshAppearance(true);
            if(StudioTheme.IsDark!=ResolveDark(AppAppearance.System)) throw new Exception("System theme resolution failed");
        } finally { settings.Appearance=saved; RefreshAppearance(true); }
        Console.WriteLine("PASS: light/dark palettes, explicit overrides and Windows app theme resolution.");
    }
}

sealed class StudioMenuColors : ProfessionalColorTable {
    public override Color ToolStripDropDownBackground { get { return StudioTheme.Surface; } }
    public override Color ImageMarginGradientBegin { get { return StudioTheme.Surface; } }
    public override Color ImageMarginGradientMiddle { get { return StudioTheme.Surface; } }
    public override Color ImageMarginGradientEnd { get { return StudioTheme.Surface; } }
    public override Color MenuItemSelected { get { return StudioTheme.Selected; } }
    public override Color MenuItemBorder { get { return StudioTheme.Border; } }
    public override Color MenuBorder { get { return StudioTheme.Border; } }
    public override Color SeparatorDark { get { return StudioTheme.Border; } }
    public override Color SeparatorLight { get { return StudioTheme.Border; } }
}
