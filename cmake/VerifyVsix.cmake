# Smoke-test a built .vsix.  Usage:
#   cmake -DVSIX_FILE=<path> -DEXPECTED_VERSION=<x.y.z> -P VerifyVsix.cmake
#
# A VSIX is a zip. This checks that it lists the entries VS needs to load the extension and
# that the version baked into extension.vsixmanifest matches the one CMake read from the source.

cmake_minimum_required(VERSION 3.21)

foreach(_var VSIX_FILE EXPECTED_VERSION)
  if(NOT DEFINED ${_var})
    message(FATAL_ERROR "VerifyVsix: -D${_var}=... is required")
  endif()
endforeach()

if(NOT EXISTS "${VSIX_FILE}")
  message(FATAL_ERROR "VerifyVsix: ${VSIX_FILE} does not exist - build the 'vsix' target first")
endif()

execute_process(
  COMMAND "${CMAKE_COMMAND}" -E tar tf "${VSIX_FILE}"
  OUTPUT_VARIABLE _listing
  ERROR_VARIABLE _list_err
  RESULT_VARIABLE _list_rc)
if(NOT _list_rc EQUAL 0)
  message(FATAL_ERROR "VerifyVsix: ${VSIX_FILE} is not a readable archive: ${_list_err}")
endif()
string(REPLACE "\r\n" "\n" _listing "${_listing}")
set(_listing "\n${_listing}\n")

set(_required
  extension.vsixmanifest
  UefiDevExtension.dll
  UefiDevExtension.pkgdef
  ProjectTemplates/UefiTemplates.vstman
  ProjectTemplates/UefiApplication/UefiApplication.vstemplate
  ProjectTemplates/UefiDriver/UefiDriver.vstemplate
  Snippets/uefi_app.snippet
  Snippets/uefi_dxe.snippet
  Snippets/uefi_inf.snippet
  Resources/UefiPackage.ico)

set(_missing "")
foreach(_entry IN LISTS _required)
  string(FIND "${_listing}" "\n${_entry}\n" _pos)
  if(_pos EQUAL -1)
    string(APPEND _missing "\n  ${_entry}")
  endif()
endforeach()
if(_missing)
  message(FATAL_ERROR "VerifyVsix: ${VSIX_FILE} is missing required entries:${_missing}")
endif()

# The manifest inside the package must carry the version CMake read from the source tree.
get_filename_component(_vsix_dir "${VSIX_FILE}" DIRECTORY)
set(_extract_dir "${_vsix_dir}/_verify")
file(REMOVE_RECURSE "${_extract_dir}")
file(ARCHIVE_EXTRACT INPUT "${VSIX_FILE}" DESTINATION "${_extract_dir}" PATTERNS extension.vsixmanifest)
file(READ "${_extract_dir}/extension.vsixmanifest" _manifest)
file(REMOVE_RECURSE "${_extract_dir}")

if(NOT _manifest MATCHES "<Identity[^>]* Version=\"([0-9]+(\\.[0-9]+)+)\"")
  message(FATAL_ERROR "VerifyVsix: no <Identity Version=...> in the packaged manifest")
endif()
if(NOT CMAKE_MATCH_1 STREQUAL EXPECTED_VERSION)
  message(FATAL_ERROR
    "VerifyVsix: packaged version is ${CMAKE_MATCH_1}, expected ${EXPECTED_VERSION}")
endif()

message(STATUS "VerifyVsix: OK - ${VSIX_FILE} (version ${EXPECTED_VERSION})")
