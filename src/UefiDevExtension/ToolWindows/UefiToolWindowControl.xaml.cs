using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using Microsoft.Win32;
using UefiDevExtension.Options;
using UefiDevExtension.Services;

namespace UefiDevExtension.ToolWindows
{
    public partial class UefiToolWindowControl : UserControl
    {
        private const double TwoColumnMinWidth = 760;
        private const string CopyGlyph = "\uE8C8";
        private const string CopiedGlyph = "\uE73E";

        private enum StatusKind { Success, Warning, Error }

        private Guid _currentGuid = Guid.NewGuid();
        private bool _isInitializing = true;
        private bool _guidGenerated;
        private bool? _isTwoColumn;

        public UefiToolWindowControl()
        {
            InitializeComponent();
            Loaded += UefiToolWindowControl_Loaded;
            Unloaded += UefiToolWindowControl_Unloaded;
            SizeChanged += (_, __) => UpdateLayoutMode();
        }

        private void UefiToolWindowControl_Loaded(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _isInitializing = true;
            LoadOptions();
            if (!_guidGenerated)
            {
                GenerateNewGuid();
                _guidGenerated = true;
            }
            RefreshDiagnostics();
            _isInitializing = false;

            UefiOptionsPage.SettingsChanged -= OnSettingsChanged;
            UefiOptionsPage.SettingsChanged += OnSettingsChanged;
        }

        private void UefiToolWindowControl_Unloaded(object sender, RoutedEventArgs e)
        {
            UefiOptionsPage.SettingsChanged -= OnSettingsChanged;
        }

        /// <summary>Keeps the panel in sync when settings are changed in Tools > Options.</summary>
        private void OnSettingsChanged(object sender, EventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            _isInitializing = true;
            try
            {
                LoadOptions();
                RefreshDiagnostics();
            }
            finally
            {
                _isInitializing = false;
            }
        }

        /// <summary>One column in a narrow panel, two side by side when docked somewhere wide.</summary>
        private void UpdateLayoutMode()
        {
            bool twoColumn = ActualWidth >= TwoColumnMinWidth;
            if (_isTwoColumn == twoColumn)
                return;
            _isTwoColumn = twoColumn;

            GutterColumn.Width = new GridLength(twoColumn ? 14 : 0);
            RightColumn.Width = twoColumn ? new GridLength(1, GridUnitType.Star) : new GridLength(0);
            Grid.SetRow(SecondaryStack, twoColumn ? 0 : 1);
            Grid.SetColumn(SecondaryStack, twoColumn ? 2 : 0);
        }

        private Brush GetStatusBrush(StatusKind kind) => (Brush)FindResource(kind switch
        {
            StatusKind.Success => "Uefi.SuccessBrush",
            StatusKind.Warning => "Uefi.WarningBrush",
            _ => "Uefi.ErrorBrush"
        });

        private static string GetStatusGlyph(StatusKind kind) => kind switch
        {
            StatusKind.Success => "\uE73E",
            StatusKind.Warning => "\uE7BA",
            _ => "\uE711"
        };

        private void SetStatus(System.Windows.Shapes.Ellipse dot, TextBlock text, StatusKind kind, string message, string? detail = null)
        {
            dot.Fill = GetStatusBrush(kind);
            text.Text = message;
            text.ToolTip = detail ?? message;
        }

        private void ShowWorkspaceFeedback(StatusKind kind, string message)
        {
            Color color = ((SolidColorBrush)GetStatusBrush(kind)).Color;
            WorkspaceFeedback.BorderBrush = new SolidColorBrush(color) { Opacity = 0.55 };
            WorkspaceFeedback.Background = new SolidColorBrush(color) { Opacity = 0.12 };
            WorkspaceFeedbackIcon.Text = GetStatusGlyph(kind);
            WorkspaceFeedbackIcon.Foreground = new SolidColorBrush(color);
            WorkspaceFeedbackText.Text = message;
            WorkspaceFeedback.Visibility = Visibility.Visible;
        }

