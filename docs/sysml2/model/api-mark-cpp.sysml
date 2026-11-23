package ApiMarkCpp {
    public import ApiMarkCpp_ClangAstParser::*;
    public import ApiMarkCpp_CppAstModel::*;
    public import ApiMarkCpp_CppEmitterGradualDisclosure::*;
    public import ApiMarkCpp_CppEmitterSingleFile::*;
    public import ApiMarkCpp_CppEmitter::*;
    public import ApiMarkCpp_CppGenerator::*;
    public import ApiMarkCpp_CppTypeLinkResolver::*;
    public import ApiMarkCpp_DocumentationCoverageChecker::*;
    private import OtsDependencies::*;
    private import SoftwareItemMetadata::*;


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
