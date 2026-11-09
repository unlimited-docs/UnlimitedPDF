using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// Builds the Page ranges demo: one part per range, in the order written, such as "1-2, 3, 2-4". Ranges may overlap
/// and leave pages out.
/// </summary>
public static class SplitRangesSample
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Splits the chosen file into one part per range.
    /// </summary>
    /// <param name="input">The file and the ranges.</param>
    /// <returns>The parts, in order, with their PDF bytes.</returns>
    /// <exception cref="InvalidOperationException">No file is chosen or the range list is not valid.</exception>
    public static IReadOnlyList<SplitPartFile> Build(SplitInput input)
    {
        var file = input.File?.File ?? throw new InvalidOperationException("Choose a PDF.");
        var ranges = PickPagesSample.ParsePages(input.Ranges, file.PageCount)
            .Select(range => new PdfPageRange(range.First, range.Last));

        // Each part is written to the stream returned for it; here, one memory stream per part.
        var streams = new List<MemoryStream>();
        var parts = PdfOperations.Split(file, ranges, part =>
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
