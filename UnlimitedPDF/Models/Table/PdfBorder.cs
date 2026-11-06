namespace UnlimitedPDF;

public class PdfBorder
{
    public PdfColor Color { get; set; } = PdfColor.Black;
    public PdfBorderStyle Style { get; set; } = PdfBorderStyle.Solid;
    public double Width { get; set; } = 0.5;
}
