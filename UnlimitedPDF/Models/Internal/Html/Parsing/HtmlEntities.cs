namespace UnlimitedPDF.Models.Internal.Html.Parsing;

/// <summary>
/// Named character references (WR-038): every HTML 4 reference plus the common HTML punctuation references. An unknown
/// name is kept as written. Characters outside WinAnsi are later written as '?' (WR-038), so the rest of the HTML
/// Standard's list would only add more of those.
/// </summary>
internal static class HtmlEntities
{
    #region Fields and Constants

    // name:code pairs. The Latin-1 block (160 to 255) is listed in code order and numbered from 160.
    private const string Latin1Names =
        "nbsp iexcl cent pound curren yen brvbar sect uml copy ordf laquo not shy reg macr deg plusmn sup2 sup3 acute " +
        "micro para middot cedil sup1 ordm raquo frac14 frac12 frac34 iquest Agrave Aacute Acirc Atilde Auml Aring AElig " +
        "Ccedil Egrave Eacute Ecirc Euml Igrave Iacute Icirc Iuml ETH Ntilde Ograve Oacute Ocirc Otilde Ouml times Oslash " +
        "Ugrave Uacute Ucirc Uuml Yacute THORN szlig agrave aacute acirc atilde auml aring aelig ccedil egrave eacute ecirc " +
        "euml igrave iacute icirc iuml eth ntilde ograve oacute ocirc otilde ouml divide oslash ugrave uacute ucirc uuml " +
        "yacute thorn yuml";

    private const string OtherNames =
        "quot:34 amp:38 lt:60 gt:62 apos:39 OElig:338 oelig:339 Scaron:352 scaron:353 Yuml:376 circ:710 tilde:732 " +
        "ensp:8194 emsp:8195 thinsp:8201 zwnj:8204 zwj:8205 lrm:8206 rlm:8207 ndash:8211 mdash:8212 lsquo:8216 rsquo:8217 " +
        "sbquo:8218 ldquo:8220 rdquo:8221 bdquo:8222 dagger:8224 Dagger:8225 permil:8240 lsaquo:8249 rsaquo:8250 euro:8364 " +
        "fnof:402 Alpha:913 Beta:914 Gamma:915 Delta:916 Epsilon:917 Zeta:918 Eta:919 Theta:920 Iota:921 Kappa:922 " +
        "Lambda:923 Mu:924 Nu:925 Xi:926 Omicron:927 Pi:928 Rho:929 Sigma:931 Tau:932 Upsilon:933 Phi:934 Chi:935 Psi:936 " +
        "Omega:937 alpha:945 beta:946 gamma:947 delta:948 epsilon:949 zeta:950 eta:951 theta:952 iota:953 kappa:954 " +
        "lambda:955 mu:956 nu:957 xi:958 omicron:959 pi:960 rho:961 sigmaf:962 sigma:963 tau:964 upsilon:965 phi:966 " +
        "chi:967 psi:968 omega:969 thetasym:977 upsih:978 piv:982 bull:8226 hellip:8230 prime:8242 Prime:8243 oline:8254 " +
        "frasl:8260 weierp:8472 image:8465 real:8476 trade:8482 alefsym:8501 larr:8592 uarr:8593 rarr:8594 darr:8595 " +
        "harr:8596 crarr:8629 lArr:8656 uArr:8657 rArr:8658 dArr:8659 hArr:8660 forall:8704 part:8706 exist:8707 " +
        "empty:8709 nabla:8711 isin:8712 notin:8713 ni:8715 prod:8719 sum:8721 minus:8722 lowast:8727 radic:8730 prop:8733 " +
        "infin:8734 ang:8736 and:8743 or:8744 cap:8745 cup:8746 int:8747 there4:8756 sim:8764 cong:8773 asymp:8776 ne:8800 " +
        "equiv:8801 le:8804 ge:8805 sub:8834 sup:8835 nsub:8836 sube:8838 supe:8839 oplus:8853 otimes:8855 perp:8869 " +
        "sdot:8901 lceil:8968 rceil:8969 lfloor:8970 rfloor:8971 lang:9001 rang:9002 loz:9674 spades:9824 clubs:9827 " +
        "hearts:9829 diams:9830 Tab:9 NewLine:10 excl:33 num:35 dollar:36 percnt:37 lpar:40 rpar:41 ast:42 plus:43 " +
        "comma:44 period:46 sol:47 colon:58 semi:59 equals:61 quest:63 commat:64 lsqb:91 lbrack:91 bsol:92 rsqb:93 " +
        "rbrack:93 Hat:94 lowbar:95 grave:96 lcub:123 lbrace:123 verbar:124 vert:124 rcub:125 rbrace:125 half:189 " +
        "hyphen:8208 dash:8208 check:10003 cross:10007 star:9734 starf:9733 copysr:8471 numero:8470";

