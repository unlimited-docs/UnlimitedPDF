using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Every N pages demo: a PDF split into parts of the same number of pages, the last part shorter when the
/// pages do not divide evenly.
/// </summary>
public static class SplitEveryNSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Splits the chosen file every <see cref="SplitInput.PagesPerPart"/> pages.
    /// </summary>
    /// <param name="input">The file and the pages per part.</param>
    /// <returns>The parts, in order, with their PDF bytes.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen.</exception>
    public static IReadOnlyList<SplitPartFile> Build(SplitInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");

        // Each part is written to the stream returned for it; here, one memory stream per part.
        var streams = new List<MemoryStream>();
        var parts = PdfOperations.Split(file, input.PagesPerPart, part =>
        {
            var stream = new MemoryStream();
            streams.Add(stream);
            return stream;
        });

        return parts.Select((part, index) => new SplitPartFile(part, streams[index].ToArray())).ToList();
    }

    #endregion

    #endregion
}
