using System;
using System.ComponentModel.Design;
using System.Diagnostics;
using System.IO;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UefiDevExtension.Options;
using UefiDevExtension.Services;
using UefiDevExtension.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension.Commands
{
    internal sealed class OpenPromptCommand
    {
        public const int CmdIdOpenPrompt = 0x0106;
        public const int CmdIdToolWindow = 0x0107;
        public const int CmdIdOptions = 0x0108;

        public static readonly Guid CommandSet = new Guid("a8e1b3d2-35c6-4e08-9df2-5d9c10825310");

        private readonly AsyncPackage _package;

        private OpenPromptCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Open Prompt
            var promptId = new CommandID(CommandSet, CmdIdOpenPrompt);
            var promptItem = new MenuCommand(ExecuteOpenPrompt, promptId);
            commandService.AddCommand(promptItem);

            // Open Tool Window
            var toolWinId = new CommandID(CommandSet, CmdIdToolWindow);
            var toolWinItem = new MenuCommand(ExecuteOpenToolWindow, toolWinId);
            commandService.AddCommand(toolWinItem);

            // Open Options
            var optId = new CommandID(CommandSet, CmdIdOptions);
            var optItem = new MenuCommand(ExecuteOpenOptions, optId);
            commandService.AddCommand(optItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService? commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new OpenPromptCommand(package, commandService);
            }
        }

        private void ExecuteOpenPrompt(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var options = UefiOptionsPage.Instance;
                string workspace = UefiBuildService.ResolveWorkspace(options);
                string edkSetup = Path.Combine(workspace, "edksetup.bat");

                string initCmd = File.Exists(edkSetup)
                    ? $"/k \"cd /d \"{workspace}\" && call edksetup.bat\""
                    : $"/k \"cd /d \"{workspace}\"\"";

                var psi = new ProcessStartInfo
                {
                    FileName = "cmd.exe",
                    Arguments = initCmd,
                    WorkingDirectory = Directory.Exists(workspace) ? workspace : Environment.CurrentDirectory,
                    UseShellExecute = true
                };

                psi.EnvironmentVariables["WORKSPACE"] = workspace;
                if (!string.IsNullOrWhiteSpace(options.PackagesPath))
                {
                    psi.EnvironmentVariables["PACKAGES_PATH"] = options.PackagesPath;
                }

                try
                {
                    Process.Start(psi);
                    await UefiBuildService.SetStatusBarAsync("UEFI: Opened Developer Prompt");
                }
                catch (Exception ex)
                {
                    await UefiBuildService.WriteLineAsync($"Error opening shell: {ex.Message}");
                }
            });
        }

        private void ExecuteOpenToolWindow(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                ToolWindowPane? window = await _package.FindToolWindowAsync(typeof(UefiToolWindow), 0, true, _package.DisposalToken);
                if (window?.Frame is IVsWindowFrame frame)
                {
                    frame.Show();
                }
            });
        }

        private void ExecuteOpenOptions(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                _package.ShowOptionPage(typeof(UefiOptionsPage));
            });
        }
    }
}
