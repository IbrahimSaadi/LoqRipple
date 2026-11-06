using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Windows.Forms;

sealed partial class RippleForm {
    readonly FlowLayoutPanel detailFields=new FlowLayoutPanel { Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.LeftToRight,WrapContents=true,Padding=new Padding(4) };
    readonly List<Control> detailCards=new List<Control>();
    void FitDetailCards() {
        int width=Math.Max(300,Math.Min(550,(detailFields.ClientSize.Width-40)/2));
        foreach(Control card in detailCards) { card.MinimumSize=new Size(width,0); card.MaximumSize=new Size(width,0); card.Width=width; }
    }
    void DetailChanged(EffectDetails data,PropertyDescriptor property,object value) {
        property.SetValue(data,value);
        if(property.Name=="Color") { data.CustomColor=true; data.Rainbow=false; }
        data.Validate(); ResetDetailAnimation(); QueueSave();
    }
    Control DetailInput(EffectDetails data,PropertyDescriptor p) {
        if(p.PropertyType==typeof(bool)) {
            var check=new StudioToggle { Checked=(bool)p.GetValue(data),Anchor=AnchorStyles.Left,AccessibleName=p.DisplayName };
            check.CheckedChanged+=delegate { DetailChanged(data,p,check.Checked); }; return check;
        }
        if(p.PropertyType.IsEnum) {
            var combo=new StudioCombo { DropDownStyle=ComboBoxStyle.DropDownList,Dock=DockStyle.Fill };
            foreach(object item in Enum.GetValues(p.PropertyType)) combo.Items.Add(item);
            combo.SelectedItem=p.GetValue(data);
            combo.SelectionChangeCommitted+=delegate { DetailChanged(data,p,combo.SelectedItem); }; return combo;
        }
        if(p.PropertyType==typeof(Color)) {
            var button=new Button { Text="Pick color",Dock=DockStyle.Fill,AutoSize=false,Height=34 };
            button.Click+=delegate {
                using(var dialog=new ColorDialog { Color=(Color)p.GetValue(data),FullOpen=true }) if(dialog.ShowDialog(this)==DialogResult.OK) {
                    DetailChanged(data,p,dialog.Color); RefreshDetails();
                }
            };
            button.Paint+=delegate(object sender,PaintEventArgs e) { using(var brush=new SolidBrush((Color)p.GetValue(data))) e.Graphics.FillEllipse(brush,7,button.Height/2-4,8,8); };
            return button;
        }
        int min=0,max=400;
        switch(p.Name) {
            case "Speed": min=10; break;
            case "Brightness": case "TailLoss": max=100; break;
            case "WaveWidth": min=50;max=3000;break;
            case "Repeats":min=1;max=5;break;
            case "Sensitivity":min=10;break;
            case "FollowSmoothing":max=2000;break;
            case "PointerFade":min=50;max=5000;break;
            case "PointerWidth":min=20;max=300;break;
            case "Attack": case "ScreenSmoothing":max=3000;break;
            case "Release":max=5000;break;
            case "ScreenSensitivity":min=10;max=300;break;
        }
        var number=new StudioSlider { Minimum=min,Maximum=max,Value=Math.Max(min,Math.Min(max,(int)p.GetValue(data))),Dock=DockStyle.Fill,AccessibleName=p.DisplayName };
        number.ValueChanged+=delegate { DetailChanged(data,p,(int)number.Value); }; return number;
    }
    void RenderDetailFields(int mode,int layer) {
        detailFields.SuspendLayout();
        try {
            while(detailFields.Controls.Count>0) detailFields.Controls[0].Dispose(); detailCards.Clear();
            if(mode<0) {
                detailFields.Controls.Add(new Label { Text="Choose a second effect in Lighting to edit its details.",AutoSize=true,Padding=new Padding(16) }); return;
            }
            EffectDetails data=Details(mode,layer);
            var groups=new Dictionary<string,TableLayoutPanel>();
            foreach(PropertyDescriptor property in new DetailView(data,mode).GetProperties()) {
                string category=property.Category;
                if(category=="Motion" && mode==14) category="Pointer";
                if(category=="Motion" && mode<4) category="Wave";
                TableLayoutPanel group;
                if(!groups.TryGetValue(category,out group)) {
                    group=new DetailCardTable { ColumnCount=2,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Width=410,Padding=new Padding(16),Margin=new Padding(6) };
                    group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,46)); group.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,54));
                    var title=new Label { Text=category=="Layer"?"Appearance":category+" controls",Font=new Font("Segoe UI",11,FontStyle.Bold),AutoSize=true,Margin=new Padding(2,4,2,14) };
                    group.Controls.Add(title,0,0); group.SetColumnSpan(title,2); group.RowCount=1;
                    group.RowStyles.Add(new RowStyle(SizeType.Absolute,44));
                    detailFields.Controls.Add(group); detailCards.Add(group); groups[category]=group;
                }
                int row=group.RowCount++;
                group.RowStyles.Add(new RowStyle(SizeType.Absolute,54));
                var label=new Label { Text=property.DisplayName,AutoSize=true,MaximumSize=new Size(210,0),Anchor=AnchorStyles.Left,Margin=new Padding(2,10,8,10) };
                group.Controls.Add(label,0,row); group.Controls.Add(DetailInput(data,property),1,row);
            }
            FitDetailCards(); StudioTheme.Apply(detailFields);
            detailFields.BackColor=StudioTheme.Background;
            foreach(Control card in detailCards) card.BackColor=StudioTheme.Surface;
        } finally { detailFields.ResumeLayout(true); }
    }
}
