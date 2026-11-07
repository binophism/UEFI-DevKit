using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UefiDevExtension.Commands;
using UefiDevExtension.Options;
using UefiDevExtension.ToolWindows;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension
{
    [PackageRegistration(UseManagedResourcesOnly = true, AllowsBackgroundLoading = true)]
    [Guid(PackageGuidString)]
    [ProvideMenuResource("Menus.ctmenu", 1)]
    [ProvideOptionPage(typeof(UefiOptionsPage), "UEFI Development", "General", 0, 0, true)]
    [ProvideProfile(typeof(UefiOptionsPage), "UEFI Development", "General", 106, 107, true)]
    [ProvideToolWindow(typeof(UefiToolWindow), Style = VsDockStyle.Tabbed, Window = ToolWindowGuids80.Outputwindow)]
    [ProvideAutoLoad(UIContextGuids80.SolutionExists, PackageAutoLoadFlags.BackgroundLoad)]
    [ProvideAutoLoad(UIContextGuids80.NoSolution, PackageAutoLoadFlags.BackgroundLoad)]
    public sealed class UefiPackage : AsyncPackage
    {
        public const string PackageGuidString = "9260c634-8c77-4c31-98ee-1f0f4a8eb2c1";

        public static UefiPackage? Instance { get; private set; }

        protected override async Task InitializeAsync(CancellationToken cancellationToken, IProgress<ServiceProgressData> progress)
        {
            Instance = this;

            await base.InitializeAsync(cancellationToken, progress);

            // Switch to main thread for menu & command registration
            await this.JoinableTaskFactory.SwitchToMainThreadAsync(cancellationToken);

            await BuildCommands.InitializeAsync(this);
            await QemuCommand.InitializeAsync(this);
            await GuidGeneratorCommand.InitializeAsync(this);
            await OpenPromptCommand.InitializeAsync(this);
        }
    }
}
