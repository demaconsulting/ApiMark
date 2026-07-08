package ApiMarkDotNet {
    import OtsDependencies::*;

    doc
    /*
     * ApiMarkDotNet provides C#/.NET language support by reading a compiled .NET assembly with Mono.Cecil and emitting gradual-disclosure Markdown documentation.
     */

    part def ApiMarkDotNetSystem {
        doc /* The ApiMarkDotNet system as a whole. */

        comment designRef /* Design: docs/design/api-mark-dot-net.md */
        comment verificationRef /* Verification: docs/verification/api-mark-dot-net.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-dot-net.yaml */

        part dotNetGenerator : DotNetGenerator;
        part dotNetAstModel : DotNetAstModel;
        part dotNetEmitter : DotNetEmitter;
        part dotNetEmitterGradualDisclosure : DotNetEmitterGradualDisclosure;
        part dotNetEmitterSingleFile : DotNetEmitterSingleFile;
        part typeLinkResolver : TypeLinkResolver;
        part xmlDocReader : XmlDocReader;
        part typeNameSimplifier : TypeNameSimplifier;
        part monoCecil : MonoCecil;
    }
}
