using System.Text;

namespace UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

/// <summary>
/// Turns a name the visitor typed, or a file name their device gave, into a safe download name (OWASP A05): no path
/// parts, control characters, or characters a file system rejects, no reserved device name, and the extension asked for.
/// </summary>
public static class SafeFileName
{
    #region Fields and Constants

    /// <summary>
    /// The longest name kept, before the extension.
    /// </summary>
    public const int MaxStemLength = 100;

    private static readonly string[] ReservedNames =
    [
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    ];

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns a safe file name ending in <paramref name="extension"/>.
    /// </summary>
    /// <param name="name">The name typed or given; may be empty.</param>
    /// <param name="fallback">The name used when nothing usable is left, without the extension, for example "merged".</param>
    /// <param name="extension">The extension, with its dot, for example ".pdf".</param>
    /// <returns>The safe name, for example "q1 report.pdf" for "../q1 report".</returns>
    public static string For(string? name, string fallback, string extension = ".pdf")
    {
        ArgumentNullException.ThrowIfNull(fallback);
        ArgumentNullException.ThrowIfNull(extension);
        string stem = Stem(name ?? string.Empty, extension);
        if (stem.Length == 0) stem = fallback;
        return stem + extension;
    }

    /// <summary>
    /// Returns the name a file is shown and saved under without its extension, made safe as <see cref="For"/> does.
    /// </summary>
    /// <param name="name">The file name, for example "Q1 report.pdf".</param>
    /// <param name="fallback">The stem used when nothing usable is left.</param>
    /// <returns>The safe stem, for example "Q1 report".</returns>
    public static string StemOf(string? name, string fallback)
    {
        ArgumentNullException.ThrowIfNull(fallback);
        string stem = Stem(name ?? string.Empty, Path.GetExtension(name ?? string.Empty));
        return stem.Length == 0 ? fallback : stem;
    }

    #endregion

    #region Private Methods

    private static string Stem(string name, string extension)
    {
        // Keep only the last path part, whichever separator the name uses.
        int slash = name.LastIndexOfAny(['/', '\\']);
        string text = slash >= 0 ? name[(slash + 1)..] : name;
        if (extension.Length > 0 && text.EndsWith(extension, StringComparison.OrdinalIgnoreCase)) text = text[..^extension.Length];

        var builder = new StringBuilder(text.Length);
        foreach (char c in text)
        {
            builder.Append(char.IsControl(c) || "<>:\"/\\|?*".Contains(c) ? '-' : c);
        }

        string stem = builder.ToString().Trim().TrimEnd('.').Trim().TrimStart('.');
        if (stem.Length > MaxStemLength) stem = stem[..MaxStemLength].TrimEnd();
        string device = stem.Split('.')[0].TrimEnd();
        if (ReservedNames.Contains(device, StringComparer.OrdinalIgnoreCase)) stem = "_" + stem;
        return stem.Trim('-').Length == 0 ? string.Empty : stem;
    }

    #endregion

    #endregion
}
