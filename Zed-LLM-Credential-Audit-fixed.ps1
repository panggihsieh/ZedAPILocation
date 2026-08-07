# Zed LLM Provider + ACP Agent Local Credential Audit
# Windows 11 / PowerShell 5.1+
# Read-only: does not reveal API keys/tokens and does not modify credentials.

$ErrorActionPreference = 'SilentlyContinue'

function Get-EnvPresence {
    param([Parameter(Mandatory=$true)][string]$Name)
    $p = [Environment]::GetEnvironmentVariable($Name, 'Process')
    $u = [Environment]::GetEnvironmentVariable($Name, 'User')
    $m = [Environment]::GetEnvironmentVariable($Name, 'Machine')
    $scopes = @()
    if (-not [string]::IsNullOrWhiteSpace($p)) { $scopes += 'Process' }
    if (-not [string]::IsNullOrWhiteSpace($u)) { $scopes += 'User' }
    if (-not [string]::IsNullOrWhiteSpace($m)) { $scopes += 'Machine' }
    [PSCustomObject]@{
        Name   = $Name
        Found  = ($scopes.Count -gt 0)
        Scopes = ($scopes -join ', ')
    }
}

function Add-AuditResult {
    param(
        [string]$Status,
        [string]$Category,
        [string]$Name,
        [string]$Location,
        [string]$Detail
    )
    $script:Results += [PSCustomObject]@{
        Status   = $Status
        Category = $Category
        Name     = $Name
        Location = $Location
        Detail   = $Detail
    }
}

function Test-AnyPath {
    param([string[]]$Paths)
    $found = @()
    foreach ($p in $Paths) {
        if (-not [string]::IsNullOrWhiteSpace($p) -and (Test-Path -LiteralPath $p)) {
            $found += $p
        }
    }
    return $found
}

$Results = @()
$CmdKey = @(cmdkey /list 2>$null)

Write-Host ''
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host ' Zed LLM Provider + ACP Agent Local Credential Audit' -ForegroundColor Cyan
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host "Computer : $env:COMPUTERNAME"
Write-Host "User     : $env:USERNAME"
Write-Host "Time     : $(Get-Date -Format 'yyyy-MM-dd HH:mm:ss')"
Write-Host ''

# ---------------------------------------------------------------------
# 1) Zed built-in LLM Providers
# ---------------------------------------------------------------------
$Providers = @(
    @{ Name='DeepSeek'; Patterns=@('api.deepseek.com'); Env=@('DEEPSEEK_API_KEY') },
    @{ Name='OpenAI'; Patterns=@('api.openai.com'); Env=@('OPENAI_API_KEY') },
    @{ Name='Anthropic'; Patterns=@('api.anthropic.com'); Env=@('ANTHROPIC_API_KEY') },
    @{ Name='Google Gemini'; Patterns=@('generativelanguage.googleapis.com'); Env=@('GEMINI_API_KEY','GOOGLE_AI_API_KEY','GOOGLE_API_KEY') },
    @{ Name='MiniMax'; Patterns=@('api.minimax'); Env=@('MINIMAX_API_KEY') },
    @{ Name='Mistral'; Patterns=@('api.mistral.ai'); Env=@('MISTRAL_API_KEY') },
    @{ Name='xAI'; Patterns=@('api.x.ai'); Env=@('XAI_API_KEY') },
    @{ Name='OpenRouter'; Patterns=@('openrouter.ai'); Env=@('OPENROUTER_API_KEY') },
    @{ Name='Vercel AI Gateway'; Patterns=@('vercel'); Env=@('VERCEL_AI_GATEWAY_API_KEY') },
    @{ Name='Ollama'; Patterns=@('ollama'); Env=@('OLLAMA_API_KEY') },
    @{ Name='LM Studio'; Patterns=@('lmstudio','lm-studio'); Env=@('LMSTUDIO_API_KEY') }
)

foreach ($provider in $Providers) {
    $matches = @()
    foreach ($pat in $provider.Patterns) {
        $matches += @($CmdKey | Select-String -SimpleMatch $pat)
    }
    $matches = @($matches | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique)
    if ($matches.Count -gt 0) {
        Add-AuditResult '[LOCAL]' 'LLM Provider' $provider.Name 'Windows Credential Manager' ($matches -join ' ; ')
    }

    foreach ($envName in $provider.Env) {
        $e = Get-EnvPresence $envName
        if ($e.Found) {
            Add-AuditResult '[ENV]' 'LLM Provider' $provider.Name 'Environment Variable' "$envName [$($e.Scopes)]"
        }
    }
}

