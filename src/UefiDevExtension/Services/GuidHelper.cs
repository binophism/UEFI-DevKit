using System;

namespace UefiDevExtension.Services
{
    public static class GuidHelper
    {
        public static string FormatCStruct(Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            uint d1 = BitConverter.ToUInt32(bytes, 0);
            ushort d2 = BitConverter.ToUInt16(bytes, 4);
            ushort d3 = BitConverter.ToUInt16(bytes, 6);

            return $"{{ 0x{d1:x8}, 0x{d2:x4}, 0x{d3:x4}, {{ 0x{bytes[8]:x2}, 0x{bytes[9]:x2}, 0x{bytes[10]:x2}, 0x{bytes[11]:x2}, 0x{bytes[12]:x2}, 0x{bytes[13]:x2}, 0x{bytes[14]:x2}, 0x{bytes[15]:x2} }} }}";
        }

        public static string FormatDecDeclaration(string name, Guid guid)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "gMyCustomGuid";

            return $"{name} = {FormatCStruct(guid)}";
        }

        public static string FormatCHeader(string name, Guid guid)
        {
            if (string.IsNullOrWhiteSpace(name))
                name = "MY_PROTOCOL";

            string upper = name.ToUpperInvariant().Replace(" ", "_");
            if (!upper.EndsWith("_GUID"))
                upper += "_GUID";

            string varName = "g" + char.ToUpperInvariant(name[0]) + (name.Length > 1 ? name.Substring(1) : "") + "Guid";

            return $"#define {upper} \\\n  {FormatCStruct(guid)}\n\nextern EFI_GUID {varName};";
        }

        public static string FormatRegistry(Guid guid)
        {
            return guid.ToString("D");
        }
    }
}
