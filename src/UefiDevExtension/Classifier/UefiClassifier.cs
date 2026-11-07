using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.VisualStudio.Language.StandardClassification;
using Microsoft.VisualStudio.Text;
using Microsoft.VisualStudio.Text.Classification;

namespace UefiDevExtension.Classifier
{
    public class UefiClassifier : IClassifier
    {
        private readonly IClassificationTypeRegistryService _registry;
        private readonly string _contentTypeName;

        private readonly IClassificationType _commentType;
        private readonly IClassificationType _keywordType;
        private readonly IClassificationType _preprocessorType;
        private readonly IClassificationType _stringType;
        private readonly IClassificationType _numberType;
        private readonly IClassificationType _sectionType;
        private readonly IClassificationType _guidType;

        // Regex patterns
        private static readonly Regex SectionRegex = new Regex(@"^\s*(\[[^\]\r\n]+\])", RegexOptions.Compiled);
        private static readonly Regex PreprocessorRegex = new Regex(@"^\s*(!include|!ifdef|!ifndef|!if|!else|!endif|DEFINE|EDK_GLOBAL)\b", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex GuidRegex = new Regex(@"(\b[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\b)|(\{\s*0x[0-9a-fA-F]+(\s*,\s*0x[0-9a-fA-F]+)+\s*\})", RegexOptions.Compiled);
        private static readonly Regex StringRegex = new Regex(@"""([^""\\]|\\.)*""", RegexOptions.Compiled);
        private static readonly Regex HexNumberRegex = new Regex(@"\b0x[0-9a-fA-F]+\b", RegexOptions.Compiled);
        private static readonly Regex DecNumberRegex = new Regex(@"\b\d+\b", RegexOptions.Compiled);

        private static readonly HashSet<string> CommonKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            // EDK II common
            "INF_VERSION", "BASE_NAME", "FILE_GUID", "MODULE_TYPE", "VERSION_STRING",
            "ENTRY_POINT", "UNLOAD_IMAGE", "CONSTRUCTOR", "DESTRUCTOR", "LIBRARY_CLASS",
            "PACKAGE_GUID", "PACKAGE_VERSION", "VALID_ARCHITECTURES", "DEC_SPECIFICATION",
            "DSC_SPECIFICATION", "OUTPUT_DIRECTORY", "SUPPORTED_ARCHITECTURES", "BUILD_TARGETS",
            "SKUID_IDENTIFIER", "FLASH_DEFINITION", "BUILD_NUMBER", "PCD_INFO_GENERATION",
            "TRUE", "FALSE", "COMMON", "SEC", "PEI_CORE", "PEIM", "DXE_CORE", "DXE_DRIVER",
            "DXE_RUNTIME_DRIVER", "DXE_SMM_DRIVER", "DXE_SAL_DRIVER", "UEFI_DRIVER", "UEFI_APPLICATION",
            "USER_DEFINED", "MM_STANDALONE", "MM_CORE_STANDALONE",

            // Sections keywords
            "Defines", "Sources", "Packages", "LibraryClasses", "Protocols", "Guids", "Ppis",
            "Pcd", "FixedPcd", "PatchPcd", "FeaturePcd", "PcdEx", "Depex", "BuildOptions",
            "Includes", "UserExtensions", "Components", "SkuIds", "PcdsFeatureFlag",
            "PcdsFixedAtBuild", "PcdsPatchableInModule", "PcdsDynamic", "PcdsDynamicEx",
            "PcdsDynamicDefault", "PcdsDynamicHii", "PcdsDynamicVpd",

            // FDF keywords
            "FD", "FV", "Rule", "SET", "FILE", "INF", "SECTION", "RAW", "PE32", "DXE_DEPEX",
            "PEI_DEPEX", "UI", "VER", "FV_IMAGE", "Capsule",

            // VFR keywords
            "formset", "form", "guid", "title", "help", "oneof", "option", "flags", "value",
            "endoneof", "checkbox", "numeric", "string", "goto", "text", "subtitle", "label",
            "grayoutif", "suppressif", "ideqval", "endif", "endform", "endformset", "reset_button",
            "default", "step", "minimum", "maximum",

            // ASL keywords
            "DefinitionBlock", "Device", "Scope", "Method", "Name", "Return", "Field",
            "OperationRegion", "Package", "If", "Else", "While", "Store", "Buffer",
            "CreateByteField", "CreateWordField", "CreateDWordField", "CreateQWordField",
            "Notify", "Acquire", "Release", "Sleep", "Local0", "Local1", "Local2", "Local3",
            "Local4", "Local5", "Local6", "Local7", "Arg0", "Arg1", "Arg2", "Arg3", "Arg4",
            "Arg5", "Arg6", "Zero", "One", "Ones"
        };

