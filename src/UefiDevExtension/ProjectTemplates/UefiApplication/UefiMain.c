/** @file
  UEFI Application Entry Point Source File.

  Copyright (c) 2026, MohammadMahdi Anbaraki. All rights reserved.
  SPDX-License-Identifier: BSD-2-Clause-Patent
**/

#include <Uefi.h>
#include <Library/UefiLib.h>
#include <Library/UefiBootServicesTableLib.h>
#include <Library/UefiApplicationEntryPoint.h>

/**
  The user Entry Point for Application. The user code starts with this function
  as the real entry point for the application.

  @param[in] ImageHandle    The firmware allocated handle for the EFI image.
  @param[in] SystemTable    A pointer to the EFI System Table.

  @retval EFI_SUCCESS       The entry point is executed successfully.
  @retval other             Some error occurs when executing this function.
**/
EFI_STATUS EFIAPI UefiMain (EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *SystemTable)
{
  Print(L"====================================================\n");
  Print(L"  UEFI Application: $safeprojectname$\n");
  Print(L"  Welcome to UEFI Dev Kit!\n");
  Print(L"====================================================\n");

  return EFI_SUCCESS;
}
