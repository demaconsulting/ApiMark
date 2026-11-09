package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppTypeLinkResolver {
        doc /* CppTypeLinkResolver resolves simplified C++ type strings to Markdown table-cell link targets. */

        metadata info : SoftwareInfo {
            description = "CppTypeLinkResolver resolves simplified C++ type strings to Markdown table-cell link targets.";
            sourceFiles = ("src/ApiMark.Cpp/CppTypeLinkResolver.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppTypeLinkResolverTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-type-link-resolver.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-type-link-resolver.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-type-link-resolver.yaml");
        }
    }
}
