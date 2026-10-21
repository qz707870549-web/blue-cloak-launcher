using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Luma;

class GlobalLibraryTests
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string text){if(!ok)throw new Exception(text);Console.WriteLine("PASS "+text);}
    static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject {
        if(root is T)yield return (T)root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Elements<T>(child))yield return item;
    }
    [STAThread] static int Main(){try{
        Program.QaMode=true;
        var state=new Config{Groups=new List<string>{"未分类","工具","隐藏","系统附带应用","软件配套工具"},HiddenGroups=new List<string>{"隐藏"},ShowSystemApps=false,ShowUtilities=false};
        var pin=new Entry{Id="p",Name="Z pin",Target="p.exe",Group="工具",TopPinned=true,SortOrder=10};
        var normal=new Entry{Id="n",Name="A normal",Target="n.exe",Group="工具",SortOrder=0};
        var hidden=new Entry{Id="h",Name="隐藏应用",Target="h.exe",Group="隐藏"};
        var nested=new Entry{Id="f",Name="隐藏文件夹内应用",Target="f.exe",Group="隐藏",FolderId="folder"};
        var system=new Entry{Id="s",Name="系统应用",Target="s.exe",SystemApp=true};
        var utility=new Entry{Id="u",Name="配套工具",Target="u.exe",Utility=true};
        state.Apps.AddRange(new[]{pin,normal,hidden,nested,system,utility});state.AppFolders.Add(new AppFolder{Id="folder",Name="隐藏合集",Group="隐藏"});
        var app=new Program{State=state};var window=new Library(app);app.Library=window;
        var scope=typeof(Library).GetField("selected",Fields);var input=(TextBox)typeof(Library).GetField("search",Fields).GetValue(window);
        Func<List<Entry>> results=()=>((List<Button>)typeof(Library).GetField("visibleCards",Fields).GetValue(window)).Select(b=>b.Tag).OfType<Entry>().ToList();
        var root=(FrameworkElement)window.Content;
        scope.SetValue(window,"全部");window.Render();
        Check(results().SequenceEqual(new[]{normal,pin}),"all-apps view ignores workspace pin priority and hides disabled groups by default");
        Check(!Elements<ReorderPanel>(root).Any(p=>(bool)typeof(ReorderPanel).GetField("pinnedArea",Fields).GetValue(p)),"all-apps view never creates a pinned strip");
        state.IncludeHiddenInAllApps=true;window.Render();
        Check(results().Contains(hidden)&&results().Contains(system)&&results().Contains(utility),"all-apps hidden-group option includes both user-hidden and disabled system/tool categories");
        scope.SetValue(window,"@folder:folder");window.Render();Check(results().Contains(nested),"hidden-group folders remain usable when included in all-apps");
        state.IncludeHiddenInAllApps=false;input.Text="隐藏";
        Check(results().Contains(hidden)&&results().Contains(nested),"global search includes hidden apps and nested folder members independently of all-apps visibility preference");
        input.Text="系统应用";Check(results().Contains(system),"global search includes apps hidden by system-category switch");
        input.Clear();scope.SetValue(window,"@overview");window.Render();
        Check(Elements<ReorderPanel>(root).Any(p=>(bool)typeof(ReorderPanel).GetField("pinnedArea",Fields).GetValue(p)),"workspace still displays its pinned icon strip");
        var cards=(Dictionary<string,Button>)typeof(Library).GetField("folderButtonsMac",Fields).GetValue(window);
        Check(cards.All(pair=>!Elements<Viewbox>(pair.Value).Any(art=>Elements<TextBlock>(art).Any(t=>t.Text==Config.GroupDisplayName(pair.Key)))),"folder names are rendered outside scaled artwork");
        Check(TextOptions.GetTextRenderingMode(window)==TextRenderingMode.ClearType&&TextOptions.GetTextFormattingMode(window)==TextFormattingMode.Display&&window.SnapsToDevicePixels&&window.UseLayoutRounding,"window uses ClearType display text and device-pixel alignment");
        Check(AirStyle.Font.Source.Contains("Microsoft YaHei UI")&&!AirStyle.Font.Source.Contains("Noto"),"small Chinese interface text uses native Windows UI font fallback");
        var frame=(Border)typeof(Library).GetField("commandSearchFrame",Fields).GetValue(window);
        Check(!Elements<TextBlock>(frame).Any(t=>t.Text.Contains("搜索应用")||t.Text.Contains("Ctrl")),"search field has no placeholder or shortcut-label text");
        state.ShowAllAppsEntry=false;state.IncludeHiddenInAllApps=true;scope.SetValue(window,"全部");window.Render();
        var nav=(StackPanel)typeof(Library).GetField("navigation",Fields).GetValue(window);
        Check(!Elements<TextBlock>(nav).Any(t=>t.Text=="所有应用")&&(string)scope.GetValue(window)=="@overview","disabling all-apps navigation safely returns the current view to workspace");
        var copy=GroupBackup.Clone(state);Check(!copy.ShowAllAppsEntry&&copy.IncludeHiddenInAllApps,"all-apps manager preferences persist across configuration serialization");
        Check(state.SaveCategoryOrder(new[]{pin,normal},true),"normal all-apps category sorting accepts mixed pinned and unpinned items");
        Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"global-library verification never creates a native window");
        return 0;
    }catch(Exception error){Console.Error.WriteLine(error);return 1;}}
}
