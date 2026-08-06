using Microsoft.Web.WebView2.Core;

namespace StickyMarkNative;

internal static class WebViewRuntime
{
    private static readonly object SyncRoot = new();
    private static Task<CoreWebView2Environment>? _environmentTask;

    public static string WebRoot => Path.Combine(AppContext.BaseDirectory, "web");

    public static Task<CoreWebView2Environment> GetEnvironmentAsync()
    {
        lock (SyncRoot)
        {
            return _environmentTask ??= CreateEnvironmentAsync();
        }
    }

    private static async Task<CoreWebView2Environment> CreateEnvironmentAsync()
    {
        var userDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "StickyMark", "WebView2");
        Directory.CreateDirectory(userDataFolder);
        if (!Directory.Exists(WebRoot)) throw new DirectoryNotFoundException($"WebView2 页面目录不存在：{WebRoot}");
        return await CoreWebView2Environment.CreateAsync(userDataFolder: userDataFolder);
    }

    public static void Configure(CoreWebView2 core)
    {
        core.SetVirtualHostNameToFolderMapping(
            "stickymark.local", WebRoot, CoreWebView2HostResourceAccessKind.Allow);
        core.Settings.AreDefaultContextMenusEnabled = false;
        core.Settings.AreDevToolsEnabled = false;
        core.Settings.IsStatusBarEnabled = false;
        core.Settings.IsZoomControlEnabled = false;
    }
}
