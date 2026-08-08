# v1.3 — Fix Credential Manager detection on zh-TW Windows

[English](#english) · [中文](#中文)

---

<a id="english"></a>

## English

### 🐛 Bug fix in v1.3

On zh-TW (or any non-UTF-8) Windows, **all Credential Manager findings were silently dropped** — DeepSeek, OpenAI, Anthropic, Google Gemini, MiniMax, the Zed Account entry, etc. — even when `cmdkey /list` clearly showed them.

#### Root cause

`Program.cs::RunCmdkey()` reads `cmdkey /list` output as **Big5 (codepage 950)** — the legacy encoding that `cmdkey` emits on Traditional Chinese Windows.

```csharp
return Encoding.GetEncoding(950).GetString(bytes);   // throws on .NET 8!
```

In **.NET 8**, codepage encodings are **opt-in**. The first call to `Encoding.GetEncoding(950)` throws `NotSupportedException` because the `CodePagesEncodingProvider` hasn't been registered.

That exception was caught by the surrounding `try { ... } catch { return ""; }`, so `RunCmdkey()` returned an empty string. Every `CheckProvider(...)` call then saw `cmdkeyOutput == ""` and skipped the Credential Manager scan entirely.

#### Why environment variables still appeared

`Environment.GetEnvironmentVariable(...)` doesn't go through `Encoding`, so `[ENV]` findings (e.g. `MINIMAX_API_KEY`, `CODEX_API_KEY`) continued to work. That made the bug look like "LLM Providers in Credential Manager simply aren't configured" — but actually they were.

#### Fix

One line added to `Main()`:

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
```

The `System.Text.Encoding.CodePages` assembly is already bundled with the .NET 8 runtime, so no extra dependency is needed.

#### Verification (11 findings now appear)

| Status | Category | Name | Location |
|---|---|---|---|
| `[LOCAL]` | ACP Agent | Claude / Claude Code | `C:\Users\hsieh\.claude` |
| `[LOCAL]` | ACP Agent | Cline | `…\saoudrizwan.claude-dev` |
| `[LOCAL]` | ACP Agent | Codex | `C:\Users\hsieh\.codex` |
| `[LOCAL]` | ACP Agent | Gemini CLI | `C:\Users\hsieh\.gemini` |
| `[LOCAL]` | ACP Agent | OpenCode | `C:\Users\hsieh\.config\opencode` |
| `[LOCAL]` | LLM Provider | **DeepSeek** | `zed:url=https://api.deepseek.com/v1` |
| `[LOCAL]` | LLM Provider | **Google Gemini** | `zed:url=https://generativelanguage.googleapis.com` |
| `[LOCAL]` | LLM Provider | **MiniMax** | `zed:url=https://api.minimax.io/v1` (×2) |
| `[LOCAL]` | Zed | **Zed Account** | `zed:url=https://zed.dev` |
| `[ENV]` | ACP Agent | Codex | `CODEX_API_KEY` |
| `[ENV]` | LLM Provider | MiniMax | `MINIMAX_API_KEY` |

---

### 📥 Download

Grab the updated self-contained executable from this release:

- `ZedCredentialAuditGui.exe` (~175 MB) — Windows 10 1607+ x64, no .NET runtime install required.

### Learn the Zed API flow

See [`ZedApiDig/ZED_API_SETUP.md`](ZedApiDig/ZED_API_SETUP.md) for the full key lifecycle guide (English + 中文), including a **DPAPI vs OS Keychain** security comparison.

---

<a id="中文"></a>

## 中文

### 🐛 v1.3 修正

在 zh-TW（或任何非 UTF-8）的 Windows 上，**所有 Credential Manager 偵測結果會被靜默丟棄** — DeepSeek、OpenAI、Anthropic、Google Gemini、MiniMax、Zed 帳號⋯⋯，即使 `cmdkey /list` 明明列出來了。

#### 根本原因

`Program.cs::RunCmdkey()` 用 **Big5（codepage 950）** 解碼 `cmdkey /list` 的輸出 — 因為 `cmdkey` 在繁體中文 Windows 預設用 Big5 編碼。

```csharp
return Encoding.GetEncoding(950).GetString(bytes);   // 在 .NET 8 會拋例外！
```

**在 .NET 8 裡，codepage 編碼是 opt-in 的**。第一次呼叫 `Encoding.GetEncoding(950)` 會拋 `NotSupportedException`，因為 `CodePagesEncodingProvider` 沒註冊。

外層 `try { ... } catch { return ""; }` 把這個例外吞掉，導致 `RunCmdkey()` 回傳空字串。所有 `CheckProvider(...)` 看到 `cmdkeyOutput == ""`，就跳過 Credential Manager 整個掃描。

#### 為何環境變數還能顯示

`Environment.GetEnvironmentVariable(...)` 不走 Encoding，所以 `[ENV]` 結果（`MINIMAX_API_KEY`、`CODEX_API_KEY` 等）依然正常。這讓 bug 看起來像是「LLM Provider 沒設 Credential Manager」，其實是偵測邏輯壞了。

#### 修正

在 `Main()` 加一行：

```csharp
Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
```

`System.Text.Encoding.CodePages` 已經隨 .NET 8 runtime 內建，不需額外依賴。

#### 驗證（修正後出現 11 筆紀錄）

| Status | Category | Name | Location |
|---|---|---|---|
| `[LOCAL]` | ACP Agent | Claude / Claude Code | `C:\Users\hsieh\.claude` |
| `[LOCAL]` | ACP Agent | Cline | `…\saoudrizwan.claude-dev` |
| `[LOCAL]` | ACP Agent | Codex | `C:\Users\hsieh\.codex` |
| `[LOCAL]` | ACP Agent | Gemini CLI | `C:\Users\hsieh\.gemini` |
| `[LOCAL]` | ACP Agent | OpenCode | `C:\Users\hsieh\.config\opencode` |
| `[LOCAL]` | LLM Provider | **DeepSeek** | `zed:url=https://api.deepseek.com/v1` |
| `[LOCAL]` | LLM Provider | **Google Gemini** | `zed:url=https://generativelanguage.googleapis.com` |
| `[LOCAL]` | LLM Provider | **MiniMax** | `zed:url=https://api.minimax.io/v1`（×2） |
| `[LOCAL]` | Zed | **Zed Account** | `zed:url=https://zed.dev` |
| `[ENV]` | ACP Agent | Codex | `CODEX_API_KEY` |
| `[ENV]` | LLM Provider | MiniMax | `MINIMAX_API_KEY` |

---

### 📥 下載

從本 release 取得更新後的 self-contained 執行檔：

- `ZedCredentialAuditGui.exe`（約 175 MB）— Windows 10 1607+ x64，免裝 .NET runtime。

### 認識 Zed API 配置流程

詳見 [`ZedApiDig/ZED_API_SETUP.md`](ZedApiDig/ZED_API_SETUP.md) — 中英對照完整生命週期指南，含 **DPAPI vs OS Keychain 安全性比較**。

---

### 🛠 給開發者

如果你的 Windows 是非 UTF-8 codepage（例如 zh-TW Big5、ja-JP Shift-JIS、ko-KR），這個 bug 也會影響你。升級到 v1.3 之後 `cmdkey /list` 解碼正常，所有 Credential Manager 紀錄都會出現。
