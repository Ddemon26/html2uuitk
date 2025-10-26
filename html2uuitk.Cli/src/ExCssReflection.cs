using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using ExCSS;

namespace html2uuitk.Cli;

internal static class ExCssReflection
{
    private static readonly Regex LeadingRuleSelectorRegex = new(@"^\s*([^{\s]+)", RegexOptions.Compiled);

    public static string GetSelectorText(IStyleRule rule)
    {
        try
        {
            var selectorTextProperty = rule.GetType().GetProperty("SelectorText", BindingFlags.Instance | BindingFlags.Public);
            if (selectorTextProperty != null)
            {
                return selectorTextProperty.GetValue(rule)?.ToString() ?? string.Empty;
            }

            var selectorsProperty = rule.GetType().GetProperty("Selectors", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (selectorsProperty != null && selectorsProperty.GetValue(rule) is IEnumerable<object> selectors)
            {
                return string.Join(", ", selectors.Select(s => s.ToString()));
            }

            var ruleString = rule.ToString();
            if (string.IsNullOrEmpty(ruleString))
            {
                return string.Empty;
            }

            var match = LeadingRuleSelectorRegex.Match(ruleString);
            return match.Success ? match.Groups[1].Value : ruleString;
        }
        catch
        {
            return string.Empty;
        }
    }

    public static Dictionary<string, string> GetDeclarations(IStyleRule rule)
    {
        var declarations = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            var styleProperty = rule.GetType().GetProperty("Style", BindingFlags.Instance | BindingFlags.Public);
            if (styleProperty != null && styleProperty.GetValue(rule) is IEnumerable<object> styleDeclarations)
            {
                foreach (var declaration in styleDeclarations)
                {
                    var nameProperty = declaration.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                    var valueProperty = declaration.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
                        ?? declaration.GetType().GetProperty("Term", BindingFlags.Instance | BindingFlags.Public);

                    if (nameProperty == null || valueProperty == null)
                    {
                        continue;
                    }

                    var name = nameProperty.GetValue(declaration)?.ToString();
                    var value = valueProperty.GetValue(declaration)?.ToString();

                    if (!string.IsNullOrEmpty(name))
                    {
                        declarations[name] = value ?? string.Empty;
                    }
                }

                return declarations;
            }

            var declarationsProperty = rule.GetType().GetProperty("Declarations", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (declarationsProperty != null && declarationsProperty.GetValue(rule) is IEnumerable<object> declarationList)
            {
                foreach (var declaration in declarationList)
                {
                    var nameProperty = declaration.GetType().GetProperty("Name", BindingFlags.Instance | BindingFlags.Public);
                    var valueProperty = declaration.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public)
                        ?? declaration.GetType().GetProperty("Term", BindingFlags.Instance | BindingFlags.Public);

                    if (nameProperty == null || valueProperty == null)
                    {
                        continue;
                    }

                    var name = nameProperty.GetValue(declaration)?.ToString();
                    var value = valueProperty.GetValue(declaration)?.ToString();

                    if (!string.IsNullOrEmpty(name))
                    {
                        declarations[name] = value ?? string.Empty;
                    }
                }
            }
        }
        catch
        {
            // ignore and return whatever we have
        }

        return declarations;
    }
}
