using System;
using System.IO;

namespace Luma;

public static class BuiltInGroups
{
    public static Config Initialize(string configPath, string directory)
    {
        // Never replace existing data or bypass the normal recovery path.
        if (File.Exists(configPath) || File.Exists(configPath + ".bak") || File.Exists(configPath + ".tmp"))
        {
            bool recovered, protect;
            return ConfigStore.Load(configPath, out recovered, out protect);
        }
        Directory.CreateDirectory(directory);
        string temporary = Path.Combine(directory, ".initial-groups-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var input = typeof(BuiltInGroups).Assembly.GetManifestResourceStream("Luma.DefaultGroups"))
            using (var output = File.Create(temporary))
            {
                if (input == null) throw new InvalidDataException("缺少内置分组资源。");
                input.CopyTo(output);
            }
            return GroupBackup.Import(temporary, new Config(), directory, configPath, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
