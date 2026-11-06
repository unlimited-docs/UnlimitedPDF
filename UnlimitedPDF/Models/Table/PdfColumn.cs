namespace UnlimitedPDF;

public class PdfColumn
{
    public int Index { get; internal set; }

    /// <summary>
    /// Width in points for the column. 
    /// Null means 'auto' and will be distributed.
    /// </summary>
    public double? Width { get; set; } = null;

    public PdfColumn(int index) { Index = index; }
}
