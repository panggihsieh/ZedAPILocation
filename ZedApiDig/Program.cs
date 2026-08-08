// =====================================================================
// Zed API Variable Access Location Analysis - GUI version (.NET 4.8 WinForms)
//
// Features:
//   - Tab 1: Findings (DataGridView, sortable)
//   - Tab 2: LLM Environment Variables
//   - Tab 3: Windows Credential Manager targets (Zed only)
//   - Bottom description panel: shows EN / 中 explanation on row click
//   - Top-right language toggle button (EN / 中)
//   - Auto-run on startup
//   - Export findings to CSV
//
// Build:
//   "C:\Program Files (x86)\Microsoft Visual Studio\2022\BuildTools\MSBuild\Current\Bin\Roslyn\csc.exe" ^
//     /target:winexe /platform:x64 /out:ZedCredentialAuditGui.exe ^
//     /reference:System.dll /reference:System.Core.dll ^
//     /reference:System.Data.dll /reference:System.Drawing.dll ^
//     /reference:System.Windows.Forms.dll Program.cs
// =====================================================================

using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;
using SkiaSharp;
using Svg.Skia;

namespace ZedCredentialAuditGui
{
    // -----------------------------------------------------------------
    // Data model
    // -----------------------------------------------------------------

    public class Finding
    {
        public string Category { get; set; } = "";
        public string Name     { get; set; } = "";
        public string Status   { get; set; } = "";
        public string Location { get; set; } = "";
        public string Detail   { get; set; } = "";
    }

    // -----------------------------------------------------------------
    // Main form
    // -----------------------------------------------------------------

    public class MainForm : Form
    {
        private enum Lang { English, Chinese }

        private static readonly System.Drawing.Color ThemeBg        = System.Drawing.Color.FromArgb(247, 249, 252);
        private static readonly System.Drawing.Color ThemePanel      = System.Drawing.Color.FromArgb(255, 255, 255);
        private static readonly System.Drawing.Color ThemeToolbar    = System.Drawing.Color.FromArgb(240, 244, 249);
        private static readonly System.Drawing.Color ThemeBorder     = System.Drawing.Color.FromArgb(216, 222, 232);
        private static readonly System.Drawing.Color ThemeAccent     = System.Drawing.Color.FromArgb(44, 106, 207);
        private static readonly System.Drawing.Color ThemeAccentLite = System.Drawing.Color.FromArgb(232, 240, 252);
        private static readonly System.Drawing.Color ThemeTextMuted  = System.Drawing.Color.FromArgb(78, 88, 105);

        private readonly DataGridView _grid;
        private readonly TextBox      _txtEnv;
        private readonly TextBox      _txtCred;
        private readonly TextBox      _txtDesc;
        private readonly Label        _lblSummary;
        private readonly Button       _btnRun;
        private readonly Button       _btnExport;
        private readonly FlagToggleButton _langBtn;
        private readonly TabControl   _tabs;

        private Lang    _lang = Lang.Chinese;
        private List<Finding> _findings = new();
        private string        _cmdkeyOutput = "";
        private IntPtr        _appIconHandle = IntPtr.Zero;

