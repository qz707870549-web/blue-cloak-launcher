using System;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma;

class WorkspacePolishTests
{
    const BindingFlags Fields=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject {
        if(root is T)yield return (T)root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Elements<T>(child))yield return item;
    }
    [STAThread] static int Main() {
        try {
            Program.QaMode=true;
            var legacy=new Config{Groups=new List<string>{"未分类","社交","创作","娱乐","效率","工具"}};
            var saved=new Entry{Id="saved",Name="保留",Group="社交",Target="saved.exe",TopPinned=true,CustomIcon="custom.png"};
            var savedFolder=new AppFolder{Name="资料",Group="创作"};
            legacy.Apps.Add(saved);legacy.AppFolders.Add(savedFolder);legacy.GroupRules.Add(new GroupRule{Name="saved",Group="娱乐"});
            legacy.RemoveLegacyDefaultGroups();
            Check(!legacy.Groups.Intersect(new[]{"社交","创作","娱乐","效率"}).Any()&&legacy.Groups.Contains("工具"),"obsolete default groups are removed while other categories remain");
            Check(legacy.Apps.Contains(saved)&&saved.Group=="未分类"&&saved.TopPinned&&saved.CustomIcon=="custom.png"&&savedFolder.Group=="未分类","default-group migration preserves applications, folders, pins and icons");
            Check(!legacy.GroupRules.Any(rule=>rule.Group=="娱乐"),"old default classification rules cannot recreate removed categories");
            legacy.Groups.Add("社交");legacy.RemoveLegacyDefaultGroups();Check(legacy.Groups.Contains("社交"),"one-time cleanup allows users to deliberately recreate a category later");
            foreach(string group in new[]{"系统附带应用","软件配套工具"}) {
                legacy.Groups.Add(group);legacy.SetGroupEnabled(group,false);
                Check(!legacy.IsGroupEnabled(group)&&!legacy.VisibleGroups().Contains(group),"category manager consistently disables "+group);
                legacy.SetGroupEnabled(group,true);Check(legacy.IsGroupEnabled(group)&&legacy.VisibleGroups().Contains(group),"category manager can re-enable legacy disabled "+group);
            }
            var themeApp=new Program{State=legacy};legacy.AccentHex="#7b5bd1";
            foreach(string theme in new[]{"浅色","深色"}) {
                FluentTheme.Apply(theme);var color=((SolidColorBrush)UI.Accent).Color;
                Check(color.R==color.G&&color.G==color.B,"old purple preference cannot tint selection or switches in "+theme);
            }
            var config=new Config { Groups=new List<string>{"未分类","游戏","工具","隐藏"}, HiddenGroups=new List<string>{"隐藏"}, LibraryListView=true };
            var a=new Entry{Id="a",Name="游戏 A",Target="a.exe",Group="游戏",TopPinned=true,SortOrder=5,PinnedSortOrder=0};
            var hidden=new Entry{Id="h",Name="隐藏",Target="h.exe",Group="隐藏",TopPinned=true,PinnedSortOrder=1};
            var b=new Entry{Id="b",Name="工具 B",Target="b.exe",Group="工具",TopPinned=true,SortOrder=8,PinnedSortOrder=2};
            var c=new Entry{Id="c",Name="工具 C",Target="c.exe",Group="工具",TopPinned=true,SortOrder=9,PinnedSortOrder=3};
            config.Apps.AddRange(new[]{a,hidden,b,c});config.CollapsedGroups.Add("置顶应用");
            Check(config.SavePinnedOrder(new[]{c,b,a}),"pinned apps can reorder across their original categories");
            Check(config.OrderedPins().SequenceEqual(new[]{c,hidden,b,a}),"reordering visible pins preserves hidden app positions");
            Check(a.Group=="游戏"&&b.Group=="工具"&&a.SortOrder==5&&b.SortOrder==8,"pin order does not change category assignments or category order");
            var restored=GroupBackup.Clone(config);
            Check(restored.OrderedPins().Select(e=>e.Id).SequenceEqual(new[]{"c","h","b","a"}),"pin order survives configuration and backup serialization");
            Check(!config.SavePinnedOrder(new[]{a,a})&&!config.SavePinnedOrder(new[]{a,new Entry{TopPinned=true}}),"invalid pin reorder requests are rejected");
            var app=themeApp;app.State=config;var window=new Library(app);app.Library=window;
            var root=(FrameworkElement)window.Content;
            var nav=(StackPanel)typeof(Library).GetField("navigation",Fields).GetValue(window);
            var navNames=Elements<TextBlock>(nav).Select(t=>t.Text).ToList();
            Check(!navNames.Any(s=>new[]{"收藏","最近使用","下载","新建分类"}.Contains(s)),"sidebar has no favorites, recent, downloads or create-category shortcuts");
            Check(!Elements<TextBlock>(root).Any(t=>t.Text=="管理分类"||t.Text.StartsWith("主题 · ")),"category management and theme controls are contained in Settings");
            var cards=(Dictionary<string,Button>)typeof(Library).GetField("folderButtonsMac",Fields).GetValue(window);
            Check(cards.All(pair=>Elements<TextBlock>(pair.Value.Content as DependencyObject).Select(t=>t.Text).SequenceEqual(new[]{Config.GroupDisplayName(pair.Key)})),"workspace folder faces display only the category name");
            Check(cards.Values.All(card=>Math.Abs(card.Height-228*0.8)<0.01&&card.Width==200),"workspace folder card dimensions are reduced by twenty percent");
            Check(!Elements<Button>(root).Any(button=>new[]{"最小化","最大化 / 还原","收起到托盘"}.Contains(System.Windows.Automation.AutomationProperties.GetName(button))),"window caption buttons are absent from the visible layout");
            var panel=Elements<ReorderPanel>(root).Single();
            Check((bool)typeof(ReorderPanel).GetField("pinnedArea",Fields).GetValue(panel)&&panel.AllowDrop,"workspace pins use the dedicated animated drag reorder panel");
            Check(panel.Children.OfType<Button>().Select(button=>((Entry)button.Tag).Id).SequenceEqual(new[]{"c","b","a"}),"workspace pin icons follow saved order in grid even when list view is selected");
            Check(!Elements<TextBlock>(root).Any(t=>t.Text=="置顶应用"),"pin strip has no heading or collapse control and ignores old collapsed state");
            Check(!panel.Children.OfType<Button>().Any(button=>Elements<TextBlock>(button.Content as DependencyObject).Any()),"pinned cards contain icons only and retain names as tooltips");
            Check(!panel.Children.OfType<Button>().Any(button=>Elements<Button>(button.Content as DependencyObject).Any(child=>System.Windows.Automation.AutomationProperties.GetName(child)=="更多操作")),"application cards no longer create a hover ellipsis button");
            Check(panel.Children.OfType<Button>().All(button=>button.ContextMenu!=null),"removing ellipsis buttons preserves right-click application operations");
            var tooltipHosts=Elements<FrameworkElement>(root).Where(element=>ToolTipService.GetToolTip(element)!=null&&ToolTipService.GetIsEnabled(element)).ToList();
            var visibility=typeof(Library).GetMethod("UpdateTransientVisibility",Fields);
            visibility.Invoke(window,new object[]{false});
            Check(tooltipHosts.Count>0&&tooltipHosts.All(element=>!ToolTipService.GetIsEnabled(element)),"hiding the launcher suppresses tooltips that could leave detached popups");
            visibility.Invoke(window,new object[]{true});
            Check(tooltipHosts.All(ToolTipService.GetIsEnabled),"opening the launcher restores tooltips without losing their labels");
            Check(WindowSizePolicy.HideForSystemCommand(0xF020)&&WindowSizePolicy.HideForSystemCommand(0xF023)&&!WindowSizePolicy.HideForSystemCommand(0xF030),"minimize system commands are intercepted without disabling maximization");
            var hook=typeof(Library).GetMethod("FluentChromeMessages",Fields);
            object[] message={IntPtr.Zero,0x112,new IntPtr(0xF020),IntPtr.Zero,false};
            hook.Invoke(window,message);
            Check((bool)message[4]&&window.WindowState!=WindowState.Minimized,"native minimize message is consumed before an iconic desktop caption can be created");
            root.Measure(new Size(1200,900));root.Arrange(new Rect(0,0,1200,900));root.UpdateLayout();
            var image=new RenderTargetBitmap(1200,900,96,96,PixelFormats.Pbgra32);image.Render(root);
            var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(image));
            using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"workspace-polish.png")))png.Save(stream);
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"workspace verification and screenshot do not open a desktop window");
            return 0;
        }catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
}
