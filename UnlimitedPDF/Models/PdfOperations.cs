namespace UnlimitedPDF;

/// <summary>
/// One-call operations on whole PDF files (PR-017). Each is built on <see cref="PdfFile"/> and
/// <see cref="PdfDocument"/>: <see cref="Merge(string, PdfFile[])"/> gives the same bytes as adding each file to a new
/// document with <see cref="PdfDocument.AddPages"/> and saving it. To add a cover page, choose page ranges, or set
/// bookmark titles, use <see cref="PdfDocument"/> directly.
/// </summary>
public static class PdfOperations
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Joins whole PDF files, in the order given, into one file at <paramref name="outputPath"/>. Each input adds one
    /// top-level bookmark. Everything is checked before the file is opened, so a failure leaves an existing file
    /// unchanged.
    /// </summary>
    /// <param name="outputPath">The path of the merged file to create or overwrite.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outputPath"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Joins whole PDF files, in the order given, into one file at a path.")]
    public static void Merge(string outputPath, params PdfFile[] inputs)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        Build(inputs, nameof(inputs)).Save(outputPath);
    }

    /// <summary>
    /// Joins whole PDF files, in the order given, into one file written to <paramref name="output"/> from its current
    /// position. The stream is left open. Each input adds one top-level bookmark.
    /// </summary>
    /// <param name="output">The stream to write the merged file to.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Joins whole PDF files, in the order given, into one file written to a stream.")]
    public static void Merge(Stream output, params PdfFile[] inputs)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        Build(inputs, nameof(inputs)).Save(output);
    }

    /// <summary>
    /// Asynchronously joins whole PDF files, in the order given, into one file at <paramref name="outputPath"/>. Each
    /// input adds one top-level bookmark. Everything is checked before the file is opened, so a failure leaves an
    /// existing file unchanged.
    /// </summary>
    /// <param name="outputPath">The path of the merged file to create or overwrite.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="outputPath"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously joins whole PDF files, in the order given, into one file at a path.")]
    public static Task MergeAsync(string outputPath, IEnumerable<PdfFile> inputs, CancellationToken cancellationToken = default)
    {
        if (outputPath is null) throw new ArgumentNullException(nameof(outputPath));
        return Build(inputs, nameof(inputs)).SaveAsync(outputPath, cancellationToken);
    }

    /// <summary>
    /// Asynchronously joins whole PDF files, in the order given, into one file written to <paramref name="output"/>
    /// from its current position. The stream is left open. Each input adds one top-level bookmark.
    /// </summary>
    /// <param name="output">The stream to write the merged file to.</param>
    /// <param name="inputs">The loaded files to join.</param>
    /// <param name="cancellationToken">A token to cancel the write.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/>, <paramref name="inputs"/>, or an input is
    /// null.</exception>
    /// <exception cref="ArgumentException"><paramref name="inputs"/> is empty.</exception>
    [AddedVersion("1.0.0")]
    [Description("Asynchronously joins whole PDF files, in the order given, into one file written to a stream.")]
    public static Task MergeAsync(Stream output, IEnumerable<PdfFile> inputs, CancellationToken cancellationToken = default)
    {
        if (output is null) throw new ArgumentNullException(nameof(output));
        return Build(inputs, nameof(inputs)).SaveAsync(output, cancellationToken);
    }

    #endregion

    #region Private Methods

    // The merged document: one AddPages call per input, so its bytes equal the AddPages path (WR-036).
    private static PdfDocument Build(IEnumerable<PdfFile>? inputs, string parameter)
    {
        if (inputs is null) throw new ArgumentNullException(parameter);
        var files = inputs.ToList();
        if (files.Count == 0) throw new ArgumentException("At least one file is needed.", parameter);
        if (files.Any(file => file is null)) throw new ArgumentNullException(parameter, "An input file is null.");

        var document = new PdfDocument();
        foreach (var file in files) document.AddPages(file);
        return document;
    }

    #endregion

    #endregion
}
