using System;
using System.Collections.Generic;

namespace html2uuitk.Cli;

internal static class TagMappings
{
    public static readonly IReadOnlyDictionary<string, string> HtmlToUi = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["div"] = "ui:VisualElement",
        ["p"] = "ui:Label",
        ["span"] = "ui:Label",
        ["button"] = "ui:Button",
        ["input"] = "ui:TextField",
        ["input[type=\"text\"]"] = "ui:TextField",
        ["input[type=\"number\"]"] = "ui:IntegerField",
        ["input[type=\"password\"]"] = "ui:TextField",
        ["input[type=\"email\"]"] = "ui:TextField",
        ["input[type=\"checkbox\"]"] = "ui:Toggle",
        ["input[type=\"radio\"]"] = "ui:RadioButton",
        ["input[type=\"range\"]"] = "ui:Slider",
        ["input[type=\"file\"]"] = "ui:TextField",
        ["text"] = "ui:Label",
        ["h1"] = "ui:Label",
        ["h2"] = "ui:Label",
        ["h3"] = "ui:Label",
        ["h4"] = "ui:Label",
        ["h5"] = "ui:Label",
        ["h6"] = "ui:Label",
        ["label"] = "ui:Label",
        ["strong"] = "ui:Label",
        ["b"] = "ui:Label",
        ["em"] = "ui:Label",
        ["i"] = "ui:Label",
        ["small"] = "ui:Label",
        ["mark"] = "ui:Label",
        ["abbr"] = "ui:Label",
        ["cite"] = "ui:Label",
        ["code"] = "ui:Label",
        ["q"] = "ui:Label",
        ["time"] = "ui:Label",
        ["section"] = "ui:VisualElement",
        ["article"] = "ui:VisualElement",
        ["header"] = "ui:VisualElement",
        ["footer"] = "ui:VisualElement",
        ["main"] = "ui:VisualElement",
        ["nav"] = "ui:VisualElement",
        ["aside"] = "ui:VisualElement",
        ["ul"] = "ui:VisualElement",
        ["ol"] = "ui:VisualElement",
        ["li"] = "ui:VisualElement",
        ["form"] = "ui:VisualElement"
    };

    public static string? GetUiTagForSelector(string selector)
    {
        if (HtmlToUi.TryGetValue(selector, out var mapped))
        {
            return mapped;
        }

        return null;
    }

    public static string? GetUiTagForElement(string tagName, string? typeAttribute = null)
    {
        if (string.Equals(tagName, "input", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(typeAttribute))
        {
            var composed = $"input[type=\"{typeAttribute}\"]";
            if (HtmlToUi.TryGetValue(composed, out var mapped))
            {
                return mapped;
            }
        }

        if (HtmlToUi.TryGetValue(tagName, out var direct))
        {
            return direct;
        }

        return null;
    }
}
