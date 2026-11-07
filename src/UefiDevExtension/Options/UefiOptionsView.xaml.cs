using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.VisualStudio.Shell;
using Microsoft.Win32;
using UefiDevExtension.Services;

namespace UefiDevExtension.Options
{
    /// <summary>
    /// WPF content of Tools > Options > UEFI Development > General. The page itself
    /// (<see cref="UefiOptionsPage"/>) owns the values; this view only edits a copy of them
    /// between <see cref="LoadFrom"/> and <see cref="TryApplyTo"/>.
    /// </summary>
    public partial class UefiOptionsView : UserControl
    {
        private const int MaxThreads = 512;

        public UefiOptionsView()
        {
            InitializeComponent();
        }

        internal void LoadFrom(UefiOptionsPage page)
        {
            WorkspaceTextBox.Text = page.Workspace;
            PackagesPathTextBox.Text = page.PackagesPath;
            SelectByContent(ArchComboBox, page.Architecture.ToString());
            SelectByContent(TargetComboBox, page.BuildTarget.ToString());
            ThreadsTextBox.Text = page.ThreadCount.ToString(CultureInfo.InvariantCulture);
            ToolchainTextBox.Text = page.ToolChainTag;
            DscTextBox.Text = page.ActiveDsc;

            QemuPathTextBox.Text = page.QemuPath;
            OvmfPathTextBox.Text = page.OvmfBiosPath;
            MemoryTextBox.Text = page.QemuMemory;
            VfatTextBox.Text = page.VirtualFatDir;
            ExtraArgsTextBox.Text = page.AdditionalQemuArgs;

            ClearOutputCheckBox.IsChecked = page.ClearOutputOnBuild;
            TimestampCheckBox.IsChecked = page.ShowTimestamp;

            AutoDetectStatus.Visibility = Visibility.Collapsed;
            UpdateWorkspaceHint();
            ValidateThreads();
        }

        /// <summary>Copies the edited values to the page. Returns false and highlights the field when a value is invalid.</summary>
        internal bool TryApplyTo(UefiOptionsPage page)
        {
            if (!TryGetThreadCount(out int threads))
            {
                ValidateThreads();
                ThreadsTextBox.Focus();
                ThreadsTextBox.SelectAll();
                return false;
            }

            page.Workspace = WorkspaceTextBox.Text.Trim();
            page.PackagesPath = PackagesPathTextBox.Text.Trim();
            if (TryGetSelected(ArchComboBox, out UefiArch arch))
                page.Architecture = arch;
            if (TryGetSelected(TargetComboBox, out UefiBuildTarget target))
                page.BuildTarget = target;
            page.ThreadCount = threads;
            page.ToolChainTag = ToolchainTextBox.Text.Trim();
            page.ActiveDsc = DscTextBox.Text.Trim();

            page.QemuPath = QemuPathTextBox.Text.Trim();
            page.OvmfBiosPath = OvmfPathTextBox.Text.Trim();
            page.QemuMemory = MemoryTextBox.Text.Trim();
            page.VirtualFatDir = VfatTextBox.Text.Trim();
            page.AdditionalQemuArgs = ExtraArgsTextBox.Text.Trim();

            page.ClearOutputOnBuild = ClearOutputCheckBox.IsChecked == true;
            page.ShowTimestamp = TimestampCheckBox.IsChecked == true;
            return true;
        }

        // ------------------------------------------------------------------ validation and hints

        private void WorkspaceTextBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateWorkspaceHint();

        private void ThreadsTextBox_TextChanged(object sender, TextChangedEventArgs e) => ValidateThreads();

        private void UpdateWorkspaceHint()
        {
            string workspace = WorkspaceTextBox.Text.Trim();
            if (workspace.Length == 0)
                SetHint(WorkspaceHintDot, WorkspaceHintText, "Uefi.ErrorBrush", "Required: the folder that contains edksetup.bat");
            else if (!Directory.Exists(workspace))
                SetHint(WorkspaceHintDot, WorkspaceHintText, "Uefi.ErrorBrush", "Folder not found");
            else if (File.Exists(Path.Combine(workspace, "edksetup.bat")))
                SetHint(WorkspaceHintDot, WorkspaceHintText, "Uefi.SuccessBrush", "EDK II workspace found (edksetup.bat is present)");
            else
                SetHint(WorkspaceHintDot, WorkspaceHintText, "Uefi.WarningBrush", "Folder exists, but edksetup.bat is not in it");
        }

        private void ValidateThreads()
        {
            bool valid = TryGetThreadCount(out _);
            ThreadsTextBox.Tag = valid ? null : "error";
            ThreadsError.Visibility = valid ? Visibility.Collapsed : Visibility.Visible;
        }

        private bool TryGetThreadCount(out int threads)
        {
            return int.TryParse(ThreadsTextBox.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out threads)
                   && threads >= 1 && threads <= MaxThreads;
        }

        private void SetHint(System.Windows.Shapes.Ellipse dot, TextBlock text, string brushKey, string message)
        {
            dot.Fill = (Brush)FindResource(brushKey);
            text.Text = message;
        }

        // ------------------------------------------------------------------ browse buttons

        private void BrowseWorkspace_Click(object sender, RoutedEventArgs e)
        {
            string? folder = PickFolder("Select the EDK II root folder (the one that contains MdePkg and edksetup.bat)", WorkspaceTextBox.Text);
            if (folder != null)
                WorkspaceTextBox.Text = folder;
        }

