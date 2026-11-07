using System.ComponentModel.Composition;
using System.Windows.Media;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace UefiDevExtension.Classifier
{
    public static class UefiClassificationDefinitions
    {
        public const string SectionHeaderClassification = "uefi.section";
        public const string GuidClassification = "uefi.guid";

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(SectionHeaderClassification)]
        public static ClassificationTypeDefinition? SectionClassificationDefinition;

        [Export(typeof(ClassificationTypeDefinition))]
        [Name(GuidClassification)]
        public static ClassificationTypeDefinition? GuidClassificationDefinition;

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = SectionHeaderClassification)]
        [Name(SectionHeaderClassification)]
        [UserVisible(true)]
        [Order(After = Priority.High)]
        public sealed class UefiSectionFormat : ClassificationFormatDefinition
        {
            public UefiSectionFormat()
            {
                DisplayName = "UEFI Section Header [ ... ]";
                ForegroundColor = Color.FromRgb(43, 145, 175); // Teal / Cyan
                IsBold = true;
            }
        }

        [Export(typeof(EditorFormatDefinition))]
        [ClassificationType(ClassificationTypeNames = GuidClassification)]
        [Name(GuidClassification)]
        [UserVisible(true)]
        [Order(After = Priority.High)]
        public sealed class UefiGuidFormat : ClassificationFormatDefinition
        {
            public UefiGuidFormat()
            {
                DisplayName = "UEFI GUID";
                ForegroundColor = Color.FromRgb(180, 100, 220); // Purple / Violet
            }
        }
    }
}
