using System;
using System.ComponentModel.Design;
using System.Windows.Forms;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UefiDevExtension.Services;
using UefiDevExtension.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension.Commands
{
    internal sealed class GuidGeneratorCommand
    {
        public const int CmdIdGuidGenerator = 0x0104;
        public const int CmdIdInsertGuid = 0x0105;

        public static readonly Guid CommandSet = new Guid("a8e1b3d2-35c6-4e08-9df2-5d9c10825310");

        private readonly AsyncPackage _package;

        private GuidGeneratorCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Open GUID Generator / Tools window
            var genId = new CommandID(CommandSet, CmdIdGuidGenerator);
            var genItem = new MenuCommand(ExecuteOpenWindow, genId);
            commandService.AddCommand(genItem);

            // Insert GUID directly at editor caret
            var insertId = new CommandID(CommandSet, CmdIdInsertGuid);
            var insertItem = new MenuCommand(ExecuteInsertGuid, insertId);
            commandService.AddCommand(insertItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService? commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new GuidGeneratorCommand(package, commandService);
            }
        }

        private void ExecuteOpenWindow(object sender, EventArgs e)
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

        private void ExecuteInsertGuid(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
                var dte = await _package.GetServiceAsync(typeof(EnvDTE.DTE)) as EnvDTE.DTE;
                if (dte?.ActiveDocument?.Selection is EnvDTE.TextSelection textSelection)
                {
                    Guid guid = Guid.NewGuid();
                    string cStruct = GuidHelper.FormatCStruct(guid);
                    textSelection.Insert(cStruct);
                    await UefiBuildService.SetStatusBarAsync($"UEFI: Inserted GUID {guid:D}");
                }
                else
                {
                    Guid guid = Guid.NewGuid();
                    Clipboard.SetText(GuidHelper.FormatCStruct(guid));
                    await UefiBuildService.SetStatusBarAsync($"UEFI: No active editor. Copied GUID to clipboard.");
                }
            });
        }
    }
}
