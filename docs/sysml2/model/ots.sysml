package OtsDependencies {
    doc /* Off-the-shelf (OTS) dependencies used by ApiMark. */

    part def MonoCecil {
        doc /* OTS: Mono.Cecil, used for reading compiled .NET assemblies without loading them into the current process. */

        comment designRef /* Design: docs/design/ots/mono-cecil.md */
        comment verificationRef /* Verification: docs/verification/ots/mono-cecil.md */
        comment reqRef /* Requirements: docs/reqstream/ots/mono-cecil.yaml */
    }

    part def Clang {
        doc /* OTS: clang (via `clang -ast-dump=json`), used as a static C++ parser to produce a JSON AST for public headers. */

        comment designRef /* Design: docs/design/ots/clang.md */
        comment verificationRef /* Verification: docs/verification/ots/clang.md */
        comment reqRef /* Requirements: docs/reqstream/ots/clang.yaml */
    }

    part def TestResults {
        doc /* OTS: DemaConsulting.TestResults, used by the SelfTest subsystem to record and serialize self-validation outcomes. */

        comment designRef /* Design: docs/design/ots/dema-consulting-test-results.md */
        comment verificationRef /* Verification: docs/verification/ots/dema-consulting-test-results.md */
        comment reqRef /* Requirements: docs/reqstream/ots/dema-consulting-test-results.yaml */
    }

    part def Antlr4RuntimeStandard {
        doc /* OTS: Antlr4.Runtime.Standard with an ANTLR4 vhdl2008 grammar, used to parse VHDL source files. */

        comment designRef /* Design: docs/design/ots/antlr4.md */
        comment verificationRef /* Verification: docs/verification/ots/antlr4.md */
        comment reqRef /* Requirements: docs/reqstream/ots/antlr4.yaml */
    }

    part def FileSystemGlobbing {
        doc /* OTS: Microsoft.Extensions.FileSystemGlobbing, used for glob-based file discovery in ApiMarkCore. */

        comment designRef /* Design: docs/design/ots/file-system-globbing.md */
        comment verificationRef /* Verification: docs/verification/ots/file-system-globbing.md */
        comment reqRef /* Requirements: docs/reqstream/ots/file-system-globbing.yaml */
    }

    part def CppAstNet {
        doc /* OTS: archived — retained for historical reference; not referenced by any current system. */

        comment designRef /* Design: docs/design/ots/cpp-ast-net.md */
        comment verificationRef /* Verification: docs/verification/ots/cpp-ast-net.md */
        comment reqRef /* Requirements: docs/reqstream/ots/cpp-ast-net.yaml */
    }
}
