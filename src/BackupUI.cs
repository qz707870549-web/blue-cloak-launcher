using System;
using Microsoft.Win32;
namespace Luma;
public partial class Program
{
    public void ImportGroupBackup(string path)
    {
        State = GroupBackup.Import(path, State, DataDir, ConfigPath);
        protectConfig = false; snapshotSaved = true;
        UI.InvalidateIcons(); Library?.RefreshAfterBackupImport();
    }
}
public partial class Library
{
    public void RefreshAfterBackupImport()
    {
        commandUndoAction = null;
        SetCommandSelection(false);
        Render();
    }
}
public static class BackupUI
{
    public static void Export(Program program, System.Windows.Window owner)
    {
        var dialog = new SaveFileDialog { Title = "导出本机分组备份", Filter = "Luma 分组备份|*.lumabackup", DefaultExt = ".lumabackup", FileName = "Luma分组备份-" + DateTime.Now.ToString("yyyyMMdd-HHmmss"), AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(owner) != true) return;
        try { GroupBackup.Export(dialog.FileName, program.State, Program.DataDir); AirStyle.Notice("分组、应用归属、网站、收藏、虚拟文件夹与可读取的图标已导出。\n备份包含本机路径和网址，请妥善保存。", "备份已导出"); }
        catch (Exception error) { AirStyle.Notice("导出失败：" + error.Message, "分组备份"); }
    }
    public static void Import(Program program, System.Windows.Window owner)
    {
        var dialog = new OpenFileDialog { Title = "导入分组备份", Filter = "Luma 分组备份与旧配置|*.lumabackup;*.json;*.bak;*.tmp|所有文件|*.*", CheckFileExists = true };
        if (dialog.ShowDialog(owner) != true) return;
        if (!AirStyle.Confirm("导入分组备份", "将恢复备份中的分组归属与排序，保留当前其他入口和显示设置。\n导入前会自动备份当前配置；不会删除程序或用户文件。", "导入")) return;
        try { program.ImportGroupBackup(dialog.FileName); AirStyle.Notice("备份已导入。原配置已留存于配置目录的 backups 文件夹。", "导入完成"); }
        catch (Exception error) { AirStyle.Notice("未导入，原分组保持不变。\n" + error.Message, "分组备份"); }
    }
}