        private static readonly Regex IdentifierRegex = new Regex(@"\b[a-zA-Z_][a-zA-Z0-9_]*\b", RegexOptions.Compiled);

        public UefiClassifier(IClassificationTypeRegistryService registry, string contentTypeName)
        {
            _registry = registry;
            _contentTypeName = contentTypeName;

            _commentType = registry.GetClassificationType(PredefinedClassificationTypeNames.Comment);
            _keywordType = registry.GetClassificationType(PredefinedClassificationTypeNames.Keyword);
            _preprocessorType = registry.GetClassificationType(PredefinedClassificationTypeNames.PreprocessorKeyword);
            _stringType = registry.GetClassificationType(PredefinedClassificationTypeNames.String);
            _numberType = registry.GetClassificationType(PredefinedClassificationTypeNames.Number);
            _sectionType = registry.GetClassificationType(UefiClassificationDefinitions.SectionHeaderClassification);
            _guidType = registry.GetClassificationType(UefiClassificationDefinitions.GuidClassification);
        }

#pragma warning disable 67
        public event EventHandler<ClassificationChangedEventArgs>? ClassificationChanged;
#pragma warning restore 67

        public IList<ClassificationSpan> GetClassificationSpans(SnapshotSpan span)
        {
            var classifications = new List<ClassificationSpan>();
            ITextSnapshot snapshot = span.Snapshot;

            int startLine = span.Start.GetContainingLine().LineNumber;
            int endLine = span.End.GetContainingLine().LineNumber;

            for (int lineNum = startLine; lineNum <= endLine; lineNum++)
            {
                ITextSnapshotLine line = snapshot.GetLineFromLineNumber(lineNum);
                string text = line.GetText();
                int lineOffset = line.Start.Position;

                if (string.IsNullOrWhiteSpace(text))
                    continue;

                // 1. Check for Section header [ ... ] (primarily INF, DEC, DSC, FDF)
                Match sectionMatch = SectionRegex.Match(text);
                if (sectionMatch.Success)
                {
                    Group g = sectionMatch.Groups[1];
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + g.Index, g.Length), _sectionType));
                    continue;
                }

                // 2. Preprocessor directive (!include, DEFINE, etc.)
                Match prepMatch = PreprocessorRegex.Match(text);
                if (prepMatch.Success)
                {
                    Group g = prepMatch.Groups[1];
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + g.Index, g.Length), _preprocessorType));
                }

                // 3. Comments (# or //)
                int commentIndex = -1;
                // ASL/VFR use // and /*, EDK II uses # and //
                int hashIdx = text.IndexOf('#');
                int slashIdx = text.IndexOf("//", StringComparison.Ordinal);

                if (hashIdx >= 0 && slashIdx >= 0)
                    commentIndex = Math.Min(hashIdx, slashIdx);
                else if (hashIdx >= 0)
                    commentIndex = hashIdx;
                else if (slashIdx >= 0)
                    commentIndex = slashIdx;

                int codeLength = commentIndex >= 0 ? commentIndex : text.Length;

                if (commentIndex >= 0)
                {
                    int commentLen = text.Length - commentIndex;
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + commentIndex, commentLen), _commentType));
                }

                string codeText = text.Substring(0, codeLength);

                // 4. Strings
                foreach (Match m in StringRegex.Matches(codeText))
                {
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + m.Index, m.Length), _stringType));
                }

                // 5. GUIDs
                foreach (Match m in GuidRegex.Matches(codeText))
                {
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + m.Index, m.Length), _guidType));
                }

                // 6. Hex Numbers
                foreach (Match m in HexNumberRegex.Matches(codeText))
                {
                    classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + m.Index, m.Length), _numberType));
                }

                // 7. Keywords
                foreach (Match m in IdentifierRegex.Matches(codeText))
                {
                    if (CommonKeywords.Contains(m.Value))
                    {
                        classifications.Add(new ClassificationSpan(new SnapshotSpan(snapshot, lineOffset + m.Index, m.Length), _keywordType));
                    }
                }
            }

            return classifications;
        }
    }
}
