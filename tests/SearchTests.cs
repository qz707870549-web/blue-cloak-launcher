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
            Check(results().SequenceEqual(new[] { calculator }), "typing in one category immediately reveals a matching app from another category");
            var summary = (TextBlock)typeof(Library).GetField("commandSummary", Fields).GetValue(window);
            Check(summary.Text == "1 个项目", "search count reflects global results rather than the selected category");
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
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle == IntPtr.Zero, "search regression checks never open a native window");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
