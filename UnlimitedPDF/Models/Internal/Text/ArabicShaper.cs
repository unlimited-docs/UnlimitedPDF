namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// Shapes Arabic-script text by the OpenType Arabic shaping model (WR-039): each letter takes its isolated, final,
/// medial, or initial form from its Unicode joining type and its neighbors' (transparent marks skipped, ZWJ and tatweel
/// joining, ZWNJ breaking), then required ligatures, contextual alternates, and ligatures apply; positioning applies
/// cursive attachment, kerning, and marks. Mirrored characters of a right-to-left run take their mirror glyph or
/// <c>rtlm</c>, as in <see cref="TextShaper"/>.
/// </summary>
internal static class ArabicShaper
{
    #region Fields and Constants

    // Bit 0 is rtlm (TextShaper.Mirror); ccmp and locl are bits 1 and 2; the joining forms are bits 3 to 9.
    private static readonly IReadOnlyList<string[]> SubstitutionStages = new[]
    {
        new[] { "rtlm" },
        new[] { "ccmp", "locl" },
        new[] { "isol" },
        new[] { "fina" },
        new[] { "fin2" },
        new[] { "fin3" },
        new[] { "medi" },
        new[] { "med2" },
        new[] { "init" },
        new[] { "rlig" },
        new[] { "calt" },
        new[] { "liga", "clig", "mset" }
    };

    private static readonly string[] PositioningFeatures = { "curs", "kern", "mark", "mkmk" };

    private const int Isolated = 3, Final = 4, Medial = 7, Initial = 9; // feature bits of isol, fina, medi, init
    private static readonly ulong FormBits = Bits(3, 4, 5, 6, 7, 8, 9);

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Shapes <paramref name="text"/>, one run of Arabic-script text in <paramref name="font"/>.
    /// </summary>
    /// <param name="font">The font.</param>
    /// <param name="text">The run's text.</param>
    /// <param name="scriptTags">OpenType script tags in order of preference.</param>
    /// <param name="rightToLeft">Whether the run is right to left (it is, unless an override forces it).</param>
    /// <returns>The shaped run, in visual order.</returns>
    public static ShapedRun Shape(PdfFont font, string text, IReadOnlyList<string> scriptTags, bool rightToLeft = true)
    {
        var buffer = TextShaper.Map(font, text);
        if (rightToLeft) TextShaper.Mirror(font, buffer);
        else foreach (var glyph in buffer) glyph.Mask &= ~TextShaper.FeatureBit(0);

        var forms = Forms(buffer.Select(g => JoiningData.TypeOf(g.CodePoint)).ToArray());
        for (int i = 0; i < buffer.Count; i++)
        {
            ulong mask = buffer[i].Mask & ~FormBits;
            if (forms[i] is { } form) mask |= TextShaper.FeatureBit(form);
            buffer[i].Mask = mask;
        }

        TextShaper.Substitute(font, buffer, scriptTags, SubstitutionStages);
        return TextShaper.Finish(font, buffer, scriptTags, PositioningFeatures, rightToLeft);
    }

    /// <summary>
    /// Returns each character's joining form as the feature bit to apply (isol, fina, medi, or init), or null for
    /// characters that take no form: non-joining, join-causing, and transparent ones.
    /// </summary>
    public static int?[] Forms(IReadOnlyList<JoiningType> types)
    {
        int count = types.Count;
        var forms = new int?[count];
        int previous = -1;
        for (int i = 0; i < count; i++)
        {
            var type = types[i];
            if (type == JoiningType.Transparent) continue;
            int next = i + 1;
            while (next < count && types[next] == JoiningType.Transparent) next++;

            bool joinsPrevious = type is JoiningType.RightJoining or JoiningType.DualJoining or JoiningType.JoinCausing
                && previous >= 0 && types[previous] is JoiningType.LeftJoining or JoiningType.DualJoining or JoiningType.JoinCausing;
            bool joinsNext = type is JoiningType.LeftJoining or JoiningType.DualJoining or JoiningType.JoinCausing
                && next < count && types[next] is JoiningType.RightJoining or JoiningType.DualJoining or JoiningType.JoinCausing;

            if (type is JoiningType.RightJoining or JoiningType.LeftJoining or JoiningType.DualJoining)
                forms[i] = joinsPrevious && joinsNext ? Medial : joinsPrevious ? Final : joinsNext ? Initial : Isolated;
            previous = i;
        }
        return forms;
    }

    #endregion

    #region Private Methods

    private static ulong Bits(params int[] bits) => bits.Aggregate(0UL, (mask, bit) => mask | TextShaper.FeatureBit(bit));

    #endregion

    #endregion
}
