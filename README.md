# Zed API Location Analysis (GUI)

[English](#english) · [中文](#中文)

---

<a id="english"></a>

## English

### What this is

A **Windows GUI tool** that shows you **how the Zed editor's LLM API keys are configured and processed** on your machine — where each provider's key lives (Windows Credential Manager, environment variables, or local ACP agent config directories) and how Zed loads and uses it.

> Read-only: it never modifies credentials and never prints any key or token — only the credential TARGET names.

### Download

Grab the **self-contained executable** from the latest [GitHub Release](https://github.com/panggilseh/ZedAPILocation/releases):

- `ZedCredentialAuditGui.exe` (~175 MB) — runs on Windows 10 1607+ x64, **no .NET runtime required**.

### What it audits

1. **LLM Providers** (12+): DeepSeek, OpenAI, Anthropic, Google Gemini, MiniMax, Mistral, xAI, OpenRouter, OpenCode, Vercel AI Gateway, Ollama, LM Studio — Credential Manager + environment variables.
2. **Zed Account** — `zed:url=https://zed.dev` in Credential Manager.
3. **Zed Internal Providers** — keys set via the Zed LLM Providers UI (stored encrypted with DPAPI).
4. **ACP External Agents** — Codex, Claude, Cline, Gemini CLI, OpenCode, Pi — local config dirs + env vars.

### Features

- Three tabs: **Findings** (sortable grid) / **Env Vars** / **Credential Targets**.
- Bottom description panel explaining the selected row in English or Chinese.
- Top-right language toggle (中文 / English) with SVG flag icons (TW / US).
- Export findings to CSV.
- App icon rendered from an embedded SVG.

### Build from source

Requirements: .NET 8 SDK (Windows).

```cmd
cd ZedApiDig
build_gui.bat
```

Output: `ZedApiDig\publish\ZedCredentialAuditGui.exe`.

### Learn the Zed API flow

See [`ZedApiDig/ZED_API_SETUP.md`](ZedApiDig/ZED_API_SETUP.md) — a bilingual walkthrough of the full key lifecycle (sign up → create key → store → use → rotate), including a **DPAPI vs OS Keychain** security comparison.

### Security

- Read-only — never modifies credentials.
- Never prints keys/tokens — only target names from `cmdkey /list`.

---

<a id="中文"></a>

## 中文

### 這是什麼

一套 **Windows GUI 工具**，用來認識 **Zed 編輯器的 LLM API key 是怎麼配置與處理的** — 每個 provider 的 key 存在哪（Windows 認證管理員、環境變數、或 ACP agent 本機設定目錄）、Zed 如何載入並使用。

> 唯讀工具：不會修改任何認證資料，也不會印出任何 key / token，只顯示認證「目標名稱」。

### 下載

請從最新的 [GitHub Release](https://github.com/panggilseh/ZedAPILocation/releases) 取得 **self-contained 可執行檔**：

- `ZedCredentialAuditGui.exe`（約 175 MB）— 可在 Windows 10 1607+ x64 直接執行，**不需安裝 .NET runtime**。

### 稽核內容

1. **LLM Providers**（12+）：DeepSeek、OpenAI、Anthropic、Google Gemini、MiniMax、Mistral、xAI、OpenRouter、OpenCode、Vercel AI Gateway、Ollama、LM Studio — 認證管理員 + 環境變數。
2. **Zed 帳號** — 認證管理員中的 `zed:url=https://zed.dev`。
3. **Zed 內部 Providers** — 透過 Zed LLM Provider UI 設定的 key（以 DPAPI 加密儲存）。
4. **ACP 外部 Agents** — Codex、Claude、Cline、Gemini CLI、OpenCode、Pi — 本機設定目錄 + 環境變數。

### 功能

- 三個分頁：**Findings**（可排序表格）/ **Env Vars** / **Credential Targets**。
- 下方說明面板，點選列即顯示中 / 英文解釋。
- 右上角語言切換（中文 / English），SVG 國旗圖示（台灣 / 美國）。
- 匯出 CSV。
- App icon 由內嵌 SVG 渲染。

### 從原始碼建置

需求：.NET 8 SDK（Windows）。

```cmd
cd ZedApiDig
build_gui.bat
```

產出：`ZedApiDig\publish\ZedCredentialAuditGui.exe`。

### 學習 Zed API 配置流程

詳見 [`ZedApiDig/ZED_API_SETUP.md`](ZedApiDig/ZED_API_SETUP.md) — 中英對照的完整 key 生命週期教學（註冊 → 建 key → 儲存 → 使用 → 輪替），並附 **DPAPI vs OS Keychain 安全性比較**。

### 安全性

- 唯讀 — 不會修改任何認證資料。
- 絕不印出 key / token — 只顯示 `cmdkey /list` 的目標名稱。
