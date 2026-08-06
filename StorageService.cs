using System.IO;
using System.Text.Json;

namespace StickyMarkNative;

public sealed class StorageService
{
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

    public string DataDirectory { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "StickyMark");

    private string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    private string NotesPath => Path.Combine(DataDirectory, "notes.json");

    public AppSettings LoadSettings()
    {
        var settings = Read<AppSettings>(SettingsPath) ?? new AppSettings();
        settings.FontSize = Math.Clamp(settings.FontSize, 9, 36);
        settings.Theme = settings.Theme is "dark" or "light" ? settings.Theme : "light";
        return settings;
    }

    public List<NoteItem> LoadNotes()
    {
        var notes = Read<List<NoteItem>>(NotesPath);
        if (notes is { Count: > 0 }) return notes;

        notes = new List<NoteItem>
        {
            new()
            {
                Title = "欢迎使用 StickyMark",
                Content = "# 欢迎使用 StickyMark\n\n这是你的第一张 Markdown 便签。\n\n- 使用全局快捷键快速呼出\n- 用工具栏整理粗体、斜体和标题\n- 复制所选内容时可选择纯文本或 Markdown\n\n> 重要内容可以随手记下来。",
                Pinned = true,
            },
        };
        SaveNotes(notes);
        return notes;
    }

    public void SaveSettings(AppSettings settings) => Write(SettingsPath, settings);
    public void SaveNotes(List<NoteItem> notes) => Write(NotesPath, notes);

    private T? Read<T>(string path)
    {
        try
        {
            if (!File.Exists(path)) return default;
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), _jsonOptions);
        }
        catch
        {
            return default;
        }
    }

    private void Write<T>(string path, T value)
    {
        Directory.CreateDirectory(DataDirectory);
        var temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, _jsonOptions));
        File.Move(temp, path, true);
    }
}
