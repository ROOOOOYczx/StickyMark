using Microsoft.Win32;
using System.Reflection;
using System.IO;

namespace StickyMarkNative;

public static class StartupManager
{
    private const string RunKeyPath = "Software\\Microsoft\\Windows\\CurrentVersion\\Run";
    private const string ValueName = "StickyMark";

    public static bool IsEnabled()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
            return key?.GetValue(ValueName) is string value && !string.IsNullOrWhiteSpace(value);
        }
        catch
        {
            return false;
        }
    }

    public static (bool Success, string? Error) Apply(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: true)
                ?? Registry.CurrentUser.CreateSubKey(RunKeyPath);
            if (key is null) return (false, "无法打开 Windows 启动项。");
            if (enabled) key.SetValue(ValueName, BuildCommand());
            else if (key.GetValue(ValueName) is not null) key.DeleteValue(ValueName, throwOnMissingValue: false);
            return (true, null);
        }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string BuildCommand()
    {
        var processPath = Environment.ProcessPath ?? string.Empty;
        var processName = Path.GetFileNameWithoutExtension(processPath);
        if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase))
        {
            var assemblyPath = Assembly.GetEntryAssembly()?.Location ?? processPath;
            return $"\"{processPath}\" \"{assemblyPath}\" --startup";
        }
        return $"\"{processPath}\" --startup";
    }
}
