using System.Drawing;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Forms = System.Windows.Forms;

namespace StickyMarkNative;

public sealed class MainForm : Forms.Form
{
    private const int HotkeyId = 0x4D51;
    private const int WsThickFrame = 0x00040000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsSysMenu = 0x00080000;
    private readonly StorageService _storage = new();
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private readonly bool _startupLaunch;
    private readonly WebView2 _webView;
    private List<NoteItem> _notes;
    private AppSettings _settings;
    private string _currentNoteId;
    private NoteForm? _noteForm;
    private Forms.NotifyIcon? _notifyIcon;
    private bool _hotkeyRegistered;
    private bool _allowExit;
    private bool _webReady;
    private string? _pendingPage;

    public MainForm(bool startupLaunch)
    {
        _startupLaunch = startupLaunch;
        _settings = _storage.LoadSettings();
        _settings.StartWithWindows = StartupManager.IsEnabled();
        _notes = _storage.LoadNotes();
        _currentNoteId = _notes[0].Id;

        Text = "StickyMark · Markdown 便签";
        Width = 1120;
        Height = 720;
        MinimumSize = new Size(940, 620);
        StartPosition = Forms.FormStartPosition.CenterScreen;
        FormBorderStyle = Forms.FormBorderStyle.None;
        ControlBox = false;
        ShowInTaskbar = true;
        MinimizeBox = true;
        MaximizeBox = true;
        BackColor = Color.FromArgb(245, 245, 247);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        _webView = new WebView2
        {
            Dock = Forms.DockStyle.Fill,
            AllowExternalDrop = false,
            BackColor = BackColor,
        };
        Controls.Add(_webView);
        Load += async (_, _) => await InitializeWebViewAsync();
        FormClosing += MainForm_FormClosing;
        FormClosed += MainForm_FormClosed;
        SetupTray();
    }

    public AppSettings Settings => _settings;
    public IReadOnlyList<NoteItem> Notes => _notes;
    public NoteItem CurrentNote => _notes.First(note => note.Id == _currentNoteId);
    public bool StartMinimizedToTray => _startupLaunch && _settings.StartMinimizedToTray;