        public MainForm()
        {
            Text          = "Zed API Variable Access Location Analysis";
            Icon          = LoadAppIcon();
            Size          = new System.Drawing.Size(1180, 760);
            MinimumSize   = new System.Drawing.Size(900, 540);
            StartPosition = FormStartPosition.CenterScreen;
            Font          = new System.Drawing.Font("Segoe UI", 9);
            BackColor     = ThemeBg;

            // -----------------------------------------------------
            // Toolbar (left = action buttons, right = language switch)
            // -----------------------------------------------------
            var toolbar = new TableLayoutPanel {
                Dock        = DockStyle.Top,
                Height      = 44,
                ColumnCount = 2,
                BackColor   = ThemeToolbar,
                Padding     = new Padding(0, 0, 0, 1)
            };
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            toolbar.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

            var leftPanel = new FlowLayoutPanel {
                Dock    = DockStyle.Fill,
                Padding = new Padding(6, 7, 0, 0),
                BackColor = ThemeToolbar
            };
            _btnRun = CreateThemeButton("執行稽核");
            _btnExport = CreateThemeButton("匯出 CSV");
            leftPanel.Controls.Add(_btnRun);
            leftPanel.Controls.Add(_btnExport);
            toolbar.Controls.Add(leftPanel, 0, 0);

            var rightPanel = new FlowLayoutPanel {
                Dock          = DockStyle.Fill,
                Padding       = new Padding(0, 7, 6, 0),
                FlowDirection = FlowDirection.RightToLeft,
                BackColor     = ThemeToolbar
            };
            // Custom-drawn button — square 40x40 to display a standard icon-sized
            // flag (the SVG bitmap is 256x256, drawn into the square button).
            _langBtn = new FlagToggleButton {
                Width        = 40,
                Height       = 40,
                Margin       = new Padding(2, 2, 8, 2),
                Padding      = new Padding(0),
                FlagBitmap   = LoadFlag(true),        // 256x256 standard icon
                BackColor    = ThemeToolbar           // match toolbar so any inset is invisible
            };
            _langBtn.FlatAppearance.BorderSize = 0;
            _langBtn.Click += (s, e) => ToggleLang();
            rightPanel.Controls.Add(_langBtn);
            toolbar.Controls.Add(rightPanel, 1, 0);

            // -----------------------------------------------------
            // Description panel (shows EN / 中 on row click)
            // -----------------------------------------------------
            _txtDesc = new TextBox {
                Dock      = DockStyle.Bottom,
                Height    = 90,
                Multiline = true,
                ReadOnly  = true,
                BackColor = ThemeAccentLite,
                ForeColor = System.Drawing.Color.FromArgb(45, 55, 72),
                BorderStyle = BorderStyle.FixedSingle,
                Font      = new System.Drawing.Font("Segoe UI", 9),
                Text      = "點選上列以查看詳情。\r\n\r\nAPI 寫入：註冊 → 取得 Key → 貼入 Zed Provider UI → Zed 加密存入認證管理員 (DPAPI)\r\nAPI 使用：Zed 從認證管理員讀取 Key → 每次請求時送至 LLM API 端點"
            };

            // -----------------------------------------------------
            // Status bar
            // -----------------------------------------------------
            _lblSummary = new Label {
                Dock        = DockStyle.Bottom,
                Height      = 26,
                TextAlign   = System.Drawing.ContentAlignment.MiddleLeft,
                Padding     = new Padding(8, 0, 0, 0),
                BorderStyle = BorderStyle.Fixed3D,
                BackColor   = ThemeToolbar,
                ForeColor   = ThemeTextMuted,
                Text        = "準備中"
            };

            // -----------------------------------------------------
            // Tabs
            // -----------------------------------------------------
            _tabs = new TabControl { Dock = DockStyle.Fill };

            _grid = new DataGridView {
                Dock                  = DockStyle.Fill,
                ReadOnly              = true,
                AllowUserToAddRows    = false,
                AllowUserToDeleteRows = false,
                SelectionMode         = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible     = false,
                BackgroundColor       = System.Drawing.Color.White,
                AutoSizeColumnsMode   = DataGridViewAutoSizeColumnsMode.Fill,
                AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle {
                    BackColor = ThemeAccentLite
                }
            };
            var pageFindings = new TabPage("掃描結果");
            pageFindings.BackColor = ThemePanel;
            pageFindings.Controls.Add(_grid);
            _tabs.TabPages.Add(pageFindings);

            _txtEnv = new TextBox {
                Dock       = DockStyle.Fill,
                Multiline  = true,
                ReadOnly   = true,
                Font       = new System.Drawing.Font("Consolas", 10),
                ScrollBars = ScrollBars.Both,
                WordWrap   = false,
                BackColor  = ThemePanel,
                ForeColor  = System.Drawing.Color.FromArgb(34, 42, 53)
            };
            var pageEnv = new TabPage("環境變數");
            pageEnv.BackColor = ThemePanel;
            pageEnv.Controls.Add(_txtEnv);
            _tabs.TabPages.Add(pageEnv);

            _txtCred = new TextBox {
                Dock       = DockStyle.Fill,
                Multiline  = true,
                ReadOnly   = true,
                Font       = new System.Drawing.Font("Consolas", 10),
                ScrollBars = ScrollBars.Both,
                WordWrap   = false,
                BackColor  = ThemePanel,
                ForeColor  = System.Drawing.Color.FromArgb(34, 42, 53)
            };
            var pageCred = new TabPage("認證目標");
            pageCred.BackColor = ThemePanel;
            pageCred.Controls.Add(_txtCred);
            _tabs.TabPages.Add(pageCred);

            _tabs.BackColor = ThemeBg;

            Controls.Add(_tabs);
            Controls.Add(_lblSummary);
            Controls.Add(_txtDesc);
            Controls.Add(toolbar);

            // -----------------------------------------------------
            // Events
            // -----------------------------------------------------
            _btnRun.Click           += (s, e) => RunAudit();
            _btnExport.Click        += (s, e) => ExportCsv();
            // _langBtn.Click is wired inside the panel setup (line ~136) because
            // the click target is now a Panel containing a PictureBox.
            _grid.SelectionChanged  += (s, e) => UpdateDescription();

            Load += (s, e) => {
                RunAudit();
                UpdateDescription();
            };
        }

        private static Button CreateThemeButton(string text)
        {
            return new Button {
                Text      = text,
                Width     = 100,
                Height    = 30,
                FlatStyle = FlatStyle.Flat,
                BackColor = ThemePanel,
                ForeColor = ThemeAccent
            };
        }

        // ---------------------------------------------------------
        // Audit logic
        // ---------------------------------------------------------