        private void BrowseDsc_Click(object sender, RoutedEventArgs e)
        {
            string workspace = WorkspaceTextBox.Text.Trim();
            string? file = PickFile("Select an EDK II platform DSC file",
                "EDK II platform (*.dsc)|*.dsc|All files (*.*)|*.*", workspace);
            if (file == null)
                return;

            // Keep paths inside the workspace relative, the way build.exe expects them.
            if (workspace.Length > 0 && file.StartsWith(workspace, StringComparison.OrdinalIgnoreCase))
                file = file.Substring(workspace.Length).TrimStart('\\', '/');
            DscTextBox.Text = file;
        }

        private void BrowseQemu_Click(object sender, RoutedEventArgs e)
        {
            string current = QemuPathTextBox.Text.Trim();
            string? initial = File.Exists(current) ? Path.GetDirectoryName(current) : null;
            string? file = PickFile("Select the QEMU executable",
                "QEMU (qemu-system-*.exe)|qemu-system-*.exe|Programs (*.exe)|*.exe", initial);
            if (file != null)
                QemuPathTextBox.Text = file;
        }

        private void BrowseOvmf_Click(object sender, RoutedEventArgs e)
        {
            string current = OvmfPathTextBox.Text.Trim();
            string workspace = WorkspaceTextBox.Text.Trim();
            string? initial = workspace;
            if (File.Exists(current))
                initial = Path.GetDirectoryName(current);
            else if (Directory.Exists(workspace) && Directory.Exists(Path.Combine(workspace, "Build")))
                initial = Path.Combine(workspace, "Build");
            string? file = PickFile("Select an OVMF firmware image",
                "Firmware images (*.fd)|*.fd|All files (*.*)|*.*", initial);
            if (file != null)
                OvmfPathTextBox.Text = file;
        }

        private void BrowseVfat_Click(object sender, RoutedEventArgs e)
        {
            string? folder = PickFolder("Select a folder to share with the virtual machine (fs0: in the UEFI Shell)", VfatTextBox.Text);
            if (folder != null)
                VfatTextBox.Text = folder;
        }

        private static string? PickFolder(string description, string current)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog { Description = description, ShowNewFolderButton = true };
            if (Directory.Exists(current.Trim()))
                dialog.SelectedPath = current.Trim();
            return dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK ? dialog.SelectedPath : null;
        }

        private static string? PickFile(string title, string filter, string? initialDirectory)
        {
            var dialog = new OpenFileDialog { Title = title, Filter = filter, CheckFileExists = true };
            if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
                dialog.InitialDirectory = initialDirectory;
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        // ------------------------------------------------------------------ auto-detect

        private void AutoDetect_Click(object sender, RoutedEventArgs e)
        {
            string qemuHint = QemuPathTextBox.Text.Trim();
            string ovmfHint = OvmfPathTextBox.Text.Trim();
            string workspace = WorkspaceTextBox.Text.Trim();

            AutoDetectButton.IsEnabled = false;
            SetHint(AutoDetectDot, AutoDetectText, "Uefi.NeutralBrush", "Searching…");
            AutoDetectStatus.Visibility = Visibility.Visible;

            _ = ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
            {
                string? qemu = null;
                string? ovmf = null;
                string? error = null;
                try
                {
                    // The OVMF search walks the workspace tree, so keep it off the UI thread.
                    (qemu, ovmf) = await Task.Run(() => (
                        QemuRunnerService.FindQemuExecutable(qemuHint),
                        QemuRunnerService.FindOvmfFirmware(workspace, ovmfHint)));
                }
                catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException)
                {
                    error = ex.Message;
                }

                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                AutoDetectButton.IsEnabled = true;

                if (qemu != null)
                    QemuPathTextBox.Text = qemu;
                if (ovmf != null)
                    OvmfPathTextBox.Text = ovmf;

                if (error != null)
                    SetHint(AutoDetectDot, AutoDetectText, "Uefi.ErrorBrush", "Search failed: " + error);
                else if (qemu != null && ovmf != null)
                    SetHint(AutoDetectDot, AutoDetectText, "Uefi.SuccessBrush", "Found QEMU and OVMF");
                else if (qemu != null)
                    SetHint(AutoDetectDot, AutoDetectText, "Uefi.WarningBrush", "Found QEMU; no OVMF image in the workspace");
                else if (ovmf != null)
                    SetHint(AutoDetectDot, AutoDetectText, "Uefi.WarningBrush", "Found OVMF; QEMU is not installed or not on PATH");
                else
                    SetHint(AutoDetectDot, AutoDetectText, "Uefi.ErrorBrush", "Neither QEMU nor OVMF was found");
            });
        }

        // ------------------------------------------------------------------ helpers

        private static void SelectByContent(ComboBox comboBox, string value)
        {
            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (string.Equals(item.Content?.ToString(), value, StringComparison.OrdinalIgnoreCase))
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
            comboBox.SelectedIndex = 0;
        }

        private static bool TryGetSelected<TEnum>(ComboBox comboBox, out TEnum value) where TEnum : struct
        {
            value = default;
            return comboBox.SelectedItem is ComboBoxItem item && Enum.TryParse(item.Content?.ToString(), out value);
        }
    }
}
