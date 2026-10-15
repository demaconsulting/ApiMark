package ApiMarkCore {
    part def SymbolNameRegistry {
        doc /* SymbolNameRegistry is the single source of truth for the file and folder name of every
              generated page. Symbols are registered first, all names are resolved once without collisions,
              and only then do writers and link resolvers read the approved names. FileNameRules escapes
              names and LinkPath builds relative links. */

        comment sourceRef /* Source: src/ApiMark.Core/SymbolNameRegistry.cs, src/ApiMark.Core/FileNameRules.cs, src/ApiMark.Core/LinkPath.cs */
        comment testRef /* Test: test/ApiMark.Core.Tests/SymbolNameRegistryTests.cs */
        comment designRef /* Design: docs/design/api-mark-core/symbol-name-registry.md */
        comment verificationRef /* Verification: docs/verification/api-mark-core/symbol-name-registry.md */
        comment reqRef /* Requirements: docs/reqstream/api-mark-core/symbol-name-registry.yaml */
    }
}
