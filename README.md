# UEFI Dev Kit

**A complete UEFI / EDK II (TianoCore) development experience inside Visual Studio.**

![Visual Studio](https://img.shields.io/badge/Visual%20Studio-2022%20%7C%202026-5C2D91?logo=visualstudio&logoColor=white)
![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.7.2-512BD4?logo=dotnet)
![Platform](https://img.shields.io/badge/platform-Windows-0078D6?logo=windows&logoColor=white)
![Status](https://img.shields.io/badge/status-active-brightgreen)

UEFI Dev Kit turns Visual Studio into a firmware development environment: it highlights EDK II file
formats, drives the EDK II build system, boots the result in QEMU with OVMF, and generates EFI GUIDs
in every format you need — without leaving the IDE.

Developed by **MohammadMahdi Anbaraki**.

---

## Table of contents

- [Features](#features)
- [Requirements](#requirements)
- [Installation](#installation)
- [Quick start](#quick-start)
- [Keyboard shortcuts](#keyboard-shortcuts)
- [Settings reference](#settings-reference)
- [Repository layout](#repository-layout)
- [Building from source](#building-from-source)
- [Troubleshooting](#troubleshooting)
- [Author](#author)

---

## Features

### Dedicated UEFI menu

Everything is reachable from **Extensions → UEFI Development**:

| Command | What it does |
| --- | --- |
| **UEFI Tools & Explorer** | Opens the dockable tool window (also under **Tools**) |
| **Build Active UEFI Target (DSC)** | Builds the active `.dsc` platform with `build.exe` |
| **Rebuild Active UEFI Target** | Cleans and rebuilds from scratch |
| **Clean UEFI Target Build Artifacts** | Removes previous build output |
| **Run in QEMU (OVMF)** | Boots the built firmware or a `.efi` image in QEMU |
| **EFI GUID Generator...** | Generates EFI-compatible GUIDs in three formats |
| **Insert EFI GUID at Caret** | Inserts a C `EFI_GUID` structure at the editor caret |
| **Open UEFI Developer Shell** | Opens a shell with `WORKSPACE` set and `edksetup.bat` applied |
| **UEFI Development Settings...** | Jumps straight to the extension's options page |

### UEFI Tools & Explorer window

A dockable panel (WPF) that keeps the whole workflow in one place:

- **Workspace setup** — pick the EDK II root, apply it to the environment, auto-detect an existing
  EDK II installation, and fix IntelliSense include paths for the active project.
- **Build & target settings** — architecture (`X64`, `IA32`, `ARM`, `AARCH64`, `RISCV64`), target
  (`DEBUG`, `RELEASE`, `NOOPT`), toolchain tag, and the active `.dsc` file.
- **Build controls** — Build / Rebuild / Clean, plus a one-click QEMU launch.
- **EFI GUID generator** — generates a fresh GUID and renders it as a C structure, a DEC/INF
  definition, or a registry/UUID string, each with copy buttons and direct insertion into the editor.
- **Diagnostics** — live status for the workspace, `QEMU`, and the `OVMF` firmware, with shortcuts to
  the developer shell and settings.

### Syntax highlighting for EDK II files

Built on the Visual Studio MEF editor classifier, with smart highlighting of section headers
(`[Defines]`, `[Sources]`, `[Packages]`, …), preprocessor directives (`!include`, `DEFINE`), keywords,
GUIDs, strings, and numbers:

| Extension | File type |
| --- | --- |
| `.inf` | Module information files |
| `.dec` | Package declaration files |
| `.dsc` | Platform description files |
| `.fdf` | Flash device / image description files |
| `.uni` | HII unicode string files |
| `.vfr`, `.hfr` | HII visual forms |
| `.asl`, `.dsl` | ACPI Source Language |

### Output pane

A dedicated **UEFI Development** pane in the Output window streams `build.exe` and `edksetup.bat`
output live, with optional timestamps and elapsed build time.

### Code snippets

| Shortcut | Content |
| --- | --- |
| `uefiapp` | Full UEFI application skeleton with a `UefiMain` entry point |
| `uefidxe` | Full DXE driver skeleton with a `DriverEntry` entry point |
| `uefiinf` | Standard EDK II module `.inf` template |

### Project templates

- **UEFI Application (EDK II)** — C source, `.inf`, `.dsc`, and `.vcxproj`, ready to build.
- **UEFI DXE Driver (EDK II)** — the same structure for driver development.

---

## Requirements

| Component | Notes |
| --- | --- |
| **Visual Studio 2022 (17.x) or 2026 (18.x)** | Community, Professional, or Enterprise, amd64 |
| **.NET Framework 4.7.2** | Required by the VSIX |
| **EDK II workspace** | A checkout of [edk2](https://github.com/tianocore/edk2) containing `edksetup.bat` |
| **NASM** | **Required** — the assembler EDK II builds with; `nasm.exe` must be on `PATH` |
| **IASL** | **Required** — Intel ACPI Source Language compiler used for `.asl` / `.dsl` and ACPI tables; `iasl.exe` must be on `PATH` |
| **Python 3** | **Required** — runs the EDK II BaseTools (`build.exe`) |
| **C++ toolchain** | **Required** — the *Desktop development with C++* workload of a Visual Studio install |
| **QEMU** *(optional)* | `qemu-system-x86_64.exe`, for running images |
| **OVMF firmware** *(optional)* | `OVMF.fd`, either built from `OvmfPkg` or downloaded |

The extension drives these tools but does not install them. **NASM, IASL, and Python 3 must be
installed before your first build**, otherwise EDK II's `build.exe` stops with a *tool not found*
error.

### Installing the required tools

| Tool | Where the extension looks for it |
| --- | --- |
| **NASM** | `%LOCALAPPDATA%\bin\NASM\nasm.exe` (added to `PATH` with `NASM_PREFIX` set automatically), or anywhere on `PATH` |
| **IASL** | `%LOCALAPPDATA%\bin\IASL\iasl.exe` (added to `PATH` with `IASL_PREFIX` set automatically), or anywhere on `PATH` |

```powershell
# NASM - required assembler
winget install NASM.NASM

# Python 3 - required by the EDK II BaseTools
winget install Python.Python.3.12
```

**IASL** (Intel ACPI Source Language compiler) ships with the ACPI Component Architecture tools:
download them from [acpica.org](https://www.acpica.org), then copy `iasl.exe` into
`%LOCALAPPDATA%\bin\IASL\` or add its folder to `PATH`. It is what compiles `.asl` / `.dsl` ACPI
sources, so platforms that build ACPI tables will not compile without it.

After changing `PATH`, restart Visual Studio so the new entries are inherited. **Open UEFI Developer
Shell** always starts with the current environment, so you can verify with `nasm -v`, `iasl -v`, and
`python --version`.

---

## Installation

### From a built VSIX

1. Close Visual Studio.
2. Double-click the `.vsix` (download it from the GitHub Releases page, or build it yourself —
   it ends up in `build\dist\Release\`).
3. Click **Install** in the VSIX Installer window.
4. Start Visual Studio — the **UEFI Development** submenu appears under **Extensions**.

### From source

See [Building from source](#building-from-source), then install the generated VSIX as above.

---

## Quick start

1. Open **Tools → Options → UEFI Development → General** and set **EDK II Workspace Root** to the
   folder that contains `edksetup.bat` (for example `D:\edk2`).
2. Open **Extensions → UEFI Development → UEFI Tools & Explorer** and click **Auto-Detect** (or
   **Apply & Fix Headers**) to sync `WORKSPACE` and IntelliSense.
3. Set the **Active DSC** file, for example `OvmfPkg/OvmfPkgX64.dsc`, and pick your architecture and
   target.
4. Press **Build** (or `Ctrl+Alt+B`). Output streams into the **UEFI Development** pane.
5. Press **Run in QEMU (OVMF)** (or `Ctrl+Alt+Q`) to boot the result.

To build existing EDK II modules from a Visual Studio solution, use the **UEFI Application** /
**UEFI DXE Driver** templates as a starting point, or point the active `.dsc` at your platform and let
the extension drive `build.exe`.

---

## Keyboard shortcuts

| Shortcut | Command |
| --- | --- |
| `Ctrl+Alt+B` | Build Active UEFI Target (DSC) |
| `Ctrl+Alt+Q` | Run in QEMU (OVMF) |
| `Ctrl+Alt+G` | EFI GUID Generator |

---

## Settings reference

**Tools → Options → UEFI Development → General**

### EDK II Build Settings

| Setting | Description | Default |
| --- | --- | --- |
| EDK II Workspace Root | Path to the workspace containing `edksetup.bat` | `D:\edk2` |
| Packages Path (`PACKAGES_PATH`) | Extra package search paths, separated by semicolons | *empty* |
| Active DSC Platform File | Path to the active `.dsc` platform description | `OvmfPkg/OvmfPkgX64.dsc` |
| Target Architecture | `X64`, `IA32`, `ARM`, `AARCH64`, `RISCV64` | `X64` |
| Tool Chain Tag | Toolchain identifier from `Conf/tools_def.txt`; empty uses `target.txt` | *empty* |
| Build Target | `DEBUG`, `RELEASE`, `NOOPT` | `DEBUG` |
| Parallel Threads | Concurrent build threads (`build -n`) | `4` |

### QEMU & OVMF Emulation

| Setting | Description | Default |
| --- | --- | --- |
| QEMU Executable Path | `qemu-system-x86_64.exe` path, or auto-detected from `PATH` | `qemu-system-x86_64` |
| OVMF Firmware File | Path to `OVMF.fd` | *empty* |
| Virtual Machine Memory | Guest memory size (`512M`, `1G`, …) | `512M` |
| Virtual FAT Shared Directory | Directory mounted as a FAT drive, visible as `fs0:` in the UEFI Shell | *empty* |
| Additional QEMU Arguments | Extra command-line arguments for QEMU | `-net none` |

### Output Window

| Setting | Description | Default |
| --- | --- | --- |
| Clear Output Before Build | Clear the pane before each build | `true` |
| Show Timestamp | Prefix output lines with a timestamp | `true` |

---

## Repository layout

```
UEFI-DevKit/
├── CMakeLists.txt                          # CMake entry point (drives MSBuild, CTest, install)
├── CMakePresets.json                       # configure / build / test presets
├── cmake/                                  # FindMSBuild + VSIX package verification
├── .github/workflows/ci.yml                # CI build + automatic release on tags
├── build.ps1                               # Generates the Visual Studio solution into build\
├── README.md
├── build/                                  # (generated, git-ignored) solution, bin, obj, dist
└── src/
    ├── Directory.Build.props               # Redirects MSBuild bin\obj output into build\
    └── UefiDevExtension/
        ├── UefiDevExtension.csproj         # SDK-style project compatible with VSSDK
        ├── source.extension.vsixmanifest   # VSIX manifest (name, publisher, assets)
        ├── UefiPackage.cs                  # AsyncPackage entry point
        ├── UefiPackage.vsct                # Menus, buttons, key bindings
        ├── Classifier/                     # EDK II syntax highlighting (MEF)
        ├── Commands/                       # Menu command handlers
        ├── Options/                        # Tools → Options page
        ├── Services/                       # Build runner, QEMU runner, GUID helper
        ├── ToolWindows/                    # UEFI Tools & Explorer (WPF)
        ├── ProjectTemplates/               # UEFI Application / DXE Driver templates
        ├── Snippets/                       # Code snippets
        └── Resources/                      # Icons and images
```

---

## Building from source

There is no hand-maintained solution file: the Visual Studio solution is generated from
`CMakeLists.txt`. Everything it produces — the solution, `bin`, `obj` and the packaged `.vsix` —
lives in the `build\` folder, so the source tree stays clean and `build\` is safe to delete.
The version comes from `source.extension.vsixmanifest`; there is nothing to keep in sync in CMake.

### Visual Studio solution with `build.ps1` (recommended)

[`build.ps1`](build.ps1) generates the Visual Studio solution from `CMakeLists.txt` into
`build\UefiDevKit.sln`. It only generates the solution; the actual compiling happens when you
build it (in Visual Studio or from a shell, see below).

**Requirements:** Windows, Visual Studio 2022 or newer, and CMake 3.21+. You do not have to install
CMake separately: if it is not on `PATH`, the script uses the copy bundled with Visual Studio
(the *C++ CMake tools for Windows* component).

**Usage** — run it from the repository root in PowerShell:

```powershell
.\build.ps1               # generate build\UefiDevKit.sln
.\build.ps1 -Open         # generate it and open it in Visual Studio
.\build.ps1 -Clean        # delete build\ first, then generate from scratch
.\build.ps1 -Clean -Open -Generator "Visual Studio 17 2022"
```

Type the `.ps1` extension: plain `.\build` refers to the `build\` folder, not the script. If
PowerShell refuses to run it because of the execution policy, use:

```powershell
powershell -ExecutionPolicy Bypass -File .\build.ps1 -Open
```

**Parameters**

| Parameter | Description |
| --- | --- |
| `-Open` | Opens the generated solution when the script finishes. |
| `-Clean` | Deletes the whole `build\` folder (solution, `bin`, `obj`, `dist`, `build\ninja`) before generating. Use it when switching Visual Studio versions or after a broken configure. |
| `-Generator <name>` | CMake generator to use, for example `"Visual Studio 17 2022"`. By default the script picks the newest installed Visual Studio for which your CMake has a generator. |

**What the script does**

1. Finds the installed Visual Studio instances with `vswhere` and sorts them newest first.
2. Finds CMake (`PATH` first, then the one bundled with Visual Studio).
3. Chooses the generator: Visual Studio 2022 → `Visual Studio 17 2022`, Visual Studio 2026 →
   `Visual Studio 18 2026`, skipping any that your CMake does not support.
4. Stops with an error if `build\` was already configured with a different generator (pass
   `-Clean` to start over).
5. Runs `cmake -S . -B build -G <generator> -A x64` and prints the path of the solution.

**Using the solution**

The solution contains the real C# project (`UefiDevExtension`, set as the startup project) so you
can browse, edit and debug the sources, plus the CMake helper projects (`ALL_BUILD`, `vsix`,
`vsix_clean`, ...). **Build → Build Solution** restores the NuGet packages, builds the extension
and copies the package to `build\dist\<Configuration>\UefiDevKit-<version>.vsix`. Pick `Debug` or
`Release` in the configuration box; the other CMake configurations are not offered because the
project only defines those two.

The same build works from a shell, and `ctest` checks that the package contains everything Visual
Studio needs and that its version matches `source.extension.vsixmanifest`:

```powershell
cmake --build build --config Release
ctest --test-dir build -C Release --output-on-failure
```

If `cmake` is not on your `PATH`, run these two commands from a *Developer PowerShell for VS*
(the script itself does not need that).

**Where things end up** (all inside `build\`, which is git-ignored and safe to delete)

| Path | Content |
| --- | --- |
| `build\UefiDevKit.sln` | The generated solution (plus the CMake `.vcxproj` helper projects and cache) |
| `build\bin\<Configuration>\net472\` | Compiled extension and the raw `UefiDevExtension.vsix` |
| `build\obj\` | Intermediate files and NuGet restore output |
| `build\dist\<Configuration>\` | The packaged `UefiDevKit-<version>.vsix` you install or release |

**Troubleshooting `build.ps1`**

| Message | Fix |
| --- | --- |
| `vswhere.exe not found` / `No Visual Studio installation with MSBuild was found` | Install Visual Studio 2022 or newer (or its Build Tools). |
| `CMake was not found` | Install CMake, or add the *C++ CMake tools for Windows* component in the Visual Studio Installer. |
| `None of the installed Visual Studio versions has a generator in this CMake` | Update CMake (Visual Studio 2026 needs a recent CMake), or pass `-Generator` explicitly. |
| `'...\build' was configured with '<X>', not '<Y>'` | Re-run with `-Clean`. |
| `cannot be loaded because running scripts is disabled` | Run it through `powershell -ExecutionPolicy Bypass -File .\build.ps1`. |
| Errors about missing `Microsoft.Cpp.*` files while building the solution | The generated helper projects are C++ projects, so the *Desktop development with C++* workload is probably missing; install it, or use the Ninja presets below. |

### Command line without an IDE (Ninja)

If you only want a headless build (for example on a machine without the C++ workload), the Ninja
presets drive MSBuild directly and keep their files in `build\ninja\`:

```powershell
cmake --preset default            # configure into .\build\ninja
cmake --build --preset release    # restore NuGet packages + build the VSIX
ctest --preset release            # verify the package contents and version
```

The VSIX is copied to `build\ninja\dist\Release\UefiDevKit-<version>.vsix`. Use `--preset debug`
for a Debug build, `--target vsix_clean` to clean, and
`cmake --install build\ninja --config Release --prefix <dir>` to install the VSIX and README
somewhere. Pass `-DMSBuild_EXECUTABLE=<path>` at configure time to pick a specific MSBuild.

### With MSBuild directly

Restore packages, then build with the MSBuild that ships with Visual Studio:

```powershell
dotnet restore "src\UefiDevExtension\UefiDevExtension.csproj"

& "C:\Program Files\Microsoft Visual Studio\18\Enterprise\MSBuild\Current\Bin\MSBuild.exe" `
    "src\UefiDevExtension\UefiDevExtension.csproj" /p:Configuration=Release
```

Adjust the MSBuild path for your Visual Studio version and edition (for example
`...\2022\Community\MSBuild\Current\Bin\MSBuild.exe`).

The VSIX is produced at the following path (`src\Directory.Build.props` redirects MSBuild output
into `build\`):

```
build\bin\Release\net472\UefiDevExtension.vsix
```

---

## Troubleshooting

**The UEFI menu does not appear**
Confirm the extension is installed under **Extensions → Manage Extensions → Installed**, then restart
Visual Studio.

**`Error: Workspace path '…' does not exist`**
Set a valid **EDK II Workspace Root** in the options page. It must be the folder containing
`edksetup.bat`, not a package subfolder.

**`Could not detect a supported Visual Studio installation`**
Set the **Tool Chain Tag** manually (for example `VS2022` or `VS2026`) so it matches an entry in
`Conf/tools_def.txt`.

**QEMU does not start**
Install QEMU, then either add it to `PATH` or set **QEMU Executable Path** to the full path of
`qemu-system-x86_64.exe`.

**No firmware found**
Build `OvmfPkg` (for example `OvmfPkg/OvmfPkgX64.dsc`) and point **OVMF Firmware File** at the
resulting `OVMF.fd`, or provide a prebuilt one.

**IntelliSense cannot find UEFI headers**
Open the tool window and click **Apply & Fix Headers** after setting the workspace.

**`'nasm' is not recognized` or NASM-related build failure**
Install NASM and make sure `nasm.exe` is reachable: either put it in `%LOCALAPPDATA%\bin\NASM\`
(the extension then sets `NASM_PREFIX` and prepends it to `PATH`) or add it to the system `PATH`.
Check with `nasm -v` in the UEFI Developer Shell.

**`'iasl' is not recognized` or ACPI tables fail to compile**
Install the ACPICA tools and place `iasl.exe` in `%LOCALAPPDATA%\bin\IASL\` or on `PATH`. Verify with
`iasl -v`.

**`python` is not recognized by `edksetup.bat`**
Install Python 3, tick *Add Python to PATH* during setup, and confirm `python --version` works in a
new shell.

---

## Authors

- **MohammadMahdi Anbaraki**
- **Amir Mohammad Jahangirzad**

Issues and feature requests are welcome through the GitHub issue tracker.

---

<sub>UEFI Dev Kit — built for firmware engineers who would rather stay in Visual Studio.</sub>
