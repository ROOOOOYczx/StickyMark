using System.Drawing;
using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;
using Forms = System.Windows.Forms;

namespace StickyMarkNative;

public sealed class NoteForm : Forms.Form
{
    private readonly MainForm _parent;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
    };
    private readonly WebView2 _webView;
    private NoteItem? _currentNote;
    private bool _webReady;
    private bool _allowClose;
    private bool? _topmostOverride;

    public NoteForm(MainForm parent)
    {
        _parent = parent;
        Text = "StickyMark 便签";
        Width = 720;
        Height = 600;
        MinimumSize = new Size(560, 400);
        StartPosition = Forms.FormStartPosition.CenterScreen;
        FormBorderStyle = Forms.FormBorderStyle.None;
        ShowInTaskbar = false;
        BackColor = Color.FromArgb(255, 255, 247, 178);
        AutoScaleMode = Forms.AutoScaleMode.Dpi;

        _webView = new WebView2
        {
            Dock = Forms.DockStyle.Fill,
            AllowExternalDrop = false,
            BackColor = BackColor,
        };
        Controls.Add(_webView);
        Load += async (_, _) => await InitializeWebViewAsync();
        FormClosing += NoteForm_FormClosing;
    }

    public void LoadNote(NoteItem note)
    {
        _currentNote = note;
        if (_webReady) SendCurrentState();
    }

    public void SendCurrentState()
    {
        if (_currentNote is null) return;
        _ = SendAsync("note-state", new { note = _currentNote, settings = _parent.Settings, topmost = EffectiveTopmost });
    }

    public void ApplyAppearance()
    {
        BackColor = ParseColor(_parent.Settings.NoteColor, Color.FromArgb(255, 255, 247, 178));
        ApplyTopmostState();
        NativeMethods.ApplyWindowChrome(Handle, BackColor);
        SendCurrentState();
    }

    private bool EffectiveTopmost => _topmostOverride ?? _parent.Settings.Topmost;

    private void ApplyTopmostState()
    {
        TopMost = EffectiveTopmost;
        if (IsHandleCreated) NativeMethods.SetTopmost(Handle, TopMost);
    }

    private void ToggleTopmost()
    {
        _topmostOverride = !EffectiveTopmost;
        ApplyTopmostState();
        SendCurrentState();
    }

    public void ForceClose()
    {
        _allowClose = true;
        Close();
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
            _webView.Source = new Uri("https://stickymark.local/index.html?view=note");
            ApplyTopmostState();
            NativeMethods.ApplyWindowChrome(Handle, BackColor);
        }
        catch (Exception ex)
        {
            Forms.MessageBox.Show($"便签界面初始化失败：{ex.Message}", "StickyMark", Forms.MessageBoxButtons.OK, Forms.MessageBoxIcon.Error);
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
                case "note-ready":
                    _webReady = true;
                    SendCurrentState();
                    break;
                case "request-note-state":
                    SendCurrentState();
                    break;
                case "begin-drag":
                    NativeMethods.BeginWindowDrag(Handle);
                    break;
                case "resize-window":
                    NativeMethods.BeginWindowResize(Handle, ReadString(payload, "direction") ?? "SouthEast");
                    break;
                case "toggle-topmost":
                    ToggleTopmost();
                    break;
                case "hide-note":
                    Hide();
                    break;
                case "open-settings":
                    _parent.ShowSettings();
                    break;
                case "delete-note":
                    if (_currentNote is not null) _parent.DeleteNote(_currentNote.Id);
                    break;
                case "save-note":
                    SaveNote(payload);
                    break;
                case "copy-text":
                    CopyText(payload);
                    break;
            }
        }
        catch (Exception ex)
        {
            await SendAsync("toast", new { message = ex.Message, tone = "error" });
        }
    }

    private void SaveNote(JsonElement payload)
    {
        var id = ReadString(payload, "id");
        if (_currentNote is null || string.IsNullOrWhiteSpace(id) || id != _currentNote.Id) return;
        _parent.UpdateNote(id, ReadString(payload, "title") ?? string.Empty, ReadString(payload, "content") ?? string.Empty);
    }

    private async void CopyText(JsonElement payload)
    {
        var text = ReadString(payload, "text") ?? string.Empty;
        try
        {
            Forms.Clipboard.SetText(text);
            await SendAsync("toast", new { message = "已复制。", tone = "success" });
        }
        catch (ExternalException)
        {
            await SendAsync("toast", new { message = "剪贴板暂时不可用。", tone = "error" });
        }
    }

    internal async Task SendAsync(string type, object payload)
    {
        if (!_webReady || _webView.CoreWebView2 is null) return;
        var message = JsonSerializer.Serialize(new { type, payload }, _jsonOptions);
        try { await _webView.ExecuteScriptAsync($"window.StickyMark && window.StickyMark.receive({message});"); }
        catch (InvalidOperationException) { }
    }

    private void NoteForm_FormClosing(object? sender, Forms.FormClosingEventArgs e)
    {
        if (_allowClose) return;
        e.Cancel = true;
        Hide();
    }

    private static Color ParseColor(string value, Color fallback)
    {
        try
        {
            var hex = value.Trim().TrimStart('#');
            if (hex.Length == 6) return Color.FromArgb(255, Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16));
            if (hex.Length == 8) return Color.FromArgb(Convert.ToInt32(hex[..2], 16), Convert.ToInt32(hex[2..4], 16), Convert.ToInt32(hex[4..6], 16), Convert.ToInt32(hex[6..8], 16));
        }
        catch { }
        return fallback;
    }

    private static string? ReadString(JsonElement payload, string name)
    {
        return payload.ValueKind == JsonValueKind.Object && payload.TryGetProperty(name, out var value)
            ? value.GetString()
            : null;
    }
}
