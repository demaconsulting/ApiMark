package ApiMarkDotNet {
    private import SoftwareItemMetadata::*;

    import OtsDependencies::*;

    doc
    /*
     * ApiMarkDotNet provides C#/.NET language support by reading a compiled .NET assembly with Mono.Cecil and emitting gradual-disclosure Markdown documentation.
     */

    part def ApiMarkDotNetSystem {
        doc /* The ApiMarkDotNet system as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-dot-net.md");
            verificationFiles = ("docs/verification/api-mark-dot-net.md");
            requirementFiles = ("docs/reqstream/api-mark-dot-net.yaml");
        }

        part dotNetGenerator : DotNetGenerator;
        part dotNetAstModel : DotNetAstModel;
        part dotNetEmitter : DotNetEmitter;
        part dotNetEmitterGradualDisclosure : DotNetEmitterGradualDisclosure;
        part dotNetEmitterSingleFile : DotNetEmitterSingleFile;
        part typeLinkResolver : TypeLinkResolver;
        part crefTargetResolver : CrefTargetResolver;
        part xmlDocReader : XmlDocReader;
        part externalXmlDocResolver : ExternalXmlDocResolver;
        part typeNameSimplifier : TypeNameSimplifier;
        part monoCecil : MonoCecil;
        part documentationCoverageChecker : DocumentationCoverageChecker;
    }
}
