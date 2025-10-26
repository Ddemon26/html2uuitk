using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace html2uuitk.Cli;

internal static class ModelVerification
{
    public static ModelVerificationReport VerifyExamples(string projectRoot)
    {
        var examplePath = Path.Combine(projectRoot, "ExampleUxml.uxml");
        var nestedPath = Path.Combine(projectRoot, "NestedExampleUxml.uxml");
        var ussPath = Path.Combine(projectRoot, "HUGE-FILE-DefaultCommonStyles.uss");
        var metadataPath = Path.Combine(projectRoot, "uss_variables_full.json");

        var exampleDocument = UxmlLoader.Load(examplePath);
        var nestedDocument = UxmlLoader.Load(nestedPath);

        var exampleTypes = new HashSet<string>(StringComparer.Ordinal);
        var nestedTypes = new HashSet<string>(StringComparer.Ordinal);

        var exampleElementCount = CountElements(exampleDocument, exampleTypes);
        var nestedElementCount = CountElements(nestedDocument, nestedTypes);

        var parser = new UssStylesheetParser();
        var stylesheet = parser.ParseFile(ussPath);
        var variables = UssLoader.LoadVariables(metadataPath);

        stylesheet.Variables.AddRange(variables);

        var rootSelectors = stylesheet.Rules
            .Where(rule => rule.IsRootRule)
            .SelectMany(rule => rule.Selectors.Select(selector => selector.Raw))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        return new ModelVerificationReport
        {
            ExampleElementCount = exampleElementCount,
            NestedElementCount = nestedElementCount,
            UssRuleCount = stylesheet.Rules.Count,
            UssVariableCount = stylesheet.Variables.Count,
            ExampleElementTypes = exampleTypes.ToArray(),
            NestedElementTypes = nestedTypes.ToArray(),
            RootSelectors = rootSelectors,
        };
    }

    private static int CountElements(UxmlElement element, ISet<string> typeSet)
    {
        var count = 1;
        typeSet.Add(element.GetType().Name);

        foreach (var child in element.Children)
        {
            count += CountElements(child, typeSet);
        }

        return count;
    }
}

internal sealed class ModelVerificationReport
{
    public required int ExampleElementCount { get; init; }
    public required int NestedElementCount { get; init; }
    public required int UssRuleCount { get; init; }
    public required int UssVariableCount { get; init; }
    public required IReadOnlyCollection<string> ExampleElementTypes { get; init; }
    public required IReadOnlyCollection<string> NestedElementTypes { get; init; }
    public required IReadOnlyCollection<string> RootSelectors { get; init; }
}
