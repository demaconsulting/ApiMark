namespace ApiMark.Core;

/// <summary>
///     The kind of a registered symbol; it decides which symbol keeps the plain name when two symbols in the
///     same directory claim the same name.
/// </summary>
public enum SymbolKind
{
    /// <summary>A structurally reserved page such as <c>api</c> or <c>operators</c>; always keeps its name.</summary>
    Reserved = 0,

    /// <summary>A namespace (or package, library, or folder-like container).</summary>
    Namespace = 1,

    /// <summary>A type or other primary symbol such as a class, enum, entity, or function.</summary>
    Type = 2,

    /// <summary>A member of a type.</summary>
    Member = 3,
}

/// <summary>
///     The single source of truth for the file and folder names of every generated page.
/// </summary>
/// <remarks>
///     <para>
///         Use is a strict three-step process. First, <see cref="Register"/> every symbol that needs a
///         file; no name is assigned yet. Second, call <see cref="Resolve"/> once: every name is escaped
///         once and made unique inside its directory, comparing names case-insensitively. Third, writers
///         and link resolvers read the approved names with <see cref="GetName"/>, <see cref="GetPath"/>,
///         and <see cref="GetDirectory"/>. Reading before <see cref="Resolve"/>, and registering after it,
///         is an error, so no page can be written or linked under a name the registry has not approved.
///     </para>
///     <para>
///         Uniqueness is decided per directory. Names are claimed in tiers: reserved structural names
///         first, then names that are exactly the symbol's own name (tier 0), then names that needed any
///         transformation (tier 1, for example <c>Foo{2}</c>). Inside a tier the claimant with the lowest
///         kind (namespace before type before member) and then the lowest ordinal key wins. A symbol that
///         loses its plain name is given a numeric suffix <c>-N</c> (N = 2, 3, ...), appended to its base
///         name, in the same order. The suffix is used only as the last resort.
///     </para>
///     <para>
///         A symbol's page <c>{path}.md</c> and its folder <c>{path}/</c> share one name, and the folder of
///         a symbol is the directory of its children, so a displaced parent carries all its children along.
///     </para>
/// </remarks>
public sealed class SymbolNameRegistry
{
    /// <summary>The first numeric suffix used when a name is already taken.</summary>
    private const int FirstCollisionNumber = 2;

    /// <summary>The registered symbols by key.</summary>
    private readonly Dictionary<string, Entry> _entries = new(StringComparer.Ordinal);

    /// <summary>Whether <see cref="Resolve"/> has completed.</summary>
    private bool _resolved;

    /// <summary>Gets a value indicating whether <see cref="Resolve"/> has completed.</summary>
    public bool IsResolved => _resolved;

    /// <summary>Gets the number of registered symbols.</summary>
    public int Count => _entries.Count;

    /// <summary>
    ///     Registers a symbol that needs a file. No name is assigned until <see cref="Resolve"/>.
    /// </summary>
    /// <param name="key">The unique, ordinal key of the symbol (for example its fully qualified name).</param>
    /// <param name="parentKey">The key of the symbol whose folder holds this symbol's page, or <see langword="null"/> for the output root.</param>
    /// <param name="rawName">
    ///     The raw, unescaped name. Braces are structural notation and are kept; every other illegal
    ///     character is escaped by <see cref="FileNameRules.Escape"/>.
    /// </param>
    /// <param name="kind">The symbol kind, which decides precedence on a name collision.</param>
    /// <exception cref="InvalidOperationException">Thrown after <see cref="Resolve"/>, for a duplicate key, or for an unregistered parent.</exception>
    public void Register(string key, string? parentKey, string rawName, SymbolKind kind)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(rawName);

        if (_resolved)
        {
            throw new InvalidOperationException("Symbols cannot be registered after the registry has been resolved.");
        }

        if (_entries.ContainsKey(key))
        {
            throw new InvalidOperationException($"Symbol '{key}' is already registered.");
        }

        if (parentKey != null && !_entries.ContainsKey(parentKey))
        {
            throw new InvalidOperationException($"Parent '{parentKey}' of symbol '{key}' is not registered.");
        }

