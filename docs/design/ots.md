# OTS Dependencies

## Selection Criteria

OTS items used in ApiMark are selected according to the following criteria:

- **License compatibility**: only items published under OSI-approved permissive or
  weak-copyleft licenses (e.g. MIT, Apache 2.0) that are compatible with
  commercial distribution are considered.
- **Community support and maturity**: items must have active maintenance, a
  demonstrated release history, and a community or vendor capable of addressing
  security disclosures.
- **Security track record**: items with a history of promptly disclosed and patched
  CVEs are preferred; items with unaddressed critical vulnerabilities are excluded
  until remediated.
- **Vendor self-validation or qualification evidence**: where available, vendor
  test suites, conformance certificates, or other qualification artifacts are
  reviewed before adoption.

## Version Management Policy

OTS version upgrades in ApiMark are managed as follows:

- Upgrades are initiated via Dependabot pull requests or manual review during
  sprint planning.
- Major version changes trigger a design review to assess API compatibility and
  any impact on the integration pattern documented in this section.
- Reproducible builds are ensured by pinning versions in project files.

## General Integration Approach

OTS items are consumed in two ways, depending on their delivery mechanism:

- **NuGet packages** (Mono.Cecil, Antlr4.Runtime.Standard, Microsoft.Extensions.FileSystemGlobbing,
  DemaConsulting.TestResults): consumed via their public NuGet package API. No wrapper classes are
  introduced unless the OTS API surface is too broad or its error model is incompatible with ApiMark
  conventions.
- **External CLI tools** (clang): invoked as an out-of-process command (`clang -ast-dump=json`).
  The consuming unit (ClangAstParser) is responsible for launching the process, capturing its output,
  and mapping any non-zero exit code or stderr output to an appropriate ApiMark diagnostic.

Error conditions from OTS items propagate as exceptions (or diagnostic messages for CLI tools) to the
consuming unit, which is responsible for logging and surfacing them appropriately.

## Qualification Strategy

OTS items are qualified by relying on vendor test suites and the integration tests
in the ApiMark test projects. Before accepting a version upgrade, the integration
test suite is run against the new version and any new CVE disclosures are reviewed.