    protected override Forms.CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.Style |= WsThickFrame | WsMinimizeBox | WsMaximizeBox | WsSysMenu;
            return parameters;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        NativeMethods.ApplyWindowChrome(Handle, BackColor);
        RegisterHotkey(showError: false);
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        if (_hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _hotkeyRegistered = false;
        }
        base.OnHandleDestroyed(e);
    }

    protected override void WndProc(ref Forms.Message m)
    {
        if (m.Msg == NativeMethods.WmHotKey && m.WParam.ToInt32() == HotkeyId)
        {
            ToggleNoteWindow();
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    private async Task InitializeWebViewAsync()
    {
        try
        {
            var environment = await WebViewRuntime.GetEnvironmentAsync();
            await _webView.EnsureCoreWebView2Async(environment);
            _webView.DefaultBackgroundColor = BackColor;
            WebViewRuntime.Configure(_webView.CoreWebView2);
            _webView.CoreWebView2.WebMessageReceived += WebView_WebMessageReceived;
            _webView.CoreWebView2.NewWindowRequested += (_, args) => args.Handled = true;
            _webView.Source = new Uri("https://stickymark.local/index.html?view=main");
        }
        catch (Exception ex)
        {
            var message = ex.Message.Contains("WebView2", StringComparison.OrdinalIgnoreCase)
                ? "当前电脑没有可用的 Microsoft Edge WebView2 Runtime，请先安装后再启动 StickyMark。"
                : $"界面初始化失败：{ex.Message}";
            Forms.MessageBox.Show(message, "StickyMark", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
        }
    }

    private async void WebView_WebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            var type = root.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
            var payload = root.TryGetProperty("payload", out var payloadElement) ? payloadElement : default;

            switch (type)
            {
                case "main-ready":
                case "request-main-state":
                    _webReady = true;
                    await SendMainStateAsync();
                    if (_pendingPage is not null)
                    {
                        var page = _pendingPage;
                        _pendingPage = null;
                        await SendAsync("navigate", new { page });
                    }
                    break;
                case "show-note":
                    ShowNoteWindow(ReadString(payload, "id"));
                    break;
                case "new-note":
                    NewNote();
                    break;
                case "save-settings":
                    SaveSettings(payload);
                    break;
                case "show-main":
                    ShowFromTray();
                    break;
                case "quit":
                    ExitApplication();
                    break;
                case "begin-main-drag":
                    NativeMethods.BeginWindowDrag(Handle);
                    break;
                case "resize-main-window":
                    NativeMethods.BeginWindowResize(Handle, ReadString(payload, "direction") ?? "SouthEast");
                    break;
                case "minimize-main":
                    WindowState = Forms.FormWindowState.Minimized;
                    break;
                case "maximize-main":
                    WindowState = WindowState == Forms.FormWindowState.Maximized
                        ? Forms.FormWindowState.Normal
                        : Forms.FormWindowState.Maximized;
                    break;
                case "close-main":
                    Close();
                    break;
            }
        }
        catch (Exception ex)
        {
            await SendAsync("toast", new { message = ex.Message, tone = "error" });
        }
    }

    private void SaveSettings(JsonElement payload)
    {
        var next = payload.Deserialize<AppSettings>(_jsonOptions);
        if (next is null)
        {
            _ = SendAsync("toast", new { message = "设置数据无效。", tone = "error" });
            return;
        }
        if (!NativeMethods.TryParseHotkey(next.Hotkey.Trim(), out _, out _, out var hotkeyError))
        {
            _ = SendAsync("toast", new { message = hotkeyError, tone = "error" });
            return;
        }
        if (next.FontSize is < 9 or > 36)
        {
            _ = SendAsync("toast", new { message = "字号需要是 9 到 36 之间的整数。", tone = "error" });
            return;
        }
        if (!IsColor(next.NoteColor))
        {
            _ = SendAsync("toast", new { message = "便签背景色需要是类似 #FFF7B2 的颜色值。", tone = "error" });
            return;
        }

        next.Hotkey = next.Hotkey.Trim();
        next.FontFamily = string.IsNullOrWhiteSpace(next.FontFamily) ? "Segoe UI" : next.FontFamily.Trim();
        next.Theme = next.Theme is "dark" ? "dark" : "light";
        next.NoteColor = next.NoteColor.Trim();
        var startupResult = StartupManager.Apply(next.StartWithWindows);
        if (!startupResult.Success)
        {
            _ = SendAsync("toast", new { message = startupResult.Error ?? "无法更新 Windows 启动项。", tone = "error" });
            return;
        }

        _settings = next;
        _settings.StartWithWindows = next.StartWithWindows;
        _storage.SaveSettings(_settings);
        RegisterHotkey(showError: false);
        _noteForm?.ApplyAppearance();
        _ = SendMainStateAsync();
        _ = SendAsync("toast", new { message = "设置已保存。", tone = "success" });
    }

    private static bool IsColor(string value)
    {
        if (!Regex.IsMatch(value.Trim(), "^#[0-9a-fA-F]{6}([0-9a-fA-F]{2})?$")) return false;
        try
        {
            _ = ColorTranslator.FromHtml(value.Trim()[..7]);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool RegisterHotkey(bool showError)
    {
        if (!IsHandleCreated) return false;
        if (_hotkeyRegistered)
        {
            NativeMethods.UnregisterHotKey(Handle, HotkeyId);
            _hotkeyRegistered = false;
        }
        if (!NativeMethods.TryParseHotkey(_settings.Hotkey, out var modifiers, out var key, out var parseError))
        {
            if (showError) Forms.MessageBox.Show(parseError, "快捷键格式错误", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
            return false;
        }
        _hotkeyRegistered = NativeMethods.RegisterHotKey(Handle, HotkeyId, modifiers, key);
        if (!_hotkeyRegistered && showError)
        {
            Forms.MessageBox.Show($"无法注册 {_settings.Hotkey}，可能已被其他程序占用。", "快捷键不可用", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Warning);
        }
        return _hotkeyRegistered;
    }

    public void ShowNoteWindow(string? noteId = null)
    {
        if (!string.IsNullOrWhiteSpace(noteId) && _notes.Any(note => note.Id == noteId)) _currentNoteId = noteId;
        _noteForm ??= CreateNoteForm();
        _noteForm.LoadNote(CurrentNote);
        if (!_noteForm.Visible) _noteForm.Show();
        _noteForm.WindowState = Forms.FormWindowState.Normal;
        _noteForm.Activate();
        _noteForm.BringToFront();
    }

    private NoteForm CreateNoteForm()
    {
        var form = new NoteForm(this);
        form.FormClosed += (_, _) => _noteForm = null;
        return form;
    }

    public void ShowSettings()
    {
        _pendingPage = "settings";
        ShowFromTray();
        if (_webReady)
        {
            _pendingPage = null;
            _ = SendAsync("navigate", new { page = "settings" });
        }
    }

    public void ShowFromTray()
    {
        if (IsDisposed) return;
        Show();
        WindowState = Forms.FormWindowState.Normal;
        Activate();
        BringToFront();
        _ = SendMainStateAsync();
    }

    private void ToggleNoteWindow()
    {
        if (_noteForm is not null && !_noteForm.IsDisposed && _noteForm.Visible)
        {
            _noteForm.Hide();
            return;
        }
        ShowNoteWindow();
    }

    private void NewNote()
    {
        var note = new NoteItem { Title = "未命名便签", Content = "# 新便签\n\n" };
        _notes.Insert(0, note);
        _currentNoteId = note.Id;
        _storage.SaveNotes(_notes);
        _ = SendMainStateAsync();
        ShowNoteWindow(note.Id);
    }

    public void UpdateNote(string id, string title, string content)
    {
        var note = _notes.FirstOrDefault(item => item.Id == id);
        if (note is null) return;
        note.Title = string.IsNullOrWhiteSpace(title) ? "未命名便签" : title.Trim();
        note.Content = content ?? string.Empty;
        note.UpdatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        _storage.SaveNotes(_notes);
        _ = SendMainStateAsync();
    }

    public void TogglePin(string id)
    {
        var note = _notes.FirstOrDefault(item => item.Id == id);
        if (note is null) return;
        note.Pinned = !note.Pinned;
        _storage.SaveNotes(_notes);
        _noteForm?.SendCurrentState();
        _ = SendMainStateAsync();
    }

    public void DeleteNote(string id)
    {
        if (_notes.Count <= 1)
        {
            Forms.MessageBox.Show("至少保留一张便签。", "无法删除", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Information);
            return;
        }
        if (Forms.MessageBox.Show("确定要删除当前便签吗？此操作不可撤销。", "删除便签", Forms.MessageBoxButtons.YesNo, Forms.MessageBoxIcon.Warning) != Forms.DialogResult.Yes) return;
        _notes.RemoveAll(note => note.Id == id);
        _currentNoteId = _notes[0].Id;
        _storage.SaveNotes(_notes);
        _noteForm?.Hide();
        _ = SendMainStateAsync();
    }

    private async Task SendMainStateAsync()
    {
        await SendAsync("main-state", new { settings = _settings, notes = _notes, currentNoteId = _currentNoteId });
    }

    internal async Task SendAsync(string type, object payload)
    {
        if (!_webReady || _webView.CoreWebView2 is null) return;
        var message = JsonSerializer.Serialize(new { type, payload }, _jsonOptions);
        try { await _webView.ExecuteScriptAsync($"window.StickyMark && window.StickyMark.receive({message});"); }
        catch (InvalidOperationException) { }
    }

    private void SetupTray()
    {
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = SystemIcons.Application,
            Text = "StickyMark",
            Visible = true,
        };
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("显示主页", null, (_, _) => BeginInvoke(ShowFromTray));
        menu.Items.Add("快速便签", null, (_, _) => BeginInvoke(ShowNoteWindow));
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("退出 StickyMark", null, (_, _) => BeginInvoke(ExitApplication));
        _notifyIcon.ContextMenuStrip = menu;
        _notifyIcon.DoubleClick += (_, _) => BeginInvoke(ShowFromTray);
    }

    private void ExitApplication()
    {
        _allowExit = true;
        _noteForm?.ForceClose();
        Close();
    }

    private void MainForm_FormClosing(object? sender, Forms.FormClosingEventArgs e)
    {
        if (_allowExit) return;
        if (_settings.CloseToTray)
        {
            e.Cancel = true;
            Hide();
        }
    }

    private void MainForm_FormClosed(object? sender, Forms.FormClosedEventArgs e)
    {
        _noteForm?.ForceClose();
        _notifyIcon?.Dispose();
        _notifyIcon = null;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;
    }
}