        private void LoadOptions()
        {
            var options = UefiOptionsPage.Instance;
            bool migrated = false;
            if (string.Equals(options.Workspace, @"C:\edk2", StringComparison.OrdinalIgnoreCase) && Directory.Exists(@"D:\edk2"))
            {
                options.Workspace = @"D:\edk2";
                migrated = true;
            }
            if (string.Equals(options.ToolChainTag, "VS2022", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(Path.Combine(@"D:\edk2", "Conf", "tools_def.txt")))
            {
                options.ToolChainTag = "VS2026";
                migrated = true;
            }
            string targetFile = Path.Combine(options.Workspace, "Conf", "target.txt");
            if (string.Equals(options.ToolChainTag, "VS2026", StringComparison.OrdinalIgnoreCase) &&
                File.Exists(targetFile) &&
                File.ReadAllText(targetFile).IndexOf("TOOL_CHAIN_TAG = VS2026", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                options.ToolChainTag = string.Empty;
                migrated = true;
            }
            if (migrated)
            {
                options.SaveSettingsToStorage();
            }

            ToolchainTextBox.Text = options.ToolChainTag;
            DscTextBox.Text = options.ActiveDsc;

            string ws = options.Workspace;
            if (string.IsNullOrWhiteSpace(ws) || !Directory.Exists(ws))
            {
                if (Directory.Exists(@"D:\edk2")) ws = @"D:\edk2";
                else if (Directory.Exists(@"C:\edk2")) ws = @"C:\edk2";
            }
            WorkspacePathTextBox.Text = ws;

            foreach (ComboBoxItem item in ArchComboBox.Items)
            {
                if (item.Content.ToString() == options.Architecture.ToString())
                {
                    ArchComboBox.SelectedItem = item;
                    break;
                }
            }

            foreach (ComboBoxItem item in TargetComboBox.Items)
            {
                if (item.Content.ToString() == options.BuildTarget.ToString())
                {
                    TargetComboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private void BrowseWorkspace_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new System.Windows.Forms.FolderBrowserDialog();
            dlg.Description = "Select EDK II Root Folder (containing MdePkg and edksetup.bat)";
            if (Directory.Exists(WorkspacePathTextBox.Text.Trim()))
            {
                dlg.SelectedPath = WorkspacePathTextBox.Text.Trim();
            }
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                WorkspacePathTextBox.Text = dlg.SelectedPath;
            }
        }

        private void AutoDetect_Click(object sender, RoutedEventArgs e)
        {
            string[] candidates = { @"D:\edk2", @"C:\edk2", @"E:\edk2", @"D:\src\edk2", @"C:\src\edk2" };
            foreach (var c in candidates)
            {
                if (Directory.Exists(c) && Directory.Exists(Path.Combine(c, "MdePkg")))
                {
                    WorkspacePathTextBox.Text = c;
                    ShowWorkspaceFeedback(StatusKind.Success, $"Found EDK II at {c}. Select Apply & fix headers to use it.");
                    return;
                }
            }
            ShowWorkspaceFeedback(StatusKind.Warning, "No EDK II checkout was found in the usual locations. Use the folder button to pick it.");
        }

        private void SetWorkspace_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            string path = WorkspacePathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(path) || !Directory.Exists(path))
            {
                ShowWorkspaceFeedback(StatusKind.Error, "This folder does not exist. Pick the EDK II root with the folder button, or try Auto-detect.");
                return;
            }

            bool looksLikeEdk2 = Directory.Exists(Path.Combine(path, "MdePkg"));

            // 1. Save to options
            var options = UefiOptionsPage.Instance;
            options.Workspace = path;
            options.SaveSettingsToStorage();

            // 2. Set environment variables (process + user)
            try
            {
                Environment.SetEnvironmentVariable("WORKSPACE", path, EnvironmentVariableTarget.Process);
                Environment.SetEnvironmentVariable("WORKSPACE", path, EnvironmentVariableTarget.User);
            }
            catch { }

            // 3. Update any active project in solution
            int updatedProjects = ConfigureActiveProjectsIntelliSense(path);

            RefreshDiagnostics();

            string msg = $"WORKSPACE set to {path}.";
            if (updatedProjects > 0)
            {
                msg += $" Include paths added to {updatedProjects} project(s).";
            }
            if (looksLikeEdk2)
            {
                ShowWorkspaceFeedback(StatusKind.Success, msg);
            }
            else
            {
                ShowWorkspaceFeedback(StatusKind.Warning, msg + " MdePkg is not in this folder, so it may not be the EDK II root.");
            }

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await UefiBuildService.WriteLineAsync($"[UEFI Extension] WORKSPACE configured: {path}");
                await UefiBuildService.SetStatusBarAsync($"UEFI: Workspace configured -> {path}");
            });
        }