    private static readonly Dictionary<string, string> Names = BuildNames();

    // References browsers accept without the closing semicolon: the Latin-1 names and four markup characters.
    private static readonly HashSet<string> LegacyNames = new(Latin1Names.Split(' ').Concat(new[] { "quot", "amp", "lt", "gt" }), StringComparer.Ordinal);

    // Windows-1252 characters that numeric references to codes 128 to 159 stand for, as browsers read them.
    private static readonly int[] C1Replacements =
    {
        0x20AC, 0x81, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x8D, 0x017D, 0x8F,
        0x90, 0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x9D, 0x017E, 0x0178
    };

    #endregion

    #region Methods

    #region Public Methods

    /// <summary>
    /// Reads a character reference starting at an ampersand.
    /// </summary>
    /// <param name="text">The text.</param>
    /// <param name="ampersand">The index of the ampersand.</param>
    /// <param name="inAttribute">Whether the text is an attribute value, where a legacy reference followed by a letter,
    /// digit, or '=' is not decoded.</param>
    /// <param name="length">The number of characters the reference uses, including the ampersand.</param>
    /// <returns>The decoded text, or null when the ampersand starts no reference.</returns>
    public static string? Read(string text, int ampersand, bool inAttribute, out int length)
    {
        length = 0;
        int i = ampersand + 1;
        if (i >= text.Length) return null;

        if (text[i] == '#')
        {
            i++;
            bool hex = i < text.Length && (text[i] == 'x' || text[i] == 'X');
            if (hex) i++;
            int start = i;
            long value = 0;
            while (i < text.Length && (hex ? Uri.IsHexDigit(text[i]) : char.IsDigit(text[i])))
            {
                value = Math.Min(value * (hex ? 16 : 10) + Convert.ToInt32(text[i].ToString(), hex ? 16 : 10), 0x110000);
                i++;
            }
            if (i == start) return null;
            if (i < text.Length && text[i] == ';') i++;
            length = i - ampersand;
            return Numeric(value);
        }

        int nameStart = i;
        while (i < text.Length && char.IsLetterOrDigit(text[i]) && i - nameStart < 32) i++;
        if (i == nameStart) return null;
        string name = text.Substring(nameStart, i - nameStart);
        if (i < text.Length && text[i] == ';' && Names.TryGetValue(name, out var value1))
        {
            length = i + 1 - ampersand;
            return value1;
        }

        // A legacy reference without a semicolon: the longest legacy name that prefixes the run.
        for (int n = name.Length; n > 0; n--)
        {
            string prefix = name.Substring(0, n);
            if (!LegacyNames.Contains(prefix)) continue;
            int after = nameStart + n;
            if (inAttribute && after < text.Length && (char.IsLetterOrDigit(text[after]) || text[after] == '=')) return null;
            length = after - ampersand;
            return Names[prefix];
        }
        return null;
    }

    #endregion

    #region Private Methods

    private static Dictionary<string, string> BuildNames()
    {
        var names = new Dictionary<string, string>(StringComparer.Ordinal);
        var latin1 = Latin1Names.Split(' ');
        for (int i = 0; i < latin1.Length; i++) names[latin1[i]] = ((char)(160 + i)).ToString();
        foreach (var pair in OtherNames.Split(' '))
        {
            int colon = pair.IndexOf(':');
            names[pair.Substring(0, colon)] = char.ConvertFromUtf32(int.Parse(pair.Substring(colon + 1), CultureInfo.InvariantCulture));
        }
        return names;
    }

    // A numeric reference's character, as browsers read it: zero, surrogates, and values past the Unicode range become
    // the replacement character, and 128 to 159 the windows-1252 characters.
    private static string Numeric(long value)
    {
        if (value == 0 || value > 0x10FFFF || (value >= 0xD800 && value <= 0xDFFF)) return "�";
        if (value >= 0x80 && value <= 0x9F) return char.ConvertFromUtf32(C1Replacements[value - 0x80]);
        return char.ConvertFromUtf32((int)value);
    }

    #endregion

    #endregion
}
