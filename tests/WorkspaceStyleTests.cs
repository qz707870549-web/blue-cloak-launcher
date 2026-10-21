using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma;

class WorkspaceStyleTests
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);}
    static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject {
        if(root is T)yield return (T)root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Elements<T>(child))yield return item;
    }
    [STAThread] static int Main(){try{
        Program.QaMode=true;
        string fixture=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"workspace-style-fixtures",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(fixture);
        Program.DataDir=fixture;Program.ConfigPath=Path.Combine(fixture,"settings.json");
        var config=new Config{Groups=new List<string>{"未分类","游戏","工具","网站"}};
        foreach(var item in SystemShortcuts.Entries()){item.Group="工具";config.Apps.Add(item);}
        config.Apps.Add(new Entry{Id="folder",Name="Windows",Target=Environment.GetFolderPath(Environment.SpecialFolder.Windows),Group="工具",FavoriteFolder=true});
        config.Apps.Add(new Entry{Id="hidden-preview",Name="Hidden utility",Target="hidden.exe",Group="工具",Utility=true});config.ShowUtilities=false;
        config.Apps[0].TopPinned=true;config.Apps[0].PinnedSortOrder=2;
        Check(config.WorkspaceFolderStyle=="layered","old configurations keep the existing layered-folder style by default");
        config.WorkspaceFolderStyle="unknown";config.Normalize();Check(config.WorkspaceFolderStyle=="layered","unknown workspace style safely falls back without replacing data");
        var app=new Program{State=config};var window=new Library(app);app.Library=window;
        var picker=AirSettings.WorkspaceStylePicker(app);
        var plain=Elements<Button>(picker).Single(button=>button.Tag as string=="minimal");
        plain.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(config.WorkspaceFolderStyle=="minimal"&&ConfigStore.Read(Program.ConfigPath).WorkspaceFolderStyle=="minimal","style selector immediately switches and persists the minimal card style");
        var cards=(Dictionary<string,Button>)typeof(Library).GetField("folderButtonsMac",Fields).GetValue(window);
        Check(cards.Values.All(card=>card.Height==144&&card.Width==200),"minimal cards use compact landscape geometry and wrap with the workspace");
        var tools=cards["工具"];
        var preview=Elements<StackPanel>(tools).Single(panel=>panel.Tag as string=="workspace-card-preview");
        var actual=preview.Children.OfType<FrameworkElement>().Select(element=>element.Tag as Entry).ToList();
        var expected=config.OrderedEntries(config.Apps.Where(a=>config.IsVisible(a)&&a.Group=="工具")).Take(4).ToList();
        Check(actual.SequenceEqual(expected)&&actual.Count==4&&!actual.Any(a=>a.Id=="hidden-preview"),"card preview uses up to four actual visible group entries in their configured order");
        Check(Elements<TextBlock>(tools).Select(t=>t.Text).SequenceEqual(new[]{"工具"}),"minimal cards show only the category name and real icons, with no count or description");
        Check(!Elements<Viewbox>(tools).Any()&&tools.IsEnabled&&tools.ToolTip!=null,"minimal card text is not scaled and the original group button remains interactive");
        Check(config.Apps.Count==5&&config.Apps[0].TopPinned&&config.Apps[0].PinnedSortOrder==2,"switching visual style leaves groups, entries and pin order unchanged");
        foreach(string theme in new[]{"浅色","深色"}){
            FluentTheme.Apply(theme);window.Render();var root=(FrameworkElement)window.Content;
            foreach(double width in new[]{800.0,1200.0}){
                window.UpdateResponsive(width);root.Measure(new Size(width,850));root.Arrange(new Rect(0,0,width,850));root.UpdateLayout();
                Check(cards.Values.All(card=>card.ActualWidth==200&&card.ActualHeight==144),"minimal card dimensions remain stable in "+theme+" at width "+width);
                var currentCard=cards["工具"];var face=(FrameworkElement)currentCard.Content;
                var title=Elements<TextBlock>(face).Single(text=>text.Text=="工具");
                var icons=Elements<StackPanel>(face).Single(panel=>panel.Tag as string=="workspace-card-preview");
                Check(face.ActualHeight==144&&icons.TranslatePoint(new Point(),face).Y>=title.TranslatePoint(new Point(),face).Y+title.ActualHeight+12,
                    "card title and preview icons occupy separate readable rows in "+theme+" at width "+width);
            }
            var image=new RenderTargetBitmap(1200,850,96,96,PixelFormats.Pbgra32);image.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
            using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,theme=="浅色"?"workspace-minimal-light.png":"workspace-minimal-dark.png")))png.Save(stream);
        }
        var layered=Elements<Button>(picker).Single(button=>button.Tag as string=="layered");layered.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
        Check(config.WorkspaceFolderStyle=="layered"&&cards.Values.All(card=>Math.Abs(card.Height-182.4)<0.01),"style selector restores original layered folders without losing contents");
        Check(GroupBackup.Clone(config).WorkspaceFolderStyle=="layered","workspace style survives configuration serialization");
        Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"style switching and screenshots never show a native window or install hooks");
        return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
