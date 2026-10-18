using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Web.Script.Serialization;
using Luma;
class PublicIntegrationTests
{
    static void Check(bool condition,string name){if(!condition)throw new Exception(name);Console.WriteLine("PASS "+name);}
    static object Canonical(object value){var map=value as IDictionary<string,object>;if(map!=null){var sorted=new SortedDictionary<string,object>();foreach(var pair in map)sorted[pair.Key]=Canonical(pair.Value);return sorted;}var array=value as object[];return array==null?value:array.Select(Canonical).ToArray();} static string Snapshot(Config config){var json=new JavaScriptSerializer{MaxJsonLength=64*1024*1024};return json.Serialize(Canonical(json.DeserializeObject(json.Serialize(config))));}
    static IEnumerable<T> Elements<T>(DependencyObject root) where T:DependencyObject {
        if(root is T)yield return (T)root;
        foreach(var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())foreach(var item in Elements<T>(child))yield return item;
    }
    [STAThread] static int Main(string[] args)
    {
        try
        {
            foreach(int win in new[]{0x5b,0x5c}) {
                var policy=new WinKeyPolicy();
                Check(!policy.Process(win,true,false,true,false)&&!policy.Process(win,true,false,true,false)&&policy.Process(win,false,false,true,false),"bare Win toggles once including held-key repeats: "+win);
                foreach(int chord in new[]{0x45,0x52,0x4c,0x44,0x10,0x11,0x41}) {
                    policy=new WinKeyPolicy();policy.Process(win,true,false,true,false);policy.Process(chord,true,false,true,false);policy.Process(chord,false,false,true,false);
                    Check(!policy.Process(win,false,false,true,false),"Windows shortcut remains native: "+win+"/"+chord);
                }
                policy=new WinKeyPolicy();policy.Process(win,true,false,true,true);Check(!policy.Process(win,false,false,true,false),"modifier held before Win does not toggle: "+win);
                policy=new WinKeyPolicy();policy.Process(win,true,false,true,false);policy.Cancel();Check(!policy.Process(win,false,false,true,false),"Win mouse chord does not toggle: "+win);
                policy=new WinKeyPolicy();policy.Process(win,true,false,true,false);Check(!policy.Process(win,false,false,false,false),"disabled during press preserves native release: "+win);
                policy=new WinKeyPolicy();Check(!policy.Process(win,true,true,true,false)&&!policy.Process(win,false,true,true,false),"synthetic input never toggles: "+win);
            }
            string directory=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"integration-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
            Program.DataDir=Path.Combine(directory,"data");Program.ConfigPath=Path.Combine(Program.DataDir,"settings.json");Program.QaMode=true;
            var seedDir=Path.Combine(directory,"embedded-first-run");var seedPath=Path.Combine(seedDir,"settings.json");
            var seeded=BuiltInGroups.Initialize(seedPath,seedDir);
            Check(seeded.Groups.SequenceEqual(new[]{"未分类","社交","创作","效率","娱乐","工具","网站"})&&seeded.Apps.Count==0&&seeded.Folders.Count==0&&seeded.GroupRules.Count==0,"public defaults contain generic groups and no private apps, sites or rules");
            Check(!Directory.Exists(Path.Combine(seedDir,"backup-icons")),"public defaults contain no private icon assets");
            seeded.Groups.Add("用户后来新增");ConfigStore.Save(seedPath,seeded);var seedBefore=File.ReadAllText(seedPath);
            Check(BuiltInGroups.Initialize(seedPath,seedDir).Groups.Contains("用户后来新增")&&File.ReadAllText(seedPath)==seedBefore,"subsequent startup never overwrites existing groups");
            var xml=new System.Xml.XmlDocument();xml.LoadXml(StartupRegistration.TaskXml(@"C:\中文 软件 & test\LumaDock.exe","S-1-5-21-123"));
            var ns=new System.Xml.XmlNamespaceManager(xml.NameTable);ns.AddNamespace("t","http://schemas.microsoft.com/windows/2004/02/mit/task");
            Check(xml.SelectSingleNode("//t:Exec/t:Command",ns).InnerText==@"C:\中文 软件 & test\LumaDock.exe"&&xml.SelectSingleNode("//t:WorkingDirectory",ns).InnerText==@"C:\中文 软件 & test","startup task safely escapes paths and sets working directory");
            Check(xml.SelectSingleNode("//t:LogonTrigger/t:Delay",ns).InnerText=="PT10S"&&xml.SelectSingleNode("//t:RestartOnFailure/t:Count",ns).InnerText=="3"&&xml.SelectSingleNode("//t:RunLevel",ns).InnerText=="LeastPrivilege","startup task has login delay, bounded retries and no elevation");
            Check(xml.SelectSingleNode("//t:DisallowStartIfOnBatteries",ns).InnerText=="false"&&xml.SelectSingleNode("//t:ExecutionTimeLimit",ns).InnerText=="PT0S","startup task allows battery operation and long-running launcher");
            bool startupBlocked=false;try{StartupRegistration.Apply(true);}catch(InvalidOperationException){startupBlocked=true;}Check(startupBlocked,"offline mode prevents all startup task and registry writes");
            Check(Branding.Name=="蓝斗篷启动"&&Branding.Avatar().PixelWidth==256,"new brand name and embedded avatar are available without loose image files");
            Check(typeof(UsageRanking).GetMethod("Suggested")==null,"automatic pin suggestion feature removed");
            Check(typeof(Program).Assembly.GetType("Luma.StatusWidget")==null&&typeof(Program).Assembly.GetType("Luma.StatusReader")==null&&typeof(Program).Assembly.GetType("Luma.DesktopStatus")==null,"desktop widget and polling implementation are removed from executable");
            var oldWidgetConfig=new Config{ShowWidget=true};oldWidgetConfig.Normalize();Check(!oldWidgetConfig.ShowWidget,"legacy enabled widget setting is safely disabled on load");
            var original=args.Length>0?ConfigStore.Read(args[0]):new Config();
            Check(typeof(Program).Assembly.GetType("Luma.Dock")==null&&typeof(Program).Assembly.GetType("Luma.AppearancePanel")==null,"Dock implementation and appearance window are absent from executable");
            string image=Path.Combine(directory,"icon.png");var bitmap=BitmapSource.Create(1,1,96,96,PixelFormats.Bgra32,null,new byte[]{40,120,240,255},4);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var stream=File.Create(image))encoder.Save(stream);
            var config=new Config();config.Groups.AddRange(new[]{"设计","网站","收藏"});config.HiddenGroups.Add("收藏");config.CollapsedGroups.Add("设计");
            var first=new Entry{Id=@"C:\Apps\editor.exe",Name="Editor",Target=@"C:\Apps\editor.exe",Group="设计",Favorite=true,TopPinned=true,Tags="常用",SortOrder=3,CustomIcon=image};
            var second=new Entry{Id="second",Name="Second",Target="second.exe",Group="设计"};
            config.Apps.AddRange(new[]{first,second,new Entry{Id="website:example",Name="Example",Target="https://example.com/",Group="网站",Website=true,ImagePath=image},new Entry{Id="folder:work",Name="Work",Target=@"C:\Work",Group="收藏",FavoriteFolder=true}});
            var folder=config.CombineEntries(first,second);Check(folder!=null&&config.FolderMembers(folder).Count()==2,"drag merge model creates virtual app folder");folder.InWorkspace=true;
            string before=Snapshot(config),backup=Path.Combine(directory,"groups.lumabackup");GroupBackup.Export(backup,config,directory);Check(before==Snapshot(config),"export never mutates live groups or icon paths");
            var document=GroupBackup.Read(backup);Check(document.Data.Groups.SequenceEqual(config.Groups)&&document.Data.HiddenGroups.Contains("收藏"),"backup roundtrip preserves group order and hidden groups");
            Check(document.Data.AppFolders.Count==1&&document.Data.AppFolders[0].InWorkspace&&document.Data.Apps.Count==4,"backup contains virtual folders, website and folder favorite");
            Check(document.Assets.Count==1,"backup embeds and deduplicates identical website and custom icons");File.Delete(image);
            var target=new Config{ThemeMode="深色",ReplaceStartMenu=false};target.Groups.Add("保留现有");target.Apps.Add(new Entry{Id=@"D:\Apps\editor.exe",Name="Editor",Target=@"D:\Apps\editor.exe",Group="保留现有"});ConfigStore.Save(Program.ConfigPath,target);
            var imported=GroupBackup.Import(backup,target,Program.DataDir,Program.ConfigPath);var mapped=imported.Apps.Single(a=>a.Name=="Editor");
            Check(mapped.Target==@"D:\Apps\editor.exe"&&mapped.Group=="设计"&&mapped.Favorite&&mapped.TopPinned&&mapped.SortOrder==3,"import matches same app on another drive while preserving its real launch target");
            Check(imported.ThemeMode=="深色"&&!imported.ReplaceStartMenu&&imported.Groups.Contains("保留现有"),"group import preserves current preferences and unrelated groups");
            Check(imported.Apps.Count(a=>a.Website)==1&&imported.Apps.Count(a=>a.FavoriteFolder)==1&&imported.AppFolders.Count==1,"import restores website, favorite folder and virtual folder membership");
            var repeat=GroupBackup.Merge(imported,document.Data);Check(repeat.Apps.Count==imported.Apps.Count&&repeat.AppFolders.Count==imported.AppFolders.Count,"reimport is idempotent for shortcuts and virtual folders");
            var newer=GroupBackup.Clone(imported);newer.Apps.First(a=>a.Website).Target="https://changed.example.com/";var restoredUrl=GroupBackup.Merge(newer,document.Data);Check(restoredUrl.Apps.First(a=>a.Website).Target=="https://example.com/","website target is restored from backup for matching bookmark identity");
            Check(IconLoader.ReadImage(mapped.CustomIcon)!=null&&IconLoader.Load(imported.Apps.First(a=>a.Website))!=null,"embedded icons restore after original images are deleted");
            Check(Directory.GetFiles(Path.Combine(Program.DataDir,"backups")).Length>0,"pre-import configuration is snapshotted automatically");
            string stored=File.ReadAllText(Program.ConfigPath);document.Version=99;string invalid=Path.Combine(directory,"invalid.lumabackup");File.WriteAllText(invalid,SnapshotDocument(document));bool rejected=false;try{GroupBackup.Import(invalid,imported,Program.DataDir,Program.ConfigPath);}catch(InvalidDataException){rejected=true;}
            Check(rejected&&File.ReadAllText(Program.ConfigPath)==stored,"unsupported backup version is rejected without overwriting live configuration");
            document.Version=1;document.Assets["../escape"]="AAAA";File.WriteAllText(invalid,SnapshotDocument(document));rejected=false;try{GroupBackup.Read(invalid);}catch(InvalidDataException){rejected=true;}Check(rejected,"invalid asset names cannot escape the backup asset directory");
            string legacy=Path.Combine(directory,"legacy.json");ConfigStore.Save(legacy,config);Check(GroupBackup.Read(legacy).Data.Apps.Count==4,"legacy settings JSON is accepted as an import source");
            string template=Path.Combine(directory,"template.json");File.WriteAllText(template,new JavaScriptSerializer().Serialize(GroupTemplate.Export(config)));Check(GroupBackup.Read(template).Data.Groups.Contains("设计"),"old group-only templates can also be imported");
            var undo=LibraryEditing.MoveEntries(config,new[]{first,second},"网站");Check(first.Group=="网站"&&second.Group=="网站","batch move applies to selected entries");undo(config);Check(first.Group=="设计"&&second.Group=="设计"&&first.FolderId==folder.Id,"undo restores groups and virtual folder membership");
            var dissolve=config.RemoveAppFolders(new[]{folder});Check(first.FolderId==null&&config.AppFolders.Count==0,"dissolving a virtual folder never deletes app shortcuts");dissolve(config);Check(first.FolderId==folder.Id&&config.AppFolders.Count==1,"folder dissolution can be undone");
            for(int i=0;i<4;i++)UsageRanking.Record(second,DateTime.UtcNow);Check(second.LaunchCount==4&&second.LastLaunchTicks>0,"launch history remains accurate without automatic pin suggestions");
            var history=new NavigationHistory("全部");history.Remember("搜索",32);history.Visit("设计");Check(history.Back().Query=="搜索"&&history.Current.Offset==32,"back navigation restores search and scroll position");history.Visit("网站");Check(!history.CanForward,"new navigation drops obsolete forward history");
            string realBackup=Path.Combine(directory,"real.lumabackup"),realBefore=Snapshot(original);GroupBackup.Export(realBackup,original,Path.GetDirectoryName(args.Length>0?args[0]:Program.ConfigPath));var recovered=GroupBackup.Read(realBackup);
            Check(original.Groups.SequenceEqual(recovered.Data.Groups)&&original.Apps.Count==recovered.Data.Apps.Count&&original.Apps.Count(a=>a.Website)==recovered.Data.Apps.Count(a=>a.Website),"actual personal groups and websites survive export/import format roundtrip");Check(realBefore==Snapshot(original),"real profile stays unchanged during export");
            var app=new Program{State=GroupBackup.Clone(original)};var window=new Library(app);app.Library=window;Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"new v1.3 library constructs without creating a native window or global hooks");
            Check(window.Topmost,"launcher uses the topmost window band for every open route");
            typeof(Library).GetMethod("SetCommandSelection",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(window,new object[]{true});
            Check(window.DismissFromEscape()&&!window.IsSelecting,"Escape dismisses launcher directly even during batch selection");
            Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"topmost and Escape checks do not create a native window");
            var root=(FrameworkElement)window.Content;root.Measure(new Size(1440,920));root.Arrange(new Rect(0,0,1440,920));root.UpdateLayout();var preview=new RenderTargetBitmap(1440,920,96,96,PixelFormats.Pbgra32);preview.Render(root);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(preview));using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"v1.3-preview.png")))png.Save(stream);
            var selectedField=typeof(Library).GetField("selected",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);
            app.State.ShowWorkspace=false;window.Render();Check((string)selectedField.GetValue(window)=="全部","workspace switch routes current workspace to grouped applications");
            var preference=Path.Combine(directory,"workspace-preference.json");ConfigStore.Save(preference,app.State);Check(!ConfigStore.Read(preference).ShowWorkspace,"workspace switch persists in configuration");
            app.State.ShowWorkspace=true;selectedField.SetValue(window,"@overview");window.Render();Check(Elements<System.Windows.Controls.TextBlock>(root).Any(t=>t.Text=="工作空间"),"workspace can be re-enabled without losing its entry");
            foreach(var entry in app.State.Apps)entry.TopPinned=false;
            app.State.Groups.Add("置顶验证");for(int n=0;n<16;n++)app.State.Apps.Add(new Entry{Id="pin-test-"+n,Name="置顶"+n,Target="pin-test-"+n+".exe",Group="置顶验证",TopPinned=true});
            app.State.LibraryListView=true;app.State.CollapsedGroups.Remove("置顶应用");window.Render();
            var pinSection=Elements<System.Windows.Controls.StackPanel>(root).First(panel=>(string)(panel.Tag as string)=="置顶应用");
            var pins=pinSection.Children.OfType<System.Windows.Controls.WrapPanel>().Single();pins.Measure(new Size(500,double.PositiveInfinity));pins.Arrange(new Rect(0,0,500,pins.DesiredSize.Height));pins.UpdateLayout();
            var pinCards=pins.Children.OfType<System.Windows.Controls.Button>().ToList();
            Check(pinCards.Count==16&&System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(pinCards[1]).Left>System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(pinCards[0]).Left&&System.Windows.Controls.Primitives.LayoutInformation.GetLayoutSlot(pinCards.Last()).Top>0,"manual pins flow horizontally and wrap into multiple rows even in list mode");
            Check(!Elements<System.Windows.Controls.TextBlock>(root).Any(t=>t.Text.Contains("建议置顶")),"pin suggestions absent from rendered interface");
            root.Measure(new Size(1440,920));root.Arrange(new Rect(0,0,1440,920));root.UpdateLayout();var pinnedPreview=new RenderTargetBitmap(1440,920,96,96,PixelFormats.Pbgra32);pinnedPreview.Render(root);var pinnedPng=new PngBitmapEncoder();pinnedPng.Frames.Add(BitmapFrame.Create(pinnedPreview));using(var stream=File.Create(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"pinned-preview.png")))pinnedPng.Save(stream);

            FluentTheme.Apply("深色");window.Render();Check(FluentTheme.Dark,"new style resources support live dark mode");Check(new System.Windows.Interop.WindowInteropHelper(window).Handle==IntPtr.Zero,"offline checks never show a desktop window");return 0;
        }
        catch(Exception error){Console.Error.WriteLine(error);return 1;}
    }
    static string SnapshotDocument(GroupBackupDocument document)=>new JavaScriptSerializer{MaxJsonLength=64*1024*1024}.Serialize(document);
}
