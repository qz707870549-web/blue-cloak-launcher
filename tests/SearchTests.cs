using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using Luma;

class SearchTests
{
    static readonly BindingFlags Fields = BindingFlags.Instance | BindingFlags.NonPublic;
    static void Check(bool ok, string name) { if (!ok) throw new Exception(name); Console.WriteLine("PASS " + name); }
    [STAThread] static int Main()
    {
        try
        {
            Program.QaMode = true;
            Program.DataDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "search-tests", Guid.NewGuid().ToString("N"));
            Program.ConfigPath = Path.Combine(Program.DataDir, "settings.json");
            var state = new Config(); state.Groups.AddRange(new[] { "设计", "工具", "隐藏分组" }); state.HiddenGroups.Add("隐藏分组");
            var editor = new Entry { Id = "editor", Name = "编辑器", Target = "editor-demo.exe", Group = "设计" };
            var calculator = new Entry { Id = "calculator", Name = "计算器", Target = "calculator-demo.exe", Group = "工具", Tags = "数学" };
            var website = new Entry { Id = "demo-site", Name = "示例网站", Target = "https://example.com/", Website = true, Group = "工具" };
            var folder = new Entry { Id = "demo-folder", Name = "资料文件夹", Target = @"C:\Demo", FavoriteFolder = true, Group = "工具" };
            state.Apps.AddRange(new[] { editor, calculator, website, folder,
                new Entry { Id = "hidden", Name = "计算器隐藏", Target = "hidden-demo.exe", Group = "隐藏分组" } });
            var app = new Program { State = state }; var window = new Library(app); app.Library = window;
            var input = (TextBox)typeof(Library).GetField("search", Fields).GetValue(window);
            var selected = typeof(Library).GetField("selected", Fields);
            Func<List<Entry>> results = () => ((List<Button>)typeof(Library).GetField("visibleCards", Fields).GetValue(window)).Select(b => b.Tag).OfType<Entry>().ToList();
            selected.SetValue(window, "设计"); window.Render(); input.Text = "计算器";
            Check(results().Contains(calculator)&&results().Any(a=>a.Id=="hidden"), "global search includes matching apps in visible and hidden categories");
            var summary = (TextBlock)typeof(Library).GetField("commandSummary", Fields).GetValue(window);
            Check(summary.Text == "2 个项目", "search count includes matching apps in hidden groups");
            input.Text = "数学"; Check(results().Contains(calculator), "search supports custom tags");
            input.Text = "示例网站"; Check(results().Contains(website), "search reveals website shortcuts across categories");
            input.Text = "资料文件夹"; Check(results().Contains(folder), "search reveals favorite folders across categories");
            selected.SetValue(window, "@favorites"); window.Render(); input.Text = "计算器";
            Check(results().Contains(calculator), "global search is not limited to the favorites view");
            input.Text = "不存在的搜索词"; Check(results().Count == 0, "unmatched query clears stale results");
            selected.SetValue(window, "设计"); input.Text = ""; window.Render();
            Check((string)selected.GetValue(window) == "设计" && results().Contains(editor) && !results().Contains(calculator), "clearing search restores the original category");
            var appFolder = new AppFolder { Id = "search-folder", Name = "设计合集", Group = "设计" }; state.AppFolders.Add(appFolder); editor.FolderId = appFolder.Id;
            selected.SetValue(window, "@folder:" + appFolder.Id); window.Render(); input.Text = "计算器";
            Check(results().Contains(calculator), "search escapes a virtual app folder to find visible apps globally");
            input.Text = ""; Check(results().Contains(editor) && !results().Contains(calculator), "clearing search returns to the original virtual folder");
            selected.SetValue(window, "@overview"); window.Render(); input.Text = "计算器";
            Check(results().Contains(calculator), "workspace search shows application results instead of category cards");
            var root = (FrameworkElement)window.Content; root.Measure(new Size(1000, 760)); root.Arrange(new Rect(0, 0, 1000, 760)); root.UpdateLayout();
            Check(input.ActualWidth > 40 && input.ActualHeight >= 20 && input.Text == "计算器", "search input retains readable dimensions and typed text");
            foreach (string theme in new[] { "浅色", "深色" }) {
                FluentTheme.Apply(theme);
                foreach (double width in new[] { 800.0, 1000.0, 1440.0 }) {
                    root.Measure(new Size(width,760));root.Arrange(new Rect(0,0,width,760));root.UpdateLayout();
                    var host=(ScrollViewer)input.Template.FindName("PART_ContentHost",input);
                    Check(host.ViewportHeight>=input.FontSize&&host.ActualWidth>40,"typed text viewport remains visible: "+theme+"/"+width);
                    Check(((System.Windows.Media.SolidColorBrush)input.Foreground).Color==((System.Windows.Media.SolidColorBrush)UI.Ink).Color,"typed text follows theme contrast: "+theme+"/"+width);
                    Check(((System.Windows.Media.SolidColorBrush)input.CaretBrush).Color==((System.Windows.Media.SolidColorBrush)UI.Accent).Color,"search caret follows theme contrast: "+theme+"/"+width);
                    Check(input.MinHeight==0 && input.Padding.Top==0 && input.Padding.Bottom==0 && !input.AcceptsReturn,"single-line search has no generic vertical padding or minimum-height constraint: "+theme+"/"+width);
                }
            }
            FluentTheme.Apply("浅色");state.EnsureUngroupedCategory();input.Clear();selected.SetValue(window,"未分类");window.Render();
            var ungrouped=new Entry{Id="ungrouped-search",Name="新安装搜索测试",Target="ungrouped.exe"};state.MergeDiscovered(new List<Entry>{ungrouped});input.Text="新安装搜索";
            Check(results().Contains(ungrouped),"newly installed ungrouped applications are immediately searchable");
            window.Render();var nav=(StackPanel)typeof(Library).GetField("navigation",Fields).GetValue(window);
            Check(nav.Children.OfType<Button>().Any(b=>b.Content is DockPanel&&((DockPanel)b.Content).Children.OfType<TextBlock>().Any(t=>t.Text=="未分组应用")),"ungrouped category is visible under its requested label in navigation");
            root.Measure(new Size(1000,760));root.Arrange(new Rect(0,0,1000,760));root.UpdateLayout();
            var preview=new System.Windows.Media.Imaging.RenderTargetBitmap(1000,760,96,96,System.Windows.Media.PixelFormats.Pbgra32);preview.Render(root);var png=new System.Windows.Media.Imaging.PngBitmapEncoder();png.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(preview));using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"search-v1.3.11.png")))png.Save(stream);
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle == IntPtr.Zero, "search regression checks never open a native window");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
