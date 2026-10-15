## File Naming Rules

<!-- All sections below are MANDATORY. If a section does not apply, write
     "N/A - {justification}" rather than removing it. -->

### Purpose

This document is the complete, numbered specification of the file and folder names that ApiMark
generates for .NET, C++, and VHDL. Anyone can follow the rules by hand to predict the path of any page.
The rules are implemented once, in `FileNameRules` (NR-06 to NR-09) and `SymbolNameRegistry` (NR-14 to
NR-16), and are fed by the language-specific name builders `DotNetNames`, `CppNames`, and `VhdlNames`.

### Data Model

N/A - this document defines rules only. The registry data model is in
`docs/design/api-mark-core/symbol-name-registry.md`.

### Key Methods

N/A - see the unit design documents for the implementing classes.

### Rules

#### NR-01 Output layout

`api.md` is the index at the output root. Each namespace has a page and, when it has children, a folder of
the same name. Each type has a page `{namespace folder}/{Type}.md` and a folder `{namespace folder}/{Type}/`
that holds its member pages and `operators.md`. The names `api` (root) and `operators` (type folder) are
reserved: they always keep their name.

#### NR-02 Identity names

A symbol whose own name is already a legal segment is used exactly as written (`Widget.md`). Unicode letters
are kept as written.

#### NR-03 Namespaces

A root namespace is one atomic segment that keeps its dots (`ApiMark.Core` is `ApiMark.Core.md`). Child
namespaces use the remaining name below their root (`Root/Inner.md`). Roots are computed from every
namespace of the assembly, including namespaces that are not visible, so names do not change with the
visibility filter. A namespace beats a type of the same name (NR-14).

#### NR-04 Generic types

A generic type adds its own arity in braces: `Foo<T>` is `Foo{1}`, `Foo<T1,T2>` is `Foo{2}`. Only the arity
declared on that type counts, not its parents'. A non-generic type is unchanged, so `Foo1` and `Foo{1}`
never collide. Generic methods get no arity.

#### NR-05 Nested types

A nested type is placed in its host's folder: `Outer{1}/Inner{1}.md`; each segment applies NR-04 on its own.

#### NR-06 Illegal and link-hostile characters

Every one of `< > : " | ? * \ /`, space, `[ ] ( ) # % ^`, the backtick, and control characters becomes `_`.
The braces `{` and `}` are structural (NR-04, NR-13) and are kept.

#### NR-07 Trailing dot and empty names

A trailing dot becomes `_`. An empty name becomes `_`.

#### NR-08 Windows reserved device names

If the part before the first dot is `CON`, `PRN`, `AUX`, `NUL`, `COM0-9`, or `LPT0-9` (any case, including the
superscript digit variants), `_` is appended to that part: `CON` is `CON_`, `Con.Foo` is `Con_.Foo`.

#### NR-09 Unicode form and length

Names are normalized to Unicode form C. A name longer than 200 UTF-16 code units is truncated (without
splitting a surrogate pair); any collision this causes is resolved by NR-15.

#### NR-10 Members

Each member (method, property, field, event, constant) has a page in its type's folder named after the member.
All overloads share one page. Members that differ only in case are separate symbols and get separate pages
(NR-14, NR-15). A member named `operators` is displaced from the reserved page.

#### NR-11 Constructors

A constructor page is named after the declaring type's simple name without arity.

#### NR-12 Operators

All operators and conversion operators of a type share the type's `operators.md` page.

#### NR-13 Explicit interface members

An explicit implementation keeps the interface's type arguments in braces, using C# aliases and no
namespace: `IFoo<int>.Get` is `IFoo{int}.Get`; several arguments are joined with `-`
(`IPair{int-string}.Get`); a nested generic argument is nested (`IFoo{List{int}}.Get`); an array argument is
`Array{E}`; an interface that is only generic by arity (all arguments are open type parameters) uses its arity
(`IFoo{1}.Get`).

#### NR-14 Claim tiers

Names are compared case-insensitively within one folder. Claims are granted in this order: reserved pages
first; then names that are exactly the symbol's own name (tier 0); then names that needed any
transformation, including braces (tier 1). Inside a tier a namespace beats a type, which beats a member;
remaining ties go to the lower ordinal key. The loser does not take the name.

Example: `namespace A { class B {} }` and `namespace A.B { class C {} }` both want `A/B`. The namespace
`A.B` keeps `A/B.md` and `A/B/`; the type `A.B` is displaced to `A/B-2.md`.

#### NR-15 Numeric suffix (last resort)

A displaced symbol gets `-N` appended to its base name, with N starting at 2 and increasing until the
name is free, in the same deterministic order (`Name.md`, `name-2.md`). This suffix is used only when
every predictable form is taken.

#### NR-16 Displaced parents carry their children

A symbol's page and folder share one name, so when a namespace or type is displaced, its children move with
it into the displaced folder.

#### NR-17 C++

Namespaces use the qualified key (`a::b`) as one escaped segment; templates use `Foo{int}`; a class,
enum, alias, and function with one name are separated in that kind order; overloaded free functions share one
page. Reserved pages `api` and `operators` apply. The global namespace is `global`.

#### NR-18 VHDL

Entities and packages sit at the root. An entity beats a package with the same name; case twins (VHDL
identifiers are case-insensitive) get NR-15 suffixes; `api` is reserved. Each subprogram overload has its own
page (`Add.md`, `Add-2.md`).

#### NR-19 Links

Every link is a relative path with forward slashes built from the registry's approved names. Link-hostile
characters in the destination (`%`, space, parentheses, `#`) are percent-encoded; braces are literal. Names
are escaped once, at registration, and never again.

#### NR-20 External assemblies

A link to a type in another (mapped) package uses that type's predictable name (NR-02 to NR-09), because the
other package's collisions cannot be known.

### Design Decisions

- Predictability first: a plain name wins over a suffixed one, and `-N` is only the last resort.
- Every symbol, visible or not, is registered so names are stable regardless of visibility.
- No legacy naming or migration support: output is regenerated on each build, so only link integrity matters.

### Error Handling

N/A - these rules are total; every name maps to a legal segment.

### Dependencies

N/A - specification only.

### Callers

N/A - specification only.
