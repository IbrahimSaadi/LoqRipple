using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

// Shared controls for every page and app-owned dialog.
sealed class StudioSwitch : CheckBox {
    public StudioSwitch() {
        AutoSize=true; Cursor=Cursors.Hand;
        SetStyle(ControlStyles.UserPaint|ControlStyles.AllPaintingInWmPaint|ControlStyles.OptimizedDoubleBuffer,true);
    }
    public override Size GetPreferredSize(Size proposedSize) {
        Size text=TextRenderer.MeasureText(Text,Font);
        return new Size(text.Width+58,Math.Max(28,text.Height+6));
    }
    protected override void OnPaint(PaintEventArgs e) {
        e.Graphics.Clear(Parent==null?StudioTheme.Surface:Parent.BackColor);
        StudioControlPaint.Switch(e.Graphics,new Point(0,Math.Max(0,(Height-26)/2)),Checked,Enabled);
        var bounds=new Rectangle(54,0,Math.Max(0,Width-54),Height);
        TextRenderer.DrawText(e.Graphics,Text,Font,bounds,Enabled?StudioTheme.Text:StudioTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if(Focused) ControlPaint.DrawFocusRectangle(e.Graphics,ClientRectangle);
    }
}
static class StudioControlPaint {
    public static void Switch(Graphics g,Point origin,bool on,bool enabled) {
        var old=g.Save(); g.TranslateTransform(origin.X,origin.Y); g.SmoothingMode=SmoothingMode.AntiAlias;
        using(var path=new GraphicsPath()) {
            path.AddArc(0,3,20,20,90,180); path.AddArc(23,3,20,20,270,180); path.CloseFigure();
            using(var fill=new SolidBrush(on && enabled?StudioTheme.Accent:StudioTheme.Border)) g.FillPath(fill,path);
        }
        using(var knob=new SolidBrush(Color.White)) g.FillEllipse(knob,on?25:3,5,16,16);
        g.Restore(old);
    }
}
class StudioCombo : ComboBox {
    public StudioCombo() {
        DrawMode=DrawMode.OwnerDrawFixed; DropDownStyle=ComboBoxStyle.DropDownList; ItemHeight=26;
        FlatStyle=FlatStyle.Flat; IntegralHeight=false; DropDownHeight=360;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        if(e.Index<0 || e.Index>=Items.Count) return;
        bool edit=(e.State&DrawItemState.ComboBoxEdit)!=0;
        Color bg=(e.State&DrawItemState.Selected)!=0 && !edit?StudioTheme.Selected:edit?StudioTheme.Input:StudioTheme.Surface;
        using(var brush=new SolidBrush(bg)) e.Graphics.FillRectangle(brush,e.Bounds);
        var r=new Rectangle(e.Bounds.Left+9,e.Bounds.Top,Math.Max(0,e.Bounds.Width-15),e.Bounds.Height);
        TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,r,Enabled?StudioTheme.Text:StudioTheme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if((e.State&DrawItemState.Focus)!=0 && !edit) ControlPaint.DrawFocusRectangle(e.Graphics,e.Bounds);
    }
    protected override void OnSizeChanged(EventArgs e) { base.OnSizeChanged(e); StudioTheme.Round(this,6); }
    protected override void OnEnter(EventArgs e) { base.OnEnter(e); Invalidate(); }
    protected override void OnLeave(EventArgs e) { base.OnLeave(e); Invalidate(); }
    protected override void WndProc(ref Message m) {
        base.WndProc(ref m);
        if((m.Msg==0xf || m.Msg==0x85) && IsHandleCreated && Width>30 && Height>10) {
            using(var g=Graphics.FromHwnd(Handle)) {
                g.SmoothingMode=SmoothingMode.AntiAlias;
                using(var brush=new SolidBrush(StudioTheme.Input)) g.FillRectangle(brush,Width-28,0,28,Height);
                float x=Width-15,y=Height/2f;
                using(var pen=new Pen(Enabled?StudioTheme.Muted:StudioTheme.Border,1.6f)) { pen.StartCap=pen.EndCap=LineCap.Round; g.DrawLine(pen,x-4,y-2,x,y+2); g.DrawLine(pen,x,y+2,x+4,y-2); }
                if(Focused) using(var pen=new Pen(StudioTheme.Accent,2)) g.DrawLine(pen,7,Height-2,Width-7,Height-2);
            }
        }
    }
}
sealed class StudioTextBox : TextBox {
    public StudioTextBox() { BorderStyle=BorderStyle.None; BackColor=StudioTheme.Input; }
}
sealed class StudioNumberView : Panel {
    public StudioNumberView(NumericUpDown state) {
        Height=42; Tag="whitepanel";
        var slider=new StudioSlider { Dock=DockStyle.Fill,Minimum=(int)state.Minimum,Maximum=(int)state.Maximum,Value=(int)state.Value,Enabled=state.Enabled,AccessibleName=state.AccessibleName??"Value" };
        slider.ValueChanged+=delegate { state.Value=slider.Value; };
        state.ValueChanged+=delegate { slider.Value=(int)state.Value; };
        state.EnabledChanged+=delegate { slider.Enabled=state.Enabled; };
        state.Visible=false; Controls.Add(state); Controls.Add(slider);
    }
}

sealed partial class RippleForm {
    Form CreateProfileDialog(out StudioTextBox name) {
        var dialog=new Form { Text="Save lighting profile",ClientSize=new Size(420,190),Font=Font,AutoScaleMode=AutoScaleMode.Dpi,FormBorderStyle=FormBorderStyle.FixedDialog,StartPosition=FormStartPosition.CenterParent,MinimizeBox=false,MaximizeBox=false,ShowInTaskbar=false };
        var title=new Label { Text="Name your lighting profile",Bounds=new Rectangle(24,20,372,28),Font=new Font("Segoe UI",12,FontStyle.Bold) };
        var field=new Panel { Bounds=new Rectangle(24,60,372,42),Padding=new Padding(12,10,12,8),Tag="input" };
        var editor=new StudioTextBox { Dock=DockStyle.Fill,MaxLength=60,AccessibleName="Profile name" }; name=editor;
        field.Controls.Add(editor);
        var save=new Button { Text="Save profile",Bounds=new Rectangle(274,126,122,38),DialogResult=DialogResult.OK,Tag="primary",Enabled=false };
        var cancel=new Button { Text="Cancel",Bounds=new Rectangle(160,126,104,38),DialogResult=DialogResult.Cancel };
        editor.TextChanged+=delegate { save.Enabled=!string.IsNullOrWhiteSpace(editor.Text); };
        dialog.Controls.Add(title); dialog.Controls.Add(field); dialog.Controls.Add(save); dialog.Controls.Add(cancel);
        dialog.AcceptButton=save; dialog.CancelButton=cancel;
        dialog.Shown+=delegate { SetDarkTitle(dialog); editor.Focus(); };
        StudioTheme.Apply(dialog); HighlightButtons(dialog); StudioTheme.Round(field,8); return dialog;
    }
    static void AuditModernControls(Control root) {
        if(!root.Visible) return;
        if(root is NumericUpDown) throw new Exception("Legacy numeric control visible: "+root.Name);
        if(root is CheckBox && !(root is StudioToggle) && !(root is StudioSwitch)) throw new Exception("Legacy checkbox visible: "+root.Text);
        if(root is ComboBox && !(root is StudioCombo)) throw new Exception("Legacy selector visible: "+root.Text);
        if(root is TextBox && !(root is StudioTextBox)) throw new Exception("Legacy text field visible: "+root.Name);
        foreach(Control child in root.Controls) AuditModernControls(child);
    }
    void TestUnifiedControls() {
        var tabs=FindStudioTabs(this); int page=tabs.SelectedIndex;
        foreach(TabPage tab in tabs.TabPages) { tabs.SelectedTab=tab; Application.DoEvents(); AuditModernControls(this); }
        tabs.SelectedIndex=page;
        settingsWindow.Show(this); AuditModernControls(settingsWindow); settingsWindow.Hide();
        StudioTextBox name;
        using(var dialog=CreateProfileDialog(out name)) {
            dialog.Show(this); AuditModernControls(dialog); name.Text="Test profile";
            if(!((Button)dialog.AcceptButton).Enabled) throw new Exception("Profile name validation failed");
            dialog.Hide();
        }
        using(var model=new NumericUpDown { Minimum=1,Maximum=100,Value=25 }) using(var view=new StudioNumberView(model)) {
            StudioSlider slider=null; foreach(Control item in view.Controls) if(item is StudioSlider) slider=(StudioSlider)item;
            model.Value=60; if(slider==null || slider.Value!=60) throw new Exception("Number view did not follow saved value");
            slider.Value=80; if(model.Value!=80) throw new Exception("Number view did not update setting");
        }
        Console.WriteLine("PASS: all pages, general settings and profile dialog use unified controls; number binding and profile validation work.");
    }
}
