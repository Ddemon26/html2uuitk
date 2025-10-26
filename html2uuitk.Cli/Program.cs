using System.Text.Json;
using html2uuitk.Cli;

if (!CliOptions.TryParse(args, out var options, out var error))
{
    if (!string.IsNullOrWhiteSpace(error))
    {
        var isUsage = error.StartsWith("Usage:", StringComparison.OrdinalIgnoreCase);
        if (isUsage)
        {
            Console.WriteLine(error);
        }
        else
        {
            Console.Error.WriteLine(error);
        }

        return isUsage ? 0 : 1;
    }

    return 1;
}

Config config;
try
{
    config = ConfigLoader.Load(options!.ConfigPath);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load configuration: {ex.Message}");
    return 1;
}

var jsonOptions = new JsonSerializerOptions
{
    PropertyNameCaseInsensitive = true
};

List<string> breakingSelectors;
Dictionary<string, UssProperty> ussProperties;

try
{
    var breakingPath = Path.Combine(AppContext.BaseDirectory, "breaking_selectors.json");
    var breakingContent = File.ReadAllText(breakingPath);
    breakingSelectors = JsonSerializer.Deserialize<List<string>>(breakingContent, jsonOptions) ?? new List<string>();
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load breaking selectors: {ex.Message}");
    return 1;
}

try
{
    var ussPath = Path.Combine(AppContext.BaseDirectory, "uss_properties.json");
    var ussContent = File.ReadAllText(ussPath);
    ussProperties = JsonSerializer.Deserialize<Dictionary<string, UssProperty>>(ussContent, jsonOptions)
                    ?? new Dictionary<string, UssProperty>(StringComparer.OrdinalIgnoreCase);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to load USS properties: {ex.Message}");
    return 1;
}

var htmlConverter = new HtmlConverter(config);
var cssConverter = new CssConverter(config, ussProperties, breakingSelectors);

try
{
    Directory.CreateDirectory(options.OutputFolder);
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Failed to create output directory '{options.OutputFolder}': {ex.Message}");
    return 1;
}

foreach (var htmlPath in options.InputFiles)
{
    try
    {
        var content = File.ReadAllText(htmlPath);
        var converted = htmlConverter.Convert(content);

        var fileName = Path.GetFileNameWithoutExtension(htmlPath);
        var targetPath = Path.Combine(options.OutputFolder, $"{fileName}.uxml");
        File.WriteAllText(targetPath, converted);
        Console.WriteLine($"{fileName} UXML written ✓");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to process HTML file '{htmlPath}': {ex.Message}");
    }
}

var cssFiles = new List<string>(options.CssFiles);
if (!string.IsNullOrEmpty(options.ResetCss))
{
    cssFiles.Add(options.ResetCss);
}

foreach (var cssPath in cssFiles)
{
    try
    {
        var content = File.ReadAllText(cssPath);
        var converted = cssConverter.Convert(content);

        var fileName = Path.GetFileNameWithoutExtension(cssPath);
        var targetPath = Path.Combine(options.OutputFolder, $"{fileName}.uss");
        File.WriteAllText(targetPath, converted);
        Console.WriteLine($"{fileName} USS written ✓");
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Failed to process CSS file '{cssPath}': {ex.Message}");
    }
}

return 0;
