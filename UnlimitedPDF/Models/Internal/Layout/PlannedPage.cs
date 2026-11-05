using UnlimitedPDF.Models.Table;

namespace UnlimitedPDF.Models.Internal.Layout;

/// <summary>
/// One saved page as the planner laid it out: which root page it is or continues, its number in reading order, its
/// geometry, the table parts carried onto it from its root page, and the body flow content placed on it.
/// </summary>
internal sealed class PlannedPage
{
    #region Constructors and Finalizers

    public PlannedPage(int rootIndex, int chainIndex, int number, PdfPageSize size, PageGeometry geometry)
    {
        RootIndex = rootIndex;
        ChainIndex = chainIndex;
        Number = number;
        Size = size;
        Geometry = geometry;
    }

    #endregion

    #region Methods

    /// <summary>
    /// Adds a carried table part.
    /// </summary>
    /// <param name="table">The table.</param>
    /// <param name="part">The part drawn on this page.</param>
    public void Carry(PdfTable table, TablePart part)
    {
        Carried.Add((table, part));
        if (part.HasRows) CarriedBottom = Math.Min(CarriedBottom ?? double.MaxValue, part.Bottom);
    }

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the parts of page-coordinate tables from the root page that continue on this page, in drawing order.
    /// </summary>
    public List<(PdfTable Table, TablePart Part)> Carried { get; } = new();

    /// <summary>
    /// Gets the lowest y-coordinate where a carried part with rows ends, or null when no rows are carried here. The
    /// body flow continues below it (WR-033).
    /// </summary>
    public double? CarriedBottom { get; private set; }

    /// <summary>
    /// Gets the index of the page among its root page (0) and the pages that continue it.
    /// </summary>
    public int ChainIndex { get; }

    /// <summary>
    /// Gets the body flow content placed on this page, in drawing order.
    /// </summary>
    public List<FlowPlacement> Flow { get; } = new();

    /// <summary>
    /// Gets the page's geometry.
    /// </summary>
    public PageGeometry Geometry { get; }

    /// <summary>
    /// Gets the 1-based page number in reading order.
    /// </summary>
    public int Number { get; }

    /// <summary>
    /// Gets the index of the root page among the planned roots.
    /// </summary>
    public int RootIndex { get; }

    /// <summary>
    /// Gets the page size.
    /// </summary>
    public PdfPageSize Size { get; }

    #endregion
}
