using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Utilities;

namespace UefiDevExtension.Classifier
{
    public static class UefiContentTypeDefinitions
    {
        // --- Content Types ---

        [Export]
        [Name("edk2-inf")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2InfContentType;

        [Export]
        [Name("edk2-dec")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2DecContentType;

        [Export]
        [Name("edk2-dsc")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2DscContentType;

        [Export]
        [Name("edk2-fdf")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2FdfContentType;

        [Export]
        [Name("edk2-uni")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2UniContentType;

        [Export]
        [Name("edk2-vfr")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? Edk2VfrContentType;

        [Export]
        [Name("uefi-asl")]
        [BaseDefinition("code")]
        public static ContentTypeDefinition? UefiAslContentType;

        // --- File Extension Mappings ---

        [Export]
        [FileExtension(".inf")]
        [ContentType("edk2-inf")]
        public static FileExtensionToContentTypeDefinition? InfFileExtension;

        [Export]
        [FileExtension(".dec")]
        [ContentType("edk2-dec")]
        public static FileExtensionToContentTypeDefinition? DecFileExtension;

        [Export]
        [FileExtension(".dsc")]
        [ContentType("edk2-dsc")]
        public static FileExtensionToContentTypeDefinition? DscFileExtension;

        [Export]
        [FileExtension(".fdf")]
        [ContentType("edk2-fdf")]
        public static FileExtensionToContentTypeDefinition? FdfFileExtension;

        [Export]
        [FileExtension(".uni")]
        [ContentType("edk2-uni")]
        public static FileExtensionToContentTypeDefinition? UniFileExtension;

        [Export]
        [FileExtension(".vfr")]
        [ContentType("edk2-vfr")]
        public static FileExtensionToContentTypeDefinition? VfrFileExtension;

        [Export]
        [FileExtension(".hfr")]
        [ContentType("edk2-vfr")]
        public static FileExtensionToContentTypeDefinition? HfrFileExtension;

        [Export]
        [FileExtension(".asl")]
        [ContentType("uefi-asl")]
        public static FileExtensionToContentTypeDefinition? AslFileExtension;

        [Export]
        [FileExtension(".dsl")]
        [ContentType("uefi-asl")]
        public static FileExtensionToContentTypeDefinition? DslFileExtension;
    }
}
