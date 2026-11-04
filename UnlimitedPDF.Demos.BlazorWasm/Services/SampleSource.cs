namespace UnlimitedPDF.Demos.BlazorWasm.Services;

/// <summary>
/// Reads a sample builder's source file, embedded in the app at build time, so each demo shows exactly the code that
/// generates its PDF.
/// </summary>
public static class SampleSource
{
    #region Fields and Constants

    private static readonly Dictionary<string, string> _cache = new();

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Gets the source text of a sample builder.
    /// </summary>
    /// <param name="fileName">The builder file name, for example "InvoiceSample.cs".</param>
    /// <returns>The file's text.</returns>
    public static string Read(string fileName)
    {
        ArgumentNullException.ThrowIfNull(fileName);
        if (_cache.TryGetValue(fileName, out var cached))
        {
            return cached;
        }

        using var stream = typeof(SampleSource).Assembly.GetManifestResourceStream($"Samples.{fileName}")
            ?? throw new InvalidOperationException($"The sample source '{fileName}' is not embedded in the app.");
        using var reader = new StreamReader(stream);
        var text = reader.ReadToEnd().Replace("\r\n", "\n");
        _cache[fileName] = text;
        return text;
    }

    #endregion

    #endregion
}
