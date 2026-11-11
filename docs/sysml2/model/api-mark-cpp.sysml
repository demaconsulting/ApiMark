package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    import OtsDependencies::*;

    doc
    /*
     * ApiMarkCpp provides C++ language support by parsing selected public headers with clang and emitting gradual-disclosure Markdown documentation.
     */

    part def ApiMarkCppSystem {
        doc /* The ApiMarkCpp system as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-cpp.md");
            verificationFiles = ("docs/verification/api-mark-cpp.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp.yaml");
        }

        part cppGenerator : CppGenerator;
        part cppAstModel : CppAstModel;
        part clangAstParser : ClangAstParser;
        part cppEmitter : CppEmitter;
        part cppEmitterGradualDisclosure : CppEmitterGradualDisclosure;
        part cppEmitterSingleFile : CppEmitterSingleFile;
        part cppTypeLinkResolver : CppTypeLinkResolver;
        part clang : Clang;
        part documentationCoverageChecker : DocumentationCoverageChecker;
    }
}
