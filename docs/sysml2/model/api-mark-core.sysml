package ApiMarkCore {
    import OtsDependencies::*;

    doc
    /*
     * ApiMarkCore is a shared-contracts system. It defines the interfaces, output configuration, and Markdown writer helpers shared by all language generators.
     */

    part def ApiMarkCoreSystem {
        doc /* The ApiMarkCore system as a whole. */

        comment designRef /* Design: docs/design/api-mark-core.md */
        comment verificationRef /* Verification: docs/verification/api-mark-core.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-core.yaml */

        part iApiGenerator : IApiGenerator;
        part iApiEmitter : IApiEmitter;
        part emitConfig : EmitConfig;
        part iContext : IContext;
        part iMarkdownWriterFactory : IMarkdownWriterFactory;
        part fileMarkdownWriterFactory : FileMarkdownWriterFactory;
        part iMarkdownWriter : IMarkdownWriter;
        part fileMarkdownWriter : FileMarkdownWriter;
        part pathHelpers : PathHelpers;
        part globFileCollector : GlobFileCollector;
        part fileSystemGlobbing : FileSystemGlobbing;
    }
}
