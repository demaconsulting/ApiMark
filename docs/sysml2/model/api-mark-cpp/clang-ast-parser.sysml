package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def ClangAstParser {
        doc /* ClangAstParser is the internal C++ parsing unit used by CppGenerator; it invokes clang and builds the AST model from its JSON output. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/CppAst/ClangAstParser.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/ClangAstParserTests.cs");
            designFiles = ("docs/design/api-mark-cpp/clang-ast-parser.md");
            verificationFiles = ("docs/verification/api-mark-cpp/clang-ast-parser.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/clang-ast-parser.yaml");
        }
    }
}
