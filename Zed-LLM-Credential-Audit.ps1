# =====================================================================
# Zed AI Local Credential Audit
# Windows 11 / PowerShell
#
# 檢查：
#   A. Zed LLM Providers
#      - Windows Credential Manager
#      - Process / User / Machine Environment Variables
#
#   B. ACP External Agents
#      - Codex
#      - Claude / Claude Code
#      - Gemini CLI
#      - OpenCode
#      - GitHub Copilot
#      - Cursor
#      - Pi
#
# 安全：
#   - 只讀
#   - 不顯示 API Key / Token / Password
#   - 不修改任何 Credential
#
# 輸出優先順序：
#   [LOCAL] / [ENV] 會排在前面
# =====================================================================

$ErrorActionPreference = "SilentlyContinue"

# ---------------------------------------------------------------------
# Console encoding (fix Windows Big5 / CP950 mojibake)
# ---------------------------------------------------------------------

try {
    [Console]::OutputEncoding = [System.Text.Encoding]::UTF8
    $OutputEncoding           = [System.Text.Encoding]::UTF8
    $PSDefaultParameterValues['Out-File:Encoding'] = 'utf8'
} catch {}

# ---------------------------------------------------------------------
# Helper functions
# ---------------------------------------------------------------------

function Test-EnvVar {
    param([string]$Name)

    $process = [Environment]::GetEnvironmentVariable($Name, "Process")
    $user    = [Environment]::GetEnvironmentVariable($Name, "User")
    $machine = [Environment]::GetEnvironmentVariable($Name, "Machine")

    return [PSCustomObject]@{
        Name    = $Name
        Process = -not [string]::IsNullOrWhiteSpace($process)
        User    = -not [string]::IsNullOrWhiteSpace($user)
        Machine = -not [string]::IsNullOrWhiteSpace($machine)
        Found   = (
            -not [string]::IsNullOrWhiteSpace($process) -or
            -not [string]::IsNullOrWhiteSpace($user) -or
            -not [string]::IsNullOrWhiteSpace($machine)
        )
    }
}

function Test-PathSafe {
    param([string]$Path)

    if ([string]::IsNullOrWhiteSpace($Path)) {
        return $false
    }

    return Test-Path $Path
}

function Add-Result {
    param(
        [string]$Category,
        [string]$Name,
        [string]$Status,
        [string]$Location,
        [string]$Detail
    )

    $script:Results += [PSCustomObject]@{
        Category = $Category
        Name     = $Name
        Status   = $Status
        Location = $Location
        Detail   = $Detail
    }
}

# ---------------------------------------------------------------------
# Header
# ---------------------------------------------------------------------

Clear-Host

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " Zed AI Local Credential Audit" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host "Computer : $env:COMPUTERNAME"
Write-Host "User     : $env:USERNAME"
Write-Host "Time     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ""

$Results = @()

# =====================================================================
# A. Windows Credential Manager
# =====================================================================

$CmdKey = cmdkey /list 2>$null

# =====================================================================
# B. Zed LLM Providers
# =====================================================================

