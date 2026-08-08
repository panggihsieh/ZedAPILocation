# Zed API Location Analysis

A Windows GUI tool that audits **where LLM API keys are stored on a Windows machine**, with a focus on the [Zed editor](https://zed.dev). The app inspects both Windows Credential Manager entries and environment variables for 12+ LLM providers and ACP agents, then displays the findings in a sortable DataGridView.

> Read-only: it never modifies credentials and never prints any key or token — only the credential TARGET names.

## What it audits

- **LLM Providers** (12+): DeepSeek, OpenAI, Anthropic, Google Gemini, MiniMax, Mistral, xAI, OpenRouter, OpenCode, Vercel AI Gateway, Ollama, LM Studio — both Credential Manager (`cmdkey /list`) and environment variables.
- **Zed Account** — `zed:url=https://zed.dev` in Credential Manager.
- **Zed Internal Providers** — Keys set via the Zed LLM Providers UI (stored encrypted with DPAPI).
- **ACP External Agents** — Codex, Claude, Cline, Gemini CLI, OpenCode, Pi — via local config directories (`~/.codex`, etc.) and environment variables.

## Features

- Three tabs: **Findings** (sortable DataGridView) / **Env Vars** / **Credential Targets**.
- Bottom description panel that explains the highlighted row in plain English or Chinese.
- Top-right language toggle (中文 / English) with SVG-rendered national flag icon (TW / US).
- Export findings to CSV.
- Auto-runs the audit on startup.
- App icon rendered from an embedded SVG (Font Awesome 7 key).

## Project layout

```
ZedApiDig/
├── Program.cs                       # Single-file WinForms source (~1100 lines)
├── ZedCredentialAuditGui.csproj     # .NET 8 Windows Forms project
├── build_gui.bat                    # Publishes self-contained single-file exe
├── Assets/
│   └── app.ico                      # Windows .ico used for the executable
└── Resources/
    ├── logo.svg                     # Font Awesome 7 key — used as Form.Icon
    ├── tw.svg                       # Taiwan flag — language toggle (zh)
    └── us.svg                       # US flag — language toggle (en)
```

All three SVGs are embedded as `EmbeddedResource` and rendered at runtime via [Svg.Skia](https://www.nuget.org/packages/Svg.Skia) + SkiaSharp. The bitmap is then handed to `Bitmap.GetHicon()` for the Form icon, or drawn directly into the flag button.

## Build

Requirements: .NET 8 SDK (Windows).

```cmd
build_gui.bat
```

This restores NuGet packages and publishes a fully self-contained single-file executable:

```
publish\ZedCredentialAuditGui.exe
```

No .NET runtime install is needed on the target machine (Windows 10 1607+ x64).

## Develop / debug

```cmd
dotnet run
```

## Security notes

- The tool is read-only.
- No key or token content is ever displayed — only the credential TARGET names returned by `cmdkey /list`.
- The source is pure ASCII so it prints cleanly under any Windows codepage (no Big5 / UTF-8 console mojibake).

## Acknowledgements

- [Svg.Skia](https://github.com/wieslawsoltes/Svg.Skia) for SVG rendering.
- [SkiaSharp](https://github.com/mono/SkiaSharp) for the underlying rasterizer.
- App icon: Font Awesome 7 Free (CC BY 4.0).
- TW / US flag icons: [flag-icons](https://github.com/lipis/flag-icons).
