#[=======================================================================[.rst:
FindMSBuild
-----------

Locates the *full* (.NET Framework) MSBuild that ships with Visual Studio or the Visual Studio
Build Tools. ``dotnet build`` is not a substitute: VSIX packaging needs the Framework MSBuild.

Result variables::

  MSBuild_FOUND        - true if MSBuild was found
  MSBuild_EXECUTABLE   - full path to MSBuild.exe

Set ``MSBuild_EXECUTABLE`` yourself (``-DMSBuild_EXECUTABLE=...``) to force a specific install.
Otherwise the newest Visual Studio found by ``vswhere`` wins, then ``msbuild`` on ``PATH``.
#]=======================================================================]

include(FindPackageHandleStandardArgs)

if(NOT MSBuild_EXECUTABLE)
  find_program(MSBuild_VSWHERE
    NAMES vswhere
    HINTS "$ENV{ProgramFiles\(x86\)}/Microsoft Visual Studio/Installer"
          "$ENV{ProgramFiles}/Microsoft Visual Studio/Installer"
    DOC "Path to vswhere.exe")
  mark_as_advanced(MSBuild_VSWHERE)

  if(MSBuild_VSWHERE)
    execute_process(
      COMMAND "${MSBuild_VSWHERE}" -latest -products "*"
              -requires Microsoft.Component.MSBuild
              -find "MSBuild\\**\\Bin\\MSBuild.exe"
      OUTPUT_VARIABLE _msbuild_vswhere_out
      RESULT_VARIABLE _msbuild_vswhere_rc
      OUTPUT_STRIP_TRAILING_WHITESPACE
      ERROR_QUIET)
    if(_msbuild_vswhere_rc EQUAL 0 AND _msbuild_vswhere_out)
      string(REGEX REPLACE "[\r\n].*" "" _msbuild_first "${_msbuild_vswhere_out}")
      file(TO_CMAKE_PATH "${_msbuild_first}" _msbuild_first)
      if(EXISTS "${_msbuild_first}")
        set(MSBuild_EXECUTABLE "${_msbuild_first}" CACHE FILEPATH "Path to MSBuild.exe")
      endif()
    endif()
  endif()
endif()

if(NOT MSBuild_EXECUTABLE)
  find_program(MSBuild_EXECUTABLE NAMES msbuild MSBuild DOC "Path to MSBuild.exe")
endif()

find_package_handle_standard_args(MSBuild REQUIRED_VARS MSBuild_EXECUTABLE)
mark_as_advanced(MSBuild_EXECUTABLE)