        _entries[key] = new Entry(key, parentKey, rawName, kind);
    }

    /// <summary>
    ///     Determines whether a symbol key is registered.
    /// </summary>
    /// <param name="key">The symbol key.</param>
    /// <returns><see langword="true"/> when the key is registered.</returns>
    public bool Contains(string key) => _entries.ContainsKey(key);

    /// <summary>
    ///     Assigns every registered symbol its final, collision-free name. May be called once.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when the registry has already been resolved.</exception>
    public void Resolve()
    {
        if (_resolved)
        {
            throw new InvalidOperationException("The registry has already been resolved.");
        }

        // Parents are always resolved before their children because a child's directory is its parent's path.
        var byDepth = _entries.Values.GroupBy(e => DepthOf(e)).OrderBy(g => g.Key);
        foreach (var level in byDepth)
        {
            foreach (var directory in level.GroupBy(e => e.ParentKey))
            {
                var parentPath = directory.Key == null ? string.Empty : _entries[directory.Key].Path!;
                ResolveDirectory(directory.ToList(), parentPath);
            }
        }

        _resolved = true;
    }

    /// <summary>
    ///     Gets the approved name (one path segment, no extension) of a symbol.
    /// </summary>
    /// <param name="key">The symbol key.</param>
    /// <returns>The final file and folder name.</returns>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Resolve"/> or for an unregistered key.</exception>
    public string GetName(string key) => Get(key).Name!;

    /// <summary>
    ///     Gets the approved page path (forward slashes, no extension) of a symbol, relative to the output root.
    /// </summary>
    /// <param name="key">The symbol key.</param>
    /// <returns>The path; the page file is <c>{path}.md</c> and the folder of its children is <c>{path}/</c>.</returns>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Resolve"/> or for an unregistered key.</exception>
    public string GetPath(string key) => Get(key).Path!;

    /// <summary>
    ///     Gets the directory that holds the page of a symbol (the path of its parent, empty for the root).
    /// </summary>
    /// <param name="key">The symbol key.</param>
    /// <returns>The directory path, or an empty string for the output root.</returns>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Resolve"/> or for an unregistered key.</exception>
    public string GetDirectory(string key)
    {
        var entry = Get(key);
        return entry.ParentKey == null ? string.Empty : _entries[entry.ParentKey].Path!;
    }

    /// <summary>
    ///     Gets the page paths of every registered symbol.
    /// </summary>
    /// <returns>The approved page paths (no extension), in ordinal key order.</returns>
    /// <exception cref="InvalidOperationException">Thrown before <see cref="Resolve"/>.</exception>
    public IReadOnlyList<string> GetAllPaths()
    {
        EnsureResolved();
        return _entries.Values.OrderBy(e => e.Key, StringComparer.Ordinal).Select(e => e.Path!).ToList();
    }

    /// <summary>Gets a resolved entry.</summary>
    /// <param name="key">The symbol key.</param>
    /// <returns>The entry.</returns>
    private Entry Get(string key)
    {
        EnsureResolved();
        if (!_entries.TryGetValue(key, out var entry))
        {
            throw new InvalidOperationException($"Symbol '{key}' is not registered.");
        }

        return entry;
    }

    /// <summary>Throws unless <see cref="Resolve"/> has completed.</summary>
    private void EnsureResolved()
    {
        if (!_resolved)
        {
            throw new InvalidOperationException("Names are not available until the registry has been resolved.");
        }
    }

    /// <summary>Computes the number of ancestors of an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The depth; zero for an entry in the output root.</returns>
    private int DepthOf(Entry entry)
    {
        var depth = 0;
        for (var parent = entry.ParentKey; parent != null; parent = _entries[parent].ParentKey)
        {
            depth++;
        }

        return depth;
    }

    /// <summary>Assigns the names of every entry in one directory.</summary>
    /// <param name="entries">The entries of the directory.</param>
    /// <param name="parentPath">The path of the directory, empty for the root.</param>
    private static void ResolveDirectory(List<Entry> entries, string parentPath)
    {
        foreach (var entry in entries)
        {
            entry.BaseName = FileNameRules.Escape(entry.RawName, out var transformed);
            entry.Tier = TierOf(entry.Kind, transformed);
        }

        var ordered = entries
            .OrderBy(e => e.Tier)
            .ThenBy(e => (int)e.Kind)
            .ThenBy(e => e.Key, StringComparer.Ordinal)
            .ToList();

        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var displaced = new List<Entry>();
        foreach (var entry in ordered)
        {
            if (used.Add(entry.BaseName!))
            {
                entry.Name = entry.BaseName;
            }
            else
            {
                displaced.Add(entry);
            }
        }

        // Last resort: numeric suffix, in the same deterministic order.
        foreach (var entry in displaced)
        {
            var number = FirstCollisionNumber;
            while (!used.Add($"{entry.BaseName}-{number}"))
            {
                number++;
            }

            entry.Name = $"{entry.BaseName}-{number}";
        }

        foreach (var entry in entries)
        {
            entry.Path = parentPath.Length > 0 ? $"{parentPath}/{entry.Name}" : entry.Name;
        }
    }

    /// <summary>Computes the claim tier of a symbol.</summary>
    /// <param name="kind">The symbol kind.</param>
    /// <param name="transformed">Whether the name needed any transformation.</param>
    /// <returns>-1 for a reserved page, 0 for an identity name, 1 for a transformed name.</returns>
    private static int TierOf(SymbolKind kind, bool transformed)
    {
        if (kind == SymbolKind.Reserved)
        {
            return -1;
        }

        return transformed ? 1 : 0;
    }

    /// <summary>A registered symbol and its resolution state.</summary>
    private sealed class Entry(string key, string? parentKey, string rawName, SymbolKind kind)
    {
        /// <summary>Gets the symbol key.</summary>
        public string Key { get; } = key;

        /// <summary>Gets the parent symbol key, or <see langword="null"/> for the output root.</summary>
        public string? ParentKey { get; } = parentKey;

        /// <summary>Gets the raw name.</summary>
        public string RawName { get; } = rawName;

        /// <summary>Gets the symbol kind.</summary>
        public SymbolKind Kind { get; } = kind;

        /// <summary>Gets or sets the escaped name before collision handling.</summary>
        public string? BaseName { get; set; }

        /// <summary>Gets or sets the claim tier (-1 reserved, 0 identity, 1 transformed).</summary>
        public int Tier { get; set; }

        /// <summary>Gets or sets the final name.</summary>
        public string? Name { get; set; }

        /// <summary>Gets or sets the final page path.</summary>
        public string? Path { get; set; }
    }
}
