## Mono.Cecil

Mono.Cecil is a .NET library for reading, writing, and generating CIL (Common
Intermediate Language) assemblies. In ApiMark, it is used exclusively for reading
— it enables the DotNet Generator to reflect on a compiled assembly's types,
members, and accessibility modifiers without loading the assembly into the current
process, thereby avoiding AppDomain pollution and file-locking issues during a build.

### Purpose

Mono.Cecil was chosen because it provides full type-reflection capabilities
(including generic parameters, nested types, method signatures, and custom
attributes) without requiring the assembly to be loaded into the host process.
The standard .NET reflection API (`System.Reflection`) loads assemblies into the
calling AppDomain and cannot be unloaded without restarting the process, which is
unacceptable in an MSBuild task context. Mono.Cecil reads assemblies as data
structures from disk, giving ApiMark complete metadata access with no runtime
coupling to the documented assembly.

### Features Used

- **Type reflection** — enumerate all `TypeDefinition` objects in an assembly to
  discover namespaces, class/interface/enum/struct/delegate definitions, and their
  inheritance relationships.
- **Member enumeration** — iterate `MethodDefinition`, `PropertyDefinition`,
  `FieldDefinition`, and `EventDefinition` collections on each type.
- **Method signatures** — read parameter lists (`ParameterDefinition`), return
  types, and generic parameter constraints to produce accurate C# signatures.
- **Accessibility modifiers** — inspect `IsPublic`, `IsFamily`, `IsAssembly`, and
  related flags to apply the `Visibility` filter from DotNetGeneratorOptions.

### Integration Pattern

Mono.Cecil is consumed via direct API calls in the Generator unit. No wrapper class
is introduced.

1. At the start of `DotNetGenerator.Generate`, call
   `AssemblyDefinition.ReadAssembly(options.AssemblyPath)` to open the assembly
   file as a Cecil object graph.
2. Iterate `AssemblyDefinition.MainModule.Types` to discover all type definitions.
3. For each type, check accessibility flags against the configured Visibility level
   before proceeding.
4. For each visible type, iterate its members and read signature metadata to build
   the output.
5. The `AssemblyDefinition` implements `IDisposable`; it is disposed at the end of
   Generate to release the file handle promptly.
