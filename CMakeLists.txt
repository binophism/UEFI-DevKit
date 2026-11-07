cmake_minimum_required(VERSION 3.21)

# UEFI Dev Kit is a Visual Studio extension (VSIX): C# / WPF on .NET Framework 4.7.2,
# packaged by the VSSDK build tools. There is no C/C++ to compile with CMake itself, so this
# project drives MSBuild for the real build and layers CMake on top for what CMake is good at:
# one entry point (configure / build / test / install), presets, and CTest-based package checks.

set(UEFIDEVKIT_PROJECT_DIR "${CMAKE_CURRENT_SOURCE_DIR}/src/UefiDevExtension")
set(UEFIDEVKIT_CSPROJ      "${UEFIDEVKIT_PROJECT_DIR}/UefiDevExtension.csproj")
set(UEFIDEVKIT_MANIFEST    "${UEFIDEVKIT_PROJECT_DIR}/source.extension.vsixmanifest")

# The VSIX manifest is the single source of truth for the version.
file(READ "${UEFIDEVKIT_MANIFEST}" _uefidevkit_manifest)
if(NOT _uefidevkit_manifest MATCHES "<Identity[^>]* Version=\"([0-9]+(\\.[0-9]+)+)\"")
  message(FATAL_ERROR "Could not read the version from ${UEFIDEVKIT_MANIFEST}")
endif()
set(UEFIDEVKIT_VERSION "${CMAKE_MATCH_1}")
set_property(DIRECTORY APPEND PROPERTY CMAKE_CONFIGURE_DEPENDS "${UEFIDEVKIT_MANIFEST}")

project(UefiDevKit
  VERSION ${UEFIDEVKIT_VERSION}
  DESCRIPTION "UEFI / EDK II development experience for Visual Studio"
  HOMEPAGE_URL "https://github.com/binophism/UEFI-DevKit"
  LANGUAGES NONE)

if(NOT CMAKE_HOST_WIN32)
  message(FATAL_ERROR
    "UEFI Dev Kit builds a Visual Studio extension (WPF, .NET Framework 4.7.2, VSSDK) and "
    "can only be built on Windows with Visual Studio 2022+ or the Build Tools installed.")
endif()

list(PREPEND CMAKE_MODULE_PATH "${CMAKE_CURRENT_SOURCE_DIR}/cmake")
find_package(MSBuild REQUIRED)

get_property(_uefidevkit_multi_config GLOBAL PROPERTY GENERATOR_IS_MULTI_CONFIG)
if(_uefidevkit_multi_config)
  # The csproj only has Debug and Release; do not offer MinSizeRel / RelWithDebInfo.
  set(CMAKE_CONFIGURATION_TYPES "Debug;Release" CACHE STRING "Build configurations" FORCE)
elseif(NOT CMAKE_BUILD_TYPE)
  set(CMAKE_BUILD_TYPE Release CACHE STRING "Build type" FORCE)
endif()

# Everything MSBuild produces (bin, obj) lands in <repo>/build; see src/Directory.Build.props.
set(UEFIDEVKIT_MSBUILD_OUTPUT_ROOT "${CMAKE_CURRENT_SOURCE_DIR}/build")

set(UEFIDEVKIT_MSBUILD_ARGS "" CACHE STRING
  "Extra arguments appended to the MSBuild command line (semicolon-separated)")

# The csproj only defines Debug and Release; map every other CMake configuration to Release.
set(_msbuild_config "$<IF:$<CONFIG:Debug>,Debug,Release>")

set(_vsix_built "${UEFIDEVKIT_MSBUILD_OUTPUT_ROOT}/bin/${_msbuild_config}/net472/UefiDevExtension.vsix")
set(_vsix_dist  "${CMAKE_BINARY_DIR}/dist/$<CONFIG>/UefiDevKit-${UEFIDEVKIT_VERSION}.vsix")

# --- Build ---------------------------------------------------------------------------------
# MSBuild restores NuGet packages (-restore), compiles, and produces the .vsix. The result is
# copied to <build>/dist/<config>/ so that CI, install() and the tests have a stable location.
add_custom_target(vsix ALL
  COMMAND "${MSBuild_EXECUTABLE}" "${UEFIDEVKIT_CSPROJ}"
          -restore -nologo -m -nodeReuse:false -verbosity:minimal
          "-p:Configuration=${_msbuild_config}"
          ${UEFIDEVKIT_MSBUILD_ARGS}
  COMMAND "${CMAKE_COMMAND}" -E make_directory "${CMAKE_BINARY_DIR}/dist/$<CONFIG>"
  COMMAND "${CMAKE_COMMAND}" -E copy_if_different "${_vsix_built}" "${_vsix_dist}"
  BYPRODUCTS "${_vsix_dist}"
  COMMENT "Building UEFI Dev Kit ${UEFIDEVKIT_VERSION} VSIX with MSBuild"
  VERBATIM
  USES_TERMINAL)

add_custom_target(vsix_clean
  COMMAND "${MSBuild_EXECUTABLE}" "${UEFIDEVKIT_CSPROJ}"
          -restore -t:Clean -nologo -nodeReuse:false -verbosity:minimal
          "-p:Configuration=${_msbuild_config}"
  COMMAND "${CMAKE_COMMAND}" -E rm -f "${_vsix_dist}"
  COMMENT "Cleaning UEFI Dev Kit VSIX build output"
  VERBATIM
  USES_TERMINAL)

# --- Visual Studio solution ----------------------------------------------------------------
# With a Visual Studio generator (see build.ps1) the generated solution also contains the
# real C# project, so the sources can be browsed, edited and debugged in the IDE.
#
# The C# project depends on 'vsix' (not the other way round): in the IDE the 'vsix' target does
# the real work first (restore, build, copy to dist/) and the C# project then finds everything up
# to date, so the two never build the same files in parallel. It is kept out of ALL_BUILD because
# a Visual Studio C++ project referencing a C# project builds it with Platform=x64, which writes
# to the wrong folder and breaks NuGet resolution; `cmake --build` only needs the 'vsix' target.
if(CMAKE_GENERATOR MATCHES "^Visual Studio")
  include_external_msproject(UefiDevExtension "${UEFIDEVKIT_CSPROJ}"
    TYPE FAE04EC0-301F-11D3-BF4B-00C04F79EFBC
    PLATFORM "Any CPU"
    vsix)
  set_target_properties(UefiDevExtension PROPERTIES EXCLUDE_FROM_ALL ON)
  set_property(DIRECTORY PROPERTY VS_STARTUP_PROJECT UefiDevExtension)
endif()

# --- Tests ---------------------------------------------------------------------------------
# Run after building:  ctest --test-dir build -C Release
enable_testing()
add_test(NAME vsix.package
  COMMAND "${CMAKE_COMMAND}"
          "-DVSIX_FILE=${_vsix_dist}"
          "-DEXPECTED_VERSION=${UEFIDEVKIT_VERSION}"
          -P "${CMAKE_CURRENT_SOURCE_DIR}/cmake/VerifyVsix.cmake")

# --- Install -------------------------------------------------------------------------------
# cmake --install build --config Release --prefix <dir>
install(FILES "${_vsix_dist}" DESTINATION . COMPONENT vsix)
install(FILES "${CMAKE_CURRENT_SOURCE_DIR}/README.md" DESTINATION . COMPONENT vsix)
