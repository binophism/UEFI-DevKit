/** @file
  UEFI DXE Driver Entry Point Source File.

  Copyright (c) 2026, MohammadMahdi Anbaraki. All rights reserved.
  SPDX-License-Identifier: BSD-2-Clause-Patent
**/

#include <Uefi.h>
#include <Library/UefiLib.h>
#include <Library/UefiBootServicesTableLib.h>
#include <Library/UefiDriverEntryPoint.h>
#include <Library/DebugLib.h>

/**
  Entry point of the DXE Driver.

  @param[in] ImageHandle    The firmware allocated handle for the EFI image.
  @param[in] SystemTable    A pointer to the EFI System Table.

  @retval EFI_SUCCESS       The driver initialized successfully.
  @retval other             Failed to initialize.
**/
EFI_STATUS EFIAPI $safeprojectname$DriverEntryPoint(EFI_HANDLE ImageHandle, EFI_SYSTEM_TABLE *SystemTable)
{
  DEBUG((DEBUG_INFO, "[$safeprojectname$] Driver loaded successfully!\n"));
  Print(L"[$safeprojectname$] DXE Driver Initialized.\n");

  return EFI_SUCCESS;
}
