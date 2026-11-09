package ApiMarkCpp {
    private import SoftwareItemMetadata::*;

    part def CppEmitterGradualDisclosure {
        doc /* CppEmitterGradualDisclosure writes the multi-file Markdown layout for C++ API documentation. */

        metadata info : SoftwareInfo {
            description = "CppEmitterGradualDisclosure writes the multi-file Markdown layout for C++ API documentation.";
            sourceFiles = ("src/ApiMark.Cpp/CppEmitterGradualDisclosure.cs");
            testFiles = ("test/ApiMark.Cpp.Tests/CppEmitterGradualDisclosureTests.cs");
            designFiles = ("docs/design/api-mark-cpp/cpp-emitter-gradual-disclosure.md");
            verificationFiles = ("docs/verification/api-mark-cpp/cpp-emitter-gradual-disclosure.md");
            requirementFiles = ("docs/reqstream/api-mark-cpp/cpp-emitter-gradual-disclosure.yaml");
        }
    }
}
