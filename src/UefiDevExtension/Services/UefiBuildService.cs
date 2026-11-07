using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.VisualStudio;
using Microsoft.VisualStudio.Shell;
using Microsoft.VisualStudio.Shell.Interop;
using UefiDevExtension.Options;
using Task = System.Threading.Tasks.Task;

namespace UefiDevExtension.Services
{
    public class UefiBuildService
    {
        private static readonly Guid UefiOutputPaneGuid = new Guid("B9743588-4A59-4D8B-9B2E-314FA4E289C3");
        private static IVsOutputWindowPane? _outputPane;
        private static Process? _currentProcess;

        public static async Task<IVsOutputWindowPane?> GetOutputPaneAsync()
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();

            if (_outputPane != null)
                return _outputPane;

            var outputWindow = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsOutputWindow)) as IVsOutputWindow;
            if (outputWindow == null)
                return null;

            Guid paneGuid = UefiOutputPaneGuid;
            int hr = outputWindow.GetPane(ref paneGuid, out _outputPane);
            if (hr != VSConstants.S_OK || _outputPane == null)
            {
                outputWindow.CreatePane(ref paneGuid, "UEFI Development", 1, 1);
                outputWindow.GetPane(ref paneGuid, out _outputPane);
            }

            return _outputPane;
        }

        public static async Task WriteLineAsync(string message)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var pane = await GetOutputPaneAsync();
            if (pane != null)
            {
                var options = UefiOptionsPage.Instance;
                string prefix = options.ShowTimestamp ? $"[{DateTime.Now:HH:mm:ss}] " : "";
                pane.OutputStringThreadSafe(prefix + message + Environment.NewLine);
            }
        }

        public static async Task SetStatusBarAsync(string text)
        {
            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            var statusBar = await AsyncServiceProvider.GlobalProvider.GetServiceAsync(typeof(SVsStatusbar)) as IVsStatusbar;
            if (statusBar != null)
            {
                statusBar.SetText(text);
            }
        }

        public static async Task<bool> RunBuildAsync(bool isClean, bool isRebuild = false)
        {
            if (_currentProcess != null && !_currentProcess.HasExited)
            {
                await WriteLineAsync("Warning: A UEFI build process is already in progress!");
                return false;
            }

            var options = UefiOptionsPage.Instance;
            var pane = await GetOutputPaneAsync();

            await ThreadHelper.JoinableTaskFactory.SwitchToMainThreadAsync();
            if (pane != null)
            {
                pane.Activate();
                if (options.ClearOutputOnBuild)
                {
                    pane.Clear();
                }
            }

            string workspace = ResolveWorkspace(options);
            if (!Directory.Exists(workspace))
            {
                await WriteLineAsync($"Error: Workspace path '{workspace}' does not exist. Please check Tools -> Options -> UEFI Development.");
                await SetStatusBarAsync("UEFI: Build failed (invalid workspace)");
                return false;
            }

            string? toolChainTag = ResolveToolChainTag(options);
            if (toolChainTag == null)
            {
                await WriteLineAsync("Error: Could not detect a supported Visual Studio installation. Install Visual Studio 2022 or 2026, or set a tool chain tag manually in Tools -> Options -> UEFI Development.");
                await SetStatusBarAsync("UEFI: Build failed (Visual Studio toolchain not found)");
                return false;
            }

            string activeDsc = ResolveActiveProjectDsc() ?? options.ActiveDsc;
            string actionName = isRebuild ? "Rebuild" : (isClean ? "Clean" : "Build");
            await WriteLineAsync($"================================================================================");
            await WriteLineAsync($" Starting UEFI {actionName}");
            await WriteLineAsync($" Workspace : {workspace}");
            await WriteLineAsync($" Platform  : {activeDsc}");
            await WriteLineAsync($" Arch      : {options.Architecture}");
            await WriteLineAsync($" Toolchain : {toolChainTag}");
            await WriteLineAsync($" Target    : {options.BuildTarget}");
            await WriteLineAsync($"================================================================================");
            await SetStatusBarAsync($"UEFI: {actionName} in progress...");

            string? solutionDirectory = ResolveSolutionDirectory();
            string? activeProjectName = ResolveActiveProjectName();

            var stopwatch = Stopwatch.StartNew();

            bool success = await Task.Run(() =>
            {
                return ExecuteProcess(workspace, options, activeDsc, isClean, isRebuild, toolChainTag);
            });

            stopwatch.Stop();
            string duration = $"{stopwatch.Elapsed.TotalSeconds:F2}s";

            if (success)
            {
                if (!isClean && solutionDirectory != null)
                {
                    string[] copyMessages = await Task.Run(() => CopyBuildOutputs(workspace, activeDsc, options, toolChainTag, solutionDirectory, activeProjectName));
                    foreach (string copyMessage in copyMessages)
                    {
                        await WriteLineAsync(copyMessage);
                    }
                }

                await WriteLineAsync($"\n>>> UEFI {actionName} SUCCEEDED in {duration} <<<");
                await SetStatusBarAsync($"UEFI: {actionName} succeeded ({duration})");
            }
            else
            {
                await WriteLineAsync($"\n>>> UEFI {actionName} FAILED after {duration} <<<");
                await SetStatusBarAsync($"UEFI: {actionName} failed ({duration})");
            }

            return success;
        }

        private static bool ExecuteProcess(string workspace, UefiOptionsPage options, string activeDsc, bool isClean, bool isRebuild, string toolChainTag)
        {
            string edkSetupBat = Path.Combine(workspace, "edksetup.bat");
            bool hasEdkSetup = File.Exists(edkSetupBat);

            string toolChainArgument = $" -t {toolChainTag}";

            string buildArgs;
            if (isClean && !isRebuild)
            {
                buildArgs = $"build clean -p \"{activeDsc}\" -a {options.Architecture}{toolChainArgument} -b {options.BuildTarget}";
            }
            else if (isRebuild)
            {
                buildArgs = $"build clean -p \"{activeDsc}\" -a {options.Architecture}{toolChainArgument} -b {options.BuildTarget} && " +
                            $"build -p \"{activeDsc}\" -a {options.Architecture}{toolChainArgument} -b {options.BuildTarget} -n {options.ThreadCount}";
            }
            else
            {
                buildArgs = $"build -p \"{activeDsc}\" -a {options.Architecture}{toolChainArgument} -b {options.BuildTarget} -n {options.ThreadCount}";
            }

            string cmdLine;
            if (hasEdkSetup)
            {
                cmdLine = $"/c \"cd /d \"{workspace}\" && call edksetup.bat {toolChainTag} >nul 2>&1 && {buildArgs}\"";
            }
            else
            {
                cmdLine = $"/c \"cd /d \"{workspace}\" && {buildArgs}\"";
            }

            var startInfo = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = cmdLine,
                WorkingDirectory = workspace,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            startInfo.EnvironmentVariables["WORKSPACE"] = workspace;
            string? projectDirectory = Path.GetDirectoryName(activeDsc);
            string packagesPath = projectDirectory ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(options.PackagesPath))
            {
                packagesPath = string.IsNullOrWhiteSpace(packagesPath)
                    ? options.PackagesPath
                    : packagesPath + ";" + options.PackagesPath;
            }
            if (!string.IsNullOrWhiteSpace(packagesPath))
            {
                startInfo.EnvironmentVariables["PACKAGES_PATH"] = packagesPath;
            }

            ConfigureExternalToolEnvironment(startInfo);

            try
            {
                using var proc = new Process { StartInfo = startInfo };
                _currentProcess = proc;

                proc.OutputDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            await WriteLineAsync(e.Data);
                        });
                    }
                };

                proc.ErrorDataReceived += (s, e) =>
                {
                    if (e.Data != null)
                    {
                        ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                        {
                            await WriteLineAsync("[STDERR] " + e.Data);
                        });
                    }
                };

                proc.Start();
                proc.BeginOutputReadLine();
                proc.BeginErrorReadLine();
                proc.WaitForExit();

                int code = proc.ExitCode;
                _currentProcess = null;
                return code == 0;
            }
            catch (Exception ex)
            {
                ThreadHelper.JoinableTaskFactory.RunAsync(async () =>
                {
                    await WriteLineAsync($"Execution Exception: {ex.Message}");
                });
                _currentProcess = null;
                return false;
            }
        }

        private static string? ResolveToolChainTag(UefiOptionsPage options)
        {
            if (!string.IsNullOrWhiteSpace(options.ToolChainTag))
                return options.ToolChainTag.Trim();

            if (ThreadHelper.CheckAccess())
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
                if (dte?.Version.StartsWith("18.", StringComparison.OrdinalIgnoreCase) == true)
                    return "VS2026";
                if (dte?.Version.StartsWith("17.", StringComparison.OrdinalIgnoreCase) == true)
                    return "VS2022";
            }

            string vsWherePath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                "Microsoft Visual Studio",
                "Installer",
                "vswhere.exe");
            if (!File.Exists(vsWherePath))
                return null;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = vsWherePath,
                    Arguments = "-latest -products * -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationVersion",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using var process = Process.Start(startInfo);
                if (process == null)
                    return null;

                string version = process.StandardOutput.ReadToEnd().Trim();
                process.WaitForExit();
                if (version.StartsWith("18.", StringComparison.OrdinalIgnoreCase))
                    return "VS2026";
                if (version.StartsWith("17.", StringComparison.OrdinalIgnoreCase))
                    return "VS2022";
            }
            catch
            {
                return null;
            }

            return null;
        }

        private static string? ResolveSolutionDirectory()
        {
            if (!ThreadHelper.CheckAccess())
                return null;

            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
            if (dte?.Solution == null || string.IsNullOrWhiteSpace(dte.Solution.FullName))
                return null;

            return Path.GetDirectoryName(dte.Solution.FullName);
        }

        private static string[] CopyBuildOutputs(string workspace, string activeDsc, UefiOptionsPage options, string toolChainTag, string solutionDirectory, string? activeProjectName)
        {
            var messages = new List<string>();
            string moduleName = !string.IsNullOrWhiteSpace(activeProjectName)
                ? activeProjectName!
                : Path.GetFileNameWithoutExtension(activeDsc);

            string buildRoot = ResolveOutputDirectory(workspace, activeDsc);
            string architectureDirectory = Path.Combine(
                buildRoot,
                $"{options.BuildTarget}_{toolChainTag}",
                options.Architecture.ToString());
            string workspaceBuildDirectory = Path.Combine(workspace, "Build");

            string efiFileName = moduleName + ".efi";
            string? efiFile = FindArtifactFile(architectureDirectory, efiFileName, SearchOption.TopDirectoryOnly)
                              ?? FindArtifactFile(architectureDirectory, efiFileName, SearchOption.AllDirectories)
                              ?? FindArtifactFile(buildRoot, efiFileName, SearchOption.AllDirectories)
                              ?? FindArtifactFile(workspaceBuildDirectory, efiFileName, SearchOption.AllDirectories);
            if (efiFile == null)
            {
                messages.Add($"Warning: No EFI output named {efiFileName} was found under {architectureDirectory}");
                return messages.ToArray();
            }

            string configurationDirectory = char.ToUpperInvariant(options.BuildTarget.ToString()[0]) +
                                             options.BuildTarget.ToString().Substring(1).ToLowerInvariant();
            string destinationDirectory = Path.Combine(
                solutionDirectory,
                options.Architecture.ToString().ToLowerInvariant(),
                configurationDirectory);
            Directory.CreateDirectory(destinationDirectory);

            string? efiDirectory = Path.GetDirectoryName(efiFile);
            string[] artifactRoots = new[] { efiDirectory, architectureDirectory, buildRoot }
                .Where(root => !string.IsNullOrEmpty(root))
                .Select(root => root!)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            string efiDestination = Path.Combine(destinationDirectory, Path.GetFileName(efiFile));
            if (!CopyArtifact(efiFile, efiDestination, messages))
                return messages.ToArray();

            string pdbFileName = moduleName + ".pdb";
            string pdbDestination = Path.Combine(destinationDirectory, pdbFileName);

            string? pdbFile = FindArtifactFromRoots(artifactRoots, pdbFileName)
                              ?? FindArtifactFile(workspaceBuildDirectory, pdbFileName, SearchOption.AllDirectories);

            if (pdbFile != null)
            {
                CopyArtifact(pdbFile, pdbDestination, messages);
            }
            else if (string.Equals(options.BuildTarget.ToString(), "DEBUG", StringComparison.OrdinalIgnoreCase))
            {
                messages.Add($"Warning: No PDB file was found for {moduleName}. Searched: {string.Join("; ", artifactRoots)}; {workspaceBuildDirectory}");
            }

            return messages.ToArray();
        }

        private static bool CopyArtifact(string sourceFile, string destination, List<string> messages)
        {
            string label = Path.GetExtension(sourceFile).TrimStart('.').ToUpperInvariant();

            try
            {
                if (!string.Equals(Path.GetFullPath(sourceFile), Path.GetFullPath(destination), StringComparison.OrdinalIgnoreCase))
                {
                    File.Copy(sourceFile, destination, true);
                }

                messages.Add($"{label} output copied: {destination}");
                return true;
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                messages.Add($"Warning: Could not copy {Path.GetFileName(sourceFile)} to {destination}: {ex.Message}");
                return false;
            }
        }

        private static string? FindArtifactFromRoots(IEnumerable<string> roots, string fileName)
        {
            foreach (string root in roots)
            {
                string? directMatch = FindArtifactFile(root, fileName, SearchOption.TopDirectoryOnly);
                if (directMatch != null)
                    return directMatch;
            }

            foreach (string root in roots)
            {
                string? nestedMatch = FindArtifactFile(root, fileName, SearchOption.AllDirectories);
                if (nestedMatch != null)
                    return nestedMatch;
            }

            return null;
        }

        private static string? FindArtifactFile(string? root, string fileName, SearchOption searchOption)
        {
            if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
                return null;

            try
            {
                return Directory.GetFiles(root, fileName, searchOption)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault();
            }
            catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
            {
                return null;
            }
        }

        private static string? ResolveActiveProjectName()
        {
            if (!ThreadHelper.CheckAccess())
                return null;

            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
            Array? activeProjects = dte?.ActiveSolutionProjects as Array;
            if (activeProjects == null || activeProjects.Length == 0)
                return null;

            return (activeProjects.GetValue(0) as EnvDTE.Project)?.Name;
        }

        private static string? ResolveActiveProjectDsc()
        {
            if (!ThreadHelper.CheckAccess())
                return null;

            ThreadHelper.ThrowIfNotOnUIThread();
            var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
            Array? activeProjects = dte?.ActiveSolutionProjects as Array;
            if (activeProjects == null || activeProjects.Length == 0)
                return null;

            var project = activeProjects.GetValue(0) as EnvDTE.Project;
            if (project == null || string.IsNullOrWhiteSpace(project.FullName))
                return null;

            string? projectDirectory = Path.GetDirectoryName(project.FullName);
            if (string.IsNullOrEmpty(projectDirectory) || !Directory.Exists(projectDirectory))
                return null;

            string projectDsc = Path.Combine(projectDirectory, project.Name + ".dsc");
            return File.Exists(projectDsc) ? projectDsc : null;
        }

        private static string ResolveOutputDirectory(string workspace, string activeDsc)
        {
            string dscPath = Path.IsPathRooted(activeDsc)
                ? activeDsc
                : Path.Combine(workspace, activeDsc);
            if (File.Exists(dscPath))
            {
                string dscContents = File.ReadAllText(dscPath);
                Match outputDirectoryMatch = Regex.Match(
                    dscContents,
                    @"(?im)^\s*OUTPUT_DIRECTORY\s*=\s*(\S+)");
                if (outputDirectoryMatch.Success)
                {
                    string outputDirectory = outputDirectoryMatch.Groups[1].Value.Trim().Replace('/', Path.DirectorySeparatorChar);
                    return Path.IsPathRooted(outputDirectory)
                        ? outputDirectory
                        : Path.Combine(workspace, outputDirectory);
                }
            }

            return Path.Combine(workspace, "Build", Path.GetFileNameWithoutExtension(activeDsc));
        }

        private static void ConfigureExternalToolEnvironment(ProcessStartInfo startInfo)
        {
            string localToolRoot = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "bin");
            string nasmDirectory = Path.Combine(localToolRoot, "NASM");
            string iaslDirectory = Path.Combine(localToolRoot, "IASL");

            if (File.Exists(Path.Combine(nasmDirectory, "nasm.exe")))
            {
                startInfo.EnvironmentVariables["NASM_PREFIX"] = nasmDirectory + Path.DirectorySeparatorChar;
                startInfo.EnvironmentVariables["PATH"] = nasmDirectory + ";" + startInfo.EnvironmentVariables["PATH"];
            }

            if (File.Exists(Path.Combine(iaslDirectory, "iasl.exe")))
            {
                startInfo.EnvironmentVariables["IASL_PREFIX"] = iaslDirectory + Path.DirectorySeparatorChar;
                startInfo.EnvironmentVariables["PATH"] = iaslDirectory + ";" + startInfo.EnvironmentVariables["PATH"];
            }
        }

        public static string ResolveWorkspace(UefiOptionsPage options)
        {
            if (!string.IsNullOrWhiteSpace(options.Workspace) && Directory.Exists(options.Workspace))
                return options.Workspace;

            // Try active solution folder on UI thread
            if (ThreadHelper.CheckAccess())
            {
                ThreadHelper.ThrowIfNotOnUIThread();
                var dte = Package.GetGlobalService(typeof(SDTE)) as EnvDTE.DTE;
                if (dte?.Solution != null && !string.IsNullOrEmpty(dte.Solution.FullName))
                {
                    string? solDir = Path.GetDirectoryName(dte.Solution.FullName);
                    if (!string.IsNullOrEmpty(solDir) && Directory.Exists(solDir))
                        return solDir;
                }
            }

            return options.Workspace;
        }
    }
}
