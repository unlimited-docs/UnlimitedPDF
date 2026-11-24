namespace UnlimitedPDF.Models.Internal.Text;

/// <summary>
/// What the Indic shaper needs to know about one script (WR-039): its OpenType script tags, the letters that can form
/// a reph and how a reph is asked for, where the reph is drawn among the syllable's glyphs, the split vowel signs and
/// their parts, and whether a pre-base form moves before the base. The values follow the OpenType script development
/// specification for each script.
/// </summary>
internal sealed class IndicScript
{
    #region Fields and Constants

    private static readonly Dictionary<string, IndicScript> ByTag;
    private static readonly HashSet<int> RaLetters;
    private static readonly Dictionary<int, int[]> SplitVowels;

    #endregion

    #region Constructors and Finalizers

    static IndicScript()
    {
        var all = new[]
        {
            new IndicScript("deva", "dev2", "deva", new[] { 0x0930 }, RephKind.Implicit, RephPlace.BeforePostBaseVowelSign,
                new Dictionary<int, int[]>(), reordersPreBaseForm: false, keepsReleasedOutput: true),
            new IndicScript("beng", "bng2", "beng", new[] { 0x09B0, 0x09F0 }, RephKind.Implicit, RephPlace.BeforePostBaseForm,
                new Dictionary<int, int[]>
                {
                    [0x09CB] = new[] { 0x09C7, 0x09BE }, // BENGALI O
                    [0x09CC] = new[] { 0x09C7, 0x09D7 }  // BENGALI AU
                }, reordersPreBaseForm: false, keepsReleasedOutput: false),
            new IndicScript("guru", "gur2", "guru", new[] { 0x0A30 }, RephKind.Implicit, RephPlace.AfterBase,
                new Dictionary<int, int[]>(), reordersPreBaseForm: false, keepsReleasedOutput: false),
            new IndicScript("gujr", "gjr2", "gujr", new[] { 0x0AB0 }, RephKind.Implicit, RephPlace.BeforePostBaseForm,
                new Dictionary<int, int[]>(), reordersPreBaseForm: false, keepsReleasedOutput: false),
            new IndicScript("orya", "ory2", "orya", new[] { 0x0B30 }, RephKind.Implicit, RephPlace.AfterBase,
                new Dictionary<int, int[]>
                {
                    [0x0B48] = new[] { 0x0B47, 0x0B56 }, // ORIYA AI
                    [0x0B4B] = new[] { 0x0B47, 0x0B3E }, // ORIYA O
                    [0x0B4C] = new[] { 0x0B47, 0x0B57 }  // ORIYA AU
                }, reordersPreBaseForm: false, keepsReleasedOutput: false),
            new IndicScript("taml", "tml2", "taml", new[] { 0x0BB0 }, RephKind.None, RephPlace.BeforePostBaseVowelSign,
                new Dictionary<int, int[]>
                {
                    [0x0BCA] = new[] { 0x0BC6, 0x0BBE }, // TAMIL O
                    [0x0BCB] = new[] { 0x0BC7, 0x0BBE }, // TAMIL OO
                    [0x0BCC] = new[] { 0x0BC6, 0x0BD7 }  // TAMIL AU
                }, reordersPreBaseForm: false, keepsReleasedOutput: true),
            new IndicScript("telu", "tel2", "telu", new[] { 0x0C30 }, RephKind.Explicit, RephPlace.BeforePostBaseVowelSign,
                new Dictionary<int, int[]>
                {
                    [0x0C48] = new[] { 0x0C46, 0x0C56 } // TELUGU AI
                }, reordersPreBaseForm: false, keepsReleasedOutput: true),
            new IndicScript("knda", "knd2", "knda", new[] { 0x0CB0 }, RephKind.Implicit, RephPlace.BeforeModifiers,
                new Dictionary<int, int[]>
                {
                    [0x0CC0] = new[] { 0x0CBF, 0x0CD5 },        // KANNADA II
                    [0x0CC7] = new[] { 0x0CC6, 0x0CD5 },        // KANNADA EE
                    [0x0CC8] = new[] { 0x0CC6, 0x0CD6 },        // KANNADA AI
                    [0x0CCA] = new[] { 0x0CC6, 0x0CC2 },        // KANNADA O
                    [0x0CCB] = new[] { 0x0CC6, 0x0CC2, 0x0CD5 } // KANNADA OO
                }, reordersPreBaseForm: false, keepsReleasedOutput: false),
            new IndicScript("mlym", "mlm2", "mlym", new[] { 0x0D30 }, RephKind.Encoded, RephPlace.AfterBase,
                new Dictionary<int, int[]>
                {
                    [0x0D4A] = new[] { 0x0D46, 0x0D3E }, // MALAYALAM O
                    [0x0D4B] = new[] { 0x0D47, 0x0D3E }, // MALAYALAM OO
                    [0x0D4C] = new[] { 0x0D46, 0x0D57 }  // MALAYALAM AU
                }, reordersPreBaseForm: true, keepsReleasedOutput: false)
        };

        All = all;
        ByTag = all.ToDictionary(s => s.Tag, StringComparer.Ordinal);
        RaLetters = new HashSet<int>(all.SelectMany(s => s.Ra));
        SplitVowels = all.SelectMany(s => s.Splits).ToDictionary(p => p.Key, p => p.Value);
    }

