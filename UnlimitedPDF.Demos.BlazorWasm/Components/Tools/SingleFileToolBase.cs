using Microsoft.AspNetCore.Components.Forms;
using UnlimitedPDF.Demos.BlazorWasm.Samples;
using UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;
using UnlimitedPDF.Demos.BlazorWasm.Services.Tools;

namespace UnlimitedPDF.Demos.BlazorWasm.Components.Tools;

/// <summary>
/// What the tools that take one PDF share (Split PDF, Extract pages, Remove pages): the PDF held, a new one replacing
/// it, the sample PDF, and the page selection over it (26112201 AC-04).
/// </summary>
public abstract class SingleFileToolBase : ToolPageBase
{
    #region Methods

    #region Protected Methods

    /// <summary>
    /// Reads the PDF the visitor chose or dropped; it replaces the one held.
    /// </summary>
    /// <param name="args">The chosen files.</param>
    /// <returns>A task that completes when the file is read.</returns>
    protected async Task OnFilesAsync(InputFileChangeEventArgs args)
    {
        await AddPdfsAsync(args, Files, replace: true);
        Reset();
    }

    /// <summary>
    /// Sets the options for a new PDF.
    /// </summary>
    protected virtual void Reset()
    {
        if (File is { } file) Selection = new PageSelection(file.Value.PageCount);
    }

    /// <summary>
    /// Goes back to the landing with nothing held.
    /// </summary>
    /// <returns>A task that completes when the result's URLs are released.</returns>
    protected async Task StartOverAsync()
    {
        Files.Clear();
        FileErrors.Clear();
        Selection = null;
        await ClearResultAsync();
        GoTo(ToolPageStep.Landing);
    }

    /// <summary>
    /// Uses the sample report, several pages long.
    /// </summary>
    protected void UseSample()
    {
        FileErrors.Clear();
        Files.Clear();
        AddSample(Files, "report.pdf", HeadersFootersSample.Build(HeadersFootersInput.Report()));
        Reset();
        GoTo(ToolPageStep.Workspace);
    }

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the PDF held, or null.
    /// </summary>
    protected ToolFile<PdfFile>? File => Files.Count > 0 ? Files.Files[0] : null;

    /// <summary>
    /// Gets the set that holds the one PDF.
    /// </summary>
    protected ToolFileSet<PdfFile> Files { get; } = new(maxFiles: 1, maxTotalBytes: ToolLimits.MaxPdfBytes);

    /// <summary>
    /// Gets the line under the workspace heading, for example "contract.pdf · 8 pages · 220 KB".
    /// </summary>
    protected string FileLine => File is { } file ? $"{file.Name} · {CountText(file.Value.PageCount, "page")} · {SizeText(file.Size)}" : string.Empty;

    /// <summary>
    /// Gets or sets the pages picked.
    /// </summary>
    protected PageSelection? Selection { get; set; }

    /// <summary>
    /// Gets the held file's name without its extension, made safe, for result names.
    /// </summary>
    protected string Stem => SafeFileName.StemOf(File?.Name, "document");

    #endregion
}
