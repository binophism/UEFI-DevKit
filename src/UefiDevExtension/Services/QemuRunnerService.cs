using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.VisualStudio.Shell;
using UefiDevExtension.Options;

namespace UefiDevExtension.Services
{
    public class QemuRunnerService
    {
        public static async Task<bool> LaunchQemuAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var options = UefiOptionsPage.Instance;
            string workspace = UefiBuildService.ResolveWorkspace(options);

            string? qemuExe = FindQemuExecutable(options.QemuPath);
            if (string.IsNullOrEmpty(qemuExe))
            {
                await UefiBuildService.WriteLineAsync("Error: Could not locate QEMU executable (qemu-system-x86_64.exe). Please install QEMU or configure the path in Tools -> Options -> UEFI Development.");
                return false;
            }

            string? ovmfPath = FindOvmfFirmware(workspace, options.OvmfBiosPath);
            if (string.IsNullOrEmpty(ovmfPath))
            {
                await UefiBuildService.WriteLineAsync("Warning: No OVMF firmware file found. Trying standard QEMU default or please build OvmfPkg or specify the path in Tools -> Options -> UEFI Development.");
            }

            string args = BuildQemuArguments(options, workspace, ovmfPath);

            await UefiBuildService.WriteLineAsync("--------------------------------------------------------------------------------");
            await UefiBuildService.WriteLineAsync(" Launching QEMU UEFI Virtual Machine");
            await UefiBuildService.WriteLineAsync($" Executable: {qemuExe}");
            await UefiBuildService.WriteLineAsync($" Firmware  : {ovmfPath ?? "(Default)"}");
            await UefiBuildService.WriteLineAsync($" Arguments : {args}");
            await UefiBuildService.WriteLineAsync("--------------------------------------------------------------------------------");

            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = qemuExe,
                    Arguments = args,
                    WorkingDirectory = Directory.Exists(workspace) ? workspace : Environment.CurrentDirectory,
                    UseShellExecute = false
                };

                Process.Start(psi);
                await UefiBuildService.SetStatusBarAsync("UEFI: QEMU started");
                return true;
            }
            catch (Exception ex)
            {
                await UefiBuildService.WriteLineAsync($"Error starting QEMU: {ex.Message}");
                return false;
            }
        }

        private static string BuildQemuArguments(UefiOptionsPage options, string workspace, string? ovmfPath)
        {
            var args = new System.Text.StringBuilder();

            if (!string.IsNullOrEmpty(ovmfPath) && File.Exists(ovmfPath))
            {
                args.Append($"-bios \"{ovmfPath}\" ");
            }

            if (!string.IsNullOrWhiteSpace(options.QemuMemory))
            {
                args.Append($"-m {options.QemuMemory} ");
            }

            string fatDir = options.VirtualFatDir;
            if (string.IsNullOrWhiteSpace(fatDir) || !Directory.Exists(fatDir))
            {
                // Look for an efi binary folder or create run_disk in workspace
                if (Directory.Exists(workspace))
                {
                    string candidate = Path.Combine(workspace, "run_disk");
                    if (!Directory.Exists(candidate))
                    {
                        try { Directory.CreateDirectory(candidate); } catch { }
                    }

                    // Look for recently built .efi files and copy them to run_disk
                    string buildFolder = Path.Combine(workspace, "Build");
                    if (Directory.Exists(buildFolder))
                    {
                        var efiFiles = Directory.GetFiles(buildFolder, "*.efi", SearchOption.AllDirectories);
                        foreach (var efi in efiFiles.OrderByDescending(f => File.GetLastWriteTimeUtc(f)).Take(5))
                        {
                            try
                            {
                                string dest = Path.Combine(candidate, Path.GetFileName(efi));
                                File.Copy(efi, dest, true);
                            }
                            catch { }
                        }
                    }

                    fatDir = candidate;
                }
            }

            if (!string.IsNullOrWhiteSpace(fatDir) && Directory.Exists(fatDir))
            {
                args.Append($"-drive file=fat:rw:\"{fatDir}\",format=raw ");
            }

            if (!string.IsNullOrWhiteSpace(options.AdditionalQemuArgs))
            {
                args.Append(options.AdditionalQemuArgs);
            }

            return args.ToString().Trim();
        }

        public static string? FindQemuExecutable(string userConfiguredPath)
        {
            if (!string.IsNullOrWhiteSpace(userConfiguredPath))
            {
                if (File.Exists(userConfiguredPath))
                    return userConfiguredPath;

                // Check in PATH
                string[] paths = (Environment.GetEnvironmentVariable("PATH") ?? "").Split(Path.PathSeparator);
                foreach (string p in paths)
                {
                    string combined = Path.Combine(p.Trim(), userConfiguredPath.Trim());
                    if (File.Exists(combined))
                        return combined;
                    if (File.Exists(combined + ".exe"))
                        return combined + ".exe";
                }
            }

            // Standard common install paths on Windows
            string[] standardPaths = {
                @"C:\Program Files\qemu\qemu-system-x86_64.exe",
                @"C:\Program Files (x86)\qemu\qemu-system-x86_64.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\qemu\qemu-system-x86_64.exe")
            };

            foreach (var p in standardPaths)
            {
                if (File.Exists(p))
                    return p;
            }

            return null;
        }

        public static string? FindOvmfFirmware(string workspace, string userPath)
        {
            if (!string.IsNullOrWhiteSpace(userPath))
            {
                if (File.Exists(userPath))
                    return userPath;

                if (!string.IsNullOrWhiteSpace(workspace))
                {
                    string full = Path.Combine(workspace, userPath);
                    if (File.Exists(full))
                        return full;
                }
            }

            if (!string.IsNullOrWhiteSpace(workspace) && Directory.Exists(workspace))
            {
                string buildDir = Path.Combine(workspace, "Build");
                if (Directory.Exists(buildDir))
                {
                    var ovmf = Directory.GetFiles(buildDir, "OVMF.fd", SearchOption.AllDirectories).FirstOrDefault();
                    if (ovmf != null)
                        return ovmf;

                    var anyFd = Directory.GetFiles(buildDir, "*.fd", SearchOption.AllDirectories).FirstOrDefault();
                    if (anyFd != null)
                        return anyFd;
                }

                // Check OvmfPkg folder
                var prebuilt = Directory.GetFiles(workspace, "OVMF*.fd", SearchOption.AllDirectories).FirstOrDefault();
                if (prebuilt != null)
                    return prebuilt;
            }

            return null;
        }
    }
}
