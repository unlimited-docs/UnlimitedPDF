using UnlimitedPDF;

namespace UnlimitedPDF.Demos.BlazorWasm.Samples.Inputs;

/// <summary>
/// A merge pack's inputs: the cover title and the generated documents, each of which can be left out.
/// </summary>
public sealed class MergePackInput
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the input.
    /// </summary>
    /// <param name="coverTitle">The default cover title.</param>
    /// <param name="documentNames">The names of the pack's documents, in order.</param>
    public MergePackInput(string coverTitle, params string[] documentNames)
    {
        CoverTitle = coverTitle;
        Documents = documentNames.Select(name => new PackDocumentInput(name)).ToList();
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the cover title.
    /// </summary>
    public string CoverTitle { get; set; }

    /// <summary>
    /// Gets the pack's documents, in order.
    /// </summary>
    public List<PackDocumentInput> Documents { get; }

    #endregion
}

/// <summary>
/// One document of a merge pack: its name, whether it is included, and its generated PDF.
/// </summary>
public sealed class PackDocumentInput
{
    #region Constructors and Finalizers

    /// <summary>
    /// Creates the input.
    /// </summary>
    /// <param name="name">The document's name, used for its bookmark and on the cover.</param>
    public PackDocumentInput(string name)
    {
        Name = name;
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets or sets the generated document, loaded as an existing PDF.
    /// </summary>
    public PdfFile? File { get; set; }

    /// <summary>
    /// Gets or sets whether the document is in the pack.
    /// </summary>
    public bool Include { get; set; } = true;

    /// <summary>
    /// Gets the document's name.
    /// </summary>
    public string Name { get; }

    #endregion
}
