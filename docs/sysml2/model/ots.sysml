package OtsDependencies {
    private import SoftwareItemMetadata::*;

    doc /* Off-the-shelf (OTS) dependencies used by ApiMark. */

    part def MonoCecil {
        doc /* OTS: Mono.Cecil, used for reading compiled .NET assemblies without loading them into the current process. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/mono-cecil.md");
            verificationFiles = ("docs/verification/ots/mono-cecil.md");
            requirementFiles = ("docs/reqstream/ots/mono-cecil.yaml");
        }
    }

    part def Clang {
        doc /* OTS: clang (via `clang -ast-dump=json`), used as a static C++ parser to produce a JSON AST for public headers. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/clang.md");
            verificationFiles = ("docs/verification/ots/clang.md");
            requirementFiles = ("docs/reqstream/ots/clang.yaml");
        }
    }

    part def TestResults {
        doc /* OTS: DemaConsulting.TestResults, used by the SelfTest subsystem to record and serialize self-validation outcomes. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/dema-consulting-test-results.md");
            verificationFiles = ("docs/verification/ots/dema-consulting-test-results.md");
            requirementFiles = ("docs/reqstream/ots/dema-consulting-test-results.yaml");
        }
    }

    part def Antlr4RuntimeStandard {
        doc /* OTS: Antlr4.Runtime.Standard with an ANTLR4 vhdl2008 grammar, used to parse VHDL source files. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/antlr4.md");
            verificationFiles = ("docs/verification/ots/antlr4.md");
            requirementFiles = ("docs/reqstream/ots/antlr4.yaml");
        }
    }

    part def FileSystemGlobbing {
        doc /* OTS: Microsoft.Extensions.FileSystemGlobbing, used for glob-based file discovery in ApiMarkCore. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/file-system-globbing.md");
            verificationFiles = ("docs/verification/ots/file-system-globbing.md");
            requirementFiles = ("docs/reqstream/ots/file-system-globbing.yaml");
        }
    }

    part def CppAstNet {
        doc /* OTS: archived — retained for historical reference; not referenced by any current system. */

        metadata info : SoftwareInfo {
            designFiles = ("docs/design/ots/cpp-ast-net.md");
            verificationFiles = ("docs/verification/ots/cpp-ast-net.md");
            requirementFiles = ("docs/reqstream/ots/cpp-ast-net.yaml");
        }
    }
}
