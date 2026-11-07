using System;
using System.ComponentModel.Design;
using Microsoft.VisualStudio.Shell;
using UefiDevExtension.Services;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension.Commands
{
    internal sealed class BuildCommands
    {
        public const int CmdIdBuild = 0x0100;
        public const int CmdIdRebuild = 0x0101;
        public const int CmdIdClean = 0x0102;

        public static readonly Guid CommandSet = new Guid("a8e1b3d2-35c6-4e08-9df2-5d9c10825310");

        private readonly AsyncPackage _package;

        private BuildCommands(AsyncPackage package, OleMenuCommandService commandService)
        {
            _package = package ?? throw new ArgumentNullException(nameof(package));
            commandService = commandService ?? throw new ArgumentNullException(nameof(commandService));

            // Build
            var menuCommandID = new CommandID(CommandSet, CmdIdBuild);
            var menuItem = new MenuCommand((s, e) => ExecuteBuild(false, false), menuCommandID);
            commandService.AddCommand(menuItem);

            // Rebuild
            var rebuildID = new CommandID(CommandSet, CmdIdRebuild);
            var rebuildItem = new MenuCommand((s, e) => ExecuteBuild(true, true), rebuildID);
            commandService.AddCommand(rebuildItem);

            // Clean
            var cleanID = new CommandID(CommandSet, CmdIdClean);
            var cleanItem = new MenuCommand((s, e) => ExecuteBuild(true, false), cleanID);
            commandService.AddCommand(cleanItem);
        }

        public static async Task InitializeAsync(AsyncPackage package)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync(package.DisposalToken);
            OleMenuCommandService? commandService = await package.GetServiceAsync(typeof(IMenuCommandService)) as OleMenuCommandService;
            if (commandService != null)
            {
                _ = new BuildCommands(package, commandService);
            }
        }

        private void ExecuteBuild(bool isClean, bool isRebuild)
        {
            _ = _package.JoinableTaskFactory.RunAsync(async () =>
            {
                await UefiBuildService.RunBuildAsync(isClean, isRebuild);
            });
        }
    }
}
