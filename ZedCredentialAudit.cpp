// =====================================================================
// Zed AI Local Credential Audit (C++17, Windows only)
//
// Maps 1:1 to Zed-LLM-Credential-Audit.ps1 with these changes:
//   - All output strings are ASCII (no console encoding issues)
//   - Uses Win32 API for env vars (no shell quoting issues)
//   - Uses std::filesystem for path checks
//   - Uses _popen("cmdkey /list 2>NUL") to read Credential Manager
//   - Never reads or displays any Token / API Key content
//
// Build:
//   MSVC : cl /EHsc /std:c++17 ZedCredentialAudit.cpp
//   MinGW: g++ -std=c++17 -O2 ZedCredentialAudit.cpp -o ZedCredentialAudit.exe
//
// Run:
//   ZedCredentialAudit.exe
// =====================================================================

#include <windows.h>
#include <cstdio>
#include <cstdlib>
#include <filesystem>
#include <iostream>
#include <sstream>
#include <string>
#include <vector>
#include <algorithm>
#include <array>

namespace fs = std::filesystem;

// ---------------------------------------------------------------------
// Data model
// ---------------------------------------------------------------------

struct Finding {
    std::string category;
    std::string name;
    std::string status;   // "[LOCAL]" or "[ENV]"
    std::string location;
    std::string detail;
};

static std::vector<Finding> g_findings;
static std::string           g_cmdkeyOutput;

// ---------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------

static std::string getEnvVar(const char* name) {
    char buf[32767];
    DWORD len = GetEnvironmentVariableA(name, buf, sizeof(buf));
    if (len == 0 || len >= sizeof(buf)) return "";
    return std::string(buf, len);
}

static bool envVarExists(const char* name) {
    char buf[2];
    DWORD len = GetEnvironmentVariableA(name, buf, sizeof(buf));
    return len > 0;
}

static bool contains(const std::string& haystack, const std::string& needle) {
    return haystack.find(needle) != std::string::npos;
}

static bool dirExists(const std::string& path) {
    if (path.empty()) return false;
    try {
        return fs::is_directory(path);
    } catch (...) {
        return false;
    }
}

static std::string trimRight(const std::string& s) {
    size_t end = s.size();
    while (end > 0 && (s[end - 1] == '\r' || s[end - 1] == '\n' ||
                       s[end - 1] == ' '  || s[end - 1] == '\t'))
        --end;
    return s.substr(0, end);
}

static std::string runCmd(const std::string& cmd) {
    std::string result;
    FILE* pipe = _popen(cmd.c_str(), "r");
    if (!pipe) return "";
    char buf[4096];
    while (fgets(buf, sizeof(buf), pipe)) {
        result += buf;
    }
    _pclose(pipe);
    return result;
}

static std::string join(const std::vector<std::string>& items, const std::string& sep) {
    std::string out;
    for (size_t i = 0; i < items.size(); ++i) {
        if (i > 0) out += sep;
        out += items[i];
    }
    return out;
}

static void addFinding(const std::string& category, const std::string& name,
                       const std::string& status, const std::string& location,
                       const std::string& detail) {
    g_findings.push_back({category, name, status, location, detail});
}

// ---------------------------------------------------------------------
// Checks
// ---------------------------------------------------------------------

static void checkProvider(const std::string& name,
                          const std::vector<std::string>& credentialPatterns,
                          const std::vector<std::string>& envVars) {
    // Credential Manager
    if (!g_cmdkeyOutput.empty()) {
        std::vector<std::string> matches;
        std::istringstream iss(g_cmdkeyOutput);
        std::string line;
        while (std::getline(iss, line)) {
            std::string t = trimRight(line);
            for (const auto& p : credentialPatterns) {
                if (contains(t, p)) {
                    matches.push_back(t);
                    break;
                }
            }
        }
        if (!matches.empty()) {
            std::sort(matches.begin(), matches.end());
            matches.erase(std::unique(matches.begin(), matches.end()), matches.end());
            addFinding("LLM Provider", name, "[LOCAL]",
                       "Windows Credential Manager",
                       join(matches, " ; "));
        }
    }

    // Environment variables
    for (const auto& var : envVars) {
        if (envVarExists(var.c_str())) {
            addFinding("LLM Provider", name, "[ENV]",
                       "Environment Variable", var);
        }
    }
}

static void checkZedAccount() {
    if (contains(g_cmdkeyOutput, "zed:url=https://zed.dev")) {
        addFinding("Zed", "Zed Account", "[LOCAL]",
                   "Windows Credential Manager",
                   "zed:url=https://zed.dev");
    }
}

