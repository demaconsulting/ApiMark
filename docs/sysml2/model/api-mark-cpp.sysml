package ApiMarkCpp {
    import OtsDependencies::*;

    doc
    /*
     * ApiMarkCpp provides C++ language support by parsing selected public headers with clang and emitting gradual-disclosure Markdown documentation.
     */

    part def ApiMarkCppSystem {
        doc /* The ApiMarkCpp system as a whole. */

        comment designRef /* Design: docs/design/api-mark-cpp.md */
        comment verificationRef /* Verification: docs/verification/api-mark-cpp.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-cpp.yaml */

        part cppGenerator : CppGenerator;
        part cppAstModel : CppAstModel;
        part clangAstParser : ClangAstParser;
        part cppEmitter : CppEmitter;
        part cppEmitterGradualDisclosure : CppEmitterGradualDisclosure;
        part cppEmitterSingleFile : CppEmitterSingleFile;
        part cppTypeLinkResolver : CppTypeLinkResolver;
        part clang : Clang;
    }
}
