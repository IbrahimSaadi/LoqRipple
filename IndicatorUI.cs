using System;
using System.Drawing;
using System.Windows.Forms;

sealed partial class RippleForm {
    void FitIndicatorRow(FlowLayoutPanel card,Control row) {
        Action fit=delegate { row.Width=Math.Max(250,card.ClientSize.Width-card.Padding.Horizontal-24); };
        card.Resize+=delegate { fit(); }; fit();
    }
    void AddIndicatorSwitch(FlowLayoutPanel card,CheckBox state,string title,string subtitle) {
        var row=new Panel { Height=60,Margin=new Padding(0,2,0,2),Tag="whitepanel" };
        var layout=new TableLayoutPanel { Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(0,5,0,7),Tag="whitepanel" };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100)); layout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,54));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute,24)); layout.RowStyles.Add(new RowStyle(SizeType.Percent,100));
        var name=new Label { Text=title,Dock=DockStyle.Fill,AutoEllipsis=true,Tag="compact-label",Font=new Font("Segoe UI",10,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft };
        var hint=new Label { Text=subtitle,Dock=DockStyle.Fill,AutoEllipsis=true,Tag="indicator-hint",Font=new Font("Segoe UI",8.5f),ForeColor=StudioTheme.Muted,TextAlign=ContentAlignment.MiddleLeft };
        var toggle=new StudioToggle { Checked=state.Checked,Anchor=AnchorStyles.Right,AccessibleName=title,Enabled=state.Enabled };
        toggle.CheckedChanged+=delegate { state.Checked=toggle.Checked; };
        state.CheckedChanged+=delegate { toggle.Checked=state.Checked; };
        state.EnabledChanged+=delegate { toggle.Enabled=state.Enabled; };
        name.Click+=delegate { if(toggle.Enabled) toggle.Checked=!toggle.Checked; };
        hint.Click+=delegate { if(toggle.Enabled) toggle.Checked=!toggle.Checked; };
        name.Cursor=hint.Cursor=Cursors.Hand;
        layout.Controls.Add(name,0,0); layout.Controls.Add(hint,0,1); layout.Controls.Add(toggle,1,0); layout.SetRowSpan(toggle,2);
        state.Visible=false; row.Controls.Add(state); row.Controls.Add(layout);
        row.Paint+=delegate(object sender,PaintEventArgs e) { using(var pen=new Pen(StudioTheme.Input)) e.Graphics.DrawLine(pen,0,row.Height-1,row.Width,row.Height-1); };
        card.Controls.Add(row); FitIndicatorRow(card,row);
    }
    void AddIndicatorNumber(FlowLayoutPanel card,NumericUpDown state,string title) {
        Section(card,title);
        state.AccessibleName=title;
        var row=new StudioNumberView(state) { Margin=new Padding(0,0,0,8) };
        card.Controls.Add(row); FitIndicatorRow(card,row);
    }
}
