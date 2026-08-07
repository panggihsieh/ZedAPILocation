# Zed API Location Analysis

Three implementations of the same Windows credential audit tool,
designed to inspect **where LLM API keys are stored on a Windows machine**
(especially for the Zed editor).

## What's included

```
api/
  - README.md
  - .gitignore
  - Zed-LLM-Credential-Audit.ps1          # original PowerShell version
  - Zed-LLM-Credential-Audit-fixed.ps1    # PowerShell version with encoding fixes
  - ZedCredentialAudit.cpp                # C++17 CLI version (uses std::filesystem)
  - ZedCredentialAudit.exe                # compiled C++ binary (gitignored)
  - build.bat                             # MSVC + vcvars64 build script
  - ZedCredentialAuditGui/                # C# WinForms GUI version (.NET 4.8)
      - Program.cs
      - ZedCredentialAuditGui.csproj      # .NET 8 csproj (kept for reference)
      - build_gui.bat                     # Roslyn csc.exe build script
```

## What it audits

1. **LLM Providers** (12+ providers)
   - DeepSeek, OpenAI, Anthropic, Google Gemini, MiniMax, Mistral, xAI,
     OpenRouter, OpenCode, Vercel AI Gateway, Ollama, LM Studio
   - Checks both Windows Credential Manager (`cmdkey /list`) and
     environment variables

2. **Zed Account** — `zed:url=https://zed.dev` in Credential Manager

3. **Zed Internal Providers** — Keys set via the LLM Providers UI
   (DeepSeek, Google Gemini, MiniMax, etc.) which are stored in
   Credential Manager with DPAPI encryption

4. **ACP External Agents** — Codex, Claude, Gemini CLI, OpenCode, Pi
   - Hash via their local config directories (`~/.codex`, etc.)
   - Environment variables

## Security

- **Read-only** — never modifies credentials
- **Never prints** any key or token — only the credential TARGET names
- The C# / C++ versions explicitly avoid the `[ENV]` PowerShell parser
  trap and the Big5 / UTF-8 console encoding issue

## Build

### C++ CLI
```cmd
build.bat
```
Requires Visual Studio 2022 Build Tools (uses `vcvars64.bat` + `cl.exe`).

### C# GUI
```cmd
cd ZedCredentialAuditGui
build_gui.bat
```
Requires the Roslyn `csc.exe` shipped with MSBuild 2022:
`C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe`

### PowerShell
```powershell
powershell -ExecutionPolicy Bypass -File .\Zed-LLM-Credential-Audit-fixed.ps1
```

## GUI features

- Three tabs: Findings (DataGridView) / Env Vars / Credential Targets
- Bottom description panel with click-to-explain
- Top-right language switch (Chinese / English) with flag emoji
- Export to CSV
- No key/token content is ever shown

## Notes on encoding

The original PowerShell version had encoding issues on Windows with
non-ASCII console output. The C# and C++ versions are written in pure
ASCII so they print cleanly under any codepage. The PowerShell fix
replaces runtime `Write-Host` strings with English to avoid mojibake.