        private void RunAudit()
        {
            _btnRun.Enabled = false;
            _lblSummary.Text = "執行稽核中...";
            Application.DoEvents();

            try
            {
                _findings     = new List<Finding>();
                _cmdkeyOutput = RunCmdkey();

                CheckProvider("DeepSeek",
                    new[] { "api.deepseek.com" },
                    new[] { "DEEPSEEK_API_KEY" });
                CheckProvider("OpenAI",
                    new[] { "api.openai.com" },
                    new[] { "OPENAI_API_KEY" });
                CheckProvider("Anthropic",
                    new[] { "api.anthropic.com" },
                    new[] { "ANTHROPIC_API_KEY" });
                CheckProvider("Google Gemini",
                    new[] { "generativelanguage.googleapis.com" },
                    new[] { "GEMINI_API_KEY", "GOOGLE_AI_API_KEY", "GOOGLE_API_KEY" });
                CheckProvider("MiniMax",
                    new[] { "api.minimax" },
                    new[] { "MINIMAX_API_KEY" });
                CheckProvider("Mistral",
                    new[] { "api.mistral.ai" },
                    new[] { "MISTRAL_API_KEY" });
                CheckProvider("xAI",
                    new[] { "api.x.ai" },
                    new[] { "XAI_API_KEY" });
                CheckProvider("OpenRouter",
                    new[] { "openrouter.ai" },
                    new[] { "OPENROUTER_API_KEY" });
                CheckProvider("OpenCode",
                    new[] { "opencode" },
                    new[] { "OPENCODE_API_KEY" });
                CheckProvider("Vercel AI Gateway",
                    new[] { "vercel" },
                    new[] { "VERCEL_AI_GATEWAY_API_KEY" });
                CheckProvider("Ollama",
                    new[] { "ollama" },
                    new[] { "OLLAMA_API_KEY" });
                CheckProvider("LM Studio",
                    new[] { "lmstudio", "lm-studio" },
                    new[] { "LMSTUDIO_API_KEY" });

                if (_cmdkeyOutput.Contains("zed:url=https://zed.dev"))
                {
                    AddFinding("Zed", "Zed Account", "[LOCAL]",
                        "Windows Credential Manager",
                        "zed:url=https://zed.dev");
                }

                string userProfile  = Environment.GetEnvironmentVariable("USERPROFILE")  ?? "";
                string appData      = Environment.GetEnvironmentVariable("APPDATA")      ?? "";
                string localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA") ?? "";

                CheckAgent("Codex",
                    new[] {
                        Path.Combine(userProfile, ".codex"),
                        Path.Combine(appData, "codex"),
                        Path.Combine(localAppData, "codex")
                    },
                    new[] { "OPENAI_API_KEY", "CODEX_API_KEY" });

                CheckAgent("Claude / Claude Code",
                    new[] {
                        Path.Combine(userProfile, ".claude"),
                        Path.Combine(appData, "Claude"),
                        localAppData + "\\Claude"
                    },
                    new[] { "ANTHROPIC_API_KEY" });

                CheckAgent("Cline",
                    new[] {
                        Path.Combine(appData, "Code", "User", "globalStorage", "saoudrizwan.claude-dev")
                    },
                    new[] { "ANTHROPIC_API_KEY", "OPENAI_API_KEY" });

                CheckAgent("Gemini CLI",
                    new[] {
                        Path.Combine(userProfile, ".gemini"),
                        Path.Combine(appData, "gemini"),
                        localAppData + "\\gemini"
                    },
                    new[] { "GEMINI_API_KEY", "GOOGLE_API_KEY", "GOOGLE_AI_API_KEY" });

                CheckAgent("OpenCode",
                    new[] {
                        Path.Combine(userProfile, ".config", "opencode"),
                        Path.Combine(appData, "opencode"),
                        localAppData + "\\opencode"
                    },
                    new[] { "OPENCODE_API_KEY" });

                CheckAgent("Pi Coding Agent",
                    new[] {
                        Path.Combine(userProfile, ".pi"),
                        Path.Combine(userProfile, ".config", "pi"),
                        Path.Combine(appData, "pi")
                    },
                    Array.Empty<string>());

                UpdateGrid();
                UpdateEnvBox();
                UpdateCredBox();

                // Special row: API lifecycle tutorial
                AddFinding("Tutorial", "API Lifecycle (register to use)",
                           "[GUIDE]", "Click for details",
                           "End-to-end flow: sign up, create key, store, use");

                int localCount = _findings.Count(f => f.Status == "[LOCAL]");
                int envCount   = _findings.Count(f => f.Status == "[ENV]");
                _lblSummary.Text =
                    $"本機：{localCount}    環境變數：{envCount}    總計：{_findings.Count}    " +
                    $"上次執行：{DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            }
            finally
            {
                _btnRun.Enabled = true;
            }
        }

        private void CheckProvider(string name, string[] credentialPatterns, string[] envVars)
        {
            if (!string.IsNullOrEmpty(_cmdkeyOutput))
            {
                var matches = new List<string>();
                foreach (var raw in _cmdkeyOutput.Split('\n'))
                {
                    var line = raw.TrimEnd('\r', '\n');
                    foreach (var p in credentialPatterns)
                    {
                        if (line.Contains(p))
                        {
                            matches.Add(line.Trim());
                            break;
                        }
                    }
                }
                if (matches.Count > 0)
                {
                    var uniq = matches.Distinct().OrderBy(s => s).ToList();
                    AddFinding("LLM Provider", name, "[LOCAL]",
                        "Windows Credential Manager",
                        string.Join(" ; ", uniq));
                }
            }

            foreach (var v in envVars)
            {
                if (Environment.GetEnvironmentVariable(v) != null)
                {
                    AddFinding("LLM Provider", name, "[ENV]",
                        "Environment Variable", v);
                }
            }
        }

        private void CheckAgent(string name, string[] paths, string[] envVars)
        {
            var found = paths.Where(Directory.Exists).ToList();
            if (found.Count > 0)
            {
                AddFinding("ACP Agent", name, "[LOCAL]",
                    "Local config directory",
                    string.Join(" ; ", found));
            }

            foreach (var v in envVars)
            {
                if (Environment.GetEnvironmentVariable(v) != null)
                {
                    AddFinding("ACP Agent", name, "[ENV]",
                        "Environment Variable", v);
                }
            }
        }

        private void AddFinding(string category, string name, string status,
                                string location, string detail)
        {
            _findings.Add(new Finding {
                Category = category,
                Name     = name,
                Status   = status,
                Location = location,
                Detail   = detail
            });
        }

        private string RunCmdkey()
        {
            try
            {
                var psi = new ProcessStartInfo("cmdkey", "/list") {
                    RedirectStandardOutput = true,
                    RedirectStandardError  = true,
                    UseShellExecute        = false,
                    CreateNoWindow         = true
                    // Note: cmdkey outputs in the system codepage (Big5 on
                    // Chinese Windows), NOT UTF-8. Reading the raw bytes
                    // and decoding as Big5 avoids the "[]" mojibake.
                };
                using var p = Process.Start(psi);
                using var ms = new MemoryStream();
                p.StandardOutput.BaseStream.CopyTo(ms);
                var bytes = ms.ToArray();
                return Encoding.GetEncoding(950).GetString(bytes);
            }
            catch
            {
                return "";
            }
        }

        // ---------------------------------------------------------
        // UI updates
        // ---------------------------------------------------------

        private void UpdateGrid()
        {
            var dt = new DataTable();
            dt.Columns.Add("Status",   typeof(string));
            dt.Columns.Add("Category", typeof(string));
            dt.Columns.Add("Name",     typeof(string));
            dt.Columns.Add("Location", typeof(string));
            dt.Columns.Add("Detail",   typeof(string));

            var sorted = _findings
                .OrderBy(f => f.Status == "[LOCAL]" ? 0 : f.Status == "[ENV]" ? 1 : 2)
                .ThenBy(f => f.Category)
                .ThenBy(f => f.Name);

            foreach (var f in sorted)
                dt.Rows.Add(f.Status, f.Category, f.Name, f.Location, f.Detail);

            _grid.DataSource = dt;

            _grid.Columns["Status"].Width   = 70;
            _grid.Columns["Category"].Width = 110;
            _grid.Columns["Name"].Width     = 180;
            _grid.Columns["Location"].Width = 200;
            _grid.Columns["Detail"].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;

            foreach (DataGridViewRow row in _grid.Rows)
            {
                string status = row.Cells["Status"].Value?.ToString() ?? "";
                if (status == "[LOCAL]")
                    row.Cells["Status"].Style.ForeColor = System.Drawing.Color.FromArgb(0, 120, 50);
                else if (status == "[ENV]")
                    row.Cells["Status"].Style.ForeColor = System.Drawing.Color.FromArgb(180, 100, 0);
                row.Cells["Status"].Style.Font = new System.Drawing.Font(Font, System.Drawing.FontStyle.Bold);
            }
        }

        private void UpdateEnvBox()
        {
            var vars = new[] {
                "OPENAI_API_KEY", "ANTHROPIC_API_KEY", "DEEPSEEK_API_KEY",
                "GEMINI_API_KEY", "GOOGLE_AI_API_KEY", "GOOGLE_API_KEY",
                "MINIMAX_API_KEY", "MISTRAL_API_KEY", "XAI_API_KEY",
                "OPENROUTER_API_KEY", "OPENCODE_API_KEY",
                "VERCEL_AI_GATEWAY_API_KEY", "OLLAMA_API_KEY", "LMSTUDIO_API_KEY",
                "CODEX_API_KEY"
            };

            var sb = new StringBuilder();
            int count = 0;
            foreach (var v in vars)
            {
                var val = Environment.GetEnvironmentVariable(v);
                if (!string.IsNullOrEmpty(val))
                {
                    sb.AppendLine($"  [ENV]  {v,-32}  length: {val.Length}");
                    count++;
                }
            }
            if (count == 0)
                sb.AppendLine("  (none detected)");

            _txtEnv.Text = sb.ToString();
        }

        private void UpdateCredBox()
        {
            if (string.IsNullOrEmpty(_cmdkeyOutput))
            {
                _txtCred.Text = "[no output from cmdkey /list]";
                return;
            }

            var sb = new StringBuilder();
            bool inZedBlock = false;
            foreach (var raw in _cmdkeyOutput.Split('\n'))
            {
                var line = raw.TrimEnd('\r', '\n');
                if (line.StartsWith("Target:"))
                    inZedBlock = line.Contains("zed:");
                if (inZedBlock)
                    sb.AppendLine("  " + line);
                if (line.Contains("Local machine persistence"))
                {
                    inZedBlock = false;
                    sb.AppendLine();
                }
            }
            _txtCred.Text = sb.ToString();
        }

        // ---------------------------------------------------------
        // Description panel (row click)
        // ---------------------------------------------------------

        private void ToggleLang()
        {
            _lang = (_lang == Lang.English) ? Lang.Chinese : Lang.English;
            // Swap the flag bitmap (rendered from embedded SVG).  Both flags are
            // standard 256x256 icon-sized, so the button stays square & consistent.
            _langBtn.FlagBitmap = (_lang == Lang.Chinese)
                ? LoadFlag(true)    // TW from tw.svg
                : LoadFlag(false);  // US from us.svg
            _langBtn.Invalidate();
            UpdateDescription();
        }

        private void UpdateDescription()
        {
            if (_grid.SelectedRows.Count == 0)
            {
    	            _txtDesc.Text = (_lang == Lang.English)
                    ? "Click a row above to see details.\r\n\r\nAPI Write: Register -> Get Key -> Paste into Zed Provider UI -> Zed encrypts & stores in Credential Manager (DPAPI)\r\nAPI Usage: Zed reads key from Credential Manager -> Sends key to LLM API endpoint on each request"
                    : "點選上列以查看詳情。\r\n\r\nAPI 寫入：註冊 → 取得 Key → 貼入 Zed Provider UI → Zed 加密存入認證管理員 (DPAPI)\r\nAPI 使用：Zed 從認證管理員讀取 Key → 每次請求時送至 LLM API 端點";
                return;
            }

            var row = _grid.SelectedRows[0];
            var f = new Finding {
                Status   = row.Cells["Status"].Value?.ToString()   ?? "",
                Category = row.Cells["Category"].Value?.ToString() ?? "",
                Name     = row.Cells["Name"].Value?.ToString()     ?? "",
                Location = row.Cells["Location"].Value?.ToString() ?? "",
                Detail   = row.Cells["Detail"].Value?.ToString()   ?? ""
            };

            var baseText = (_lang == Lang.English)
                ? GetEnglishDescription(f)
                : GetChineseDescription(f);
            _txtDesc.Text = baseText + "\r\n\r\n" + ((_lang == Lang.English)
                    ? "--- API Write / Usage ---\r\nWrite: Register -> Get Key -> Paste into Zed -> Stored in Credential Manager (DPAPI)\r\nUsage: Zed reads key from Credential Manager -> Sends key to LLM API endpoint on each request"
                    : "--- API 寫入 / 使用 ---\r\n寫入：註冊 → 取得 Key → 貼入 Zed → 加密存於認證管理員 (DPAPI)\r\n使用：Zed 讀取 Key → 每次請求送至 LLM API 端點");
        }

        private string GetEnglishDescription(Finding f)
        {
            // ====== Special: API Lifecycle tutorial ======
            if (f.Name.StartsWith("API Lifecycle"))
            {
                return "API Lifecycle (register -> use)\n\n" +
                       "1. Sign up\n" +
                       "   - Go to the LLM provider website (e.g. platform.deepseek.com)\n" +
                       "   - Verify email / phone\n\n" +
                       "2. Add payment (paid API)\n" +
                       "   - Top up or link a credit card on the Billing page\n" +
                       "   - Many providers give a free tier on signup\n\n" +
                       "3. Create an API Key\n" +
                       "   - On the API Keys page, click 'Create new key'\n" +
                       "   - Set permissions and rate limits\n" +
                       "   - Copy the full sk-xxx string (shown only once)\n\n" +
                       "4. Store locally\n" +
                       "   - Secure path: paste into Zed's LLM Provider UI -> saved to Windows Credential Manager (DPAPI)\n" +
                       "   - Easier path: save to an environment variable (plaintext)\n" +
                       "   - This tool scans and reports which providers have keys on this machine\n\n" +
                       "5. Use\n" +
                       "   - Zed loads the key on startup and calls the LLM API\n" +
                       "   - Click any row above to see its local-storage status\n\n" +
                       "6. Monitor and rotate\n" +
                       "   - Periodically check the provider's usage dashboard\n" +
                       "   - Rotate the key immediately on suspicious activity\n" +
                       "   - Enable 2FA on important accounts";
            }

            // ====== [LOCAL] stored in Windows Credential Manager (DPAPI) ======
            if (f.Status == "[LOCAL]" && f.Location == "Windows Credential Manager")
            {
                if (f.Name == "DeepSeek")
                    return "DeepSeek API Key\n" +
                           "- Set via Zed built-in Provider UI\n" +
                           "- Stored in Windows Credential Manager\n" +
                           "- Protected with DPAPI encryption (recommended)";
                if (f.Name == "Google Gemini")
                    return "Google Gemini API Key\n" +
                           "- Set via Zed built-in Provider UI\n" +
                           "- Stored in Windows Credential Manager\n" +
                           "- Protected with DPAPI encryption (recommended)";
                if (f.Name == "MiniMax")
                    return "MiniMax API Key\n" +
                           "- Set via Zed built-in Provider UI\n" +
                           "- Stored in Windows Credential Manager\n" +
                           "- Protected with DPAPI encryption (recommended)";
                if (f.Name == "Zed Account")
                    return "Zed account login token\n" +
                           "- Synced with Zed cloud (cloud.zed.dev)\n" +
                           "- Available across devices\n" +
                           "- Re-login may be required after device changes";
                return $"{f.Name} key is stored in Windows Credential Manager with DPAPI encryption.";
            }

            // ====== [ENV] environment variable ======
            if (f.Status == "[ENV]")
            {
                if (f.Name == "MiniMax")
                    return "MINIMAX_API_KEY environment variable\n" +
                           "- Stored in plaintext under HKCU\\Environment\n" +
                           "- Readable by user-level processes\n" +
                           "- Mapped from settings.json via {env:MINIMAX_API_KEY}\n" +
                           "- Suggestion: remove if Credential Manager copy already exists";
                if (f.Name == "Codex")
                    return "CODEX_API_KEY environment variable\n" +
                           "- Stored in plaintext in registry\n" +
                           "- Backup API key path for OpenAI\n" +
                           "- Codex CLI mainly uses OAuth (auth.json)\n" +
                           "- Suggestion: remove if OAuth-only workflow is used";
                return $"{f.Name} environment variable is stored in plaintext in registry.";
            }

            // ====== [LOCAL] local config directory ======
            if (f.Status == "[LOCAL]" && f.Location == "Local config directory")
            {
                if (f.Name == "Codex")
                    return "Codex CLI is installed and authenticated\n" +
                           "- Config directory: ~/.codex\n" +
                           "- Auth: ~/.codex/auth.json (ChatGPT OAuth)\n" +
                           "- Usage: launched as an ACP external agent";
                if (f.Name == "Claude / Claude Code")
                    return "Claude Code is installed\n" +
                           "- Config directory: ~/.claude\n" +
                           "- Auth: managed by Claude Code (OS Keychain or config file)";
                if (f.Name == "Cline")
                    return "Cline VS Code extension is installed\n" +
                           "- Storage: %APPDATA%\\Code\\User\\globalStorage\\saoudrizwan.claude-dev\n" +
                           "- Auth: typically uses Anthropic or OpenAI API credentials";
                if (f.Name == "Gemini CLI")
                    return "Google Gemini CLI is installed\n" +
                           "- Config directory: ~/.gemini\n" +
                           "- Auth: ~/.gemini/oauth_creds.json (Google OAuth)";
                if (f.Name == "OpenCode")
                    return "OpenCode (sst/opencode) is installed\n" +
                           "- Config directory: ~/.config/opencode";
                if (f.Name == "Pi Coding Agent")
                    return "Pi Coding Agent is installed\n" +
                           "- Config directory: ~/.pi";
                return $"Local config directory exists for {f.Name}.";
            }

            var sb = new StringBuilder();
            sb.AppendLine($"Status    : {f.Status}");
            sb.AppendLine($"Category  : {f.Category}");
            sb.AppendLine($"Name      : {f.Name}");
            sb.AppendLine($"Location  : {f.Location}");
            sb.AppendLine($"Detail    : {f.Detail}");
            return sb.ToString();
        }

        private string GetChineseDescription(Finding f)
        {
            // ====== Special: API Lifecycle tutorial ======
            if (f.Name.StartsWith("API Lifecycle"))
            {
                return "API 完整生命週期（註冊 → 使用）\n\n" +
                       "1. 註冊帳號\n" +
                       "   - 到 LLM Provider 官方網站（如 platform.deepseek.com）註冊\n" +
                       "   - 完成 email / 手機驗證\n\n" +
                       "2. 儲值（付費 API）\n" +
                       "   - 到 Billing 頁面儲值或綁定信用卡\n" +
                       "   - 部分 provider 註冊送免費額度\n\n" +
                       "3. 產生 API Key\n" +
                       "   - 到 API Keys 頁面 Create new key\n" +
                       "   - 設定 Key 權限與額度上限\n" +
                       "   - 複製 sk-xxx 完整字串（僅顯示一次）\n\n" +
                       "4. 儲存到本機\n" +
                       "   - 安全方案：在 Zed 的 LLM Provider UI 貼上 → 存到 Windows Credential Manager（DPAPI 加密）\n" +
                       "   - 簡易方案：存到環境變數（明文）\n" +
                       "   - 本工具可掃描並顯示本機有哪些 provider 的 Key 存在\n\n" +
                       "5. 使用\n" +
                       "   - Zed 啟動時自動載入 Key，呼叫 LLM API\n" +
                       "   - 點選上方 row 可看到本機儲存狀態\n\n" +
                       "6. 監控與管理\n" +
                       "   - 定期到 Provider 後台查看 usage、log\n" +
                       "   - 發現異常用量立即 Rotate Key\n" +
                       "   - 重要帳號啟用 2FA";
            }
            // ====== [LOCAL] stored in Windows Credential Manager (DPAPI) ======
            if (f.Status == "[LOCAL]" && f.Location == "Windows Credential Manager")
            {
                if (f.Name == "DeepSeek")
                    return "DeepSeek API Key\n" +
                           "• 透過 Zed 內建 Provider UI 設定\n" +
                           "• 儲存於 Windows 認證管理員\n" +
                           "• 受 DPAPI 加密（最安全）";
                if (f.Name == "Google Gemini")
                    return "Google Gemini API Key\n" +
                           "• 透過 Zed 內建 Provider UI 設定\n" +
                           "• 儲存於 Windows 認證管理員\n" +
                           "• 受 DPAPI 加密（最安全）";
                if (f.Name == "MiniMax")
                    return "MiniMax API Key\n" +
                           "• 透過 Zed 內建 Provider UI 設定\n" +
                           "• 儲存於 Windows 認證管理員\n" +
                           "• 受 DPAPI 加密（最安全）";
                if (f.Name == "Zed Account")
                    return "Zed 帳號登入 Token\n" +
                           "• 與 Zed 雲端（cloud.zed.dev）同步\n" +
                           "• 跨機器可用\n" +
                           "• 變動裝置後需重新登入";
                return $"{f.Name} Key 以 DPAPI 加密儲存於 Windows 認證管理員。";
            }

            // ====== [ENV] environment variable ======
            if (f.Status == "[ENV]")
            {
                if (f.Name == "MiniMax")
                    return "MINIMAX_API_KEY 環境變數\n" +
                           "• 明文儲存於登錄檔 HKCU\\Environment\n" +
                           "• 任何跑在使用者帳號下的程式都能讀\n" +
                           "• 對應 settings.json 內的 {env:MINIMAX_API_KEY}\n" +
                           "• 建議：Credential Manager 已有副本，可移除";
                if (f.Name == "Codex")
                    return "CODEX_API_KEY 環境變數\n" +
                           "• 明文儲存於登錄檔\n" +
                           "• 對 OpenAI 的備用 API Key\n" +
                           "• 目前 Codex CLI 走 OAuth（auth.json）為主\n" +
                           "• 建議：如僅用 OAuth，可移除";
                return $"{f.Name} 環境變數 — 明文儲存於登錄檔";
            }

            // ====== [LOCAL] local config directory ======
            if (f.Status == "[LOCAL]" && f.Location == "Local config directory")
            {
                if (f.Name == "Codex")
                    return "Codex CLI 已安裝並登入\n" +
                           "• 設定目錄：~/.codex\n" +
                           "• 認證：~/.codex/auth.json (OAuth ChatGPT)\n" +
                           "• 使用方式：ACP 啟動外部程式";
                if (f.Name == "Claude / Claude Code")
                    return "Claude Code 已安裝\n" +
                           "• 設定目錄：~/.claude\n" +
                           "• 認證：依該程式設定（OS Keychain 或設定檔）";
                if (f.Name == "Cline")
                    return "Cline VS Code 擴充已安裝\n" +
                           "• 儲存位置：%APPDATA%\\Code\\User\\globalStorage\\saoudrizwan.claude-dev\n" +
                           "• 認證：通常使用 Anthropic 或 OpenAI API 憑證";
                if (f.Name == "Gemini CLI")
                    return "Google Gemini CLI 已安裝\n" +
                           "• 設定目錄：~/.gemini\n" +
                           "• 認證：~/.gemini/oauth_creds.json (Google OAuth)";
                if (f.Name == "OpenCode")
                    return "OpenCode (sst/opencode) 已安裝\n" +
                           "• 設定目錄：~/.config/opencode";
                if (f.Name == "Pi Coding Agent")
                    return "Pi Coding Agent 已安裝\n" +
                           "• 設定目錄：~/.pi";
                return $"{f.Name} 本機設定目錄存在";
            }

            // ====== fallback ======
            return $"狀態：{f.Status}\n" +
                   $"類別：{f.Category}\n" +
                   $"名稱：{f.Name}\n" +
                   $"位置：{f.Location}\n" +
                   $"詳細：{f.Detail}";
        }

        // ---------------------------------------------------------
        // Export
        // ---------------------------------------------------------

        private void ExportCsv()
        {
            if (_findings.Count == 0)
            {
                MessageBox.Show("No findings to export. Run the audit first.",
                    "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var sfd = new SaveFileDialog {
                Filter    = "CSV file (*.csv)|*.csv|All files (*.*)|*.*",
                FileName  = $"ZedCredentialAudit_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
                Title     = "Export findings to CSV"
            };
            if (sfd.ShowDialog(this) != DialogResult.OK) return;

            try
            {
                using var w = new StreamWriter(sfd.FileName, false, Encoding.UTF8);
                w.WriteLine($"Status,Category,Name,Location,Detail");
                foreach (var f in _findings
                                  .OrderBy(f => f.Status)
                                  .ThenBy(f => f.Category)
                                  .ThenBy(f => f.Name))
                {
                    w.WriteLine($"\"{Esc(f.Status)}\",\"{Esc(f.Category)}\",\"{Esc(f.Name)}\",\"{Esc(f.Location)}\",\"{Esc(f.Detail)}\"");
                }
                MessageBox.Show(this,
                    $"Saved {_findings.Count} entries to:\n{sfd.FileName}",
                    "Export complete",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this,
                    $"Export failed:\n{ex.Message}",
                    "Export error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
        }

        private static string Esc(string s) => (s ?? "").Replace("\"", "\"\"");

        // ---------------------------------------------------------
        // Entry point
        // ---------------------------------------------------------

        // ---------------------------------------------------------
        // Application icon — rendered from embedded Resources/logo.svg via Svg.Skia
        // (Font Awesome 7 key-logo).  Falls back to the painted icon if the SVG
        // resource is missing or fails to render.
        // ---------------------------------------------------------
        private Icon LoadAppIcon()
        {
            // 1) Prefer the embedded logo.svg rendered at standard icon size.
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var s = asm.GetManifestResourceStream("ZedCredentialAuditGui.Resources.logo.svg");
                if (s != null)
                {
                    using var bmp = RenderSvgToBitmap(s, AppIconSize);
                    _appIconHandle = bmp.GetHicon();
                    return Icon.FromHandle(_appIconHandle);
                }
            }
            catch { /* fall through */ }

            // 2) Legacy fallback: api.png near the executable, if luminance is OK.
            try
            {
                string[] candidates = {
                    Path.Combine(Application.StartupPath, "Assets", "api.png"),
                    Path.Combine(Application.StartupPath, "api.png"),
                    Path.Combine(Application.StartupPath, "..", "..", "..", "..", "Assets", "api.png"),
                    Path.Combine(Application.StartupPath, "..", "..", "..", "Assets", "api.png"),
                    Path.Combine(Application.StartupPath, "..", "..", "Assets", "api.png"),
                };
                foreach (var p in candidates)
                {
                    if (File.Exists(p))
                    {
                        using var bmp = new Bitmap(p);
                        if (AverageLuminance(bmp) >= 30)
                        {
                            _appIconHandle = bmp.GetHicon();
                            return Icon.FromHandle(_appIconHandle);
                        }
                        break;
                    }
                }
            }
            catch { /* fall through to fallback */ }
            return BuildFallbackIcon();
        }

        private static double AverageLuminance(Bitmap bmp)
        {
            // Sample every 8th pixel for speed on a 512x512 source.
            long sum = 0;
            int n = 0;
            for (int y = 0; y < bmp.Height; y += 8)
                for (int x = 0; x < bmp.Width; x += 8)
                {
                    var c = bmp.GetPixel(x, y);
                    // Rec. 601 luma
                    sum += (int)(0.299 * c.R + 0.587 * c.G + 0.114 * c.B);
                    n++;
                }
            return n == 0 ? 0 : (double)sum / n;
        }

        private Icon BuildFallbackIcon()
        {
            const int size = 64;
            using var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.AntiAlias;

                // Blue rounded background
                using var bg = new SolidBrush(System.Drawing.Color.FromArgb(44, 106, 207));
                g.FillRectangle(bg, 0, 0, size, size);

                // "API" label
                using var font = new System.Drawing.Font("Segoe UI", 20, System.Drawing.FontStyle.Bold);
                using var fg = new SolidBrush(System.Drawing.Color.White);
                var sz = g.MeasureString("API", font);
                g.DrawString("API", font, fg, (size - sz.Width) / 2, 6);

                // Tiny key at bottom-right (head circle + shaft + teeth)
                using var pen = new System.Drawing.Pen(System.Drawing.Color.White, 2);
                int kx = size - 18, ky = size - 14;
                g.DrawEllipse(pen, kx, ky, 8, 8);
                g.DrawLine(pen, kx + 8, ky + 4, kx + 16, ky + 4);
                g.DrawLine(pen, kx + 13, ky + 4, kx + 13, ky + 7);
                g.DrawLine(pen, kx + 16, ky + 4, kx + 16, ky + 7);
            }
            _appIconHandle = bmp.GetHicon();
            return Icon.FromHandle(_appIconHandle);
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            base.OnFormClosed(e);
            if (_appIconHandle != IntPtr.Zero)
            {
                DestroyIcon(_appIconHandle);
                _appIconHandle = IntPtr.Zero;
            }
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        // ---------------------------------------------------------
        // National flag — renders the embedded SVG (tw.svg / us.svg) into a
        // standard icon-sized System.Drawing.Bitmap via Svg.Skia.
        //
        // The returned bitmap is always `iconSize` x `iconSize` (e.g. 256x256),
        // letterbox-fitted to the SVG's intrinsic 4:3 viewBox.  The toggle button
        // is a square (1:1) so this shape keeps aspect-correct without distortion.
        // ---------------------------------------------------------
        private const int FlagIconSize = 256;   // standard icon size for the language flag button
        private const int AppIconSize  = 256;   // standard icon size for the Form.Icon

        private static Bitmap LoadFlag(bool isChinese, int targetW = 0, int targetH = 0)
        {
            string resName = isChinese ? "ZedCredentialAuditGui.Resources.tw.svg"
                                       : "ZedCredentialAuditGui.Resources.us.svg";
            int size = (targetW > 0 && targetH > 0) ? Math.Min(targetW, targetH) : FlagIconSize;
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                using var s = asm.GetManifestResourceStream(resName);
                if (s != null)
                {
                    return RenderSvgToBitmap(s, size);
                }
            }
            catch { /* fall through */ }
            // No SVG embedded — shouldn't happen, but render a grey placeholder
            // so the button still draws something.
            var bmp = new Bitmap(size, size);
            using (var g = Graphics.FromImage(bmp))
                g.Clear(Color.LightGray);
            return bmp;
        }

        // Renders an SVG stream to a square System.Drawing.Bitmap of `size`x`size`
        // pixels using Svg.Skia + SkiaSharp.  The SVG is letterbox-fitted into the
        // square canvas so its intrinsic aspect ratio is preserved.
        private static Bitmap RenderSvgToBitmap(Stream svgStream, int size)
        {
            // Materialize stream into a byte array because SKSvg needs a seekable
            // Stream (FileStream is seekable; ManifestResourceStream may not be).
            byte[] svgBytes;
            using (var ms = new MemoryStream())
            {
                svgStream.CopyTo(ms);
                svgBytes = ms.ToArray();
            }
            using var svg = new SKSvg();
            using var ms2 = new MemoryStream(svgBytes);
            SKPicture picture = svg.Load(ms2);
            if (picture == null)
                throw new InvalidOperationException("Svg.Skia returned null picture");

            var cull = picture.CullRect;
            float srcW = cull.Width;
            float srcH = cull.Height;
            if (srcW <= 0) srcW = 640;
            if (srcH <= 0) srcH = 480;

            // Letterbox-fit into size x size.
            float sa = srcW / srcH;
            int drawW, drawH;
            if (sa >= 1f) { drawW = size; drawH = (int)(size / sa); }
            else          { drawH = size; drawW = (int)(size * sa); }
            int offX = (size - drawW) / 2;
            int offY = (size - drawH) / 2;

            using var skBmp = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
            using (var skCanvas = new SKCanvas(skBmp))
            {
                skCanvas.Clear(SKColors.Transparent);
                float scaleX = (float)drawW / srcW;
                float scaleY = (float)drawH / srcH;
                var matrix = SKMatrix.CreateTranslation(offX, offY);
                matrix = matrix.PostConcat(SKMatrix.CreateScale(scaleX, scaleY));
                skCanvas.SetMatrix(matrix);
                skCanvas.DrawPicture(picture);
                skCanvas.ResetMatrix();
                skCanvas.Flush();
            }
            return SkBitmapToBitmap(skBmp);
        }

        // Convert SKBitmap (RGBA8888 premultiplied) into a System.Drawing.Bitmap.
        private static Bitmap SkBitmapToBitmap(SKBitmap skBmp)
        {
            // PNG-encode then decode is the simplest cross-platform path; preserves
            // alpha without manual pixel manipulation.
            using var img   = SKImage.FromBitmap(skBmp);
            using var data  = img.Encode(SKEncodedImageFormat.Png, 100);
            using var ms    = new MemoryStream();
            data.SaveTo(ms);
            ms.Position = 0;
            return new Bitmap(ms);
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    // ---------------------------------------------------------
    // Custom button that draws a flag Bitmap centered with correct aspect ratio
    // (BackgroundImageLayout.Zoom is not honoured by Button on all frameworks,
    // so we paint it ourselves).
    // ---------------------------------------------------------
    public class FlagToggleButton : Button
    {
        public Bitmap FlagBitmap { get; set; }

        public FlagToggleButton()
        {
            FlatStyle  = FlatStyle.Flat;
            FlatAppearance.BorderSize = 0;
            Cursor     = Cursors.Hand;
            TabStop    = false;
            SetStyle(ControlStyles.UserPaint
                   | ControlStyles.AllPaintingInWmPaint
                   | ControlStyles.OptimizedDoubleBuffer
                   | ControlStyles.ResizeRedraw
                   | ControlStyles.SupportsTransparentBackColor, true);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;

            // Optional rounded background
            using (var bg = new SolidBrush(BackColor))
                g.FillRectangle(bg, ClientRectangle);

            if (FlagBitmap == null) return;

            // Both bitmap (256x256) and button are square, so a 1:1 fill is fine.
            // We still use aspect-fit code in case the user resizes the button.
            float srcAspect = (float)FlagBitmap.Width / FlagBitmap.Height;
            float dstAspect = (float)ClientRectangle.Width / ClientRectangle.Height;
            int drawW, drawH, offX, offY;
            if (srcAspect > dstAspect) { drawW = ClientRectangle.Width; drawH = (int)(ClientRectangle.Width / srcAspect); offX = ClientRectangle.Left; offY = ClientRectangle.Top + (ClientRectangle.Height - drawH) / 2; }
            else { drawH = ClientRectangle.Height; drawW = (int)(ClientRectangle.Height * srcAspect); offY = ClientRectangle.Top; offX = ClientRectangle.Left + (ClientRectangle.Width - drawW) / 2; }

            g.DrawImage(FlagBitmap, offX, offY, drawW, drawH);
        }
    }
}