static void checkAgent(const std::string& name,
                       const std::vector<std::string>& paths,
                       const std::vector<std::string>& envVars) {
    std::vector<std::string> foundPaths;
    for (const auto& p : paths) {
        if (dirExists(p)) foundPaths.push_back(p);
    }
    if (!foundPaths.empty()) {
        addFinding("ACP Agent", name, "[LOCAL]",
                   "Local config/auth directory",
                   join(foundPaths, " ; "));
    }
    for (const auto& var : envVars) {
        if (envVarExists(var.c_str())) {
            addFinding("ACP Agent", name, "[ENV]",
                       "Environment Variable", var);
        }
    }
}

// ---------------------------------------------------------------------
// Output
// ---------------------------------------------------------------------

static int statusRank(const std::string& s) {
    if (s == "[LOCAL]") return 0;
    if (s == "[ENV]")   return 1;
    return 2;
}

static std::string fitColumn(const std::string& s, size_t w) {
    if (s.size() <= w) return s;
    if (w <= 3)        return s.substr(0, w);
    return s.substr(0, w - 3) + "...";
}

static size_t maxFieldLen(const std::vector<Finding>& v,
                          const std::string Finding::* field) {
    size_t m = 0;
    for (const auto& f : v) {
        size_t n = (f.*field).size();
        if (n > m) m = n;
    }
    return m;
}

static void printTable(const std::vector<Finding>& items) {
    std::vector<Finding> sorted = items;
    std::sort(sorted.begin(), sorted.end(),
              [](const Finding& a, const Finding& b) {
                  int ra = statusRank(a.status);
                  int rb = statusRank(b.status);
                  if (ra != rb) return ra < rb;
                  if (a.category != b.category) return a.category < b.category;
                  return a.name < b.name;
              });

    const size_t wStatus = 8;
    const size_t wCat    = std::min<size_t>(20, std::max<size_t>(8,  maxFieldLen(sorted, &Finding::category)));
    const size_t wName   = std::min<size_t>(28, std::max<size_t>(8,  maxFieldLen(sorted, &Finding::name)));
    const size_t wLoc    = std::min<size_t>(32, std::max<size_t>(14, maxFieldLen(sorted, &Finding::location)));
    const size_t wDet    = 80;

    auto printRow = [&](const std::string& s, const std::string& c,
                        const std::string& n, const std::string& l,
                        const std::string& d) {
        std::printf("%-*s %-*s %-*s %-*s %s\n",
                    (int)wStatus, s.c_str(),
                    (int)wCat,    c.c_str(),
                    (int)wName,   n.c_str(),
                    (int)wLoc,    l.c_str(),
                    fitColumn(d, wDet).c_str());
    };

    printRow("Status", "Category", "Name", "Location", "Detail");
    printRow("------", "--------", "----", "--------", "------");
    for (const auto& f : sorted) {
        printRow(f.status, f.category, f.name, f.location, f.detail);
    }
}

static void printCredentialTargets() {
    std::cout << "\n";
    std::cout << "============================================================\n";
    std::cout << "  ZED WINDOWS CREDENTIAL TARGETS\n";
    std::cout << "============================================================\n";

    if (g_cmdkeyOutput.empty()) {
        std::cout << "[no output from cmdkey /list]\n";
        return;
    }

    std::istringstream iss(g_cmdkeyOutput);
    std::string line;
    bool inZedBlock = false;
    while (std::getline(iss, line)) {
        std::string t = trimRight(line);
        if (t.find("Target:") == 0) {
            inZedBlock = (t.find("zed:") != std::string::npos);
        }
        if (inZedBlock) {
            std::cout << "  " << t << "\n";
        }
        if (t.find("Local machine persistence") != std::string::npos) {
            inZedBlock = false;
            std::cout << "\n";
        }
    }
}

static void printEnvVars() {
    std::cout << "\n";
    std::cout << "============================================================\n";
    std::cout << "  LLM ENVIRONMENT VARIABLES\n";
    std::cout << "============================================================\n";

    static const std::array<const char*, 16> kKnownVars = {
        "OPENAI_API_KEY", "ANTHROPIC_API_KEY",
        "DEEPSEEK_API_KEY",
        "GEMINI_API_KEY", "GOOGLE_AI_API_KEY", "GOOGLE_API_KEY",
        "MINIMAX_API_KEY",
        "MISTRAL_API_KEY", "XAI_API_KEY",
        "OPENROUTER_API_KEY", "OPENCODE_API_KEY",
        "VERCEL_AI_GATEWAY_API_KEY",
        "OLLAMA_API_KEY", "LMSTUDIO_API_KEY",
        "CODEX_API_KEY"
    };

    for (const auto* var : kKnownVars) {
        if (envVarExists(var)) {
            std::cout << "  [ENV] " << var << "\n";
        }
    }
}

// ---------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------

