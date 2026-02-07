# Bender

![icon](icon.png)

A terminal user interface (TUI) tool for visualizing messy JSON, XML, YAML, and CSV files directly in your console.

## Purpose

Bender helps you quickly inspect and navigate structured data files without leaving your terminal. It provides:

- **Tree-based visualization** - Expand and collapse nested structures
- **Syntax highlighting** - Color-coded source view for easy reading
- **Auto-format detection** - Automatically detects JSON, XML, YAML, or CSV
- **Stdin support** - Pipe data directly from other commands
- **Cross-platform** - Works on Windows, Linux, and macOS

## Installation

Bender is distributed as a .NET global tool. Requires [.NET 10 SDK](https://dotnet.microsoft.com/download) or later.

```bash
dotnet tool install -g Bender.TUI
```

To update to the latest version:

```bash
dotnet tool update -g Bender.TUI
```

To uninstall:

```bash
dotnet tool uninstall -g Bender.TUI
```

## Usage

### Basic Usage

```bash
# Open a file
Bender -f data.json

# Or specify the file path directly
Bender --file config.xml
```

### Pipe from stdin

```bash
# Pipe JSON from curl
curl -s https://api.example.com/data | Bender

# Pipe from another command
cat data.yml | Bender

# Pipe from clipboard (Windows)
powershell Get-Clipboard | Bender
```

### Force a specific format

When auto-detection doesn't work (e.g., piping data without file extension):

```bash
# Force JSON format
cat data.txt | Bender -j

# Force XML format
Bender -f config.txt -x

# Force YAML format
Bender -f settings.txt -y

# Force CSV format
Bender -f export.txt -c
```

### Command Line Options

| Option | Description |
|--------|-------------|
| `-f, --file <path>` | Path to the input file to read |
| `-x, --xml` | Force XML format |
| `-y, --yml` | Force YAML format |
| `-j, --json` | Force JSON format |
| `-c, --csv` | Force CSV format |
| `-h, --help` | Show help message |

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+O` | Open file |
| `Ctrl+W` | Close window |
| `↑/↓` | Navigate tree |
| `←/→` | Collapse/Expand nodes |
| `Enter/Space` | Toggle expand/collapse |

## Examples

```bash
# View a JSON API response
curl -s https://jsonplaceholder.typicode.com/posts/1 | Bender

# Inspect a package.json
Bender -f package.json

# View XML configuration
Bender -f app.config -x

# Examine CSV data
Bender -f export.csv
```

## License

MIT License - see [LICENSE](LICENSE) for details.

