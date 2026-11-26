---
title: Documentation coverage
summary: Report, or fail on, API items that have no documentation comment.
aliases:
  - enforce-docs
  - coverage
  - undocumented
tags:
  - reference
order: 7
see-also:
  - dotnet
  - cpp
  - vhdl
  - msbuild
examples:
  - name: serial
    kind: source
    language: vhdl
    save-as: serial.vhd
    description: An entity with one port that has no comment.
    content: |
      --! @brief Serial transmitter.
      ENTITY serial_tx IS
          PORT (
              clk : IN STD_LOGIC; --! Rising-edge clock
              tx  : OUT STD_LOGIC
          );
      END ENTITY serial_tx;
  - name: report
    kind: command
    language: shell
    description: List the undocumented items. Exits 0.
    content: '{{tool}} vhdl --source serial.vhd --output api --enforce-docs Public'
    expect: '[Undocumented]'
  - name: fail
    kind: command
    language: shell
    description: Fail when an item is undocumented. Exits 1.
    content: '{{tool}} vhdl --source serial.vhd --output api --enforce-docs Public --enforce-docs-severity Error'
    exit: 1
    expect: 'undocumented API item(s) found'
---
The check is off by default. `--enforce-docs <tier>` turns it on for
`dotnet`, `cpp`, and `vhdl`, and the pages are still written.

| Option | Meaning |
| --- | --- |
| `--enforce-docs <tier>` | `Public`, `PublicAndProtected`, or `All`. |
| `--enforce-docs-severity <v>` | `Warning` (default) or `Error`. |

**What is checked.** For .NET, each type and member at the tier needs an
XML `<summary>`. For C++, each declaration at the tier needs a Doxygen
summary such as `@brief`. VHDL has no visibility, so the three tiers check
the same items: entities with their ports and generics, architectures,
packages, and the declarations of a package. The tier is independent of
`--visibility`, which only chooses what the pages show.

**Report.** The tool prints `Documentation coverage check:`, one line
`[Undocumented] <kind>: <name>` per item, and
`Documentation coverage: <n> undocumented of <m> checked.`
([[#report]]). With `--enforce-docs-severity Error`, any undocumented item
also prints `Error: <n> undocumented API item(s) found` and the exit code
is 1 ([[#fail]]). With `Warning` the exit code stays 0.

The MSBuild package sets the same check with the properties
`ApiMarkEnforceDocs` and `ApiMarkEnforceDocsSeverity`; see [[msbuild]].
