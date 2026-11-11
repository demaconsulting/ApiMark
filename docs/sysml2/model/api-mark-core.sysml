package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    import OtsDependencies::*;

    doc
    /*
     * ApiMarkCore is a shared-contracts system. It defines the interfaces, output configuration, and Markdown writer helpers shared by all language generators.
     */

    part def ApiMarkCoreSystem {
        doc /* The ApiMarkCore system as a whole. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/api-mark-core.md");
            verificationFiles = ("docs/verification/api-mark-core.md");
            requirementFiles = ("docs/reqstream/api-mark-core.yaml");
        }

        part iApiGenerator : IApiGenerator;
        part iApiEmitter : IApiEmitter;
        part emitConfig : EmitConfig;
        part iContext : IContext;
        part iMarkdownWriterFactory : IMarkdownWriterFactory;
        part fileMarkdownWriterFactory : FileMarkdownWriterFactory;
        part iMarkdownWriter : IMarkdownWriter;
        part fileMarkdownWriter : FileMarkdownWriter;
        part pathHelpers : PathHelpers;
        part symbolNameRegistry : SymbolNameRegistry;
        part globFileCollector : GlobFileCollector;
        part fileSystemGlobbing : FileSystemGlobbing;
        part iDocumentationCoverageCapable : IDocumentationCoverageCapable;
    }
}
