package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppEmitter {
        doc /* CppEmitter implements IApiEmitter for C++ documentation, storing the parsed AST and dispatching to an emit strategy. */

        metadata info : SoftwareInfo {
            sourceFiles = ("src/ApiMark.Cpp/CppEmitter.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppEmitterTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-emitter.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-emitter.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-emitter.yaml");
        }
    }
}
