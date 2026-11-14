using System.Collections.ObjectModel;

namespace UnlimitedPDF.Models.Internal.Fonts;

/// <summary>
/// A list of fonts that rejects null entries (<see cref="PdfDocument.FontFallback"/>).
/// </summary>
internal sealed class FontList : Collection<PdfFont>, IReadOnlyList<PdfFont>
{
    #region Methods

    #region Protected Methods

    protected override void InsertItem(int index, PdfFont item) => base.InsertItem(index, item ?? throw new ArgumentNullException(nameof(item)));

    protected override void SetItem(int index, PdfFont item) => base.SetItem(index, item ?? throw new ArgumentNullException(nameof(item)));

    #endregion

    #endregion
}
