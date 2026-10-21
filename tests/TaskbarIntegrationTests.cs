using System;
using System.Web.Script.Serialization;
using System.Windows;
using Luma;

class TaskbarIntegrationTests
{
    static void Check(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    [STAThread] static int Main()
    {
        try
        {
            Program.QaMode = true;
            var button = StartIntegration.AutomationBounds(new Rect(1050.25, 1392, 48, 48));
            Check(button != null && button.Handle == 0 && button.Contains(1075, 1410), "centered XAML Start retains its UIA rectangle without taskbar host HWND substitution");
            Check(!button.Contains(10, 1410) && !button.Contains(1400, 1410), "other taskbar buttons are not captured as Start");
            Check(StartIntegration.AutomationBounds(new Rect(0,1392,2560,48)) == null, "whole-taskbar automation rectangles cannot become Start hit targets");
            Check(StartIntegration.AutomationBounds(Rect.Empty) == null, "missing Start automation geometry is rejected");
            foreach (double scale in new[] { 1.0, 1.25, 1.5, 2.0 })
            {
                var area = new Rect(-2560, 0, 2560, 1392);
                var placed = DesktopPlacement.Bounds(area, new Point(-1700,1392), true,false,scale,998,760);
                Check(Math.Abs((placed.Left + placed.Right) / 2 - (area.Left + area.Right) / 2) < 0.01 && placed.Bottom < area.Bottom,
                    "centered taskbar placement stays centered above secondary-monitor taskbar at scale " + scale);
            }
            var config = new Config { HideDesktopIcons = true };
            var json = new JavaScriptSerializer();
            Check(json.Deserialize<Config>(json.Serialize(config)).HideDesktopIcons, "desktop visibility preference persists in configuration");
            Check(!json.Deserialize<Config>("{}").HideDesktopIcons, "older configuration defaults to leaving desktop icons unchanged");
            Check(DesktopIcons.SetHidden(true) && DesktopIcons.SetHidden(false), "QA desktop visibility calls are no-ops and never touch Explorer");
            var app = new Program(); var window = new Library(app);
            Check(!window.ShowInTaskbar, "launcher does not create a taskbar application button");
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle == IntPtr.Zero, "taskbar verification never creates a native window or installs hooks");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