$Providers = @(

    @{
        Name = "DeepSeek"
        CredentialPatterns = @(
            "api.deepseek.com"
        )
        EnvVars = @(
            "DEEPSEEK_API_KEY"
        )
    },

    @{
        Name = "OpenAI"
        CredentialPatterns = @(
            "api.openai.com"
        )
        EnvVars = @(
            "OPENAI_API_KEY"
        )
    },

    @{
        Name = "Anthropic"
        CredentialPatterns = @(
            "api.anthropic.com"
        )
        EnvVars = @(
            "ANTHROPIC_API_KEY"
        )
    },

    @{
        Name = "Google Gemini"
        CredentialPatterns = @(
            "generativelanguage.googleapis.com"
        )
        EnvVars = @(
            "GEMINI_API_KEY",
            "GOOGLE_AI_API_KEY",
            "GOOGLE_API_KEY"
        )
    },

    @{
        Name = "MiniMax"
        CredentialPatterns = @(
            "api.minimax"
        )
        EnvVars = @(
            "MINIMAX_API_KEY"
        )
    },

    @{
        Name = "Mistral"
        CredentialPatterns = @(
            "api.mistral.ai"
        )
        EnvVars = @(
            "MISTRAL_API_KEY"
        )
    },

    @{
        Name = "xAI"
        CredentialPatterns = @(
            "api.x.ai"
        )
        EnvVars = @(
            "XAI_API_KEY"
        )
    },

    @{
        Name = "OpenRouter"
        CredentialPatterns = @(
            "openrouter.ai"
        )
        EnvVars = @(
            "OPENROUTER_API_KEY"
        )
    },

    @{
        Name = "OpenCode"
        CredentialPatterns = @(
            "opencode"
        )
        EnvVars = @(
            "OPENCODE_API_KEY"
        )
    },

    @{
        Name = "Vercel AI Gateway"
        CredentialPatterns = @(
            "vercel"
        )
        EnvVars = @(
            "VERCEL_AI_GATEWAY_API_KEY"
        )
    },

    @{
        Name = "Ollama"
        CredentialPatterns = @(
            "ollama"
        )
        EnvVars = @(
            "OLLAMA_API_KEY"
        )
    },

    @{
        Name = "LM Studio"
        CredentialPatterns = @(
            "lmstudio",
            "lm-studio"
        )
        EnvVars = @(
            "LMSTUDIO_API_KEY"
        )
    }
)

foreach ($Provider in $Providers) {

    # -------------------------------------------------------------
    # Credential Manager
    # -------------------------------------------------------------

    $CredentialMatches = @()

    foreach ($Pattern in $Provider.CredentialPatterns) {

        $Match = $CmdKey |
            Select-String -Pattern ([regex]::Escape($Pattern))

        if ($Match) {
            $CredentialMatches += $Match
        }
    }

    if ($CredentialMatches.Count -gt 0) {

        $Targets = $CredentialMatches |
            ForEach-Object {
                $_.Line.Trim()
            } |
            Sort-Object -Unique

        Add-Result `
            -Category "LLM Provider" `
            -Name $Provider.Name `
            -Status "[LOCAL]" `
            -Location "Windows Credential Manager" `
            -Detail ($Targets -join " ; ")
    }

    # -------------------------------------------------------------
    # Environment variables
    # -------------------------------------------------------------

    foreach ($EnvName in $Provider.EnvVars) {

        $EnvResult = Test-EnvVar $EnvName

        if ($EnvResult.Found) {

            $Scopes = @()

            if ($EnvResult.Process) { $Scopes += "Process" }
            if ($EnvResult.User)    { $Scopes += "User" }
            if ($EnvResult.Machine) { $Scopes += "Machine" }

            Add-Result `
                -Category "LLM Provider" `
                -Name $Provider.Name `
                -Status "[ENV]" `
                -Location "Environment Variable" `
                -Detail "$EnvName [$($Scopes -join ', ')]"
        }
    }
}

# =====================================================================
# C. Zed Account Credential
# =====================================================================

$ZedCredential = $CmdKey |
    Select-String "zed:url=https://zed.dev"

if ($ZedCredential) {

    Add-Result `
        -Category "Zed" `
        -Name "Zed Account" `
        -Status "[LOCAL]" `
        -Location "Windows Credential Manager" `
        -Detail "zed:url=https://zed.dev"
}

# =====================================================================
# D. ACP Agents
# =====================================================================

# ---------------------------------------------------------------------
# Codex
# ---------------------------------------------------------------------

$CodexPaths = @(

    "$env:USERPROFILE\.codex",
    "$env:APPDATA\codex",
    "$env:LOCALAPPDATA\codex"
)

$CodexFoundPaths = @()

foreach ($Path in $CodexPaths) {

    if (Test-PathSafe $Path) {
        $CodexFoundPaths += $Path
    }
}

$CodexEnvVars = @(
    "OPENAI_API_KEY",
    "CODEX_API_KEY"
)

$CodexEnvFound = @()

foreach ($Var in $CodexEnvVars) {

    $E = Test-EnvVar $Var

    if ($E.Found) {
        $CodexEnvFound += $Var
    }
}

$CodexCmd = Get-Command codex -ErrorAction SilentlyContinue

if ($CodexFoundPaths.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Codex" `
        -Status "[LOCAL]" `
        -Location "Local config/auth directory" `
        -Detail ($CodexFoundPaths -join " ; ")
}

