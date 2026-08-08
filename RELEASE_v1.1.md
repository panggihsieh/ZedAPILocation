# v1.1 — ZedApiDig: Minimal Single-File GUI Repo

[English](#english) · [中文](#中文)

---

<a id="english"></a>

## English

### What's new in v1.1

- **`ZedApiDig/`** — extracted a clean, minimal subset of the project into its own directory so you can fork or publish just the GUI app without dragging in the C++ / PowerShell variants.
- **Self-contained single-file executable** — `publish\ZedCredentialAuditGui.exe` is a fully self-contained .NET 8 binary (~175 MB). No .NET runtime install is required on the target machine (Windows 10 1607+ x64).
- **SVG-rendered icons** — the window icon and the language-toggle flag button are now rendered at runtime from embedded SVGs via [Svg.Skia](https://www.nuget.org/packages/Svg.Skia):
  - 🔑 App icon — Font Awesome 7 key logo (`Resources/logo.svg`)
  - 🇹🇼 🇺🇸 TW / US flags for the language toggle (`Resources/tw.svg`, `Resources/us.svg`)
- **Standard icon size** — both bitmaps are rendered at 256×256, then drawn into the 40×40 toggle button or converted to an HICON for the Form.
- **No more white margins around the flag** — the flag button is now a clean square; the SVG aspect ratio is preserved by letterbox-fitting inside the square canvas (no stretch / no distortion).
- **Original `ZedCredentialAuditGui/` kept in place** — the C++ / PowerShell legacy versions and the working source tree are untouched at the repo root.

### Repository layout

```
Zed_API_Location/
├── ZedApiDig/                       ← minimal GUI project (this release)
│   ├── Program.cs                   (single-file WinForms source)
│   ├── ZedCredentialAuditGui.csproj (.NET 8 WinForms)
│   ├── build_gui.bat                (publish self-contained single-file exe)
│   ├── README.md
│   ├── Assets/app.ico               (Windows .ico for the executable)
│   └── Resources/                   (embedded SVG icons)
│       ├── logo.svg  tw.svg  us.svg
├── ZedCredentialAuditGui/           (full working tree, unchanged)
├── ZedCredentialAudit.cpp           (C++ CLI version)
├── ZedCredentialAudit.exe
├── Zed-LLM-Credential-Audit.ps1     (PowerShell version)
├── Zed-LLM-Credential-Audit-fixed.ps1
├── build.bat
└── README.md
```

### Build the GUI

Requirements: .NET 8 SDK (Windows).

```cmd
cd ZedApiDig
build_gui.bat
```

Output: `ZedApiDig\publish\ZedCredentialAuditGui.exe` — fully self-contained, ~175 MB.

### Security

- **Read-only** — the audit never modifies credentials.
- **No secrets displayed** — only credential TARGET names returned by `cmdkey /list`.

### Acknowledgements

- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) — SVG rendering.
- [SkiaSharp](https://github.com/mono/SkiaSharp) — rasterizer.
- App icon: Font Awesome 7 Free (CC BY 4.0).
- Flag icons: [flag-icons](https://github.com/lipis/flag-icons).

---

<a id="中文"></a>

## 中文

### v1.1 新增內容

- **`ZedApiDig/`** — 從原本的 working tree 抽出乾淨、最小的 GUI 專案目錄，方便只想 fork 或 release GUI 版本的使用者，不必連 C++ / PowerShell 版本一起打包。
- **Self-contained single-file 可執行檔** — `publish\ZedCredentialAuditGui.exe` 是一個完整自帶 .NET 8 runtime 的單檔 exe（約 175 MB），目標機器不需要預先安裝 .NET runtime（支援 Windows 10 1607+ x64）。
- **SVG 渲染圖示** — 視窗左上角 icon 與右上角語言切換旗幟按鈕，現在執行階段用 [Svg.Skia](https://www.nuget.org/packages/Svg.Skia) 從內嵌 SVG 即時渲染：
  - 🔑 App icon — Font Awesome 7 鑰匙 logo（`Resources/logo.svg`）
  - 🇹🇼 🇺🇸 台灣 / 美國國旗切換按鈕（`Resources/tw.svg`、`Resources/us.svg`）
- **標準 icon 大小** — 兩個 bitmap 都是 256×256 渲染，再依需要繪製到 40×40 旗幟按鈕，或透過 `GetHicon()` 變成 `Form.Icon`。
- **國旗按鈕不再有白邊** — 按鈕改為正方形；SVG 內部以 letterbox-fit 維持原始比例，按鈕本身完全貼齊，不再拉伸變形。
- **保留原始 `ZedCredentialAuditGui/`** — repo 根目錄的 C++ / PowerShell 舊版本與工作複本完全不動。

### Repo 結構

```
Zed_API_Location/
├── ZedApiDig/                       ← 本次 release 的精簡 GUI 專案
│   ├── Program.cs                   (單檔 WinForms 原始碼)
│   ├── ZedCredentialAuditGui.csproj (.NET 8 WinForms)
│   ├── build_gui.bat                (publish 自含 runtime 的單檔 exe)
│   ├── README.md
│   ├── Assets/app.ico               (Windows .ico，用於 exe 圖示)
│   └── Resources/                   (內嵌 SVG 圖示)
│       ├── logo.svg  tw.svg  us.svg
├── ZedCredentialAuditGui/           (完整工作複本，未更動)
├── ZedCredentialAudit.cpp           (C++ CLI 版本)
├── ZedCredentialAudit.exe
├── Zed-LLM-Credential-Audit.ps1     (PowerShell 版本)
├── Zed-LLM-Credential-Audit-fixed.ps1
├── build.bat
└── README.md
```

### 建置 GUI

需求：.NET 8 SDK（Windows）。

```cmd
cd ZedApiDig
build_gui.bat
```

產出：`ZedApiDig\publish\ZedCredentialAuditGui.exe` — 完整自含 runtime，約 175 MB。

### 安全性

- **唯讀** — 稽核過程不會修改任何認證資料。
- **不顯示密鑰** — 只顯示 `cmdkey /list` 回傳的「認證目標名稱」，不會印出任何 key 或 token 內容。

### 致謝

- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) — SVG 渲染。
- [SkiaSharp](https://github.com/mono/SkiaSharp) — 底層 rasterizer。
- App icon：Font Awesome 7 Free（CC BY 4.0）。
- 國旗 icon：[flag-icons](https://github.com/lipis/flag-icons)。
