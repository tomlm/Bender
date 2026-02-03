namespace DumpViewer;

/// <summary>
/// Represents a range in source text with line/column and character offset information.
/// </summary>
/// <param name="StartLine">The 1-based start line number.</param>
/// <param name="StartColumn">The 1-based start column number.</param>
/// <param name="EndLine">The 1-based end line number.</param>
/// <param name="EndColumn">The 1-based end column number.</param>
/// <param name="StartOffset">The 0-based character offset for the start.</param>
/// <param name="EndOffset">The 0-based character offset for the end.</param>
public record SourceRange(
    int StartLine,
    int StartColumn,
    int EndLine,
    int EndColumn,
    int StartOffset,
    int EndOffset)
{
    /// <summary>
    /// Gets the length of the range in characters.
    /// </summary>
    public int Length => EndOffset - StartOffset;

    /// <summary>
    /// Extracts the text for this range from the source text.
    /// </summary>
    public string GetText(string sourceText)
    {
        if (StartOffset < 0 || EndOffset > sourceText.Length || StartOffset > EndOffset)
            return string.Empty;
        return sourceText[StartOffset..EndOffset];
    }
}
