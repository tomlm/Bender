using System.CommandLine;
using System.IO;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;

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
/// ViewModel that handles command line argument parsing.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    /// <summary>
    /// Gets the file path specified via -f/--file argument.
    /// </summary>
    [ObservableProperty]
    private string? _filePath;

    /// <summary>
    /// Gets the format specified via command line flags (-x, -y, -j, -c).
    /// </summary>
    [ObservableProperty]
    private DataFormat _format = DataFormat.Auto;

    /// <summary>
    /// Gets whether command line parsing failed.
    /// </summary>
    [ObservableProperty]
    private bool _hasError;

    /// <summary>
    /// Gets the error message if parsing failed.
    /// </summary>
    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>
    /// Parses command line arguments.
    /// </summary>
    /// <param name="args">Command line arguments</param>
    /// <returns>True if successful, false if there was an error</returns>
    public async Task<bool> ParseArgumentsAsync(string[] args)
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

        FilePath = file?.FullName;
        Format = DetermineFormat(isXml, isYaml, isJson, isCsv);

        return true;
    }

    private static DataFormat DetermineFormat(bool isXml, bool isYaml, bool isJson, bool isCsv)
    {
        if (isXml) return DataFormat.Xml;
        if (isYaml) return DataFormat.Yaml;
        if (isJson) return DataFormat.Json;
        if (isCsv) return DataFormat.Csv;
        return DataFormat.Auto;
    }
}
