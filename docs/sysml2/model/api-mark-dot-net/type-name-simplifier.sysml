package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    part def TypeNameSimplifier {
        doc /* TypeNameSimplifier converts Mono.Cecil type references into idiomatic, readable C# type names. */

        metadata info : SoftwareInfo {
            description = "TypeNameSimplifier converts Mono.Cecil type references into idiomatic, readable C# type names.";
            sourceFiles = ("src/ApiMark.DotNet/TypeNameSimplifier.cs");
            testFiles = ("test/ApiMark.DotNet.Tests/TypeNameSimplifierTests.cs");
            designFiles = ("docs/design/api-mark-dot-net/type-name-simplifier.md");
            verificationFiles = ("docs/verification/api-mark-dot-net/type-name-simplifier.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net/type-name-simplifier.yaml");
        }
    }
}