# Zed account credential
$zedAccount = @($CmdKey | Select-String -SimpleMatch 'zed:url=https://zed.dev')
if ($zedAccount.Count -gt 0) {
    Add-AuditResult '[LOCAL]' 'Zed' 'Zed Account' 'Windows Credential Manager' 'zed:url=https://zed.dev'
}

# ---------------------------------------------------------------------
# 2) ACP / External Agents - local installation/auth/config evidence
# ---------------------------------------------------------------------
$Agents = @(
    @{
        Name='Codex'
        Commands=@('codex')
        Paths=@("$env:USERPROFILE\.codex", "$env:APPDATA\codex", "$env:LOCALAPPDATA\codex")
        Env=@('OPENAI_API_KEY','CODEX_API_KEY')
        CredPatterns=@('codex','openai')
    },
    @{
        Name='Claude Code'
        Commands=@('claude')
        Paths=@("$env:USERPROFILE\.claude", "$env:APPDATA\Claude", "$env:LOCALAPPDATA\Claude")
        Env=@('ANTHROPIC_API_KEY')
        CredPatterns=@('claude','anthropic')
    },
    @{
        Name='Gemini CLI'
        Commands=@('gemini')
        Paths=@("$env:USERPROFILE\.gemini", "$env:APPDATA\gemini", "$env:LOCALAPPDATA\gemini")
        Env=@('GEMINI_API_KEY','GOOGLE_API_KEY','GOOGLE_AI_API_KEY')
        CredPatterns=@('gemini:','google')
    },
    @{
        Name='OpenCode'
        Commands=@('opencode')
        Paths=@("$env:USERPROFILE\.config\opencode", "$env:APPDATA\opencode", "$env:LOCALAPPDATA\opencode")
        Env=@('OPENCODE_API_KEY')
        CredPatterns=@('opencode')
    },
    @{
        Name='GitHub Copilot / GitHub CLI'
        Commands=@('gh')
        Paths=@("$env:APPDATA\GitHub CLI", "$env:APPDATA\github-copilot")
        Env=@('GH_TOKEN','GITHUB_TOKEN')
        CredPatterns=@('gh:github.com','git:https://github.com','copilot')
    }
)

foreach ($agent in $Agents) {
    $foundPaths = @(Test-AnyPath $agent.Paths)
    if ($foundPaths.Count -gt 0) {
        Add-AuditResult '[LOCAL]' 'ACP Agent' $agent.Name 'Local config/auth directory' ($foundPaths -join ' ; ')
    }

    foreach ($cmd in $agent.Commands) {
        $c = Get-Command $cmd -ErrorAction SilentlyContinue
        if ($null -ne $c) {
            Add-AuditResult '[LOCAL]' 'ACP Agent' $agent.Name 'Executable' $c.Source
        }
    }

    foreach ($envName in $agent.Env) {
        $e = Get-EnvPresence $envName
        if ($e.Found) {
            Add-AuditResult '[ENV]' 'ACP Agent' $agent.Name 'Environment Variable' "$envName [$($e.Scopes)]"
        }
    }

    $credMatches = @()
    foreach ($pat in $agent.CredPatterns) {
        $credMatches += @($CmdKey | Select-String -SimpleMatch $pat)
    }
    $credMatches = @($credMatches | ForEach-Object { $_.Line.Trim() } | Sort-Object -Unique)
    if ($credMatches.Count -gt 0) {
        Add-AuditResult '[LOCAL]' 'ACP Agent' $agent.Name 'Windows Credential Manager' ($credMatches -join ' ; ')
    }
}

# ---------------------------------------------------------------------
# 3) Zed local configuration paths (evidence only; no file content read)
# ---------------------------------------------------------------------
$ZedConfigCandidates = @(
    "$env:APPDATA\Zed\settings.json",
    "$env:LOCALAPPDATA\Zed\settings.json",
    "$env:USERPROFILE\.config\zed\settings.json"
)
$zedConfigs = @(Test-AnyPath $ZedConfigCandidates)
foreach ($p in $zedConfigs) {
    Add-AuditResult '[LOCAL]' 'Zed Configuration' 'settings.json' 'Local configuration file' $p
}

# ---------------------------------------------------------------------
# 4) Display LOCAL/ENV findings FIRST
# ---------------------------------------------------------------------
Write-Host '=====================================================================' -ForegroundColor Green
Write-Host ' LOCAL / ENV AUTHENTICATION EVIDENCE FOUND' -ForegroundColor Green
Write-Host '=====================================================================' -ForegroundColor Green

