namespace UnlimitedPDF.Models.Internal.Importing;

/// <summary>
/// Builds a merged document's layer list (WR-036): every layer (optional content group) the imported pages use, from
/// every imported file, with the on or off state its file's default configuration gives it.
/// </summary>
internal static class OptionalContentMerger
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the <c>/OCProperties</c> dictionary as PDF syntax, or null when no imported page uses a layer.
    /// </summary>
    /// <param name="files">Each imported file with its source-to-document object map.</param>
    /// <returns>The syntax, or null.</returns>
    public static string? Build(IEnumerable<(PdfSourceFile Source, IReadOnlyDictionary<int, int> Copies)> files)
    {
        var groups = new List<int>();
        var off = new List<int>();
        foreach (var (source, copies) in files)
        {
            var table = source.Objects;
            if (table.Resolve(source.Catalog["OCProperties"]) is not PdfReadDictionary properties) continue;
            if (table.Resolve(properties["OCGs"]) is not PdfReadArray all) continue;

            var configuration = table.Resolve(properties["D"]) as PdfReadDictionary;
            bool baseOff = configuration?["BaseState"] is PdfReadName { Value: "OFF" };
            var listedOn = References(table, configuration?["ON"]);
            var listedOff = References(table, configuration?["OFF"]);

            foreach (var item in all.Items)
            {
                if (item is not PdfReadReference reference || !copies.TryGetValue(reference.ObjectNumber, out int copy) || groups.Contains(copy)) continue;
                groups.Add(copy);
                bool isOff = listedOff.Contains(reference.ObjectNumber) || (baseOff && !listedOn.Contains(reference.ObjectNumber));
                if (isOff) off.Add(copy);
            }
        }

        if (groups.Count == 0) return null;
        string List(IEnumerable<int> numbers) => "[" + string.Join(" ", numbers.Select(n => n.ToString(CultureInfo.InvariantCulture) + " 0 R")) + "]";
        return $"<< /OCGs {List(groups)} /D << /Order {List(groups)} /ON {List(groups.Except(off))} /OFF {List(off)} >> >>";
    }

    #endregion

    #region Private Methods

    private static HashSet<int> References(PdfObjectTable table, PdfReadValue? value) =>
        table.Resolve(value) is PdfReadArray array
            ? new HashSet<int>(array.Items.OfType<PdfReadReference>().Select(r => r.ObjectNumber))
            : new HashSet<int>();

    #endregion

    #endregion
}