if ($CodexEnvFound.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Codex" `
        -Status "[ENV]" `
        -Location "Environment Variable" `
        -Detail ($CodexEnvFound -join ", ")
}

if ($CodexCmd) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Codex CLI" `
        -Status "[LOCAL]" `
        -Location "Executable" `
        -Detail $CodexCmd.Source
}

# ---------------------------------------------------------------------
# Claude / Claude Code
# ---------------------------------------------------------------------

$ClaudePaths = @(

    "$env:USERPROFILE\.claude",
    "$env:APPDATA\Claude",
    "$env:LOCALAPPDATA\Claude"
)

$ClaudeFoundPaths = @()

foreach ($Path in $ClaudePaths) {

    if (Test-PathSafe $Path) {
        $ClaudeFoundPaths += $Path
    }
}

if ($ClaudeFoundPaths.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Claude / Claude Code" `
        -Status "[LOCAL]" `
        -Location "Local config/auth directory" `
        -Detail ($ClaudeFoundPaths -join " ; ")
}

$ClaudeEnv = Test-EnvVar "ANTHROPIC_API_KEY"

if ($ClaudeEnv.Found) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Claude / Claude Code" `
        -Status "[ENV]" `
        -Location "Environment Variable" `
        -Detail "ANTHROPIC_API_KEY"
}

$ClaudeCmd = Get-Command claude -ErrorAction SilentlyContinue

if ($ClaudeCmd) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Claude Code CLI" `
        -Status "[LOCAL]" `
        -Location "Executable" `
        -Detail $ClaudeCmd.Source
}

# ---------------------------------------------------------------------
# Gemini CLI
# ---------------------------------------------------------------------

$GeminiPaths = @(

    "$env:USERPROFILE\.gemini",
    "$env:APPDATA\gemini",
    "$env:LOCALAPPDATA\gemini"
)

$GeminiFoundPaths = @()

foreach ($Path in $GeminiPaths) {

    if (Test-PathSafe $Path) {
        $GeminiFoundPaths += $Path
    }
}

if ($GeminiFoundPaths.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Gemini CLI" `
        -Status "[LOCAL]" `
        -Location "Local config/auth directory" `
        -Detail ($GeminiFoundPaths -join " ; ")
}

foreach ($Var in @(
    "GEMINI_API_KEY",
    "GOOGLE_API_KEY",
    "GOOGLE_AI_API_KEY"
)) {

    $E = Test-EnvVar $Var

    if ($E.Found) {

        Add-Result `
            -Category "ACP Agent" `
            -Name "Gemini CLI" `
            -Status "[ENV]" `
            -Location "Environment Variable" `
            -Detail $Var
    }
}

$GeminiCmd = Get-Command gemini -ErrorAction SilentlyContinue

if ($GeminiCmd) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Gemini CLI" `
        -Status "[LOCAL]" `
        -Location "Executable" `
        -Detail $GeminiCmd.Source
}

# ---------------------------------------------------------------------
# OpenCode
# ---------------------------------------------------------------------

$OpenCodePaths = @(

    "$env:USERPROFILE\.config\opencode",
    "$env:APPDATA\opencode",
    "$env:LOCALAPPDATA\opencode"
)

$OpenCodeFound = @()

foreach ($Path in $OpenCodePaths) {

    if (Test-PathSafe $Path) {
        $OpenCodeFound += $Path
    }
}

if ($OpenCodeFound.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "OpenCode" `
        -Status "[LOCAL]" `
        -Location "Local config/auth directory" `
        -Detail ($OpenCodeFound -join " ; ")
}

$OpenCodeEnv = Test-EnvVar "OPENCODE_API_KEY"

if ($OpenCodeEnv.Found) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "OpenCode" `
        -Status "[ENV]" `
        -Location "Environment Variable" `
        -Detail "OPENCODE_API_KEY"
}

$OpenCodeCmd = Get-Command opencode -ErrorAction SilentlyContinue

if ($OpenCodeCmd) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "OpenCode CLI" `
        -Status "[LOCAL]" `
        -Location "Executable" `
        -Detail $OpenCodeCmd.Source
}

# ---------------------------------------------------------------------
# GitHub Copilot
# ---------------------------------------------------------------------

$CopilotPatterns = @(

    "github.com",
    "copilot"
)

$CopilotMatches = @()

foreach ($Pattern in $CopilotPatterns) {

    $M = $CmdKey |
        Select-String -Pattern ([regex]::Escape($Pattern))

    if ($M) {
        $CopilotMatches += $M
    }
}

if ($CopilotMatches.Count -gt 0) {

    $CopilotTargets = $CopilotMatches |
        ForEach-Object {
            $_.Line.Trim()
        } |
        Sort-Object -Unique

    Add-Result `
        -Category "ACP Agent" `
        -Name "GitHub / Copilot" `
        -Status "[LOCAL]" `
        -Location "Windows Credential Manager" `
        -Detail ($CopilotTargets -join " ; ")
}