$Sorted = @($Results | Sort-Object @{Expression={ if ($_.Status -eq '[LOCAL]') {0} elseif ($_.Status -eq '[ENV]') {1} else {2} }}, Category, Name, Location)

if ($Sorted.Count -gt 0) {
    $Sorted | Format-Table Status, Category, Name, Location, Detail -Wrap -AutoSize
} else {
    Write-Host 'No local credential/auth evidence found.' -ForegroundColor DarkYellow
}

# ---------------------------------------------------------------------
# 5) All Zed Credential Manager targets
# ---------------------------------------------------------------------
Write-Host ''
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host ' ZED WINDOWS CREDENTIAL TARGETS' -ForegroundColor Cyan
Write-Host '=====================================================================' -ForegroundColor Cyan

$zedTargets = @($CmdKey | Select-String -SimpleMatch 'zed:' | ForEach-Object { $_.Line.Trim() })
if ($zedTargets.Count -gt 0) {
    foreach ($line in $zedTargets) {
        Write-Host "[LOCAL] $line" -ForegroundColor Green
    }
} else {
    Write-Host 'No zed:* entries found.'
}

# ---------------------------------------------------------------------
# 6) Known API/Auth environment variables (presence only)
# ---------------------------------------------------------------------
Write-Host ''
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host ' KNOWN AI / AGENT ENVIRONMENT VARIABLES' -ForegroundColor Cyan
Write-Host '=====================================================================' -ForegroundColor Cyan

$KnownVars = @(
    'OPENAI_API_KEY','CODEX_API_KEY','ANTHROPIC_API_KEY','DEEPSEEK_API_KEY',
    'GEMINI_API_KEY','GOOGLE_AI_API_KEY','GOOGLE_API_KEY','MINIMAX_API_KEY',
    'MISTRAL_API_KEY','XAI_API_KEY','OPENROUTER_API_KEY','OPENCODE_API_KEY',
    'VERCEL_AI_GATEWAY_API_KEY','OLLAMA_API_KEY','LMSTUDIO_API_KEY',
    'GH_TOKEN','GITHUB_TOKEN'
)

$envFoundCount = 0
foreach ($v in $KnownVars) {
    $e = Get-EnvPresence $v
    if ($e.Found) {
        $envFoundCount++
        Write-Host "[ENV] $v [$($e.Scopes)]" -ForegroundColor Yellow
    }
}
if ($envFoundCount -eq 0) {
    Write-Host 'No known AI/API environment variables detected.'
}

# ---------------------------------------------------------------------
# 7) DeepSeek detailed check (safe: no secret value)
# ---------------------------------------------------------------------
Write-Host ''
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host ' DEEPSEEK DETAIL' -ForegroundColor Cyan
Write-Host '=====================================================================' -ForegroundColor Cyan

$deep = @($CmdKey | Select-String -SimpleMatch 'deepseek' -Context 2,3)
if ($deep.Count -gt 0) {
    Write-Host '[LOCAL] Windows Credential Manager entry found:' -ForegroundColor Green
    $deep
} else {
    Write-Host 'DeepSeek credential entry not found.'
}

$de = Get-EnvPresence 'DEEPSEEK_API_KEY'
if ($de.Found) {
    Write-Host "[ENV] DEEPSEEK_API_KEY [$($de.Scopes)]" -ForegroundColor Yellow
} else {
    Write-Host 'DEEPSEEK_API_KEY environment variable: not set'
}

# ---------------------------------------------------------------------
# 8) Summary
# ---------------------------------------------------------------------
Write-Host ''
Write-Host '=====================================================================' -ForegroundColor Cyan
Write-Host ' SUMMARY' -ForegroundColor Cyan
Write-Host '=====================================================================' -ForegroundColor Cyan

$localCount = @($Results | Where-Object Status -eq '[LOCAL]').Count
$envCount   = @($Results | Where-Object Status -eq '[ENV]').Count

Write-Host "[LOCAL] findings : $localCount" -ForegroundColor Green
Write-Host "[ENV] findings   : $envCount" -ForegroundColor Yellow
Write-Host ''
Write-Host 'Meaning:'
Write-Host '  [LOCAL] = local credential store, local auth/config directory, executable, or Zed config evidence exists.'
Write-Host '  [ENV]   = API/Auth environment variable exists; the secret value is NOT displayed.'
Write-Host ''
Write-Host 'Important: an ACP agent config directory or executable proves local presence, not necessarily an active login/token.' -ForegroundColor DarkYellow
Write-Host 'This script is read-only and does not reveal, change, or delete any credential.'
Write-Host ''
Write-Host 'Audit completed.' -ForegroundColor Cyan
