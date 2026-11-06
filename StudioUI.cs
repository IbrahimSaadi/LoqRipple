using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Runtime.InteropServices;

sealed class MacCard : FlowLayoutPanel {
    protected override void OnSizeChanged(EventArgs e) {
        base.OnSizeChanged(e);
        if(Width<36 || Height<36) return;
        using(var p=new GraphicsPath()) {
            int r=22; p.AddArc(0,0,r,r,180,90); p.AddArc(Width-r-1,0,r,r,270,90); p.AddArc(Width-r-1,Height-r-1,r,r,0,90); p.AddArc(0,Height-r-1,r,r,90,90); p.CloseFigure();
            Region old=Region; Region=new Region(p); if(old!=null) old.Dispose();
        }
    }
}
sealed class StudioToggle : CheckBox {
    public StudioToggle() { AutoSize=false; Size=new Size(44,26); Text=""; Cursor=Cursors.Hand; SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true); }
    protected override void OnPaintBackground(PaintEventArgs e) { e.Graphics.Clear(Parent==null?StudioTheme.Surface:Parent.BackColor); }
    protected override void OnPaint(PaintEventArgs e) {
        StudioControlPaint.Switch(e.Graphics,Point.Empty,Checked,Enabled);
        if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
}
sealed class DetailCardTable : TableLayoutPanel {
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); StudioTheme.Round(this,12); }
}
sealed class StudioSlider : Control {
    int minimum,maximum=100,value;
    public int Minimum { get { return minimum; } set { minimum=value; } }
    public int Maximum { get { return maximum; } set { maximum=value; } }
    public int TickFrequency { get; set; }
    public int Value { get { return value; } set { int next=Math.Max(minimum,Math.Min(maximum,value)); if(this.value==next) return; this.value=next; Invalidate(); if(ValueChanged!=null) ValueChanged(this,EventArgs.Empty); } }
    public event EventHandler ValueChanged;
    public StudioSlider() { SetStyle(ControlStyles.AllPaintingInWmPaint|ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer|ControlStyles.Selectable,true); Size=new Size(320,38); TabStop=true; AccessibleRole=AccessibleRole.Slider; Cursor=Cursors.Hand; }
    void MoveValue(int x) { Value=minimum+(int)Math.Round(Math.Max(0,Math.Min(1,(x-9.0)/Math.Max(1,Width-70)))*(maximum-minimum)); }
    protected override void OnMouseDown(MouseEventArgs e) { base.OnMouseDown(e); if(e.Button==MouseButtons.Left) { Focus(); Capture=true; MoveValue(e.X); } }
    protected override void OnMouseMove(MouseEventArgs e) { base.OnMouseMove(e); if(Capture && e.Button==MouseButtons.Left) MoveValue(e.X); }
    protected override void OnMouseUp(MouseEventArgs e) { base.OnMouseUp(e); Capture=false; }
    protected override bool IsInputKey(Keys key) { return key==Keys.Left || key==Keys.Right || key==Keys.Up || key==Keys.Down || base.IsInputKey(key); }
    protected override void OnKeyDown(KeyEventArgs e) { base.OnKeyDown(e); if(e.KeyCode==Keys.Left || e.KeyCode==Keys.Down) { Value--; e.Handled=true; } if(e.KeyCode==Keys.Right || e.KeyCode==Keys.Up) { Value++; e.Handled=true; } if(e.KeyCode==Keys.Home) Value=minimum; if(e.KeyCode==Keys.End) Value=maximum; }
    protected override void OnPaint(PaintEventArgs e) {
        base.OnPaint(e); var g=e.Graphics; g.SmoothingMode=SmoothingMode.AntiAlias;
        float center=Height/2f;
        float end=Math.Max(10,Width-61), x=9+(end-9)*(value-minimum)/(float)Math.Max(1,maximum-minimum);
        using(var bg=new Pen(StudioTheme.Border,5)) { bg.StartCap=bg.EndCap=LineCap.Round; g.DrawLine(bg,9,center,end,center); }
        using(var fill=new Pen(StudioTheme.Accent,5)) { fill.StartCap=fill.EndCap=LineCap.Round; g.DrawLine(fill,9,center,x,center); }
        using(var knob=new SolidBrush(Enabled?StudioTheme.Text:StudioTheme.Muted)) g.FillEllipse(knob,x-6,center-6,12,12);
        TextRenderer.DrawText(g,value.ToString(),Font,new Rectangle(Width-48,0,46,Height),StudioTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.Right);
        if(Focused) using(var focus=new Pen(StudioTheme.Accent)) g.DrawEllipse(focus,x-9,center-9,18,18);
    }
}
sealed class StudioTabs : TabControl {
    public StudioTabs() { DrawMode=TabDrawMode.OwnerDrawFixed; SizeMode=TabSizeMode.Fixed; ItemSize=new Size(155,38); Padding=new Point(12,4); }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        var r=GetTabRect(e.Index);
        using(var brush=new SolidBrush(StudioTheme.Background)) e.Graphics.FillRectangle(brush,r);
        bool selected=e.Index==SelectedIndex;
        TextRenderer.DrawText(e.Graphics,TabPages[e.Index].Text,Font,r,selected?StudioTheme.Text:StudioTheme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);
        if(selected) using(var pen=new Pen(StudioTheme.Accent,3)) e.Graphics.DrawLine(pen,r.Left+16,r.Bottom-3,r.Right-16,r.Bottom-3);
    }
}
static class StudioTheme {
    public static Color Background=Color.FromArgb(245,246,248),Surface=Color.White,Input=Color.FromArgb(242,244,247),Border=Color.FromArgb(221,226,233),Text=Color.FromArgb(25,32,45),Muted=Color.FromArgb(88,102,122),Accent=Color.FromArgb(79,70,229);
    public static bool IsDark { get; private set; }
    public static Color OnAccent=Color.White;
    public static Color Selected=Color.FromArgb(232,230,255);
    public static void SetDark(bool dark) {
        IsDark=dark; OnAccent=dark?Color.FromArgb(20,20,35):Color.White;
        Background=dark?Color.FromArgb(18,21,28):Color.FromArgb(245,246,248);
        Surface=dark?Color.FromArgb(27,32,43):Color.White;
        Input=dark?Color.FromArgb(37,43,57):Color.FromArgb(242,244,247);
        Border=dark?Color.FromArgb(64,73,91):Color.FromArgb(221,226,233);
        Text=dark?Color.FromArgb(235,239,247):Color.FromArgb(25,32,45);
        Muted=dark?Color.FromArgb(165,177,198):Color.FromArgb(88,102,122);
        Accent=dark?Color.FromArgb(151,140,255):Color.FromArgb(79,70,229);
        Selected=dark?Color.FromArgb(57,49,91):Color.FromArgb(232,230,255);
    }
    static void RoundButton(object sender,EventArgs e) { Round((Control)sender,7); }
    public static void Round(Control control,int radius) {
        if(control.Width<radius*2 || control.Height<radius*2) return;
        using(var p=new GraphicsPath()) {
            int d=radius*2,w=control.Width-1,h=control.Height-1;
            p.AddArc(0,0,d,d,180,90); p.AddArc(w-d,0,d,d,270,90); p.AddArc(w-d,h-d,d,d,0,90); p.AddArc(0,h-d,d,d,90,90); p.CloseFigure();
            Region old=control.Region; control.Region=new Region(p); if(old!=null) old.Dispose();
        }
    }
    public static void Apply(Control root) {
        if(Convert.ToString(root.Tag)=="rgb-preview") return;
        root.BackColor=root is Form || root is TabControl || root is TabPage || root is TableLayoutPanel?Background:Surface; root.ForeColor=Text;
        if(root is DetailCardTable || Convert.ToString(root.Tag)=="whitepanel") root.BackColor=Surface;
        if(Convert.ToString(root.Tag)=="canvas") root.BackColor=Background;
        if(Convert.ToString(root.Tag)=="input") root.BackColor=Input;
        if(root is Label && root.Parent!=null) root.BackColor=Color.Transparent;
        if(root is ScrollableControl) { root.HandleCreated-=RippleForm.NativeThemeHandleCreated; root.HandleCreated+=RippleForm.NativeThemeHandleCreated; RippleForm.ThemeNativeControl(root); }
        foreach(Control child in root.Controls) Apply(child);
        var flow=root as FlowLayoutPanel;
        if(flow!=null) flow.Margin=root is MacCard?new Padding(8):new Padding(0);
        var label=root as Label;
        if(label!=null) { label.UseMnemonic=false; label.Margin=new Padding(3,5,3,3); if(Convert.ToString(label.Tag)=="muted" || Convert.ToString(label.Tag)=="indicator-hint") label.ForeColor=Muted; if(Convert.ToString(label.Tag)=="compact-label" || Convert.ToString(label.Tag)=="indicator-hint") label.Margin=Padding.Empty; }
        var button=root as Button;
        if(button!=null) { button.FlatStyle=FlatStyle.Flat; button.FlatAppearance.BorderColor=Border; button.FlatAppearance.MouseOverBackColor=Input; button.BackColor=Input; button.Padding=new Padding(10,3,10,3); button.MinimumSize=new Size(100,32); button.Margin=new Padding(3,3,7,3); button.Cursor=Cursors.Hand; Round(button,7); button.Resize-=RoundButton; button.Resize+=RoundButton; }
        var combo=root as ComboBox;
        if(combo!=null) { combo.FlatStyle=FlatStyle.Flat; combo.BackColor=Input; combo.Margin=new Padding(0,3,0,3); }
        var number=root as NumericUpDown;
        if(number!=null) { number.BackColor=Input; number.ForeColor=Text; number.BorderStyle=BorderStyle.FixedSingle; number.Margin=new Padding(3,5,3,9); }
        var check=root as CheckBox;
        if(check!=null) { check.FlatStyle=FlatStyle.Flat; check.Margin=check is StudioSwitch?new Padding(0,0,6,0):new Padding(3,5,3,5); }
        var box=root as TextBox;
        if(box!=null) { box.BackColor=Input; box.BorderStyle=box is StudioTextBox?BorderStyle.None:BorderStyle.FixedSingle; }
        var grid=root as PropertyGrid;
        if(grid!=null) {
            grid.ViewBackColor=Surface; grid.ViewForeColor=Text; grid.ViewBorderColor=Border; grid.LineColor=Input;
            grid.CategoryForeColor=Accent; grid.CategorySplitterColor=Border; grid.HelpBackColor=Input; grid.HelpForeColor=Muted; grid.HelpBorderColor=Border;
            grid.SelectedItemWithFocusBackColor=Color.FromArgb(218,234,255); grid.SelectedItemWithFocusForeColor=Text;
        }
        var split=root as SplitContainer; if(split!=null) split.BackColor=Background;
    }
}
sealed partial class RippleForm {
    FlowLayoutPanel StudioCard(string title,string subtitle) {
        var card=new MacCard { Dock=DockStyle.Fill,FlowDirection=FlowDirection.TopDown,WrapContents=false,AutoScroll=true,Padding=new Padding(18),Margin=new Padding(8) };
        card.Controls.Add(new Label { Text=title,AutoSize=true,Font=new Font("Segoe UI",12,FontStyle.Bold) });
        if(subtitle!=null) card.Controls.Add(new Label { Text=subtitle,AutoSize=true,MaximumSize=new Size(370,0),ForeColor=StudioTheme.Muted,Tag="muted" });
        return card;
    }
    static void Section(FlowLayoutPanel card,string text) { card.Controls.Add(new Label { Text=text,AutoSize=true,Font=new Font("Segoe UI",9,FontStyle.Bold),Margin=new Padding(3,18,3,4) }); }
    TableLayoutPanel lightingLayout,primaryControls;
    Button sharedColorButton;
    Control BuildLightingStudio(FlowLayoutPanel old) {
        var table=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(16,8,16,16),Tag="canvas" };
        lightingLayout=table;
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,57)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,43));
        table.RowStyles.Add(new RowStyle(SizeType.Percent,100)); table.RowStyles.Add(new RowStyle(SizeType.Absolute,154));
        var main=new Panel { Dock=DockStyle.Fill,Padding=new Padding(22),Margin=new Padding(0,0,12,12),Tag="whitepanel" };
        main.Resize+=delegate { StudioTheme.Round(main,12); };
        FlowLayoutPanel profileRow=null,strip=null;
        foreach(Control c in old.Controls) if(c is FlowLayoutPanel) { if(profileRow==null) profileRow=(FlowLayoutPanel)c; else strip=(FlowLayoutPanel)c; }
        BuildAdvanced();
        primaryControls=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Tag="whitepanel",Margin=Padding.Empty };
        primaryControls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        int[] heights={26,44,26,38,50,26,38,70};
        foreach(int height in heights) primaryControls.RowStyles.Add(new RowStyle(SizeType.Absolute,height));
        primaryControls.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        string[] headings={"Saved profile","Main effect","Blend with a second effect"}; int[] rows={0,2,5};
        for(int i=0;i<headings.Length;i++) primaryControls.Controls.Add(new Label { Text=headings[i],Dock=DockStyle.Fill,Tag="compact-label",Font=new Font("Segoe UI",10,FontStyle.Bold) },0,rows[i]);
        if(profileRow!=null) {
            var profileLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Tag="whitepanel",Margin=Padding.Empty };
            profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); profileLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,112));
            var save=profileRow.Controls[1]; save.AutoSize=false; profiles.Dock=DockStyle.Fill; save.Dock=DockStyle.Fill; save.Text="Save";
            profileLayout.Controls.Add(profiles,0,0); profileLayout.Controls.Add(save,1,0); primaryControls.Controls.Add(profileLayout,0,1);
        }
        effect.Dock=DockStyle.Fill; primaryControls.Controls.Add(effect,0,3);
        description.Dock=DockStyle.Fill; description.Tag="muted"; primaryControls.Controls.Add(description,0,4);
        secondary.Dock=DockStyle.Fill; primaryControls.Controls.Add(secondary,0,6);
        mixOptions.Dock=DockStyle.Fill; mixOptions.AutoSize=false; mixOptions.Margin=Padding.Empty;
        foreach(Control item in mixOptions.Controls) if(item is Label) item.Text="Blend amount";
        mixOptions.Resize+=delegate { mix.Width=Math.Max(180,mixOptions.ClientSize.Width-8); };
        primaryControls.Controls.Add(mixOptions,0,7); main.Controls.Add(primaryControls);
        var previewCard=new Panel { Dock=DockStyle.Fill,Padding=new Padding(22,12,22,12),Margin=new Padding(0,0,12,0),Tag="whitepanel" };
        previewCard.Resize+=delegate { StudioTheme.Round(previewCard,12); };
        var previewLayout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=4,Tag="whitepanel" };
        foreach(int h in new[]{26,44,28,28}) previewLayout.RowStyles.Add(new RowStyle(SizeType.Absolute,h));
        previewLayout.Controls.Add(new Label { Text="Live keyboard preview",AutoSize=true,Font=new Font("Segoe UI",10,FontStyle.Bold),Tag="compact-label" },0,0);
        var zones=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,Tag="whitepanel" };
        for(int i=0;i<4;i++) { zones.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,25)); preview[i].Dock=DockStyle.Fill; preview[i].BorderStyle=BorderStyle.None; preview[i].Margin=new Padding(0,3,6,7); zones.Controls.Add(preview[i],i,0); }
        previewLayout.Controls.Add(zones,0,1);
        rippleOverlay.Text="Typing waves"; clickRipple.Text="Click waves"; pureScreen.Text="Pure screen colors";
        var reactionRow=new FlowLayoutPanel { Dock=DockStyle.Fill,WrapContents=false,Margin=Padding.Empty };
        reactionRow.Controls.Add(rippleOverlay); reactionRow.Controls.Add(clickRipple); previewLayout.Controls.Add(reactionRow,0,2);
        previewLayout.Controls.Add(pureScreen,0,3); previewCard.Controls.Add(previewLayout);
        var tools=new Panel { Dock=DockStyle.Fill,Padding=new Padding(22),Margin=Padding.Empty,Tag="whitepanel" };
        tools.Resize+=delegate { StudioTheme.Round(tools,12); };
        var controls=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=1,RowCount=9,Tag="whitepanel" };
        controls.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));
        int[] sizes={32,28,48,60,44,44,44,42};
        foreach(int h in sizes) controls.RowStyles.Add(new RowStyle(SizeType.Absolute,h)); controls.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        Action<string,int> label=delegate(string text,int row) { controls.Controls.Add(new Label { Text=text,Dock=DockStyle.Fill,Tag="compact-label",Font=new Font("Segoe UI",10,FontStyle.Bold) },0,row); };
        label("Playback",0); label("Master brightness",1); brightness.Dock=DockStyle.Fill; controls.Controls.Add(brightness,0,2);
        controls.Controls.Add(new Label { Text="Controls the overall light level. Adjust motion and colors in Effect settings.",Dock=DockStyle.Fill,Tag="muted" },0,3);
        var buttons=new System.Collections.Generic.List<Button>(); foreach(Control c in old.Controls) if(c is Button) buttons.Add((Button)c);
        int actionRow=4;
        foreach(var button in buttons) {
            button.AutoSize=false; button.Dock=DockStyle.Fill;
            if(button.Text=="Choose color") { sharedColorButton=button; continue; }
            if(button.Text=="Start / Resume") { button.Text="Start lighting"; button.Tag="primary"; }
            if(button.Text=="Stop / Restore light") button.Text="Stop lighting";
            controls.Controls.Add(button,0,actionRow++);
        }
        status.Dock=DockStyle.Fill; status.AutoSize=false; status.Tag="muted"; controls.Controls.Add(status,0,7); tools.Controls.Add(controls);
        table.Controls.Add(main,0,0); table.Controls.Add(previewCard,0,1); table.Controls.Add(tools,1,0); table.SetRowSpan(tools,2); return table;
    }
    Control BuildIndicatorStudio(FlowLayoutPanel old) {
        var table=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,Padding=new Padding(8) };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,50)); table.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var sources=StudioCard("System signals","Choose what your keyboard tells you.");
        var preferences=StudioCard("Behavior & timing","Tune your alerts and focus sessions.");
        Section(sources,"SYSTEM EVENTS");
        notifyInternet.Text="Internet connection"; notifyHeat.Text="CPU / GPU temperature";
        AddIndicatorSwitch(sources,notifyVolume,"Volume & mute","A brief level bar when volume changes");
        AddIndicatorSwitch(sources,notifyBrightness,"Screen brightness","Follow brightness adjustments");
        AddIndicatorSwitch(sources,notifyBattery,"Battery alerts","Low charge and charger changes");
        AddIndicatorSwitch(sources,notifyInternet,"Internet connection","Signal disconnects and recovery");
        AddIndicatorSwitch(sources,notifyHeat,"Temperature warning","Alert when CPU or GPU gets hot");
        BuildUtilityControls(sources,preferences);
        Section(sources,"LIVE SOURCES"); sources.Controls.Add(notificationSources); sources.Controls.Add(healthSources);
        AddIndicatorNumber(preferences,noticeDuration,"ALERT DURATION · SECONDS");
        AddIndicatorNumber(preferences,batteryThreshold,"LOW BATTERY · PERCENT");
        AddIndicatorNumber(preferences,heatThreshold,"TEMPERATURE LIMIT · CELSIUS");
        Button preview=null; foreach(Control c in old.Controls) if(c is Button) preview=(Button)c;
        Section(preferences,"TRY IT OUT"); if(preview!=null) { preview.Width=260; preferences.Controls.Add(preview); }
        preferences.Controls.Add(new Label { Text="Alerts return to the previous effect automatically. Your brightness limit always applies.",AutoSize=true,MaximumSize=new Size(320,0),Tag="muted" });
        table.Controls.Add(sources,0,0); table.Controls.Add(preferences,1,0); return table;
    }
    [DllImport("user32.dll")] static extern bool ReleaseCapture();
    [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr h,int message,IntPtr w,IntPtr l);
    Control BuildStudioHeader() {
        var header=new Panel { Dock=DockStyle.Top,Height=88,Tag="canvas" };
        var title=new Label { Text="LOQ Ripple",AutoSize=false,Size=new Size(300,38),Location=new Point(24,12),Font=new Font("Segoe UI",19,FontStyle.Bold) };
        var subtitle=new Label { Text="Your keyboard. Your atmosphere.",AutoSize=false,Size=new Size(360,24),Location=new Point(27,53),Tag="muted" };
        header.Controls.Add(title); header.Controls.Add(subtitle);
        var settingsButton=new Button { Text="Settings",Size=new Size(110,36),Anchor=AnchorStyles.Top|AnchorStyles.Right };
        header.Controls.Add(settingsButton); header.Resize+=delegate { settingsButton.Location=new Point(header.Width-134,24); };
        settingsButton.Click+=delegate { ShowGeneralSettings(); }; return header;
    }
    StudioTabs studioTabs;
    Action refreshNavigation;
    Control BuildTabHost(StudioTabs tabs) {
        studioTabs=tabs;
        var host=new Panel { Dock=DockStyle.Fill,Padding=new Padding(0),Tag="canvas" };
        var viewport=new Panel { Tag="canvas" }; host.Controls.Add(viewport);
        tabs.Dock=DockStyle.None; viewport.Controls.Add(tabs);
        Action fit=delegate {
            int width=Math.Min(host.ClientSize.Width,1160);
            int height=Math.Min(host.ClientSize.Height,720);
            viewport.Bounds=new Rectangle((host.ClientSize.Width-width)/2,0,width,height);
            tabs.Bounds=new Rectangle(-4,-45,width+8,height+49);
        };
        host.Resize+=delegate { fit(); }; fit(); return host;
    }
    Control BuildMacNavigation(StudioTabs tabs) {
        var strip=new Panel { Dock=DockStyle.Left,Width=166,Padding=new Padding(12),Tag="canvas" };
        var label=new Label { Text="WORKSPACE",Location=new Point(24,20),AutoSize=true,Font=new Font("Segoe UI",8,FontStyle.Bold),Tag="muted" }; strip.Controls.Add(label);
        string[] names={"Lighting","Effect settings","Indicators"}; var buttons=new Button[3];
        Action update=delegate {
            for(int i=0;i<3;i++) {
                bool active=tabs.SelectedIndex==i;
                buttons[i].BackColor=active?StudioTheme.Selected:StudioTheme.Background;
                buttons[i].ForeColor=active?StudioTheme.Accent:StudioTheme.Muted;
            }
        };
        for(int i=0;i<3;i++) {
            int index=i;
            buttons[i]=new Button { Text=names[i],Bounds=new Rectangle(12,49+i*49,148,42),TextAlign=ContentAlignment.MiddleLeft,FlatStyle=FlatStyle.Flat,Tag="nav",Font=new Font("Segoe UI",10,FontStyle.Bold) };
            buttons[i].FlatAppearance.BorderSize=0; buttons[i].Click+=delegate { tabs.SelectedIndex=index; }; strip.Controls.Add(buttons[i]);
        }
        var hint=new Label { Text="QUICK STOP\nCtrl + Alt + F12",AutoSize=false,Size=new Size(140,44),Tag="muted" }; strip.Controls.Add(hint);
        strip.Resize+=delegate { hint.Location=new Point(24,strip.Height-64); };
        refreshNavigation=update;
        tabs.SelectedIndexChanged+=delegate { update(); }; Shown+=delegate { update(); }; return strip;
    }
    TabControl FindStudioTabs(Control root) {
        foreach(Control c in root.Controls) { if(c is TabControl) return (TabControl)c; var found=FindStudioTabs(c); if(found!=null) return found; }
        return null;
    }
    void ApplyStudioTheme() {
        StudioTheme.Apply(this); StudioTheme.Apply(settingsWindow);
        foreach(Control c in Controls) if(c.Dock==DockStyle.Top) { c.BackColor=StudioTheme.Background; foreach(Control item in c.Controls) if(!(item is Button)) item.BackColor=StudioTheme.Background; }
        HighlightButtons(this);
        trayMenu.Renderer=new ToolStripProfessionalRenderer(new StudioMenuColors());
        trayMenu.BackColor=StudioTheme.Surface; trayMenu.ForeColor=StudioTheme.Text;
        foreach(ToolStripItem item in trayMenu.Items) item.ForeColor=StudioTheme.Text;
        if(refreshNavigation!=null) refreshNavigation();
        Invalidate(true); if(settingsWindow!=null) settingsWindow.Invalidate(true);
    }
    void HighlightButtons(Control parent) {
        foreach(Control c in parent.Controls) { if(Convert.ToString(c.Tag)=="rail") c.BackColor=StudioTheme.Input; var button=c as Button; if(button!=null && Convert.ToString(button.Tag)=="primary") { button.BackColor=StudioTheme.Accent; button.ForeColor=StudioTheme.OnAccent; } if(button!=null && Convert.ToString(button.Tag)=="nav") { button.MinimumSize=Size.Empty; button.Padding=new Padding(12,0,0,0); button.FlatAppearance.BorderSize=0; } HighlightButtons(c); }
    }
}
