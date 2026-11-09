package ApiMarkCore {
    private import SoftwareItemMetadata::*;

    part def SymbolNameRegistry {
        doc /* SymbolNameRegistry is the single source of truth for the file and folder name of every
              generated page. Symbols are registered first, all names are resolved once without collisions,
              and only then do writers and link resolvers read the approved names. FileNameRules escapes
              names and LinkPath builds relative links. */

        metadata info : SoftwareInfo {
            description = "SymbolNameRegistry is the single source of truth for the file and folder name of every generated page. Symbols are registered first, all names are resolved once without collisions, and only then do writers and link resolvers read the approved names. FileNameRules escapes names and LinkPath builds relative links.";
            sourceFiles = ("src/ApiMark.Core/SymbolNameRegistry.cs", "src/ApiMark.Core/FileNameRules.cs", "src/ApiMark.Core/LinkPath.cs");
            testFiles = ("test/ApiMark.Core.Tests/SymbolNameRegistryTests.cs");
            designFiles = ("docs/design/api-mark-core/symbol-name-registry.md");
            verificationFiles = ("docs/verification/api-mark-core/symbol-name-registry.md");
            requirementFiles = ("docs/reqstream/api-mark-core/symbol-name-registry.yaml");
        }
    }
}
