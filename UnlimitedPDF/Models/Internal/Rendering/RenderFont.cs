namespace UnlimitedPDF.Models.Internal.Rendering;

/// <summary>
/// A font the renderer can draw text in: it splits shown strings into glyphs with their widths and characters.
/// </summary>
internal abstract class RenderFont
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Splits the bytes of a shown string into glyphs.
    /// </summary>
    /// <param name="bytes">The string's bytes as the content stream holds them.</param>
    /// <returns>The glyphs, in order.</returns>
    public abstract IReadOnlyList<RenderGlyph> Glyphs(byte[] bytes);

    #endregion

    #endregion
}

/// <summary>
/// One glyph of a shown string.
/// </summary>
/// <param name="Code">The character code the string holds for it.</param>
/// <param name="Width">The advance width in 1/1000 of the font size.</param>
/// <param name="IsWordSpace">Whether word spacing applies: a single-byte code 32.</param>
/// <param name="Text">The characters the glyph stands for; empty when the font maps it to none.</param>
internal readonly record struct RenderGlyph(int Code, double Width, bool IsWordSpace, string Text);
