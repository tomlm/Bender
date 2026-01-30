using System;
using System.Collections.Generic;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Globalization;
using System.IO;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using System.Xml;
using CommunityToolkit.Mvvm.ComponentModel;
using CsvHelper;
using YamlDotNet.RepresentationModel;

namespace Blender.ViewModels;

/// <summary>
/// Supported data formats for input.
/// </summary>
public enum DataFormat
{
    Auto,
    Xml,
    Yaml,
    Json,
    Csv
}

/// <summary>
/// ViewModel that handles command line argument parsing and input data loading.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    [ObservableProperty]
    private string? _filePath;

    [ObservableProperty]
    private DataFormat _format = DataFormat.Auto;

    [ObservableProperty]
    private string? _inputData;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gets the deserialized data object based on the detected format.
    /// Can be JsonNode, XmlDocument, YamlStream, or List&lt;dynamic&gt; for CSV.
    /// </summary>
    [ObservableProperty]
    private object? _data;

    /// <summary>
    /// Parses command line arguments and loads input data.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>True if successful, false if there was an error</returns>
    public async Task<bool> InitializeAsync(string[] args)
    {
        var fileOption = new Option<FileInfo?>(
            aliases: ["-f", "--file"],
            description: "Path to the input file to read");

        var xmlOption = new Option<bool>(
            aliases: ["-x", "--xml"],
            description: "Force XML format");

        var yamlOption = new Option<bool>(
            aliases: ["-y", "--yml"],
            description: "Force YAML format");

        var jsonOption = new Option<bool>(
            aliases: ["-j", "--json"],
            description: "Force JSON format");

        var csvOption = new Option<bool>(
            aliases: ["-c", "--csv"],
            description: "Force CSV format");

        var rootCommand = new RootCommand("Blender - Visualize structured text data")
        {
            fileOption,
            xmlOption,
            yamlOption,
            jsonOption,
            csvOption
        };

        FileInfo? file = null;
        bool isXml = false, isYaml = false, isJson = false, isCsv = false;

        rootCommand.SetHandler((fileValue, xmlValue, yamlValue, jsonValue, csvValue) =>
        {
            file = fileValue;
            isXml = xmlValue;
            isYaml = yamlValue;
            isJson = jsonValue;
            isCsv = csvValue;
        }, fileOption, xmlOption, yamlOption, jsonOption, csvOption);

        var parseResult = await rootCommand.InvokeAsync(args);
        if (parseResult != 0)
        {
            HasError = true;
            ErrorMessage = "Failed to parse command line arguments.";
            return false;
        }

        // Determine format from flags
        Format = DetermineFormat(isXml, isYaml, isJson, isCsv);

        // Load input data
        if (file != null)
        {
            FilePath = file.FullName;
            if (!file.Exists)
            {
                HasError = true;
                ErrorMessage = $"File not found: {file.FullName}";
                return false;
            }

            try
            {
                InputData = await File.ReadAllTextAsync(file.FullName);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error reading file: {ex.Message}";
                return false;
            }
        }
        else if (Console.IsInputRedirected)
        {
            // Read piped input from stdin
            try
            {
                InputData = await Console.In.ReadToEndAsync();
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error reading from stdin: {ex.Message}";
                return false;
            }
        }

        // Auto-detect format if not specified
        if (Format == DataFormat.Auto && !string.IsNullOrEmpty(InputData))
        {
            Format = DetectFormat(InputData, FilePath);
        }

        // Deserialize the input data based on format
        if (!string.IsNullOrEmpty(InputData) && Format != DataFormat.Auto)
        {
            try
            {
                Data = DeserializeData(InputData, Format);
            }
            catch (Exception ex)
            {
                HasError = true;
                ErrorMessage = $"Error deserializing {Format} data: {ex.Message}";
                return false;
            }
        }

        return true;
    }

    private static object? DeserializeData(string data, DataFormat format)
    {
        return format switch
        {
            DataFormat.Json => JsonNode.Parse(data),
            DataFormat.Xml => ParseXml(data),
            DataFormat.Yaml => ParseYaml(data),
            DataFormat.Csv => ParseCsv(data),
            _ => null
        };
    }

    private static XmlDocument ParseXml(string data)
    {
        var doc = new XmlDocument();
        doc.LoadXml(data);
        return doc;
    }

    private static YamlStream ParseYaml(string data)
    {
        var yaml = new YamlStream();
        using var reader = new StringReader(data);
        yaml.Load(reader);
        return yaml;
    }

    private static List<dynamic> ParseCsv(string data)
    {
        using var reader = new StringReader(data);
        using var csv = new CsvReader(reader, CultureInfo.InvariantCulture);
        return [.. csv.GetRecords<dynamic>()];
    }

    private static DataFormat DetermineFormat(bool isXml, bool isYaml, bool isJson, bool isCsv)
    {
        if (isXml) return DataFormat.Xml;
        if (isYaml) return DataFormat.Yaml;
        if (isJson) return DataFormat.Json;
        if (isCsv) return DataFormat.Csv;
        return DataFormat.Auto;
    }

    private static DataFormat DetectFormat(string data, string? filePath)
    {
        // First try to detect by file extension
        if (!string.IsNullOrEmpty(filePath))
        {
            var extension = Path.GetExtension(filePath).ToLowerInvariant();
            switch (extension)
            {
                case ".xml":
                    return DataFormat.Xml;
                case ".yml" or ".yaml":
                    return DataFormat.Yaml;
                case ".json":
                    return DataFormat.Json;
                case ".csv":
                    return DataFormat.Csv;
            }
        }

        // Then try to detect by content
        var trimmed = data.TrimStart();
        if (trimmed.StartsWith('<'))
        {
            return DataFormat.Xml;
        }
        if (trimmed.StartsWith('{') || trimmed.StartsWith('['))
        {
            return DataFormat.Json;
        }

        // Check for YAML-like structure (key: value patterns)
        if (trimmed.Contains(':') && !trimmed.Contains(','))
        {
            return DataFormat.Yaml;
        }

        // Check for CSV-like structure (comma-separated with consistent columns)
        if (trimmed.Contains(','))
        {
            return DataFormat.Csv;
        }

        return DataFormat.Auto;
    }
}
