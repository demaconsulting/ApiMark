# OTS Verification

## Verification Strategy

ApiMark verifies OTS software items by testing the exact externally supplied behavior that the
repository depends on rather than attempting to re-validate the full third-party product. For the
current scope, Mono.Cecil is exercised through ApiMark.DotNet integration tests that open fixture
assemblies, enumerate metadata, and feed that metadata into Markdown generation. This local evidence
is preferred because ApiMark depends on a specific subset of Mono.Cecil features that must remain
stable across package upgrades.

## Qualification Evidence

Qualification evidence consists of passing automated integration tests, expected-output comparisons,
and focused scenario coverage for the metadata features ApiMark consumes: namespace and type
discovery, member signatures, accessibility, generics, nullable types, and relevant attributes such
as obsolete markers. Package upgrade review also includes inspection of upstream release notes for
breaking changes that could affect metadata interpretation.

## Regression Approach

Whenever the Mono.Cecil package version changes, the repository re-runs all DotNet generator and OTS
verification scenarios against the same fixture assemblies used for baseline qualification. Any
change in discovered members, rendered signatures, or generated file layout is treated as a
regression candidate and must be reviewed before the upgrade is accepted.