int main() {
    SetConsoleOutputCP(CP_UTF8);
    SetConsoleCP(CP_UTF8);

    std::cout << "\n";
    std::cout << "============================================================\n";
    std::cout << "  Zed AI Local Credential Audit (C++)\n";
    std::cout << "============================================================\n";
    std::cout << "Computer : " << getEnvVar("COMPUTERNAME") << "\n";
    std::cout << "User     : " << getEnvVar("USERNAME")     << "\n";
    std::cout << "Time     : " << __DATE__ << " " << __TIME__ << "\n";
    std::cout << "\n";

    g_cmdkeyOutput = runCmd("cmdkey /list 2>NUL");

    // ===== LLM Providers =====
    checkProvider("DeepSeek",
        {"api.deepseek.com"},
        {"DEEPSEEK_API_KEY"});

    checkProvider("OpenAI",
        {"api.openai.com"},
        {"OPENAI_API_KEY"});

    checkProvider("Anthropic",
        {"api.anthropic.com"},
        {"ANTHROPIC_API_KEY"});

    checkProvider("Google Gemini",
        {"generativelanguage.googleapis.com"},
        {"GEMINI_API_KEY", "GOOGLE_AI_API_KEY", "GOOGLE_API_KEY"});

    checkProvider("MiniMax",
        {"api.minimax"},
        {"MINIMAX_API_KEY"});

    checkProvider("Mistral",
        {"api.mistral.ai"},
        {"MISTRAL_API_KEY"});

    checkProvider("xAI",
        {"api.x.ai"},
        {"XAI_API_KEY"});

    checkProvider("OpenRouter",
        {"openrouter.ai"},
        {"OPENROUTER_API_KEY"});

    checkProvider("OpenCode",
        {"opencode"},
        {"OPENCODE_API_KEY"});

    checkProvider("Vercel AI Gateway",
        {"vercel"},
        {"VERCEL_AI_GATEWAY_API_KEY"});

    checkProvider("Ollama",
        {"ollama"},
        {"OLLAMA_API_KEY"});

    checkProvider("LM Studio",
        {"lmstudio", "lm-studio"},
        {"LMSTUDIO_API_KEY"});

    checkZedAccount();

    // ===== ACP Agents =====
    const std::string userProfile  = getEnvVar("USERPROFILE");
    const std::string appData      = getEnvVar("APPDATA");
    const std::string localAppData = getEnvVar("LOCALAPPDATA");

    checkAgent("Codex",
        {userProfile + "\\.codex",
         appData + "\\codex",
         localAppData + "\\codex"},
        {"OPENAI_API_KEY", "CODEX_API_KEY"});

    checkAgent("Claude / Claude Code",
        {userProfile + "\\.claude",
         appData + "\\Claude",
         localAppData + "\\Claude"},
        {"ANTHROPIC_API_KEY"});

    checkAgent("Gemini CLI",
        {userProfile + "\\.gemini",
         appData + "\\gemini",
         localAppData + "\\gemini"},
        {"GEMINI_API_KEY", "GOOGLE_API_KEY", "GOOGLE_AI_API_KEY"});

    checkAgent("OpenCode",
        {userProfile + "\\.config\\opencode",
         appData + "\\opencode",
         localAppData + "\\opencode"},
        {"OPENCODE_API_KEY"});

    checkAgent("Pi Coding Agent",
        {userProfile + "\\.pi",
         userProfile + "\\.config\\pi",
         appData + "\\pi"},
        {});

    // ===== Output =====
    std::cout << "============================================================\n";
    std::cout << "  LOCAL AUTH / CREDENTIALS FOUND\n";
    std::cout << "============================================================\n";

    if (g_findings.empty()) {
        std::cout << "No local credentials detected.\n";
    } else {
        printTable(g_findings);
    }

    printCredentialTargets();
    printEnvVars();

    // ===== Summary =====
    size_t localCount = 0, envCount = 0;
    for (const auto& f : g_findings) {
        if (f.status == "[LOCAL]")      ++localCount;
        else if (f.status == "[ENV]")   ++envCount;
    }

    std::cout << "\n";
    std::cout << "============================================================\n";
    std::cout << "  SUMMARY\n";
    std::cout << "============================================================\n";
    std::cout << "Local credential/config evidence : " << localCount << "\n";
    std::cout << "Environment variable evidence    : " << envCount  << "\n";
    std::cout << "\n";
    std::cout << "Interpretation:\n";
    std::cout << "  [LOCAL] = Windows Credential Manager / CLI auth/config / local Zed setting detected\n";
    std::cout << "  [ENV]   = API/Auth related environment variable detected\n";
    std::cout << "\n";
    std::cout << "Note:\n";
    std::cout << "  Detecting an ACP Agent folder or CLI only indicates that the agent's\n";
    std::cout << "  config/install exists on this machine. It does NOT guarantee that the\n";
    std::cout << "  folder contains a valid token.\n";
    std::cout << "  This script never reads or displays any Token / API Key content.\n";
    std::cout << "\nAudit completed.\n";

    return 0;
}
