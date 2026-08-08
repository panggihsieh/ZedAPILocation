# v1.2 — Zed API 配置流程說明 + DPAPI vs OS Keychain 安全性比較

[English](#english) · [中文](#中文)

---

<a id="english"></a>

## English

### 🎯 What this project is about

**The main goal of this program is to help you understand how the Zed editor's LLM API keys are configured and processed end-to-end** — from signing up with the provider, through storing the key on your Windows machine, to sending authenticated HTTPS requests to the LLM API endpoint.

It is **not** a credential manager, not a secrets vault, and not a key injector. It is a **read-only inspector** that shows you:

1. Which LLM providers have keys configured on your machine.
2. Where each key lives — Windows Credential Manager, an environment variable, or a local ACP agent config directory.
3. How Zed reads that key on startup and attaches it to outbound requests.

If you have ever wondered *"where exactly does Zed put my DeepSeek key, and how does it use it?"*, this tool answers that question.

### 📚 What's new in v1.2

- **`ZED_API_SETUP.md`** — a new bilingual guide that walks through the **full Zed API configuration lifecycle**, step by step:
  1. Sign up with the provider
  2. Add a payment method
  3. Create an API key on the provider dashboard
  4. Store the key on your machine (Credential Manager **or** environment variable)
  5. How Zed loads the key and uses it
  6. Monitor usage and rotate keys
- **Bilingual (English + 中文)** — both languages are written out in full so you can read whichever is more comfortable.
- **`ZedApiDig/`** (unchanged from v1.1) — the minimal single-file WinForms project.
- **Self-contained single-file exe** (~175 MB) — runs on any Windows 10 1607+ x64 box without a .NET install.

---
### 🔐 DPAPI vs OS Keychain — security comparison

Windows offers several ways to store a secret locally. The two you are most likely to meet are **DPAPI** (used by Windows Credential Manager) and the **OS keychain** concept (Keychain on macOS, Secret Service / KWallet on Linux, Credential Manager on Windows is the equivalent). Below is a side-by-side comparison.

| Dimension | **DPAPI** (Windows Credential Manager) | **OS Keychain** (macOS Keychain / Linux Secret Service) |
|---|---|---|
| **Scope** | Per Windows user account, machine-local. | Per OS user account, machine-local. |
| **Encryption key** | Derived from the user's password (DPAPI master key, cached in `C:\Users\<u>\AppData\Roaming\Microsoft\Protect\<SID>\`). | Derived from the user's login password (Keychain) or unlocked by the active desktop session (Secret Service). |
| **Who can decrypt** | Code running as the **same Windows user**, on the **same machine**, without re-entering a password. Other users / other machines **cannot** decrypt. | Code running as the **same OS user**, on the **same machine**. Other users / other machines **cannot** decrypt. |
| **Backups** | Not included in standard Windows backups. If the user profile is lost (or the password is reset), the DPAPI master key is regenerated and old credentials become **unrecoverable**. | macOS Keychain can be backed up via iCloud Keychain (end-to-end encrypted with the iCloud Security Code). Linux Secret Service is **not** backed up by default. |
| **Mobile / cross-device sync** | ❌ No. DPAPI is per-machine. | ✅ Available on macOS / iOS via iCloud Keychain, and on some Linux desktops via the freedesktop Secret Service. |
| **Typical API surface** | `CredWrite` / `CredRead` Win32 APIs; .NET's `CredentialManagement` or `PasswordVault`. | macOS: Security framework (`SecKeychainAddGenericPassword`). Linux: `libsecret` via D-Bus. |
| **Threat: another local user** | ❌ Cannot read (different user = different DPAPI master key). | ❌ Cannot read (per-user sandboxing). |
| **Threat: malware running as your user** | ⚠️ Can read. There is **no master password prompt** for DPAPI; once your Windows session is unlocked, any process can call `CredRead`. | ⚠️ Mostly can read. macOS Keychain will prompt the user the first time an app reads a credential, and the user can deny it. Linux Secret Service prompts are distro-dependent. |
| **Threat: stolen disk** | The DPAPI master key is wrapped with the user's NT password hash. Offline brute force is possible if the password is weak. | Same — the Keychain master key is also wrapped with the user's login password. |
| **Threat: cloud backup leak** | ✅ Safe (DPAPI keys are not backed up). | ⚠️ Depends — iCloud Keychain is E2E-encrypted with the user's Security Code, but if the user forgets the code the data is unrecoverable. |

#### TL;DR

- **DPAPI and OS Keychains offer comparable security for the same threat model**: per-user, per-machine, decryptable only by code running as that user. Neither protects against malware running **as** you.
- The **practical difference** is mobility: macOS Keychain / iCloud Keychain can sync across devices; DPAPI cannot.
- For a developer laptop running Zed on Windows, **DPAPI-backed Credential Manager is the right choice** — it is the default storage that Zed's LLM Provider UI uses, and it is already audited by Microsoft.

---
### 📂 What you get in this release

```
ZedApiDig/
├── Program.cs                   (single-file WinForms source, ~1100 lines)
├── ZedCredentialAuditGui.csproj (.NET 8 WinForms, self-contained publish)
├── build_gui.bat                (builds the exe)
├── README.md
├── ZED_API_SETUP.md             ← new in v1.2: bilingual lifecycle guide
├── Assets/app.ico               (Windows .ico for the exe)
└── Resources/
    ├── logo.svg                 (Font Awesome 7 key — Form.Icon)
    ├── tw.svg                   (Taiwan flag — language toggle)
    └── us.svg                   (US flag — language toggle)
```

### Build

```cmd
cd ZedApiDig
build_gui.bat
```

Output: `ZedApiDig\publish\ZedCredentialAuditGui.exe` (~175 MB, fully self-contained).

### Security

- Read-only — never modifies credentials.
- Never prints any key, token, or password — only the **target name** returned by `cmdkey /list`.
- DPAPI-protected credentials are decrypted only inside the Zed process; this tool never accesses them.

### Acknowledgements

- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) + [SkiaSharp](https://github.com/mono/SkiaSharp) for SVG rendering.
- App icon: Font Awesome 7 Free (CC BY 4.0).
- Flag icons: [flag-icons](https://github.com/lipis/flag-icons).

---

<a id="中文"></a>

## 中文

### 🎯 這個專案在做什麼

**本程式的主要目的，是幫助你認識 Zed 編輯器的 LLM API key 是怎麼配置的、處理流程是什麼** — 從在 Provider 官網註冊、到把 key 存到 Windows 電腦、再到送出帶有認證的 HTTPS 請求到 LLM API 端點。

它**不是**密碼管理器、不是 secrets vault、也不是 key injector。它是一個**唯讀的檢查工具**，會告訴你：

1. 你的機器上有哪些 LLM provider 已經設定好 key。
2. 每個 key 放在哪裡 — Windows 認證管理員、環境變數、還是 ACP agent 的本機設定目錄。
3. Zed 在啟動時怎麼讀取 key 並加到外送請求上。

如果你曾經想過「Zed 到底把我的 DeepSeek key 放在哪裡？它怎麼用的？」，這個工具就是答案。

---
### 📚 v1.2 新增內容

- **`ZED_API_SETUP.md`** — 一份新的中英對照指南，**逐步**走完 Zed API 配置的完整生命週期：
  1. 在 Provider 官網註冊
  2. 加值 / 綁定付款方式
  3. 在 Provider 後台建立 API key
  4. 把 key 存到本機（認證管理員 **或** 環境變數）
  5. Zed 怎麼載入金鑰並使用
  6. 監控用量、輪替 key
- **中英對照** — 兩種語言都完整撰寫，閱讀哪個都順。
- **`ZedApiDig/`**（與 v1.1 相同）— 精簡的 single-file WinForms 專案。
- **Self-contained single-file exe**（約 175 MB）— 可在任何 Windows 10 1607+ x64 機器執行，無需安裝 .NET runtime。

---

### 🔐 DPAPI vs OS Keychain — 安全性比較

Windows 提供好幾種在本機儲存秘密的方式。最常見的兩種是 **DPAPI**（Windows 認證管理員背後用的）跟 **OS keychain** 概念（macOS 是 Keychain、Linux 是 Secret Service / KWallet）。下表是兩者並排比較。

| 面向 | **DPAPI**（Windows 認證管理員） | **OS Keychain**（macOS Keychain / Linux Secret Service） |
|---|---|---|
| **範圍** | 單一 Windows 使用者帳號、本機。 | 單一 OS 使用者帳號、本機。 |
| **加密金鑰** | 由使用者密碼衍生（DPAPI master key，cache 在 `C:\Users\<u>\AppData\Roaming\Microsoft\Protect\<SID>\`）。 | 由使用者登入密碼衍生（Keychain），或由目前桌面 session 解鎖（Secret Service）。 |
| **誰能解密** | 在**同一台機器**以**同一個 Windows 帳號**執行的程式，不需要再輸入密碼。別的使用者、別的機器**無法**解密。 | 在**同一台機器**以**同一個 OS 帳號**執行的程式。別的使用者、別的機器**無法**解密。 |
| **備份** | 不包含在標準 Windows 備份中。如果使用者 profile 遺失（或密碼被重設），DPAPI master key 會重新生成，舊的認證變成**無法還原**。 | macOS Keychain 可透過 iCloud Keychain 備份（以 iCloud Security Code 端對端加密）。Linux Secret Service 預設**不備份**。 |
| **行動 / 跨裝置同步** | ❌ 不行，DPAPI 是 per-machine。 | ✅ macOS / iOS 透過 iCloud Keychain 可同步；Linux 部分桌面環境透過 freedesktop Secret Service 支援。 |
| **API 介面** | Win32 `CredWrite` / `CredRead`；.NET 的 `CredentialManagement` 或 `PasswordVault`。 | macOS：Security framework（`SecKeychainAddGenericPassword`）。Linux：透過 D-Bus 的 `libsecret`。 |
| **威脅：別的本機使用者** | ❌ 讀不到（不同使用者 = 不同的 DPAPI master key）。 | ❌ 讀不到（per-user 沙箱）。 |
| **威脅：以你身份執行的惡意程式** | ⚠️ 讀得到。DPAPI **不會**彈出主密碼提示；Windows session 一旦解鎖，任何 process 都能呼叫 `CredRead`。 | ⚠️ 大多讀得到。macOS Keychain 在 app 第一次讀認證時會提示使用者，使用者可拒絕。Linux Secret Service 提示則視 distro 而定。 |
| **威脅：磁碟被偷** | DPAPI master key 用使用者 NT 密碼 hash 包起來，密碼太弱時可離線暴力破解。 | 一樣，Keychain master key 也是用登入密碼包起來。 |
| **威脅：雲端備份外洩** | ✅ 安全（DPAPI 金鑰不會被備份）。 | ⚠️ 看情境，iCloud Keychain 是用 Security Code 端對端加密，但使用者忘記 Security Code 時資料也救不回來。 |

#### 一句話總結

- **DPAPI 和 OS Keychain 在同樣的威脅模型下提供差不多強度的保護**：per-user、per-machine，只能由以該使用者身份執行的程式解密。**兩者都擋不住以你身份執行的惡意程式**。
- 真正的差異在**行動性**：macOS Keychain / iCloud Keychain 可以跨裝置同步；DPAPI 不行。
- 在 Windows 上跑 Zed 開發機，**DPAPI 支援的認證管理員是正解** — 這也是 Zed LLM Provider UI 的預設儲存方式，且經過 Microsoft 稽核。

---

### 📂 本次 release 包含的檔案

```
ZedApiDig/
├── Program.cs                   (單檔 WinForms 原始碼，約 1100 行)
├── ZedCredentialAuditGui.csproj (.NET 8 WinForms，self-contained publish)
├── build_gui.bat                (建構 exe)
├── README.md
├── ZED_API_SETUP.md             ← v1.2 新增：中英對照生命週期指南
├── Assets/app.ico               (Windows .ico，用於 exe 圖示)
└── Resources/
    ├── logo.svg                 (Font Awesome 7 key — Form.Icon)
    ├── tw.svg                   (台灣國旗 — 語言切換)
    └── us.svg                   (美國國旗 — 語言切換)
```

### 建構

```cmd
cd ZedApiDig
build_gui.bat
```

產出：`ZedApiDig\publish\ZedCredentialAuditGui.exe`（約 175 MB，完整自含 runtime）。

### 安全性

- 唯讀 — 不會修改任何認證資料。
- 絕不印出任何 key / token / 密碼，只顯示 `cmdkey /list` 回傳的**目標名稱**。
- DPAPI 加密的認證只會在 Zed 程序內解密；本工具不會存取它們。

### 致謝

- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) + [SkiaSharp](https://github.com/mono/SkiaSharp) — SVG 渲染。
- App icon：Font Awesome 7 Free（CC BY 4.0）。
- 國旗 icon：[flag-icons](https://github.com/lipis/flag-icons)。
