using System;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using Microsoft.VisualStudio.Shell;

namespace UefiDevExtension.Options
{
    public enum UefiArch
    {
        X64,
        IA32,
        ARM,
        AARCH64,
        RISCV64
    }

    public enum UefiBuildTarget
    {
        DEBUG,
        RELEASE,
        NOOPT
    }

    [ComVisible(true)]
    [Guid("c85e2b81-64d8-4be6-a947-f584e031eb92")]
    public class UefiOptionsPage : UIElementDialogPage
    {
        private UefiOptionsView? _view;

        /// <summary>Raised on the UI thread after the user saves this page with OK.</summary>
        public static event EventHandler? SettingsChanged;

        protected override UIElement Child => _view ??= new UefiOptionsView();

        protected override void OnActivate(CancelEventArgs e)
        {
            base.OnActivate(e);
            ((UefiOptionsView)Child).LoadFrom(this);
        }

        protected override void OnApply(PageApplyEventArgs e)
        {
            if (e.ApplyBehavior == ApplyKind.Apply && _view != null && !_view.TryApplyTo(this))
            {
                // Keep the dialog open on this page so the highlighted field can be fixed.
                e.ApplyBehavior = ApplyKind.CancelNoNavigate;
                return;
            }

            base.OnApply(e);

            if (e.ApplyBehavior == ApplyKind.Apply)
            {
                SettingsChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        [Category("EDK II Build Settings")]
        [DisplayName("EDK II Workspace Root")]
        [Description("Full path to the EDK II workspace directory (where edksetup.bat resides, e.g. D:\\edk2).")]
        public string Workspace { get; set; } = @"D:\edk2";

        [Category("EDK II Build Settings")]
        [DisplayName("Packages Path (PACKAGES_PATH)")]
        [Description("Optional additional package search paths separated by semicolons.")]
        public string PackagesPath { get; set; } = string.Empty;

        [Category("EDK II Build Settings")]
        [DisplayName("Active DSC Platform File")]
        [Description("Relative or absolute path to the active .dsc platform file (e.g. OvmfPkg/OvmfPkgX64.dsc or EmulatorPkg/EmulatorPkg.dsc).")]
        public string ActiveDsc { get; set; } = @"OvmfPkg/OvmfPkgX64.dsc";

        [Category("EDK II Build Settings")]
        [DisplayName("Target Architecture")]
        [Description("Target processor architecture (X64, IA32, ARM, AARCH64, RISCV64).")]
        public UefiArch Architecture { get; set; } = UefiArch.X64;

        [Category("EDK II Build Settings")]
        [DisplayName("Tool Chain Tag")]
        [Description("Optional tool chain identifier in Conf/tools_def.txt. Leave empty to use EDK II target.txt default.")]
        public string ToolChainTag { get; set; } = string.Empty;

        [Category("EDK II Build Settings")]
        [DisplayName("Build Target")]
        [Description("Build configuration target: DEBUG, RELEASE, or NOOPT.")]
        public UefiBuildTarget BuildTarget { get; set; } = UefiBuildTarget.DEBUG;

        [Category("EDK II Build Settings")]
        [DisplayName("Parallel Threads")]
        [Description("Number of concurrent build threads (-n parameter for build).")]
        public int ThreadCount { get; set; } = 4;

        [Category("QEMU & OVMF Emulation")]
        [DisplayName("QEMU Executable Path")]
        [Description("Path to qemu-system-x86_64.exe (or auto-detected from PATH / standard installs).")]
        public string QemuPath { get; set; } = @"qemu-system-x86_64";

        [Category("QEMU & OVMF Emulation")]
        [DisplayName("OVMF Firmware File")]
        [Description("Path to the OVMF.fd firmware binary (e.g. Build/OvmfX64/DEBUG_VS2026/FV/OVMF.fd).")]
        public string OvmfBiosPath { get; set; } = string.Empty;

        [Category("QEMU & OVMF Emulation")]
        [DisplayName("Virtual Machine Memory")]
        [Description("Memory allocated to QEMU guest (e.g. 512M, 1024M, 2G).")]
        public string QemuMemory { get; set; } = "512M";

        [Category("QEMU & OVMF Emulation")]
        [DisplayName("Virtual FAT Shared Directory")]
        [Description("Optional directory mounted as a virtual FAT drive inside UEFI (accessible as fs0: in UEFI Shell).")]
        public string VirtualFatDir { get; set; } = string.Empty;

        [Category("QEMU & OVMF Emulation")]
        [DisplayName("Additional QEMU Arguments")]
        [Description("Extra arguments to append when launching QEMU (e.g. -serial stdio -net none).")]
        public string AdditionalQemuArgs { get; set; } = "-net none";

        [Category("Output Window")]
        [DisplayName("Clear Output Before Build")]
        [Description("Clear the UEFI Development output pane before launching a new build.")]
        public bool ClearOutputOnBuild { get; set; } = true;

        [Category("Output Window")]
        [DisplayName("Show Timestamp")]
        [Description("Prefix output messages with time stamps.")]
        public bool ShowTimestamp { get; set; } = true;

        public static UefiOptionsPage Instance =>
            UefiPackage.Instance?.GetDialogPage(typeof(UefiOptionsPage)) as UefiOptionsPage ?? new UefiOptionsPage();
    }
}
