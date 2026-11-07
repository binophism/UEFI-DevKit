using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using UefiDevExtension.Services;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension.Commands
{
    internal sealed class QemuCommand
    {
        public const int CommandId = 0x0103;
        public static readonly Guid CommandSet = new Guid("a8e1b3d2-35c6-4e08-9df2-5d9c10825310");

        private readonly AsyncPackage _package;

        private QemuCommand(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            var menuCommandID = new CommandID(CommandSet, CommandId);
            var menuItem = new MenuCommand(Execute, menuCommandID);
            commandService.AddCommand(menuItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService? commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new QemuCommand(package, commandService);
            }
        }

        private void Execute(object sender, EventArgs e)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await QemuRunnerService.LaunchQemuAsync();
            });
        }
    }
}
