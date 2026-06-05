using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public sealed class RuleResolver
{
    private readonly string _backend;
    private readonly IReadOnlyList<OrderedMetricProfile> _metricProfiles;
    private readonly IReadOnlyList<OrderedRule> _rules;

    public RuleResolver(GeneratorRuleSet? ruleSet, string backend)
    {
        _backend = backend ?? string.Empty;
        _metricProfiles = (ruleSet?.MetricProfiles ?? [])
            .Select((profile, index) => new OrderedMetricProfile(GeneratorRuleExecutionCompiler.Compile(profile), index))
            .ToList();
        _rules = (ruleSet?.Rules ?? [])
            .Select((rule, index) => new OrderedRule(GeneratorRuleExecutionCompiler.Compile(rule), index))
            .ToList();
    }

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node)
        => Resolve(documentName, node, RuleSelectionContext.Root);

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node, RuleSelectionContext context)
        => Resolve(documentName, node, context, includeMetricProfiles: true);

    public ResolvedGeneratorPolicy Resolve(string documentName, ComponentNode node, RuleSelectionContext context, bool includeMetricProfiles)
    {
        var resolved = new ResolvedGeneratorPolicy();
        IEnumerable<OrderedMetricProfile> metricProfiles = includeMetricProfiles
            ? _metricProfiles
                .Where(candidate => Matches(candidate.Profile.Selector, documentName, node, context))
                .OrderBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Profile.Selector))
                .ThenBy(candidate => candidate.Index)
            : Array.Empty<OrderedMetricProfile>();
        foreach (var match in metricProfiles)
        {
            resolved = resolved.Apply(match.Profile.Action);
        }

        foreach (var match in _rules
                     .Where(candidate => Matches(candidate.Rule.Selector, documentName, node, context))
                     .OrderBy(candidate => GeneratorRulePlanner.GetPhaseOrder(candidate.Rule.Phase))
                     .ThenBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Rule.Selector))
                     .ThenBy(candidate => candidate.Index))
        {
            resolved = resolved.Apply(match.Rule.Action);
        }

        return resolved;
    }

    public ResolvedGeneratorMotionPolicy ResolveMotion(string documentName, MotionRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var resolved = new ResolvedGeneratorMotionPolicy();
        foreach (var match in _rules
                     .Where(candidate => MatchesMotion(candidate.Rule.Selector, documentName, context))
                     .OrderBy(candidate => GeneratorRulePlanner.GetPhaseOrder(candidate.Rule.Phase))
                     .ThenBy(candidate => GeneratorRulePlanner.GetSpecificity(candidate.Rule.Selector))
                     .ThenBy(candidate => candidate.Index))
        {
            resolved = resolved.Apply(match.Rule.Action.Motion);
        }

        return resolved;
    }

    private bool Matches(GeneratorRuleSelector selector, string documentName, ComponentNode node, RuleSelectionContext context)
    {
        if (!string.IsNullOrWhiteSpace(selector.Backend)
            && !string.Equals(selector.Backend, _backend, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentName)
            && !string.Equals(selector.DocumentName, documentName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.NodeId)
            && !string.Equals(selector.NodeId, node.Id, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SourceNodeId))
        {
            if (!node.InstanceOverrides.TryGetValue(BoomHudMetadataKeys.OriginalPencilId, out var rawSourceNodeId))
            {
                return false;
            }

            var normalizedSourceNodeId = GeneratorRuleMetadata.NormalizeValue(rawSourceNodeId);
            if (!string.Equals(selector.SourceNodeId, normalizedSourceNodeId, StringComparison.Ordinal))
            {
                return false;
            }
        }

        if (selector.ComponentType is { } componentType && componentType != node.Type)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.FontFamily))
        {
            var nodeFontFamily = RuleSelectorClassifier.ResolveFontFamily(node);
            if (!string.Equals(selector.FontFamily, nodeFontFamily, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.TextGrowth))
        {
            var nodeTextGrowth = RuleSelectorClassifier.ResolveTextGrowth(node);
            if (!string.Equals(selector.TextGrowth, nodeTextGrowth, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (!string.IsNullOrWhiteSpace(selector.SemanticClass)
            && !RuleSelectorClassifier.HasSemanticClass(node, context, selector.SemanticClass))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SizeBand))
        {
            var nodeSizeBand = RuleSelectorClassifier.ResolveSizeBand(node);
            if (!string.Equals(selector.SizeBand, nodeSizeBand, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(selector.MetadataKey))
        {
            return true;
        }

        if (!node.InstanceOverrides.TryGetValue(selector.MetadataKey, out var rawMetadata))
        {
            return false;
        }

        if (selector.MetadataValue == null)
        {
            return true;
        }

        var normalized = GeneratorRuleMetadata.NormalizeValue(rawMetadata);
        return string.Equals(normalized, selector.MetadataValue, StringComparison.OrdinalIgnoreCase);
    }

    private bool MatchesMotion(GeneratorRuleSelector selector, string documentName, MotionRuleContext context)
    {
        if (!string.IsNullOrWhiteSpace(selector.Backend)
            && !string.Equals(selector.Backend, _backend, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentName)
            && !string.Equals(selector.DocumentName, documentName, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.ClipId)
            && !string.Equals(selector.ClipId, context.ClipId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.TrackId)
            && !string.Equals(selector.TrackId, context.TrackId, StringComparison.Ordinal))
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.TargetId)
            && !string.Equals(selector.TargetId, context.TargetId, StringComparison.Ordinal))
        {
            return false;
        }

        if (selector.MotionProperty is { } property && property != context.MotionProperty)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(selector.SequenceId)
            && !string.Equals(selector.SequenceId, context.SequenceId, StringComparison.Ordinal))
        {
            return false;
        }

        return true;
    }

    private sealed record OrderedRule(GeneratorRule Rule, int Index);

    private sealed record OrderedMetricProfile(GeneratorMetricProfile Profile, int Index);
}
