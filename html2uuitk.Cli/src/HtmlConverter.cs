using System;
using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace html2uuitk.Cli;

internal sealed class HtmlConverter
{
    private const string XmlHeader = "<ui:UXML xmlns:ui=\"UnityEngine.UIElements\" xmlns:uie=\"UnityEditor.UIElements\" editor-extension-mode=\"False\">";
    private const string XmlFooter = "</ui:UXML>";

    private readonly Config _config;
    private readonly HtmlParser _parser = new();

    public HtmlConverter(Config config)
    {
        _config = config;
    }

    public string Convert(string html)
    {
        var document = _parser.ParseDocument(html);
        var body = document.Body ?? document.DocumentElement;

        if (body is null)
        {
            return FormatXml(XmlHeader + XmlFooter);
        }

        var converted = ConvertElement(body, parentUiTag: null);

        if (string.IsNullOrWhiteSpace(converted))
        {
            return FormatXml(XmlHeader + XmlFooter);
        }

        converted = converted.Replace("<body>", XmlHeader, StringComparison.OrdinalIgnoreCase)
                             .Replace("</body>", XmlFooter, StringComparison.OrdinalIgnoreCase);

        // Remove script tags - they're not valid in UXML
        converted = RemoveScriptTags(converted);

        return FormatXml(converted);
    }

    private string ConvertElement(IElement element, string? parentUiTag)
    {
        var uiTag = GetUiTag(element);
        var builder = new StringBuilder();
        var valid = true;

        builder.Append('<').Append(uiTag);

        if (string.Equals(uiTag, "ui:Label", StringComparison.OrdinalIgnoreCase))
        {
            var text = element.TextContent ?? string.Empty;
            var condensed = CollapseForValidation(text);

            if (condensed.Length == 0)
            {
                valid = false;
            }
            else
            {
                if (_config.Options.Uppercase)
                {
                    text = text.ToUpperInvariant();
                }

                builder.Append(" text=\"").Append(EscapeXml(text)).Append('"');
            }
        }
        else if (string.Equals(uiTag, "ui:Button", StringComparison.OrdinalIgnoreCase))
        {
            // Convert button text to text attribute
            var text = element.TextContent?.Trim();
            if (string.IsNullOrEmpty(text))
            {
                // Self-closing button if no text content
                valid = false;
            }
            else
            {
                builder.Append(" text=\"").Append(EscapeXml(text)).Append('"');
            }
        }

        foreach (var attribute in element.Attributes)
        {
            var (attrName, attrValue, shouldInclude) = TransformAttribute(attribute.Name, attribute.Value, uiTag);
            if (shouldInclude)
            {
                builder.Append(' ')
                       .Append(attrName)
                       .Append("=\"")
                       .Append(EscapeXml(attrValue))
                       .Append('"');
            }
        }

        if (string.Equals(uiTag, TagMappings.HtmlToUi["input"], StringComparison.OrdinalIgnoreCase)
            && _config.Options.Focusable.HasValue)
        {
            builder.Append(" focusable=\"")
                   .Append(_config.Options.Focusable.Value ? "true" : "false")
                   .Append('"');
        }

        builder.Append('>');

        // For labels and buttons with text content, don't process children (text is already in the attribute)
        if ((string.Equals(uiTag, "ui:Label", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(uiTag, "ui:Button", StringComparison.OrdinalIgnoreCase)) &&
            !string.IsNullOrWhiteSpace(element.TextContent))
        {
            // Self-closing with text attribute
            builder.Insert(builder.Length - 1, '/');
        }
        else
        {
            foreach (var child in element.ChildNodes)
            {
                var portion = ConvertNode(child, uiTag);
                builder.Append(portion);
            }

            // Handle self-closing tags for empty elements
            if (element.ChildNodes.Count() == 0)
            {
                builder.Insert(builder.Length - 1, '/');
            }
            else
            {
                builder.Append("</").Append(uiTag).Append('>');
            }
        }

        return valid ? builder.ToString() : string.Empty;
    }

    private string ConvertNode(INode node, string? parentUiTag)
    {
        return node switch
        {
            IComment => string.Empty,
            IText text => ConvertTextNode(text, parentUiTag),
            IElement element => ConvertElement(element, parentUiTag),
            _ => ConvertChildren(node, parentUiTag)
        };
    }

    private string ConvertChildren(INode node, string? parentUiTag)
    {
        var builder = new StringBuilder();
        foreach (var child in node.ChildNodes)
        {
            builder.Append(ConvertNode(child, parentUiTag));
        }
        return builder.ToString();
    }

    private string ConvertTextNode(IText textNode, string? parentUiTag)
    {
        var text = textNode.TextContent ?? string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        // If the parent is a VisualElement, wrap text in a Label
        if (string.Equals(parentUiTag, "ui:VisualElement", StringComparison.OrdinalIgnoreCase))
        {
            var trimmedText = text.Trim();
            if (!string.IsNullOrEmpty(trimmedText))
            {
                return $"<ui:Label text=\"{EscapeXml(trimmedText)}\" />";
            }
            return string.Empty;
        }

        // For other parents (like Labels), return the escaped text directly
        return EscapeXml(text);
    }

    private static (string name, string value, bool shouldInclude) TransformAttribute(string attrName, string attrValue, string uiTag)
    {
        var lowerAttrName = attrName.ToLowerInvariant();

        // Filter out invalid HTML attributes that Unity doesn't support
        var invalidAttributes = new[] { "onclick", "onchange", "onload", "type", "min", "max", "step", "src", "href", "action", "method" };
        if (invalidAttributes.Contains(lowerAttrName))
        {
            return (attrName, attrValue, false);
        }

        // Transform HTML attributes to Unity equivalents
        return lowerAttrName switch
        {
            "id" => ("name", attrValue, true), // Convert id to name
            "placeholder" => ("placeholder-text", attrValue, true), // Convert placeholder to placeholder-text
            "class" => ("class", attrValue, true), // Keep class as-is
            "style" => ("style", attrValue, true), // Keep style as-is (if needed)
            _ => (attrName, attrValue, true) // Keep other attributes
        };
    }

    private static string GetUiTag(IElement element)
    {
        var tagName = element.TagName.ToLowerInvariant();
        var type = element.GetAttribute("type")?.ToLowerInvariant();
        return TagMappings.GetUiTagForElement(tagName, type) ?? tagName;
    }

    internal static string FormatXml(string xml, string tab = "\t")
    {
        // Very simple approach: just return the XML as-is without any complex formatting
        // to avoid breaking the XML structure
        return xml;
    }

    private static string CollapseForValidation(string text)
    {
        var trimmed = text.Trim();
        return trimmed.Replace(" ", string.Empty, StringComparison.Ordinal);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    // Remove script tags - they're not valid in UXML
    private static string RemoveScriptTags(string xml)
    {
        return System.Text.RegularExpressions.Regex.Replace(xml, @"<script[^>]*>.*?</script>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);
    }
}