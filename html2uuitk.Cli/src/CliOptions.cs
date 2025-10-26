using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace html2uuitk.Cli;

internal sealed class CliOptions
{
    private CliOptions()
    {
    }

    public List<string> InputFiles { get; } = new();
    public List<string> CssFiles { get; } = new();
    public string? ResetCss { get; private set; }
    public string ConfigPath { get; private set; } = Path.Combine(AppContext.BaseDirectory, "config.json");
    public string OutputFolder { get; private set; } = string.Empty;
    public bool ExtractCss { get; private set; } = true;
    public string? CssOutputName { get; private set; }

    public static bool TryParse(string[] args, out CliOptions? options, out string? error)
    {
        options = new CliOptions();
        error = null;

        for (var i = 0; i < args.Length; i++)
        {
            var raw = args[i];

            if (!raw.StartsWith('-'))
            {
                error = $"Unexpected argument '{raw}'.";
                options = null;
                return false;
            }

            var (flag, inlineValue) = SplitOption(raw);

            switch (flag)
            {
                case "--input":
                case "-i":
                    {
                        var values = inlineValue is not null
                            ? new List<string> { inlineValue }
                            : ConsumeList(args, ref i);

                        if (values.Count == 0)
                        {
                            error = "Option '--input' requires at least one value.";
                            options = null;
                            return false;
                        }

                        options.InputFiles.AddRange(values.Select(ExpandPath));
                        break;
                    }

                case "--css":
                    {
                        var values = inlineValue is not null
                            ? new List<string> { inlineValue }
                            : ConsumeList(args, ref i);

                        if (values.Count == 0)
                        {
                            error = "Option '--css' requires at least one value.";
                            options = null;
                            return false;
                        }

                        options.CssFiles.AddRange(values.Select(ExpandPath));
                        break;
                    }

                case "--reset":
                    {
                        if (inlineValue is null)
                        {
                            var valueList = ConsumeList(args, ref i);
                            if (valueList.Count == 0)
                            {
                                error = "Option '--reset' requires a value.";
                                options = null;
                                return false;
                            }

                            if (valueList.Count > 1)
                            {
                                error = "Option '--reset' accepts only a single value.";
                                options = null;
                                return false;
                            }

                            options.ResetCss = ExpandPath(valueList[0]);
                        }
                        else
                        {
                            options.ResetCss = ExpandPath(inlineValue);
                        }

                        break;
                    }

                case "--config":
                case "-c":
                    {
                        try
                        {
                            var value = inlineValue ?? ConsumeSingle(args, ref i, flag);
                            options.ConfigPath = ExpandPath(value);
                        }
                        catch (InvalidDataException ex)
                        {
                            error = ex.Message;
                            options = null;
                            return false;
                        }

                        break;
                    }

                case "--output":
                case "-o":
                    {
                        try
                        {
                            var value = inlineValue ?? ConsumeSingle(args, ref i, flag);
                            options.OutputFolder = ExpandPath(value);
                        }
                        catch (InvalidDataException ex)
                        {
                            error = ex.Message;
                            options = null;
                            return false;
                        }

                        break;
                    }

                case "--extract-css":
                    {
                        if (inlineValue is not null)
                        {
                            if (bool.TryParse(inlineValue, out var extractValue))
                            {
                                options.ExtractCss = extractValue;
                            }
                            else
                            {
                                error = "Option '--extract-css' must be 'true' or 'false'.";
                                options = null;
                                return false;
                            }
                        }
                        else
                        {
                            options.ExtractCss = true;
                        }

                        break;
                    }

                case "--css-output-name":
                    {
                        try
                        {
                            var value = inlineValue ?? ConsumeSingle(args, ref i, flag);
                            options.CssOutputName = value;
                        }
                        catch (InvalidDataException ex)
                        {
                            error = ex.Message;
                            options = null;
                            return false;
                        }

                        break;
                    }

                case "--help":
                case "-h":
                    {
                        error = GetUsage();
                        options = null;
                        return false;
                    }

                default:
                    {
                        error = $"Unknown option '{flag}'.";
                        options = null;
                        return false;
                    }
            }
        }

        if (options.InputFiles.Count == 0)
        {
            error = "At least one input HTML file must be specified via '--input'.";
            options = null;
            return false;
        }

        if (options.CssFiles.Count == 0 && !options.ExtractCss)
        {
            error = "At least one CSS file must be specified via '--css' or enable CSS extraction with '--extract-css'.";
            options = null;
            return false;
        }

        if (string.IsNullOrWhiteSpace(options.OutputFolder))
        {
            error = "The output folder must be specified via '--output'.";
            options = null;
            return false;
        }

        if (!Path.IsPathRooted(options.OutputFolder))
        {
            options.OutputFolder = Path.GetFullPath(options.OutputFolder);
        }

        if (!File.Exists(options.ConfigPath))
        {
            error = $"Configuration file '{options.ConfigPath}' could not be found.";
            options = null;
            return false;
        }

        if (options.ResetCss is not null && !File.Exists(options.ResetCss))
        {
            error = $"Reset CSS file '{options.ResetCss}' could not be found.";
            options = null;
            return false;
        }

        return true;
    }

    private static (string Flag, string? InlineValue) SplitOption(string option)
    {
        var equalsIndex = option.IndexOf('=');
        return equalsIndex < 0
            ? (option, null)
            : (option[..equalsIndex], option[(equalsIndex + 1)..]);
    }

    private static List<string> ConsumeList(string[] args, ref int index)
    {
        var values = new List<string>();
        while (index + 1 < args.Length && !args[index + 1].StartsWith('-'))
        {
            index++;
            values.Add(args[index]);
        }
        return values;
    }

    private static string ConsumeSingle(string[] args, ref int index, string flag)
    {
        if (index + 1 >= args.Length || args[index + 1].StartsWith('-'))
        {
            throw new InvalidDataException($"Option '{flag}' requires a value.");
        }

        index++;
        return args[index];
    }

    private static string GetUsage()
    {
        return """
Usage: html2uuitk --input <files...> [--css <files...>] [--reset <file>] [--config <file>] --output <folder> [--extract-css] [--css-output-name <name>]

Options:
  -i, --input           Input HTML files (one or more values).
  --css                 CSS files to convert (one or more values). Optional if CSS extraction is enabled.
  --reset               Optional reset CSS file appended to the CSS list.
  -c, --config          Conversion configuration file (JSON). Defaults to config.json next to the executable.
  -o, --output          Output folder for generated UXML and USS files.
  --extract-css         Extract CSS from <style> tags in HTML files (default: true).
  --css-output-name     Custom filename for extracted CSS (without extension).
  -h, --help            Show this message.

Examples:
  html2uuitk --input page.html --css styles.css --output output
  html2uuitk --input single-file.html --output output                    # Auto-extract CSS
  html2uuitk --input page.html --css extra.css --output output           # Mixed mode
""";
    }

    private static string ExpandPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return path;
        }

        path = Environment.ExpandEnvironmentVariables(path);
        return Path.IsPathRooted(path)
            ? path
            : Path.GetFullPath(path);
    }
}