$GhCmd = Get-Command gh -ErrorAction SilentlyContinue

if ($GhCmd) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "GitHub CLI" `
        -Status "[LOCAL]" `
        -Location "Executable" `
        -Detail $GhCmd.Source
}

# ---------------------------------------------------------------------
# Cursor
# ---------------------------------------------------------------------

$CursorPaths = @(

    "$env:APPDATA\Cursor",
    "$env:LOCALAPPDATA\Programs\cursor",
    "$env:LOCALAPPDATA\Cursor"
)

$CursorFound = @()

foreach ($Path in $CursorPaths) {

    if (Test-PathSafe $Path) {
        $CursorFound += $Path
    }
}

if ($CursorFound.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Cursor" `
        -Status "[LOCAL]" `
        -Location "Local application/config directory" `
        -Detail ($CursorFound -join " ; ")
}

# ---------------------------------------------------------------------
# Pi Coding Agent
# ---------------------------------------------------------------------

$PiPaths = @(

    "$env:USERPROFILE\.pi",
    "$env:USERPROFILE\.config\pi",
    "$env:APPDATA\pi"
)

$PiFound = @()

foreach ($Path in $PiPaths) {

    if (Test-PathSafe $Path) {
        $PiFound += $Path
    }
}

if ($PiFound.Count -gt 0) {

    Add-Result `
        -Category "ACP Agent" `
        -Name "Pi Coding Agent" `
        -Status "[LOCAL]" `
        -Location "Local config directory" `
        -Detail ($PiFound -join " ; ")
}

# =====================================================================
# E. Zed ACP configuration
# =====================================================================

$ZedConfigPaths = @(

    "$env:APPDATA\Zed\settings.json",
    "$env:LOCALAPPDATA\Zed\settings.json",
    "$env:USERPROFILE\.config\zed\settings.json"
)

foreach ($Path in $ZedConfigPaths) {

    if (Test-PathSafe $Path) {

        Add-Result `
            -Category "Zed Configuration" `
            -Name "Zed settings.json" `
            -Status "[LOCAL]" `
            -Location "Local configuration file" `
            -Detail $Path
    }
}

# =====================================================================
# F. OUTPUT
# =====================================================================

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Green
Write-Host " LOCAL AUTH / CREDENTIALS FOUND" -ForegroundColor Green
Write-Host "=====================================================================" -ForegroundColor Green

$LocalResults = $Results |
    Where-Object {
        $_.Status -eq "[LOCAL]" -or
        $_.Status -eq "[ENV]"
    } |
    Sort-Object `
        @{Expression={
            if ($_.Status -eq "[LOCAL]") { 0 }
            elseif ($_.Status -eq "[ENV]") { 1 }
            else { 2 }
        }},
        Category,
        Name

if ($LocalResults) {

    $LocalResults |
        Format-Table `
            Status,
            Category,
            Name,
            Location,
            Detail `
            -Wrap `
            -AutoSize
}
else {

    Write-Host "No local credentials detected." -ForegroundColor DarkYellow
}

