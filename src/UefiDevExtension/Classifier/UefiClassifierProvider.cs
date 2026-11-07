using System.ComponentModel.Composition;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;
using Microsoft.VisualStudio.Utilities;

namespace UefiDevExtension.Classifier
{
    [Export(typeof(IClassifierProvider))]
    [ContentType("edk2-inf")]
    [ContentType("edk2-dec")]
    [ContentType("edk2-dsc")]
    [ContentType("edk2-fdf")]
    [ContentType("edk2-uni")]
    [ContentType("edk2-vfr")]
    [ContentType("uefi-asl")]
    public class UefiClassifierProvider : IClassifierProvider
    {
        [Import]
        public IClassificationTypeRegistryService ClassificationRegistry { get; set; } = null!;

        public IClassifier GetClassifier(ITextBuffer textBuffer)
        {
            return textBuffer.Properties.GetOrCreateSingletonProperty(() =>
                new UefiClassifier(ClassificationRegistry, textBuffer.ContentType.TypeName));
        }
    }
}
