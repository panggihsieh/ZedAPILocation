# Zed API Configuration Flow

[English](#english) · [中文](#中文)

This document describes the **full lifecycle of an LLM API key in the Zed editor** — from signing up with the provider to storing the key locally to making your first request.

---

<a id="english"></a>

## English

### Overview

```
Provider Web       Windows Machine              LLM Provider API
─────────────      ─────────────────────        ──────────────────
1. Sign up          ─                                ─
2. Add payment
3. Create Key  ── sk-xxx ──► 4. Store on machine
                              ├─ 4a. Credential Manager (DPAPI)
                              └─ 4b. Environment variable (plaintext)
                                       │
                                       ▼
                              5. Zed loads key ── HTTPS ──► 6. LLM
```

### Step 1 — Sign up with the provider

- Open the provider's website (e.g. `platform.deepseek.com`, `platform.openai.com`, `console.anthropic.com`).
- Create an account with email / phone.
- Verify your email / phone when prompted.

### Step 2 — Add a payment method (paid APIs only)

- Go to the **Billing** page.
- Top up credits or link a credit card.
- Many providers give a small free tier on signup — enough for light testing without any card.

### Step 3 — Create an API Key

- Open the **API Keys** page on the provider's dashboard.
- Click **Create new key** (or the equivalent).
- Set **permissions** (read / write / admin) and **rate limits** if the provider supports per-key policies.
- Give the key a descriptive name (e.g. `zed-laptop`) so you can identify it later.
- **Copy the full key string** (usually starts with `sk-…`). The provider shows it only once — if you close the dialog you must create a new one.

> ⚠️ Treat the key like a password. Don't paste it into chats, screenshots, or git repos.

### Step 4 — Store the key on your machine

#### 4a. Credential Manager (recommended — secure)

Zed's **LLM Provider** UI stores keys in **Windows Credential Manager**, encrypted with **DPAPI** (Data Protection API, keyed to your Windows user account).

1. In Zed: `Ctrl+Shift+P` → **LLM: Configure Providers**.
2. Pick the provider you want (e.g. `OpenAI`).
3. Paste the `sk-…` key into the **API Key** field.
4. Click **Save**.

Zed then writes an entry into Windows Credential Manager. Verify with:

```cmd
cmdkey /list | findstr /I "deepseek openai anthropic"
```

The target name will look like `zed:provider=openai` (exact name varies by version).

#### 4b. Environment variable (easier — less secure)

```cmd
setx DEEPSEEK_API_KEY "sk-xxxxxxxxxxxx"
```

Close and reopen Zed so it picks up the new environment variable.

> ⚠️ The key is stored in **plaintext** and is visible to every process running under your user account. Prefer Credential Manager for long-lived keys.

### Step 5 — Use the key

When you ask Zed to use a model, Zed:

1. Looks up the matching provider in its config.
2. Reads the API key from **Credential Manager** (or the environment variable as a fallback).
3. Adds an `Authorization: Bearer sk-…` header to the HTTPS request.
4. Sends the request to the provider's API endpoint.
5. Streams the response back into the editor.

You never see the key in Zed's UI — only the request / response.

### Step 6 — Monitor and rotate

- Periodically open the provider's **Usage** dashboard to see token consumption.
- If you suspect a key has leaked, **revoke it immediately** and create a new one.
- Enable **2FA / IP allow-lists** on the provider dashboard for important accounts.
- This tool (`ZedApiDig`) can be re-run any time to confirm which providers still have keys configured.

---
### How `ZedApiDig` audits your machine

This tool does **not** modify anything. It scans three locations:

| Source | What it reads | What it shows |
|---|---|---|
| **Windows Credential Manager** (`cmdkey /list`) | All `Target=` lines | Credential targets matching LLM providers or ACP agents |
| **Environment variables** | All `Process.GetEnvironmentVariables()` | Variables whose names end in `_API_KEY`, `_TOKEN`, etc. |
| **Local config directories** (`~/.claude`, `~/.codex`, `~/.gemini`, …) | Directory presence | Which ACP agent configs exist on disk |

Each finding is labelled with a **Status**:

- `[LOCAL]` — found in a local config directory
- `[ENV]` — found in an environment variable
- `[CRED]` — found in Credential Manager (only the target name, never the secret)

The **API Lifecycle** row in the Findings tab is a built-in tutorial that summarises this whole document.

### Security notes

- The tool **never prints** any key, token, or password — only the **target name** that `cmdkey /list` returns.
- Scanning is **read-only**. No credentials are created, modified, or deleted.
- The DPAPI-encrypted credentials in Credential Manager can only be decrypted by code running **as your Windows user account** — another user on the same machine cannot read them, and they are not backed up by Windows.

---

<a id="中文"></a>

## 中文

### 概觀

```
Provider 官網       Windows 電腦                  LLM Provider API
─────────────      ─────────────────────        ──────────────────
1. 註冊帳號          ─                                ─
2. 儲值/綁定付款方式
3. 建立 Key     ── sk-xxx ──► 4. 存到本機
                            ├─ 4a. 認證管理員 (DPAPI 加密)
                            └─ 4b. 環境變數 (明文)
                                       │
                                       ▼
                              5. Zed 載入金鑰 ── HTTPS ──► 6. LLM
```

### Step 1 — 在 Provider 官網註冊

- 打開 Provider 官網（例如 `platform.deepseek.com`、`platform.openai.com`、`console.anthropic.com`）。
- 用 Email / 手機建立帳號。
- 完成 Email / 手機驗證。

### Step 2 — 加值 / 綁定付款方式（付費 API 才需要）

- 進入 **Billing** 頁面。
- 加值儲值金 或 綁定信用卡。
- 許多 Provider 在註冊時會送少量免費額度，輕量測試不需要先付款。

### Step 3 — 建立 API Key

- 進入 Provider 後台的 **API Keys** 頁面。
- 按 **Create new key**（或對應按鈕）。
- 設定 **權限**（read / write / admin）和 **rate limit**（如果支援）。
- 給 Key 取個好辨識的名稱（例如 `zed-laptop`），方便之後管理。
- **複製完整的 key 字串**（通常以 `sk-…` 開頭）。Provider 為了安全**只會顯示一次**，關閉對話框就看不到，必須重新建立。

> ⚠️ 請把 API key 當密碼對待。不要貼到聊天室、截圖、或 git repo。

---
### Step 4 — 把 Key 存到本機電腦

兩種方式：

#### 4a. 認證管理員（推薦 — 安全）

Zed 的 **LLM Provider** UI 會把 key 存到 **Windows 認證管理員**，用 **DPAPI**（Data Protection API，以你的 Windows 帳號加密）加密。

1. Zed 裡：`Ctrl+Shift+P` → **LLM: Configure Providers**。
2. 選你要設定的 provider（例如 `OpenAI`）。
3. 把 `sk-…` 貼到 **API Key** 欄位。
4. 按 **Save**。

Zed 接著會在 Windows 認證管理員新增一筆紀錄。可以用以下指令驗證：

```cmd
cmdkey /list | findstr /I "deepseek openai anthropic"
```

Target 名稱看起來像 `zed:provider=openai`（視版本而異）。

#### 4b. 環境變數（簡單 — 較不安全）

如果只是臨時測試、或是想在多個工具共用 key：

```cmd
setx DEEPSEEK_API_KEY "sk-xxxxxxxxxxxx"
```

關閉再重開 Zed 才會讀到新環境變數。

> ⚠️ 環境變數是**明文儲存**，同帳號下任何 process 都看得到。長期使用的 key 建議還是用認證管理員。

### Step 5 — 開始使用

當你在 Zed 切換到某個 model（例如 `claude-3-5-sonnet`）開始對話時，Zed 會：

1. 從設定檔找到對應的 provider。
2. 從**認證管理員**讀取 API key（或 fallback 到環境變數）。
3. 在 HTTPS 請求加上 `Authorization: Bearer sk-…` 標頭。
4. 把請求送到 Provider 的 API 端點（`https://api.deepseek.com/…` 等）。
5. 把回應串流回編輯器。

你在 Zed UI 上**完全不會看到 key**，只會看到請求和回應。

### Step 6 — 監控與輪替

- 定期到 Provider 後台的 **Usage** 頁面看 token 用量。
- 如果懷疑 key 外洩（不小心 commit、貼到公開截圖等），**立刻撤銷**並建新的。
- 在 Provider 後台對重要帳號開 **2FA / IP allow-list**。
- 隨時可以重跑這個工具（`ZedApiDig`）確認目前機器上還有哪些 provider 設定了 key。

### `ZedApiDig` 怎麼稽核你的電腦

這個工具**不會修改任何東西**。它掃描三個位置：

| 來源 | 讀取什麼 | 顯示什麼 |
|---|---|---|
| **Windows 認證管理員**（`cmdkey /list`） | 所有 `Target=` 列 | 名稱符合 LLM provider / ACP agent 的項目 |
| **環境變數** | 所有 `Process.GetEnvironmentVariables()` | 名稱結尾 `_API_KEY`、`_TOKEN` 等的變數 |
| **本機設定目錄**（`~/.claude`、`~/.codex`、`~/.gemini` 等） | 目錄存在與否 | 哪些 ACP agent 的設定檔在磁碟上 |

每一筆紀錄會標上 **Status**：

- `[LOCAL]` — 找到本機設定目錄
- `[ENV]` — 找到環境變數
- `[CRED]` — 找到認證管理員（只顯示名稱，不顯示密鑰）

Findings tab 裡的 **API Lifecycle** 是本工具內建的教學列，把這份文件濃縮成一張總覽。

### 安全性備註

- 工具**絕不會印出**任何 key / token / 密碼，只會顯示 `cmdkey /list` 回傳的**目標名稱**。
- 掃描是**唯讀**的。不會新增、修改、刪除任何認證資料。
- 認證管理員裡 DPAPI 加密的紀錄只能由**同一個 Windows 使用者帳號**執行的程式解密，別的帳號拿不到，也不會被 Windows 自動備份。
