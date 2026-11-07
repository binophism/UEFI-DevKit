using System;
using System.Runtime.InteropServices;
using Microsoft.VisualStudio.Shell;

namespace UefiDevExtension.ToolWindows
{
    [Guid("e491c14d-7204-4c55-8db6-43a9bf0df180")]
    public class UefiToolWindow : ToolWindowPane
    {
        public UefiToolWindow() : base(null)
        {
            this.Caption = "UEFI Development Kit";
            this.Content = new UefiToolWindowControl();
        }
    }
}
