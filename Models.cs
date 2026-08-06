namespace StickyMarkNative;

public sealed class AppSettings
{
    public string Hotkey { get; set; } = "Ctrl+Alt+Space";
    public string FontFamily { get; set; } = "Segoe UI";
    public int FontSize { get; set; } = 14;
    public string NoteColor { get; set; } = "#FFF7B2";
    public string Theme { get; set; } = "light";
    public bool Topmost { get; set; } = true;
    public bool StartWithWindows { get; set; }
    public bool StartMinimizedToTray { get; set; }
    public bool CloseToTray { get; set; } = true;
}

public sealed class NoteItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "未命名便签";
    public string Content { get; set; } = "# 新便签\n\n";
    public string CreatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public string UpdatedAt { get; set; } = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    public bool Pinned { get; set; }
}
