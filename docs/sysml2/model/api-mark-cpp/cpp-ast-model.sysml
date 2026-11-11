package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppAstModel {
        doc /* CppAstModel defines the immutable record types that represent the parsed C++ AST. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/CppAst/CppAstModel.cs", "src/ApiMark.Cpp/CppExternalTypeInfo.cs", "src/ApiMark.Cpp/ApiVisibility.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppAstModelTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-ast-model.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-ast-model.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-ast-model.yaml");
        }
    }
}
