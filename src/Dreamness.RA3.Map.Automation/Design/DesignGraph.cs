namespace Dreamness.RA3.Map.Automation.Design;

internal sealed class DesignGraph
{
    public IReadOnlyList<string> Order { get; }
    public IReadOnlyDictionary<string, HashSet<string>> Ancestors { get; }

    public DesignGraph(IReadOnlyDictionary<string, DesignEntitySpec> specs)
    {
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var children = specs.Keys.ToDictionary(id => id, _ => new List<string>(), StringComparer.Ordinal);
        foreach (var pair in specs)
        {
            var list = pair.Value.DependsOn ?? new List<string>();
            if (list.Count > 32 || list.Distinct(StringComparer.Ordinal).Count() != list.Count)
                throw new AutomationException("INVALID_ARGUMENT", "dependsOn 最多32项且不能重复。");
            var set = new HashSet<string>(StringComparer.Ordinal);
            foreach (var parent in list.Concat(ImpliedDependencies(pair.Value)).Distinct(StringComparer.Ordinal))
            {
                if (string.IsNullOrWhiteSpace(parent) || !specs.ContainsKey(parent))
                    throw new AutomationException("ENTITY_DEPENDENCY", $"实体 {pair.Key} 的依赖不存在: {parent}");
                set.Add(parent);
                children[parent].Add(pair.Key);
            }
            dependencies.Add(pair.Key, set);
        }
        var remaining = dependencies.ToDictionary(p => p.Key, p => p.Value.Count, StringComparer.Ordinal);
        var ready = new SortedSet<string>(remaining.Where(p => p.Value == 0).Select(p => p.Key), StringComparer.Ordinal);
        var ordered = new List<string>();
        var ancestors = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        while (ready.Count > 0)
        {
            var id = ready.Min!;
            ready.Remove(id);
            ordered.Add(id);
            var inherited = new HashSet<string>(dependencies[id], StringComparer.Ordinal);
            foreach (var parent in dependencies[id]) inherited.UnionWith(ancestors[parent]);
            ancestors[id] = inherited;
            foreach (var child in children[id]) if (--remaining[child] == 0) ready.Add(child);
        }
        if (ordered.Count != specs.Count) throw new AutomationException("ENTITY_DEPENDENCY_CYCLE", "设计实体依赖包含环。");
        Order = ordered;
        Ancestors = ancestors;
    }

    public bool IncludeDescendants(HashSet<string> changed)
    {
        var added = false;
        foreach (var id in Order)
            if (!changed.Contains(id) && Ancestors[id].Overlaps(changed)) added |= changed.Add(id);
        return added;
    }

    private static IEnumerable<string> ImpliedDependencies(DesignEntitySpec spec)
    {
        var fields = spec.Kind == "derived" ? new[] { "sourceEntityId" }
            : spec.Kind == "ramp" ? new[] { "startHeightFrom", "endHeightFrom" } : Array.Empty<string>();
        foreach (var field in fields)
            if (spec.Parameters.TryGetProperty(field, out var value))
            {
                if (value.ValueKind != System.Text.Json.JsonValueKind.String || string.IsNullOrWhiteSpace(value.GetString()))
                    throw new AutomationException("INVALID_ARGUMENT", field + " 必须是实体 ID。");
                yield return value.GetString()!;
            }
        if (spec.Kind == "derived" && !spec.Parameters.TryGetProperty("sourceEntityId", out _))
            throw new AutomationException("INVALID_ARGUMENT", "derived.sourceEntityId 必填。");
    }
}