        private int ConfigureActiveProjectsIntelliSense(string workspacePath)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            int count = 0;
            try
            {
                var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
                if (dte?.Solution == null || string.IsNullOrEmpty(dte.Solution.FullName))
                    return 0;

                string solDir = Path.GetDirectoryName(dte.Solution.FullName) ?? "";
                if (Directory.Exists(solDir))
                {
                    var vcxprojs = Directory.GetFiles(solDir, "*.vcxproj", SearchOption.AllDirectories);
                    string concreteInclude = $@"{workspacePath}\MdePkg\Include;{workspacePath}\MdePkg\Include\X64;{workspacePath}\MdeModulePkg\Include;";

                    foreach (var vcx in vcxprojs)
                    {
                        try
                        {
                            string content = File.ReadAllText(vcx);
                            bool modified = false;

                            if (!content.Contains(workspacePath + @"\MdePkg\Include") && content.Contains("NMakeIncludeSearchPath"))
                            {
                                content = content.Replace("<NMakeIncludeSearchPath>", "<NMakeIncludeSearchPath>" + concreteInclude);
                                modified = true;
                            }

                            if (modified)
                            {
                                File.WriteAllText(vcx, content);
                                count++;
                            }
                        }
                        catch { }
                    }
                }
            }
            catch { }
            return count;
        }

        private void RefreshDiagnostics()
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var options = UefiOptionsPage.Instance;
            string workspace = UefiBuildService.ResolveWorkspace(options);

            if (Directory.Exists(workspace))
            {
                if (File.Exists(Path.Combine(workspace, "edksetup.bat")))
                    SetStatus(WorkspaceStatusDot, WorkspaceStatusText, StatusKind.Success, workspace);
                else
                    SetStatus(WorkspaceStatusDot, WorkspaceStatusText, StatusKind.Warning,
                        $"edksetup.bat missing · {workspace}", $"{workspace} exists but has no edksetup.bat");
            }
            else
            {
                SetStatus(WorkspaceStatusDot, WorkspaceStatusText, StatusKind.Error,
                    string.IsNullOrWhiteSpace(workspace) ? "Not set" : $"Not found · {workspace}");
            }

            string? qemu = QemuRunnerService.FindQemuExecutable(options.QemuPath);
            if (qemu != null)
                SetStatus(QemuStatusDot, QemuStatusText, StatusKind.Success, Path.GetFileName(qemu), qemu);
            else
                SetStatus(QemuStatusDot, QemuStatusText, StatusKind.Warning, "Not found",
                    "Install QEMU, add it to PATH, or set its path in Settings");

            string? ovmf = QemuRunnerService.FindOvmfFirmware(workspace, options.OvmfBiosPath);
            if (ovmf != null)
                SetStatus(OvmfStatusDot, OvmfStatusText, StatusKind.Success, Path.GetFileName(ovmf), ovmf);
            else
                SetStatus(OvmfStatusDot, OvmfStatusText, StatusKind.Warning, "Not found",
                    "Build OvmfPkg, or set the OVMF.fd path in Settings");
        }

        private void Refresh_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            RefreshDiagnostics();
        }

        private void GenerateNewGuid()
        {
            _currentGuid = Guid.NewGuid();
            UpdateGuidDisplays();
        }

        private void UpdateGuidDisplays()
        {
            string name = GuidNameTextBox.Text.Trim();
            CStructTextBox.Text = GuidHelper.FormatCStruct(_currentGuid);
            DecFormatTextBox.Text = GuidHelper.FormatDecDeclaration(name, _currentGuid);
            RegistryTextBox.Text = GuidHelper.FormatRegistry(_currentGuid);
        }

        private void GenerateGuid_Click(object sender, RoutedEventArgs e)
        {
            GenerateNewGuid();
        }

        private void GuidNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isInitializing)
            {
                UpdateGuidDisplays();
            }
        }

        private void CopyCStruct_Click(object sender, RoutedEventArgs e) => CopyToClipboard(sender, CStructTextBox.Text);

        private void CopyDecFormat_Click(object sender, RoutedEventArgs e) => CopyToClipboard(sender, DecFormatTextBox.Text);

        private void CopyRegistry_Click(object sender, RoutedEventArgs e) => CopyToClipboard(sender, RegistryTextBox.Text);

        private static void CopyToClipboard(object sender, string text)
        {
            try
            {
                Clipboard.SetText(text);
            }
            catch (System.Runtime.InteropServices.ExternalException)
            {
                return; // the clipboard is locked by another process
            }

            if (sender is Button button)
            {
                // Swap the copy glyph for a check mark briefly so the click is acknowledged.
                button.Content = CopiedGlyph;
                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1.4) };
                timer.Tick += (_, __) =>
                {
                    timer.Stop();
                    button.Content = CopyGlyph;
                };
                timer.Start();
            }
        }

        private void InsertAtCaret_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte?.ActiveDocument?.Selection is EnvDTE.TextSelection textSelection)
                {
                    textSelection.Insert(CStructTextBox.Text);
                    await UefiBuildService.SetStatusBarAsync($"UEFI: Inserted GUID {_currentGuid:D}");
                }
                else
                {
                    Clipboard.SetText(CStructTextBox.Text);
                    await UefiBuildService.SetStatusBarAsync($"UEFI: Copied GUID to clipboard");
                }
            });
        }

        private void ArchComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            ThreadHelper.ThrowIfNotOnUIThread();
            if (ArchComboBox.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Content.ToString(), out UefiArch arch))
            {
                UefiOptionsPage.Instance.Architecture = arch;
                UefiOptionsPage.Instance.SaveSettingsToStorage();
            }
        }

        private void TargetComboBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isInitializing) return;
            ThreadHelper.ThrowIfNotOnUIThread();
            if (TargetComboBox.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Content.ToString(), out UefiBuildTarget target))
            {
                UefiOptionsPage.Instance.BuildTarget = target;
                UefiOptionsPage.Instance.SaveSettingsToStorage();
            }
        }

        private void ToolchainTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            ThreadHelper.ThrowIfNotOnUIThread();
            UefiOptionsPage.Instance.ToolChainTag = ToolchainTextBox.Text.Trim();
            UefiOptionsPage.Instance.SaveSettingsToStorage();
        }

        private void DscTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_isInitializing) return;
            ThreadHelper.ThrowIfNotOnUIThread();
            UefiOptionsPage.Instance.ActiveDsc = DscTextBox.Text.Trim();
            UefiOptionsPage.Instance.SaveSettingsToStorage();
        }

        private void BrowseDsc_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var dlg = new OpenFileDialog
            {
                Filter = "EDK II Platform DSC (*.dsc)|*.dsc|All Files (*.*)|*.*",
                Title = "Select EDK II Platform DSC File"
            };

            var options = UefiOptionsPage.Instance;
            string workspace = UefiBuildService.ResolveWorkspace(options);
            if (Directory.Exists(workspace))
            {
                dlg.InitialDirectory = workspace;
            }

            if (dlg.ShowDialog() == true)
            {
                string path = dlg.FileName;
                if (!string.IsNullOrEmpty(workspace) && path.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
                {
                    path = path.Substring(workspace.Length).TrimStart('\\', '/');
                }
                DscTextBox.Text = path;
                options.ActiveDsc = path;
                options.SaveSettingsToStorage();
            }
        }

        private void Build_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await UefiBuildService.RunBuildAsync(false, false);
            });
        }

        private void Rebuild_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await UefiBuildService.RunBuildAsync(true, true);
            });
        }

        private void Clean_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await UefiBuildService.RunBuildAsync(true, false);
            });
        }

        private void Qemu_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await QemuRunnerService.LaunchQemuAsync();
            });
        }

        private void OpenShell_Click(object sender, RoutedEventArgs e)
        {
            ThreadHelper.ThrowIfNotOnUIThread();
            var options = UefiOptionsPage.Instance;
            string workspace = UefiBuildService.ResolveWorkspace(options);
            string edkSetup = Path.Combine(workspace, "edksetup.bat");

            string initCmd = File.Exists(edkSetup)
                ? $"/k \"cd /d \"{workspace}\" && call edksetup.bat\""
                : $"/k \"cd /d \"{workspace}\"\"";

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = initCmd,
                WorkingDirectory = Directory.Exists(workspace) ? workspace : Environment.CurrentDirectory,
                UseShellExecute = true
            };

            try
            {
                System.Diagnostics.Process.Start(psi);
            }
            catch { }
        }

        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                UefiPackage.Instance?.ShowOptionPage(typeof(UefiOptionsPage));
            });
        }
    }
}
