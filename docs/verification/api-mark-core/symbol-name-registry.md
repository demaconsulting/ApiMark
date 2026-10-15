## SymbolNameRegistry

### Verification Approach

SymbolNameRegistry, FileNameRules, and LinkPath are verified with unit tests in
`test/ApiMark.Core.Tests` (`SymbolNameRegistryTests`, `FileNameRulesTests`, `LinkPathTests`) that call the
real implementation. End-to-end uniqueness, legality, and link resolution are verified by
`DotNetFileNamingTests`, `CppFileNamingTests`, `CppFixtureFileNamingTests`, and `VhdlFileNamingTests`.

### Test Environment

N/A - standard test environment using the .NET test runner is sufficient. The C++ fixture test needs clang.

### Acceptance Criteria

- All tests in the three Core test classes pass with zero failures.
- Legal names are unchanged; illegal characters, trailing dots, empty names, reserved device names,
  decomposed Unicode, and over-long names produce legal segments.
- Names are granted in tier order; a namespace beats a type; reserved pages keep their name; displaced
  parents carry their children; `-N` is used only when needed and skips taken suffixes.
- The result does not depend on registration order, and generated paths are legal and unique without
  regard to case.
- Registry misuse throws.

### Test Scenarios

**FileNameRules tests** (`FileNameRules_Escape_*`, `FileNameRules_IsLegalSegment_*`): Verify each escape
rule NR-06 to NR-09 with exact outputs and the legality predicate.

**SymbolNameRegistry_Resolve_UniqueNames_AreUnchanged**, **..._IdentityName_BeatsTransformedName**,
**..._NamespaceAndType_NamespaceWins**, **..._ReservedName_KeepsNameAndDisplacesSymbol**,
**..._DisplacedParent_ChildrenFollow**, **..._BracesBracketsAndEscapeLookalikes_AreDistinctAndExact**: Verify rules NR-14 and NR-16.

**SymbolNameRegistry_Resolve_CaseTwins_LowestKeyWinsAndOtherGetsSuffix**,
**..._SuffixCandidateTaken_UsesNextNumber**, **..._CanonicallyEquivalentNames_AreDistinguished**: Verify
rule NR-15.

**SymbolNameRegistry_Resolve_RegistrationOrder_DoesNotMatter**,
**..._GeneratedSymbolSet_PathsAreLegalAndCaseInsensitivelyUnique**: Verify determinism and the
uniqueness property over a generated symbol set.

**SymbolNameRegistry_Resolve_CalledTwice_Throws**, **..._GetName_BeforeResolve_Throws**,
**..._Register_AfterResolve_Throws**, **..._Register_InvalidInput_Throws**, **..._GetName_UnknownKey_Throws**:
Verify call-order and input validation.

**LinkPath_Relative_Pages_ReturnsForwardSlashRelativePath**, **LinkPath_Escape_LinkHostileCharacters_AreEncoded**:
Verify rule NR-19.

**Language tests**: `DotNetFileNamingTests` (namespace vs type, case twins, `api`, brace arity, `operators`,
explicit interface members, fixtures for all visibilities, every link resolves),
`DotNetSyntheticNamingTests` (a hand-built assembly with hostile type names, device names, names like `api`
and `operators`, namespace-versus-type collisions and case twins, asserting exact names, links and
breadcrumbs at every visibility), `CppFileNamingTests`, and
`VhdlFileNamingTests` (entity vs package, case twins, reserved names, overloads, fixtures) verify NR-03
to NR-05, NR-10 to NR-13, NR-17, and NR-18.
