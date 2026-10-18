using System;
using System.Windows;
using Luma;
class WindowSizingTests {
    static void Check(bool ok,string label){if(!ok)throw new Exception(label);Console.WriteLine("PASS "+label);}
    static int Main(){try{
        Check(!WindowSizePolicy.ToggleCloses(true,WindowState.Minimized),"visible minimized window is restored by launcher instead of hidden");
        Check(WindowSizePolicy.ToggleCloses(true,WindowState.Normal)&&WindowSizePolicy.ToggleCloses(true,WindowState.Maximized),"second start click hides an already expanded window");
        Check(WindowSizePolicy.RestoreState(WindowState.Minimized,WindowState.Normal)==WindowState.Normal,"normal window is restored before placement");
        Check(WindowSizePolicy.RestoreState(WindowState.Minimized,WindowState.Maximized)==WindowState.Maximized,"minimized maximized window retains maximization");
        Check(!WindowSizePolicy.CanSave(new Size(160,28),WindowState.Minimized,true,true,false),"iconic mini-window geometry never overwrites saved size");
        Check(!WindowSizePolicy.CanSave(new Size(998,1014),WindowState.Normal,true,false,false),"moving without manual resizing never saves transient DPI geometry");
        Check(!WindowSizePolicy.CanSave(new Size(998,1014),WindowState.Normal,true,true,true),"programmatic placement cannot save a temporary size");
        Check(!WindowSizePolicy.CanSave(new Size(2560,1400),WindowState.Maximized,true,true,false),"maximized screen size is not stored as normal restore size");
        Check(WindowSizePolicy.CanSave(new Size(1100,760),WindowState.Normal,true,true,false),"stable manual resize persists");
        var repaired=WindowSizePolicy.Normalize(160,28);Check(repaired.Width==1440&&repaired.Height==920,"previously corrupted small size recovers sensible defaults");
        var invalid=WindowSizePolicy.Normalize(Double.NaN,Double.PositiveInfinity);Check(invalid.Width==1440&&invalid.Height==920,"nonfinite stored dimensions are rejected");
        var original=WindowSizePolicy.Normalize(998,1014);Check(original.Width==998&&original.Height==1014,"existing legitimate custom size remains intact");
        Size saved=original;foreach(double scale in new[]{1.0,1.25,1.5,2.0,1.0})foreach(var mini in new[]{new Size(0,0),new Size(160,28),new Size(500,420)}){if(WindowSizePolicy.CanSave(mini,WindowState.Minimized,true,true,false))saved=mini;var state=WindowSizePolicy.RestoreState(WindowState.Minimized,WindowState.Normal);Check(state==WindowState.Normal&&saved==original,"minimize/restore retains size at DPI scale "+scale);var bounds=DesktopPlacement.Bounds(new Rect(-2560,0,2560,1400),new Point(-2400,1400),false,false,scale,saved.Width,saved.Height);if(bounds.Width<=0||bounds.Height<=0||bounds.Right>0||bounds.Left< -2560)throw new Exception("invalid restored monitor bounds");}
        var config=new Config{LibraryWidth=160,LibraryHeight=28};config.Groups.Add("保留分组");config.Apps.Add(new Entry{Id="website",Name="Example",Target="https://example.com",Website=true,Group="保留分组"});config.Normalize();Check(config.LibraryWidth==1440&&config.LibraryHeight==920&&config.Groups.Contains("保留分组")&&config.Apps.Count==1&&config.Apps[0].Website,"size migration preserves groups and websites");return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
