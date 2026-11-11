package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppGenerator {
        doc /* CppGenerator implements IApiGenerator for C++ libraries, collecting public headers and invoking clang to parse them. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/CppGenerator.cs", "src/ApiMark.Cpp/CppGeneratorOptions.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppGeneratorTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-generator.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-generator.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-generator.yaml");
        }
    }
}
