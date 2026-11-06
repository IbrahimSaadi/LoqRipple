using System;
using System.Drawing;
using System.Windows.Forms;

// Visual order can change without changing persisted effect IDs or shortcut semantics.
sealed class GroupedEffectCombo : StudioCombo {
    sealed class Choice {
        public int Id; public string Name,Group; public bool First;
        public override string ToString() { return Name; }
    }
    static readonly string[] groups={"Typing & keys","Touchpad & mouse","Motion & waves","Nature & atmosphere","Colors & pulse","Music & audio","Screen & battery"};
    static readonly int[][] modes={
        new[]{0,1,2,3,20,23,24,34,36,39},
        new[]{14,15,16,17},
        new[]{7,18,19,29,30,33,41},
        new[]{8,9,10,11,12,27,31,32,37,38},
        new[]{4,5,6,13,28},
        new[]{21,26,35,40},
        new[]{22,25}
    };
    public GroupedEffectCombo() {
        DrawMode=DrawMode.OwnerDrawVariable; DropDownStyle=ComboBoxStyle.DropDownList;
        IntegralHeight=false; DropDownHeight=440;
    }
    // Keep the original index contract for saved profiles, hotkeys and rendering.
    public new int SelectedIndex {
        get { var choice=SelectedItem as Choice; return choice==null?-1:choice.Id; }
        set {
            if(value<0) { base.SelectedIndex=-1; return; }
            for(int i=0;i<Items.Count;i++) if(((Choice)Items[i]).Id==value) { base.SelectedIndex=i; return; }
            throw new ArgumentOutOfRangeException("value");
        }
    }
    public void Populate(bool secondary) {
        BeginUpdate();
        try {
            Items.Clear();
            if(secondary) Items.Add(new Choice { Id=0,Name="None",Group="",First=false });
            for(int group=0;group<groups.Length;group++) for(int i=0;i<modes[group].Length;i++) {
                int mode=modes[group][i];
                Items.Add(new Choice { Id=mode+(secondary?1:0),Name=Effects.Names[mode],Group=groups[group],First=i==0 });
            }
        } finally { EndUpdate(); }
    }
    protected override void OnMeasureItem(MeasureItemEventArgs e) {
        base.OnMeasureItem(e);
        var choice=e.Index>=0 && e.Index<Items.Count?Items[e.Index] as Choice:null;
        int line=Font.Height+10;
        e.ItemHeight=choice!=null && choice.First?line+Font.Height+10:line;
        e.ItemWidth=Width;
    }
    protected override void OnDrawItem(DrawItemEventArgs e) {
        if(e.Index<0 || e.Index>=Items.Count) return;
        var choice=(Choice)Items[e.Index];
        bool edit=(e.State&DrawItemState.ComboBoxEdit)!=0;
        bool selected=(e.State&DrawItemState.Selected)!=0;
        Rectangle row=e.Bounds;
        using(var brush=new SolidBrush(StudioTheme.Surface)) e.Graphics.FillRectangle(brush,row);
        if(choice.First && !edit) {
            int height=Font.Height+10;
            var heading=new Rectangle(row.X,row.Y,row.Width,height);
            using(var brush=new SolidBrush(StudioTheme.Input)) e.Graphics.FillRectangle(brush,heading);
            using(var bold=new Font(Font,FontStyle.Bold)) TextRenderer.DrawText(e.Graphics,choice.Group,bold,new Rectangle(heading.X+10,heading.Y,heading.Width-16,heading.Height),StudioTheme.Accent,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
            row.Y+=height; row.Height-=height;
        }
        Color back=selected?StudioTheme.Selected:edit?StudioTheme.Input:StudioTheme.Surface;
        using(var brush=new SolidBrush(back)) e.Graphics.FillRectangle(brush,row);
        TextRenderer.DrawText(e.Graphics,choice.Name,Font,new Rectangle(row.X+8,row.Y,Math.Max(0,row.Width-14),row.Height),StudioTheme.Text,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        if((e.State&DrawItemState.Focus)!=0) ControlPaint.DrawFocusRectangle(e.Graphics,row);
    }
    public static void Test() {
        var seen=new bool[Effects.Names.Length]; int count=0;
        foreach(var group in modes) foreach(int mode in group) {
            if(mode<0 || mode>=seen.Length || seen[mode]) throw new Exception("Duplicate or invalid grouped effect");
            seen[mode]=true; count++;
        }
        if(count!=Effects.Names.Length) throw new Exception("Some effects are missing from groups");
        using(var primary=new GroupedEffectCombo()) using(var secondary=new GroupedEffectCombo()) {
            primary.Populate(false); secondary.Populate(true);
            for(int mode=0;mode<Effects.Names.Length;mode++) {
                primary.SelectedIndex=mode; secondary.SelectedIndex=mode+1;
                if(primary.SelectedIndex!=mode || secondary.SelectedIndex!=mode+1 || primary.Text!=Effects.Names[mode] || secondary.Text!=Effects.Names[mode]) throw new Exception("Grouped effect ID mapping failed");
            }
            secondary.SelectedIndex=0; if(secondary.Text!="None") throw new Exception("None choice lost");
        }
        Console.WriteLine("PASS: seven groups cover all effects exactly once; persisted IDs and secondary None mapping preserved.");
    }
}
