(function () {
  "use strict";

  const view = new URLSearchParams(location.search).get("view") || "main";
  const app = document.getElementById("app");
  const state = { settings: null, notes: [], currentNoteId: null, page: "home", note: null, saveTimer: 0, toastTimer: 0 };
  const $ = (selector, root = document) => root.querySelector(selector);
  const $$ = (selector, root = document) => Array.from(root.querySelectorAll(selector));

  window.StickyMark = {
    post(type, payload = {}) {
      if (window.chrome && window.chrome.webview) window.chrome.webview.postMessage({ type, payload });
    },
    receive(message) {
      if (!message) return;
      if (message.type === "toast") return showToast(message.payload || {});
      if (view === "main" && message.type === "navigate") {
        state.page = message.payload && ["home", "notes", "settings"].includes(message.payload.page) ? message.payload.page : "home";
        return renderMain();
      }
      if (view === "main" && message.type === "main-state") return renderMainState(message.payload || {});
      if (view === "note" && message.type === "note-state") return renderNoteState(message.payload || {});
    }
  };

  function escapeHtml(value) {
    return String(value ?? "").replace(/[&<>"']/g, (character) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[character]));
  }

  function formatTime(value) {
    if (!value) return "刚刚更新";
    return String(value).replace("T", " ").slice(0, 16);
  }

  function showToast(payload) {
    let toast = $(".toast");
    if (!toast) {
      toast = document.createElement("div");
      toast.className = "toast";
      document.body.appendChild(toast);
    }
    toast.textContent = payload.message || "已完成";
    toast.className = `toast show ${payload.tone || ""}`;
    clearTimeout(state.toastTimer);
    state.toastTimer = setTimeout(() => toast.classList.remove("show"), 2200);
  }

  function applyTheme(settings) {
    const theme = settings && settings.theme === "dark" ? "dark" : "light";
    document.documentElement.dataset.theme = theme;
  }

  function renderMainState(payload) {
    state.settings = payload.settings || state.settings || {};
    state.notes = Array.isArray(payload.notes) ? payload.notes : state.notes;
    state.currentNoteId = payload.currentNoteId || state.currentNoteId || (state.notes[0] && state.notes[0].id);
    applyTheme(state.settings);
    renderMain();
  }

  function renderMain() {
    const activePage = state.page;
    app.innerHTML = `
      <div class="main-shell">
        <header class="window-titlebar" aria-label="窗口标题栏">
          <div class="window-brand main-drag-zone"><span class="brand-dot"></span><span>StickyMark</span></div>
          <div class="window-title main-drag-zone">Markdown 便签</div>
          <div class="window-controls">
            <button class="window-control" data-window-command="minimize-main" title="最小化" aria-label="最小化"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M5 12h14"></path></svg></button>
            <button class="window-control" data-window-command="maximize-main" title="最大化或还原" aria-label="最大化或还原"><svg viewBox="0 0 24 24" aria-hidden="true"><rect x="6" y="6" width="12" height="12" rx=".5"></rect></svg></button>
            <button class="window-control close" data-window-command="close-main" title="关闭" aria-label="关闭"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18"></path></svg></button>
          </div>
        </header>
        <div class="main-body">
          <aside class="sidebar">
            <div class="brand">
              <div class="brand-mark"><span class="brand-dot"></span><span>StickyMark</span></div>
              <div class="brand-subtitle">一张轻盈的 Markdown 便签</div>
            </div>
            <nav class="nav" aria-label="主导航">
              <button class="nav-button ${activePage === "home" ? "active" : ""}" data-page="home"><span class="nav-icon">⌂</span><span>主页</span></button>
              <button class="nav-button ${activePage === "notes" ? "active" : ""}" data-page="notes"><span class="nav-icon">▣</span><span>我的便签</span></button>
              <button class="nav-button ${activePage === "settings" ? "active" : ""}" data-page="settings"><span class="nav-icon">◌</span><span>设置</span></button>
            </nav>
            <div class="sidebar-bottom">
              <div class="hotkey-label">全局呼出</div>
              <div class="hotkey-value">${escapeHtml(state.settings.hotkey || "Ctrl+Alt+Space")}</div>
              <div class="tray-state">${state.settings.startWithWindows ? "已设置开机启动" : "开机启动未启用"}</div>
            </div>
          </aside>
          <main class="main-content">${activePage === "settings" ? settingsPage() : activePage === "notes" ? notesPage() : homePage()}</main>
        </div>
        <div class="main-resize-handle n" data-direction="North"></div><div class="main-resize-handle s" data-direction="South"></div><div class="main-resize-handle e" data-direction="East"></div><div class="main-resize-handle w" data-direction="West"></div><div class="main-resize-handle ne" data-direction="NorthEast"></div><div class="main-resize-handle nw" data-direction="NorthWest"></div><div class="main-resize-handle se" data-direction="SouthEast"></div><div class="main-resize-handle sw" data-direction="SouthWest"></div>
      </div>`;
    bindMainEvents();
  }

  function pageHeader(eyebrow, title, description, actions = "") {
    return `<div class="page-header"><div><div class="eyebrow">${eyebrow}</div><h1 class="page-title">${title}</h1><p class="page-description">${description}</p></div><div class="header-actions">${actions}</div></div>`;
  }

  function homePage() {
    const notes = state.notes.slice().sort((a, b) => Number(Boolean(b.pinned)) - Number(Boolean(a.pinned)) || String(b.updatedAt).localeCompare(String(a.updatedAt))).slice(0, 3);
    const actions = `<button class="button primary" data-action="new-note">新建便签</button><button class="button ghost" data-action="open-current">打开便签</button>`;
    return `<div class="content-wrap">${pageHeader("YOUR QUIET SPACE", "记录下来，稍后再看。", "一个安静、顺手的桌面角落。用全局快捷键随时呼出，内容会自动保存。", actions)}
      <section class="hero-card"><div class="hero-copy"><h2>让想法停在桌面上</h2><p>StickyMark 用一张轻盈的便签承载 Markdown 内容。你可以像在 Word 里一样整理文字，也可以随时复制纯文本或 Markdown。</p><div class="hero-actions"><button class="button primary" data-action="new-note">开始写一张便签 <span aria-hidden="true">→</span></button><button class="button" data-page="settings">调整偏好</button></div></div><div class="hero-orbit"><div class="orbit-ring"></div><div class="paper-preview"></div></div></section>
      <div class="stats"><div class="stat-card"><div class="stat-value">${state.notes.length}</div><div class="stat-label">全部便签</div></div><div class="stat-card"><div class="stat-value">${state.notes.filter((note) => note.pinned).length}</div><div class="stat-label">置顶便签</div></div><div class="stat-card"><div class="stat-value">${state.settings.closeToTray ? "托盘" : "退出"}</div><div class="stat-label">关闭主页后的行为</div></div></div>
      <div class="section-heading"><h3>最近编辑</h3><span>${state.notes.length ? "保持在手边" : "还没有便签"}</span></div>
      ${notes.length ? `<div class="note-grid">${notes.map(noteCard).join("")}</div>` : `<div class="empty-state">还没有便签，点击右上角新建一张吧。</div>`}
    </div>`;
  }

  function notesPage() {
    return `<div class="content-wrap">${pageHeader("YOUR NOTES", "我的便签", "所有内容都保存在本机 AppData 文件夹中。", `<button class="button primary" data-action="new-note">新建便签</button>`)}
      ${state.notes.length ? `<div class="note-grid">${state.notes.slice().sort((a, b) => Number(Boolean(b.pinned)) - Number(Boolean(a.pinned)) || String(b.updatedAt).localeCompare(String(a.updatedAt))).map(noteCard).join("")}</div>` : `<div class="empty-state">还没有便签。</div>`}</div>`;
  }

  function noteCard(note) {
    const title = note.title || "未命名便签";
    const text = String(note.content || "").replace(/^#+\s*/gm, "").replace(/[>*_`~-]/g, "").trim();
    return `<article class="note-card" data-note-id="${escapeHtml(note.id)}"><div class="note-card-title">${note.pinned ? '<span class="pin-mark">✦</span>' : ""}<span>${escapeHtml(title)}</span></div><div class="note-card-content">${escapeHtml(text || "空白便签")}</div><div class="note-card-time">${formatTime(note.updatedAt)}</div></article>`;
  }

  function settingsPage() {
    const s = state.settings || {};
    const fonts = ["Segoe UI", "Microsoft YaHei", "Microsoft YaHei UI", "Arial", "Consolas"];
    return `<div class="content-wrap">${pageHeader("PREFERENCES", "设置", "把 StickyMark 调整成适合你的记录方式。")}
      <section class="card settings-card"><form class="settings-form" id="settings-form">
        <div class="setting-row"><div class="setting-label">全局快捷键</div><input class="field" name="hotkey" value="${escapeHtml(s.hotkey || "Ctrl+Alt+Space")}" placeholder="Ctrl+Alt+Space"><div class="setting-help">例如 Ctrl+Alt+Space。按下后会显示当前便签。</div></div>
        <div class="setting-row"><div class="setting-label">便签字体</div><select class="select" name="fontFamily">${fonts.map((font) => `<option ${font === s.fontFamily ? "selected" : ""}>${escapeHtml(font)}</option>`).join("")}</select><div class="setting-help">编辑区和预览区会同步使用该字体。</div></div>
        <div class="setting-row"><div class="setting-label">字号</div><select class="select" name="fontSize">${[12, 13, 14, 15, 16, 18, 20, 22].map((size) => `<option value="${size}" ${Number(s.fontSize) === size ? "selected" : ""}>${size}px</option>`).join("")}</select><div class="setting-help">建议使用 12–18px，便于长时间阅读。</div></div>
        <div class="setting-row"><div class="setting-label">界面主题</div><select class="select" name="theme"><option value="light" ${s.theme !== "dark" ? "selected" : ""}>浅色</option><option value="dark" ${s.theme === "dark" ? "selected" : ""}>深色</option></select><div class="setting-help">只影响主页和设置页的界面颜色。</div></div>
        <div class="setting-row"><div class="setting-label">便签背景色</div><div class="color-field"><input class="field" name="noteColor" value="${escapeHtml(s.noteColor || "#FFF7B2")}" pattern="^#[0-9a-fA-F]{6}$"><input type="color" id="note-color-picker" value="${normalizeColor(s.noteColor)}"></div><div class="setting-help">只影响便签窗口，不改变主页主题。</div></div>
        <div class="setting-row"><div class="setting-label">窗口行为</div><label class="check"><input type="checkbox" name="topmost" ${s.topmost ? "checked" : ""}>便签始终置顶</label><div class="setting-help">适合把便签放在工作区上方随时查看。</div></div>
        <div class="setting-row"><div class="setting-label">启动方式</div><label class="check"><input type="checkbox" name="startWithWindows" ${s.startWithWindows ? "checked" : ""}>开机自动启动 StickyMark</label><div class="setting-help">写入当前用户的 Windows 启动项，不需要管理员权限。</div></div>
        <div class="setting-row"><div class="setting-label">静默启动</div><label class="check"><input type="checkbox" name="startMinimizedToTray" ${s.startMinimizedToTray ? "checked" : ""}>静默启动到系统托盘</label><div class="setting-help">仅在开机自动启动时生效，手动打开程序仍会显示主页。</div></div>
        <div class="setting-row"><div class="setting-label">关闭行为</div><label class="check"><input type="checkbox" name="closeToTray" ${s.closeToTray ? "checked" : ""}>关闭主页时收进系统托盘</label><div class="setting-help">关闭按钮只隐藏主页，程序和全局快捷键继续运行。</div></div>
        <div class="settings-footer"><button class="button primary" type="submit">保存设置</button><span class="settings-note">设置会保存到本机 AppData 文件夹。</span></div>
      </form></section></div>`;
  }

  function bindMainEvents() {
    $$("[data-page]").forEach((button) => button.addEventListener("click", () => { state.page = button.dataset.page; renderMain(); }));
    $$('[data-action="new-note"]').forEach((button) => button.addEventListener("click", () => StickyMark.post("new-note")));
    $$('[data-window-command]').forEach((button) => button.addEventListener("click", () => StickyMark.post(button.dataset.windowCommand)));
    const titlebar = $(".window-titlebar");
    if (titlebar) titlebar.addEventListener("mousedown", (event) => { if (!event.target.closest("button, input, select, a")) StickyMark.post("begin-main-drag"); });
    $$(".main-resize-handle").forEach((handle) => handle.addEventListener("mousedown", (event) => { event.preventDefault(); event.stopPropagation(); StickyMark.post("resize-main-window", { direction: handle.dataset.direction }); }));
    const currentButton = $('[data-action="open-current"]');
    if (currentButton) currentButton.addEventListener("click", () => StickyMark.post("show-note", { id: state.currentNoteId }));
    $$("[data-note-id]").forEach((card) => card.addEventListener("click", () => StickyMark.post("show-note", { id: card.dataset.noteId })));
    const form = $("#settings-form");
    if (!form) return;
    const colorText = $('[name="noteColor"]', form);
    const colorPicker = $("#note-color-picker", form);
    colorPicker.addEventListener("input", () => colorText.value = colorPicker.value.toUpperCase());
    colorText.addEventListener("input", () => { if (/^#[0-9a-f]{6}$/i.test(colorText.value)) colorPicker.value = colorText.value; });
    form.addEventListener("submit", (event) => {
      event.preventDefault();
      const data = new FormData(form);
      StickyMark.post("save-settings", {
        hotkey: String(data.get("hotkey") || ""), fontFamily: String(data.get("fontFamily") || "Segoe UI"), fontSize: Number(data.get("fontSize") || 14),
        noteColor: String(data.get("noteColor") || "#FFF7B2"), theme: String(data.get("theme") || "light"),
        topmost: data.has("topmost"), startWithWindows: data.has("startWithWindows"), startMinimizedToTray: data.has("startMinimizedToTray"), closeToTray: data.has("closeToTray")
      });
    });
  }

  function normalizeColor(value) { return /^#[0-9a-f]{6}$/i.test(String(value || "")) ? value : "#FFF7B2"; }

  function renderNoteState(payload) {
    state.note = payload.note || state.note;
    state.settings = payload.settings || state.settings || {};
    state.topmost = typeof payload.topmost === "boolean" ? payload.topmost : Boolean(state.settings.topmost);
    applyTheme(state.settings);
    renderNote();
  }

  function renderNote() {
    const note = state.note || { id: "", title: "未命名便签", content: "", pinned: false, updatedAt: "" };
    const s = state.settings || {};
    const noteColor = normalizeColor(s.noteColor);
    app.innerHTML = `<div class="note-shell" style='--note-color:${noteColor};--note-font:${cssFont(s.fontFamily)};--note-size:${Number(s.fontSize) || 14}px' data-theme="${s.theme === "dark" ? "dark" : "light"}">
      <div class="note-header drag-zone"><div class="note-symbol">✦</div><input class="note-title" id="note-title" value="${escapeHtml(note.title || "未命名便签")}" aria-label="便签标题"><div class="note-actions"><button class="icon-button copy-button" id="copy-button" title="复制所选文本">复制</button><button class="icon-button ${state.topmost ? "active" : ""}" id="pin-button" title="${state.topmost ? "取消置顶" : "置顶"}" aria-label="${state.topmost ? "取消置顶" : "置顶"}">★</button><button class="icon-button" id="hide-button" title="隐藏便签">×</button></div></div>
      <div class="format-bar"><select class="format-select font" id="font-select" aria-label="字体"><option value="">字体</option><option>Segoe UI</option><option>Microsoft YaHei</option><option>Microsoft YaHei UI</option><option>Arial</option><option>Consolas</option></select><select class="format-select size" id="size-select" aria-label="字号"><option value="">字号</option><option value="12">12</option><option value="14">14</option><option value="16">16</option><option value="18">18</option><option value="20">20</option><option value="24">24</option></select><span class="format-divider"></span><button class="format-button bold" data-command="bold" title="粗体">B</button><button class="format-button italic" data-command="italic" title="斜体">I</button><button class="format-button underline" data-command="underline" title="下划线">U</button><button class="format-button strike" data-command="strikeThrough" title="删除线">S</button><span class="format-divider"></span><select class="format-select heading" id="heading-select" aria-label="标题级别"><option value="p">正文</option><option value="h1">标题 1</option><option value="h2">标题 2</option><option value="h3">标题 3</option></select><span class="format-divider"></span><button class="format-button" data-command="insertUnorderedList" title="无序列表">•</button><button class="format-button" data-command="insertOrderedList" title="有序列表">1.</button></div>
      <div class="editor-wrap"><div id="editor" class="editor" contenteditable="true" spellcheck="false"></div></div>
      <div class="note-footer"><span class="note-status" id="note-status">${note.updatedAt ? `最后保存于 ${escapeHtml(formatTime(note.updatedAt))}` : "自动保存"}</span><span><button id="settings-button">打开主页设置</button><button class="delete" id="delete-button">删除便签</button></span></div>
      <div class="resize-handle n" data-direction="North"></div><div class="resize-handle s" data-direction="South"></div><div class="resize-handle e" data-direction="East"></div><div class="resize-handle w" data-direction="West"></div><div class="resize-handle ne" data-direction="NorthEast"></div><div class="resize-handle nw" data-direction="NorthWest"></div><div class="resize-handle se" data-direction="SouthEast"></div><div class="resize-handle sw" data-direction="SouthWest"></div>
    </div>`;
    const editor = $("#editor");
    editor.innerHTML = markdownToHtml(note.content || "");
    bindNoteEvents();
  }

  function cssFont(font) { return /^[\w\s-]+$/.test(String(font || "Segoe UI")) ? `"${String(font || "Segoe UI")}"` : '"Segoe UI"'; }

  function bindNoteEvents() {
    const editor = $("#editor");
    const title = $("#note-title");
    const header = $(".note-header");
    const save = () => {
      clearTimeout(state.saveTimer);
      state.saveTimer = setTimeout(() => StickyMark.post("save-note", { id: state.note && state.note.id, title: title.value.trim() || "未命名便签", content: htmlToMarkdown(editor) }), 420);
      const status = $("#note-status");
      if (status) status.textContent = "正在保存……";
    };
    editor.addEventListener("input", save);
    title.addEventListener("input", save);
    header.addEventListener("mousedown", (event) => { if (!event.target.closest("button, input, select")) StickyMark.post("begin-drag"); });
    $("#hide-button").addEventListener("click", () => StickyMark.post("hide-note"));
    $("#pin-button").addEventListener("click", () => StickyMark.post("toggle-topmost"));
    $("#settings-button").addEventListener("click", () => StickyMark.post("open-settings"));
    $("#delete-button").addEventListener("click", () => { if (confirm("确定删除这张便签吗？")) StickyMark.post("delete-note"); });
    $("#copy-button").addEventListener("click", () => showCopyChoices());
    $$(".format-button").forEach((button) => button.addEventListener("mousedown", (event) => event.preventDefault()));
    $$(".format-button").forEach((button) => button.addEventListener("click", () => { focusEditor(editor); document.execCommand(button.dataset.command, false, null); editor.focus(); save(); }));
    $("#font-select").addEventListener("change", (event) => { focusEditor(editor); document.execCommand("fontName", false, event.target.value); editor.focus(); save(); });
    $("#size-select").addEventListener("change", (event) => { focusEditor(editor); document.execCommand("fontSize", false, "7"); $$("font[size=\"7\"]", editor).forEach((font) => { font.removeAttribute("size"); font.style.fontSize = `${event.target.value}px`; }); editor.focus(); save(); });
    $("#heading-select").addEventListener("change", (event) => { focusEditor(editor); document.execCommand("formatBlock", false, event.target.value); editor.focus(); save(); });
    $$(".resize-handle").forEach((handle) => handle.addEventListener("mousedown", (event) => { event.preventDefault(); event.stopPropagation(); StickyMark.post("resize-window", { direction: handle.dataset.direction }); }));
  }

  function focusEditor(editor) { if (document.activeElement !== editor) editor.focus(); }

  function showCopyChoices() {
    let menu = $("#copy-menu");
    if (menu) { menu.remove(); return; }
    menu = document.createElement("div");
    menu.id = "copy-menu";
    menu.style.cssText = "position:fixed;top:65px;right:70px;z-index:12;padding:5px;background:rgba(255,255,245,.96);border:1px solid rgba(82,73,35,.22);border-radius:9px;box-shadow:0 8px 24px rgba(50,45,20,.16);font-size:12px";
    menu.innerHTML = '<button data-copy-mode="plain" style="display:block;width:100%;padding:7px 10px;text-align:left;background:transparent">复制纯文本</button><button data-copy-mode="markdown" style="display:block;width:100%;padding:7px 10px;text-align:left;background:transparent">复制 Markdown</button>';
    document.body.appendChild(menu);
    $$("[data-copy-mode]", menu).forEach((button) => button.addEventListener("click", () => { const selection = window.getSelection(); const mode = button.dataset.copyMode; const text = mode === "markdown" ? selectedMarkdown(selection) : String(selection || ""); StickyMark.post("copy-text", { text }); menu.remove(); }));
  }

  function selectedMarkdown(selection) {
    if (!selection || selection.rangeCount === 0 || !String(selection).trim()) return "";
    const fragment = selection.getRangeAt(0).cloneContents();
    const wrapper = document.createElement("div");
    wrapper.appendChild(fragment);
    return htmlToMarkdown(wrapper);
  }

  function markdownToHtml(markdown) {
    const lines = String(markdown || "").replace(/\r/g, "").split("\n");
    const output = [];
    let paragraph = [];
    let list = null;
    let inCode = false;
    let code = [];
    const flushParagraph = () => { if (paragraph.length) { output.push(`<p>${inlineMarkdown(paragraph.join(" "))}</p>`); paragraph = []; } };
    const flushList = () => { if (list) { output.push(`<${list.type}>${list.items.map((item) => `<li>${inlineMarkdown(item)}</li>`).join("")}</${list.type}>`); list = null; } };
    const flushCode = () => { if (inCode) { output.push(`<pre><code>${escapeHtml(code.join("\n"))}</code></pre>`); code = []; inCode = false; } };
    for (const line of lines) {
      if (/^\s*```/.test(line)) { if (inCode) flushCode(); else { flushParagraph(); flushList(); inCode = true; } continue; }
      if (inCode) { code.push(line); continue; }
      const heading = line.match(/^\s*(#{1,3})\s+(.+?)\s*#*$/);
      const unordered = line.match(/^\s*[-*+]\s+(.+)/);
      const ordered = line.match(/^\s*\d+[.)]\s+(.+)/);
      if (heading) { flushParagraph(); flushList(); output.push(`<h${heading[1].length}>${inlineMarkdown(heading[2])}</h${heading[1].length}>`); continue; }
      if (unordered || ordered) { flushParagraph(); const nextType = unordered ? "ul" : "ol"; if (!list || list.type !== nextType) { flushList(); list = { type: nextType, items: [] }; } list.items.push((unordered || ordered)[1]); continue; }
      if (/^\s*>/.test(line)) { flushParagraph(); flushList(); output.push(`<blockquote>${inlineMarkdown(line.replace(/^\s*>\s?/, ""))}</blockquote>`); continue; }
      if (!line.trim()) { flushParagraph(); flushList(); continue; }
      paragraph.push(line.trim());
    }
    flushCode(); flushParagraph(); flushList();
    return output.join("") || "<p><br></p>";
  }

  function inlineMarkdown(value) {
    let text = escapeHtml(value);
    const stash = [];
    const save = (html) => { stash.push(html); return `\u0000${stash.length - 1}\u0000`; };
    text = text.replace(/`([^`]+)`/g, (_, value) => save(`<code>${value}</code>`));
    text = text.replace(/!?\[([^\]]+)\]\(([^\s)]+)\)/g, (_, label, url) => save(`<a href="${url.replace(/"/g, "&quot;")}" target="_blank">${label}</a>`));
    text = text.replace(/\*\*(.+?)\*\*|__(.+?)__/g, (_, a, b) => `<strong>${a || b}</strong>`);
    text = text.replace(/~~(.+?)~~/g, "<s>$1</s>");
    text = text.replace(/\*([^*]+)\*|_([^_]+)_/g, (_, a, b) => `<em>${a || b}</em>`);
    text = text.replace(/\u0000(\d+)\u0000/g, (_, index) => stash[Number(index)]);
    return text;
  }

  function htmlToMarkdown(root) {
    const walk = (node, context = {}) => {
      if (node.nodeType === Node.TEXT_NODE) return node.nodeValue || "";
      if (node.nodeType !== Node.ELEMENT_NODE) return "";
      const tag = node.tagName.toLowerCase();
      if (tag === "br") return "\n";
      const inner = Array.from(node.childNodes).map((child) => walk(child, { ...context, parent: tag })).join("");
      if (tag === "strong" || tag === "b") return `**${inner}**`;
      if (tag === "em" || tag === "i") return `*${inner}*`;
      if (tag === "u") return `<u>${inner}</u>`;
      if (tag === "s" || tag === "del" || tag === "strike") return `~~${inner}~~`;
      if (tag === "code") return `\`${inner}\``;
      if (tag === "a") return `[${inner}](${node.getAttribute("href") || ""})`;
      if (tag === "li") return inner;
      if (tag === "ul" || tag === "ol") return Array.from(node.children).map((item, index) => `${tag === "ul" ? "-" : `${index + 1}.`} ${walk(item, { list: true })}`).join("\n") + "\n\n";
      if (tag === "blockquote") return inner.trim().split("\n").map((line) => `> ${line}`).join("\n") + "\n\n";
      if (tag === "pre") return `\n\`\`\`\n${node.textContent || ""}\n\`\`\`\n\n`;
      if (/^h[1-3]$/.test(tag)) return `${"#".repeat(Number(tag[1]))} ${inner.trim()}\n\n`;
      if (tag === "p" || tag === "div") return `${inner.trim()}\n\n`;
      return inner;
    };
    return walk(root).replace(/\n{3,}/g, "\n\n").trim() + "\n";
  }

  if (view === "main") {
    app.innerHTML = "<div class=\"main-shell\"></div>";
    StickyMark.post("main-ready");
  } else {
    app.innerHTML = "<div class=\"note-shell\"><div style=\"padding:30px\">正在打开便签……</div></div>";
    StickyMark.post("note-ready");
  }
})();
