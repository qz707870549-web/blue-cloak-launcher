using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma;
using ApplicationIdentity = Luma.ApplicationIdentity;

class CompactDuplicateTests
{
    const BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool value, string message) { if(!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
    static Entry App(string exe, string args="", string group="未分类") => new Entry { Id = exe + (args == "" ? "" : " | " + args), Target=exe, Name=Path.GetFileNameWithoutExtension(exe), Group=group };
    [STAThread] static int Main()
    {
        try {
            Program.QaMode = true;
            foreach(string file in new[]{"wps.exe","Doubao.exe","Quark.exe","Bandizip.exe"}) {
                var first=App(@"C:\Apps\1.2.3\"+file,"--from=desktop");
                var second=App(@"C:\Apps\1.3.4\"+file);
                Check(ApplicationIdentity.Same(first,second),file+" scan aliases and version directories share one application identity");
            }
            Check(!ApplicationIdentity.Same(App(@"C:\Apps\Quark.exe","--profile-directory=Work"),App(@"C:\Apps\Quark.exe")),"explicit browser profiles stay separate");
            Check(!ApplicationIdentity.Same(App(@"C:\Apps\wps.exe","/print report.docx"),App(@"C:\Apps\wps.exe")),"document and functional launch arguments are preserved");
            Check(!ApplicationIdentity.Same(App(@"C:\Apps\wps.exe"),App(@"C:\Apps\et.exe")),"WPS spreadsheet and document components are not merged");
            Check(!ApplicationIdentity.Same(App(@"C:\SW2024\SLDWORKS.exe"),App(@"C:\SW2025\SLDWORKS.exe")),"separate professional application versions remain distinct");
            var custom=App(@"C:\Apps\wps.exe", "", "效率"); custom.CustomIcon="my.png"; custom.TopPinned=true;
            var duplicate=App(@"C:\Apps\wps.exe","--from=desktop"); duplicate.Favorite=true; duplicate.Tags="办公";
            var config=new Config(); config.Groups.Add("效率");config.Apps.AddRange(new[]{duplicate,custom}); config.Normalize();
            Check(config.Apps.Count==1 && config.Apps[0]==custom && custom.Group=="效率" && custom.CustomIcon=="my.png" && custom.TopPinned && custom.Favorite && custom.Tags.Contains("办公"),"existing scan duplicates merge without losing grouping, icon, pins or tags");
            Check(config.DuplicateEntries.Count==1 && config.DuplicateEntries[0]==duplicate,"merged original entry is archived for recovery");
            config.Normalize();Check(config.Apps.Count==1 && config.DuplicateEntries.Count==1,"duplicate migration is idempotent");
            config.MergeDiscovered(new System.Collections.Generic.List<Entry>{App(@"C:\Apps\wps.exe","--source=registry")});
            Check(config.Apps.Count==1,"subsequent scans cannot reintroduce shortcut aliases");
            var app=new Program { State=config };var window=new Library(app); app.Library=window;
            var header=(StackPanel)typeof(Library).GetField("commandHeader",Fields).GetValue(window);
            var searchFrame=(Border)typeof(Library).GetField("commandSearchFrame",Fields).GetValue(window);
            var row=(StackPanel)header.Children[0];
            Check(header.Children.Count==1 && row.Children.OfType<Button>().Count()==2 && searchFrame.Visibility==Visibility.Collapsed,"title toolbar initially contains only search and add icons");
            var root=(FrameworkElement)window.Content;
            foreach(double width in new[]{800.0,1000.0,1440.0}) {
                window.UpdateResponsive(width);root.Measure(new Size(width,900));root.Arrange(new Rect(0,0,width,900));root.UpdateLayout();
                Check(header.ActualHeight<=44,"compact title bar frees vertical space at width "+width);
            }
            root.Measure(new Size(1440,900));root.Arrange(new Rect(0,0,1440,900));root.UpdateLayout();
            var bitmap=new RenderTargetBitmap(1440,900,96,96,PixelFormats.Pbgra32);bitmap.Render(root);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(bitmap));
            using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"compact-ui.png"))) png.Save(stream);
            window.FocusSearch();Check(searchFrame.Visibility==Visibility.Visible,"search icon and Ctrl K can reveal the real search input");
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"compact UI screenshot is rendered without opening a desktop window");
            return 0;
        } catch(Exception error) {Console.Error.WriteLine(error);return 1;}
    }
}
