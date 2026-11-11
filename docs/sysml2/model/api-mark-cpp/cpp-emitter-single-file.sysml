package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppEmitterSingleFile {
        doc /* CppEmitterSingleFile writes all C++ API documentation into a single Markdown file. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/CppEmitterSingleFile.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppEmitterSingleFileTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-emitter-single-file.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-emitter-single-file.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-emitter-single-file.yaml");
        }
    }
}
