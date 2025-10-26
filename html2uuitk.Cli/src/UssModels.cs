using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;

namespace html2uuitk.Cli;

internal enum UssVariableType
{
    AssetReference,
    Integer,
    Keyword,
    Length,
    Unknown,
}

internal sealed class UssVariableDefinition
{
    public required string Name { get; init; }
    public required UssVariableType Type { get; init; }
    public bool Defined { get; init; }
    public string? Example { get; init; }
    public IReadOnlyList<int> LinesDefined { get; init; } = Array.Empty<int>();
    public IReadOnlyList<int> LinesReferenced { get; init; } = Array.Empty<int>();

    public bool IsUnityVariable => Name.StartsWith("--unity-", StringComparison.OrdinalIgnoreCase);
    public bool IsThemeVariable => Name.StartsWith("--theme-", StringComparison.OrdinalIgnoreCase);
}

internal sealed class UssStylesheet
{
    public List<UssVariableDefinition> Variables { get; } = new();
    public List<UssRule> Rules { get; } = new();

    public IEnumerable<UssRule> GetRulesForClass(string className) =>
        Rules.Where(r => r.Selectors.Any(s => s.ContainsClass(className)));
}

internal sealed class UssRule
{
    public List<UssSelector> Selectors { get; } = new();
    public List<UssDeclaration> Declarations { get; } = new();

    public bool IsRootRule => Selectors.Any(s => s.IsRoot);
    public bool ContainsCustomProperties => Declarations.Any(d => d.IsCustomProperty);
}

internal sealed class UssSelector
{
    public List<UssSelectorSegment> Segments { get; } = new();
    public string Raw { get; set; } = string.Empty;

    public bool IsRoot
    {
        get
        {
            if (Segments.Count == 1 && Segments[0].Type == UssSelectorSegmentType.PseudoClass)
            {
                return Segments[0].Value.Equals(":root", StringComparison.Ordinal);
            }

            return Raw.Trim().Equals(":root", StringComparison.Ordinal);
        }
    }

    public bool ContainsClass(string className) =>
        Segments.Any(segment =>
            segment.Type == UssSelectorSegmentType.Class &&
            segment.Value.Equals($".{className}", StringComparison.OrdinalIgnoreCase))
        || (!string.IsNullOrEmpty(Raw) && Raw.Contains($".{className}", StringComparison.OrdinalIgnoreCase));

    public override string ToString() => string.IsNullOrEmpty(Raw)
        ? string.Concat(Segments.Select(s => s.Value))
        : Raw;
}

internal enum UssSelectorSegmentType
{
    Universal,
    Type,
    Class,
    Id,
    PseudoClass,
    PseudoElement,
    Attribute,
    Combinator,
    Whitespace,
    Unknown,
}

internal sealed class UssSelectorSegment
{
    public required UssSelectorSegmentType Type { get; init; }
    public required string Value { get; init; }
    public string? Namespace { get; init; }

    public bool IsCombinator => Type == UssSelectorSegmentType.Combinator || Type == UssSelectorSegmentType.Whitespace;
    public bool IsPseudo => Type is UssSelectorSegmentType.PseudoClass or UssSelectorSegmentType.PseudoElement;
}

internal sealed class UssDeclaration
{
    public required string Property { get; init; }
    public required IReadOnlyList<UssValueFragment> Value { get; init; }
    public bool Important { get; init; }

    public bool IsCustomProperty => Property.StartsWith("--", StringComparison.Ordinal);
    public bool IsUnityVendorProperty => Property.StartsWith("-unity-", StringComparison.OrdinalIgnoreCase);
}

internal enum UssValueFragmentKind
{
    Number,
    Integer,
    Length,
    Percentage,
    Color,
    Keyword,
    VariableReference,
    Resource,
    Url,
    String,
    Boolean,
    Function,
    Comma,
    Slash,
    Operator,
    AssetReference,
    Angle,
    Time,
    Unknown,
}

internal sealed class UssValueFragment
{
    public required UssValueFragmentKind Kind { get; init; }
    public required string Raw { get; init; }
    public string? Unit { get; init; }
    public string? FunctionName { get; init; }
    public IReadOnlyList<UssValueFragment> Arguments { get; init; } = Array.Empty<UssValueFragment>();

    public static readonly IReadOnlyList<UssValueFragment> Empty = Array.AsReadOnly(Array.Empty<UssValueFragment>());
}

internal static class UssMetadataFactory
{
    public static UssVariableDefinition FromJson(JsonElement element)
    {
        var name = element.TryGetProperty("name", out var nameElement) ? nameElement.GetString() ?? string.Empty : string.Empty;
        var typeRaw = element.TryGetProperty("type", out var typeElement) ? typeElement.GetString() : null;
        var defined = element.TryGetProperty("defined", out var definedElement) && definedElement.GetBoolean();
        var example = element.TryGetProperty("value_example", out var exampleElement) ? exampleElement.GetString() : null;

        return new UssVariableDefinition
        {
            Name = name,
            Type = ParseVariableType(typeRaw),
            Defined = defined,
            Example = example,
            LinesDefined = ToIntList(element, "lines_defined"),
            LinesReferenced = ToIntList(element, "lines_referenced"),
        };
    }

    private static UssVariableType ParseVariableType(string? raw) => raw?.ToLowerInvariant() switch
    {
        "asset-reference" => UssVariableType.AssetReference,
        "integer" => UssVariableType.Integer,
        "keyword" => UssVariableType.Keyword,
        "length" => UssVariableType.Length,
        _ => UssVariableType.Unknown,
    };

    private static IReadOnlyList<int> ToIntList(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<int>();
        }

        var buffer = new List<int>();
        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.Number && item.TryGetInt32(out var value))
            {
                buffer.Add(value);
            }
        }

        return new ReadOnlyCollection<int>(buffer);
    }
}