    private IndicScript(string tag, string version2Tag, string version1Tag, int[] ra, RephKind reph, RephPlace rephPlace,
        Dictionary<int, int[]> splits, bool reordersPreBaseForm, bool keepsReleasedOutput)
    {
        KeepsReleasedOutput = keepsReleasedOutput;
        Tag = tag;
        Version2Tag = version2Tag;
        Version1Tag = version1Tag;
        Ra = ra;
        Reph = reph;
        RephPosition = rephPlace;
        Splits = splits;
        ReordersPreBaseForm = reordersPreBaseForm;
    }

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the settings of the script with the given detection tag (<c>deva</c>, <c>telu</c>, ...), or null.
    /// </summary>
    /// <param name="tag">The script's detection tag.</param>
    /// <returns>The script's settings, or null for a script the Indic shaper does not handle.</returns>
    public static IndicScript? For(string tag) => ByTag.TryGetValue(tag, out var script) ? script : null;

    /// <summary>
    /// Returns whether the code point is a letter Ra that can form a reph in its script.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>True for a Ra letter.</returns>
    public static bool IsRa(int codePoint) => RaLetters.Contains(codePoint);

    /// <summary>
    /// Returns where a vowel sign drawn after the base goes among the base's other glyphs, by the script's rule.
    /// </summary>
    /// <param name="codePoint">The vowel sign.</param>
    /// <param name="position">Its Unicode positional category.</param>
    /// <returns>The vowel sign's place.</returns>
    public MatraPlace PlaceOf(int codePoint, IndicPositionalCategory position)
    {
        bool top = position is IndicPositionalCategory.Top;
        bool bottom = position is IndicPositionalCategory.Bottom;
        return Tag switch
        {
            "beng" => bottom ? MatraPlace.AfterSub : MatraPlace.AfterPost,
            "guru" => MatraPlace.AfterPost,
            "gujr" => top ? MatraPlace.AfterSub : MatraPlace.AfterPost,
            "orya" => top ? MatraPlace.AfterMain : bottom ? MatraPlace.AfterSub : MatraPlace.AfterPost,
            "knda" => top || bottom || codePoint < 0x0CC3 || codePoint > 0x0CD6 ? MatraPlace.BeforeSub : MatraPlace.AfterSub,
            "mlym" => MatraPlace.AfterPost,
            _ => MatraPlace.AfterPost
        };
    }

    /// <summary>
    /// Returns the parts a split vowel sign is drawn as (its canonical decomposition), or null for any other code point.
    /// </summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The parts in text order, or null.</returns>
    public static int[]? SplitParts(int codePoint) => SplitVowels.TryGetValue(codePoint, out var parts) ? parts : null;

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets every script the Indic shaper handles.
    /// </summary>
    public static IReadOnlyList<IndicScript> All { get; }

    /// <summary>
    /// Gets whether the script keeps the output version 1.1.0 shipped (WR-039): Devanagari, Telugu, and Tamil. The
    /// other scripts also follow these rules of the OpenType Indic model: a syllable that ends in halant + ZWJ asks for
    /// the half form of its consonants, and a joiner may stand before a halant inside a syllable.
    /// </summary>
    public bool KeepsReleasedOutput { get; }

    /// <summary>
    /// Gets the letters Ra of the script that form a reph.
    /// </summary>
    public IReadOnlyList<int> Ra { get; }

    /// <summary>
    /// Gets whether a glyph the font's <c>pref</c> feature forms moves before the base consonant.
    /// </summary>
    public bool ReordersPreBaseForm { get; }

    /// <summary>
    /// Gets how the script asks for a reph.
    /// </summary>
    public RephKind Reph { get; }

    /// <summary>
    /// Gets where the reph is drawn among the syllable's glyphs after the basic features.
    /// </summary>
    public RephPlace RephPosition { get; }

    /// <summary>
    /// Gets the script's split vowel signs and their parts.
    /// </summary>
    public IReadOnlyDictionary<int, int[]> Splits { get; }

    /// <summary>
    /// Gets the tag the text pipeline uses for the script (the OpenType version-1 script tag).
    /// </summary>
    public string Tag { get; }

    /// <summary>
    /// Gets the OpenType version-1 script tag, used when a font has no version-2 tag.
    /// </summary>
    public string Version1Tag { get; }

    /// <summary>
    /// Gets the OpenType version-2 script tag, preferred when the font has it.
    /// </summary>
    public string Version2Tag { get; }

    #endregion

    #region Nested Types

    /// <summary>
    /// Where a vowel sign after the base goes: right after the base, before the below-base forms, after them, or after
    /// the post-base forms.
    /// </summary>
    internal enum MatraPlace
    {
        /// <summary>Right after the base consonant.</summary>
        AfterMain,

        /// <summary>After the base, before the below-base forms.</summary>
        BeforeSub,

        /// <summary>After the below-base forms, before the post-base forms.</summary>
        AfterSub,

        /// <summary>After the post-base forms.</summary>
        AfterPost
    }

    /// <summary>
    /// How a script asks for a reph.
    /// </summary>
    internal enum RephKind
    {
        /// <summary>The script draws no reph.</summary>
        None,

        /// <summary>Ra + virama at the start of a syllable forms a reph; a joiner after the virama asks for none.</summary>
        Implicit,

        /// <summary>Ra + virama + ZWJ at the start of a syllable forms a reph.</summary>
        Explicit,

        /// <summary>The reph is its own character, typed before the syllable's consonants (Malayalam dot reph).</summary>
        Encoded
    }

    /// <summary>
    /// Where the reph goes in final reordering.
    /// </summary>
    internal enum RephPlace
    {
        /// <summary>Before the first post-base vowel sign drawn on the right or the first syllable modifier, else last.</summary>
        BeforePostBaseVowelSign,

        /// <summary>As <see cref="BeforePostBaseVowelSign"/>, and also before the first post-base consonant form.</summary>
        BeforePostBaseForm,

        /// <summary>Right after the base consonant.</summary>
        AfterBase,

        /// <summary>Last in the syllable, before its syllable modifiers.</summary>
        BeforeModifiers
    }

    #endregion
}
