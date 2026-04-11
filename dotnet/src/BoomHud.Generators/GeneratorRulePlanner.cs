using BoomHud.Abstractions.Generation;
using BoomHud.Abstractions.IR;

namespace BoomHud.Generators;

public static class GeneratorRulePlanner
{
    public static GeneratorRulePlan CreatePlan(
        GeneratorRuleSet ruleSet,
        IEnumerable<GeneratorRuleFact>? initialFacts = null,
        string? name = null)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);

        var facts = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var initialFactList = (initialFacts ?? []).Where(f => !string.IsNullOrWhiteSpace(f.Key)).ToList();
        foreach (var fact in initialFactList)
        {
            facts[fact.Key] = fact.Value;
        }

        var remaining = ruleSet.Rules
            .Select((rule, index) => new IndexedRule(GeneratorRuleExecutionCompiler.Compile(rule), index))
            .ToList();

        var applied = new List<GeneratorPlannedRule>();
        var totalCost = 0d;

        while (remaining.Count > 0)
        {
            var next = remaining
                .Where(candidate => PreconditionsSatisfied(candidate.Rule, facts))
                .OrderBy(candidate => GetPhaseOrder(candidate.Rule.Phase))
                .ThenBy(candidate => candidate.Rule.Cost ?? 1d)
                .ThenByDescending(candidate => GetSpecificity(candidate.Rule.Selector))
                .ThenBy(candidate => candidate.Index)
                .FirstOrDefault();

            if (next == null)
            {
                break;
            }

            remaining.Remove(next);
            var cost = next.Rule.Cost ?? 1d;
            totalCost += cost;
            applied.Add(new GeneratorPlannedRule
            {
                OriginalIndex = next.Index,
                Name = next.Rule.Name,
                Phase = next.Rule.Phase,
                Cost = cost,
                Specificity = GetSpecificity(next.Rule.Selector),
                Selector = next.Rule.Selector,
                Action = next.Rule.Action,
                Preconditions = next.Rule.Preconditions,
                Effects = next.Rule.Effects
            });

            foreach (var effect in next.Rule.Effects.Where(effect => !string.IsNullOrWhiteSpace(effect.Key)))
            {
                facts[effect.Key] = effect.Value;
            }
        }

        var skipped = remaining
            .Select(candidate => new GeneratorSkippedRule
            {
                OriginalIndex = candidate.Index,
                Name = candidate.Rule.Name,
                Phase = candidate.Rule.Phase,
                MissingPreconditions = candidate.Rule.Preconditions
                    .Where(precondition => !FactSatisfied(precondition, facts))
                    .ToList()
            })
            .ToList();

        return new GeneratorRulePlan
        {
            Name = name,
            InitialFacts = initialFactList,
            FinalFacts = facts
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .Select(pair => new GeneratorRuleFact
                {
                    Key = pair.Key,
                    Value = pair.Value
                })
                .ToList(),
            AppliedRules = applied,
            SkippedRules = skipped,
            TotalCost = Math.Round(totalCost, 4)
        };
    }

    public static GeneratorRuleSet BuildExecutableRuleSet(GeneratorRulePlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return new GeneratorRuleSet
        {
            Version = plan.Version,
            Rules = plan.AppliedRules
                .OrderBy(rule => GetPhaseOrder(rule.Phase))
                .ThenByDescending(rule => rule.Specificity)
                .ThenBy(rule => rule.OriginalIndex)
                .Select(rule => new GeneratorRule
                {
                    Name = rule.Name,
                    Phase = rule.Phase,
                    Cost = rule.Cost,
                    Preconditions = rule.Preconditions,
                    Effects = rule.Effects,
                    Template = null,
                    Selector = rule.Selector,
                    Action = GeneratorRuleExecutionCompiler.Compile(new GeneratorRule
                    {
                        Name = rule.Name,
                        Phase = rule.Phase,
                        Cost = rule.Cost,
                        Preconditions = rule.Preconditions,
                        Effects = rule.Effects,
                        Selector = rule.Selector,
                        Action = rule.Action
                    }).Action
                })
                .ToList()
        };
    }

    public static int GetSpecificity(GeneratorRuleSelector selector)
    {
        ArgumentNullException.ThrowIfNull(selector);

        var score = 0;
        if (!string.IsNullOrWhiteSpace(selector.Backend))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.DocumentName))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.NodeId))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.SourceNodeId))
        {
            score++;
        }

        if (selector.ComponentType != null)
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.FontFamily))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.TextGrowth))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.SemanticClass))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.SizeBand))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.MetadataKey))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.MetadataValue))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.ClipId))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.TrackId))
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.TargetId))
        {
            score++;
        }

        if (selector.MotionProperty != null)
        {
            score++;
        }

        if (!string.IsNullOrWhiteSpace(selector.SequenceId))
        {
            score++;
        }

        return score;
    }

    public static int GetPhaseOrder(GeneratorRulePhase? phase)
        => phase switch
        {
            GeneratorRulePhase.Normalize => 0,
            GeneratorRulePhase.Structure => 1,
            GeneratorRulePhase.Layout => 2,
            GeneratorRulePhase.Text => 3,
            GeneratorRulePhase.Icon => 4,
            GeneratorRulePhase.Motion => 5,
            GeneratorRulePhase.Finalize => 6,
            _ => 100
        };

    private static bool PreconditionsSatisfied(GeneratorRule rule, IReadOnlyDictionary<string, string?> facts)
        => rule.Preconditions.Count == 0
           || rule.Preconditions.All(precondition => FactSatisfied(precondition, facts));

    private static bool FactSatisfied(GeneratorRuleFact fact, IReadOnlyDictionary<string, string?> facts)
    {
        if (string.IsNullOrWhiteSpace(fact.Key))
        {
            return true;
        }

        if (!facts.TryGetValue(fact.Key, out var value))
        {
            return false;
        }

        return fact.Value == null
               || string.Equals(fact.Value, value, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record IndexedRule(GeneratorRule Rule, int Index);
}