# =====================================================================
# G. Zed Credential Manager raw targets
# =====================================================================

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " ZED WINDOWS CREDENTIAL TARGETS" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$ZedCmdKeyEntries = $CmdKey |
    Select-String "zed:" |
    ForEach-Object {
        $_.Line.Trim()
    }

if ($ZedCmdKeyEntries) {

    $ZedCmdKeyEntries |
        ForEach-Object {
            Write-Host "[LOCAL] $_" -ForegroundColor Green
        }
}
else {

    Write-Host "No zed:* Credential Manager entries found."
}

# =====================================================================
# H. Known LLM environment variables
# =====================================================================

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " LLM ENVIRONMENT VARIABLES" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$KnownEnvVars = @(

    "OPENAI_API_KEY",
    "ANTHROPIC_API_KEY",

    "DEEPSEEK_API_KEY",

    "GEMINI_API_KEY",
    "GOOGLE_AI_API_KEY",
    "GOOGLE_API_KEY",

    "MINIMAX_API_KEY",

    "MISTRAL_API_KEY",
    "XAI_API_KEY",

    "OPENROUTER_API_KEY",
    "OPENCODE_API_KEY",

    "VERCEL_AI_GATEWAY_API_KEY",

    "OLLAMA_API_KEY",
    "LMSTUDIO_API_KEY",

    "CODEX_API_KEY"
)

foreach ($Var in $KnownEnvVars) {

    $E = Test-EnvVar $Var

    if ($E.Found) {

        $Scopes = @()

        if ($E.Process) { $Scopes += "Process" }
        if ($E.User)    { $Scopes += "User" }
        if ($E.Machine) { $Scopes += "Machine" }

        Write-Host `
            "[ENV] $Var  [$($Scopes -join ', ')]" `
            -ForegroundColor Yellow
    }
}

# =====================================================================
# I. DeepSeek detailed example
# =====================================================================

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " DEEPSEEK CREDENTIAL DETAIL" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$DeepSeekCredential = $CmdKey |
    Select-String "deepseek"

if ($DeepSeekCredential) {

    Write-Host "[LOCAL] Windows Credential Manager" -ForegroundColor Green

    $DeepSeekCredential |
        ForEach-Object {
            Write-Host ("  " + $_.Line.Trim())
        }
}
else {

    Write-Host "DeepSeek Credential Manager entry not found."
}

# =====================================================================
# J. Summary
# =====================================================================

Write-Host ""
Write-Host "=====================================================================" -ForegroundColor Cyan
Write-Host " SUMMARY" -ForegroundColor Cyan
Write-Host "=====================================================================" -ForegroundColor Cyan

$LocalCount = @(
    $Results |
    Where-Object {
        $_.Status -eq "[LOCAL]"
    }
).Count

$EnvCount = @(
    $Results |
    Where-Object {
        $_.Status -eq "[ENV]"
    }
).Count

Write-Host "Local credential/config evidence : $LocalCount"
Write-Host "Environment variable evidence    : $EnvCount"

Write-Host ""
Write-Host "Interpretation:" -ForegroundColor White

Write-Host "[LOCAL]" -ForegroundColor Green -NoNewline
Write-Host " = Windows Credential Manager / CLI auth/config / local Zed setting detected"

Write-Host "`[ENV`]" -ForegroundColor Yellow -NoNewline
Write-Host "   = API/Auth related environment variable detected"

Write-Host ""
Write-Host "Note:" -ForegroundColor Yellow
Write-Host "Detecting an ACP Agent folder or CLI only indicates that the agent's config/install exists on this machine."
Write-Host "It does NOT guarantee that the folder contains a valid token."
Write-Host "This script never reads or displays any Token / API Key content."

Write-Host ""
Write-Host "Audit completed." -ForegroundColor Cyan
Write-Host ""
