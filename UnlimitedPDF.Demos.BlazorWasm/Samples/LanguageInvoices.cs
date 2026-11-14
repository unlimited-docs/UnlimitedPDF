namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The five sample invoices of the Fonts and languages demo (D-024): Hindi, Telugu, Tamil, French, and a mixed one with
/// English labels and Telugu and Hindi names. All businesses and addresses are fictional.
/// </summary>
public static class LanguageInvoices
{
    #region Methods

    #region Public Methods

    /// <summary>
    /// Returns the preset with an id, or the first preset when no preset has it.
    /// </summary>
    /// <param name="id">The preset id.</param>
    /// <returns>The preset.</returns>
    public static LanguageInvoice Find(string? id) => All.FirstOrDefault(p => p.Id == id) ?? All[0];

    #endregion

    #endregion

    #region Properties, Indexers

    /// <summary>
    /// Gets the presets in button order.
    /// </summary>
    public static IReadOnlyList<LanguageInvoice> All { get; } =
    [
        new("hindi", "Hindi",
            new InvoiceLabels("कर बीजक", "बीजक संख्या", "दिनांक", "विक्रेता", "क्रेता", "विवरण", "मात्रा", "दर", "राशि", "कुल योग", "आपके व्यापार के लिए धन्यवाद!"),
            "शर्मा वस्त्र भंडार", "चांदनी चौक, दिल्ली 110006",
            "गुप्ता ट्रेडर्स", "एम. आई. रोड, जयपुर 302001",
            [("सूती कुर्ता", 2, 850m), ("रेशमी दुपट्टा", 1, 1200m), ("हथकरघा शॉल", 1, 2400m)]),
        new("telugu", "Telugu",
            new InvoiceLabels("పన్ను ఇన్‌వాయిస్", "ఇన్‌వాయిస్ సంఖ్య", "తేదీ", "విక్రేత", "కొనుగోలుదారు", "వివరణ", "పరిమాణం", "ధర", "విలువ", "మొత్తం", "మీ వ్యాపారానికి ధన్యవాదాలు!"),
            "శ్రీ లక్ష్మి టెక్స్‌టైల్స్", "బీసెంట్ రోడ్, విజయవాడ 520002",
            "రామకృష్ణ స్టోర్స్", "అబిడ్స్, హైదరాబాద్ 500001",
            [("కాటన్ చీర", 2, 1500m), ("పట్టు జాకెట్", 1, 800m), ("చేనేత దుప్పటి", 1, 1350m)]),
        new("tamil", "Tamil",
            new InvoiceLabels("வரி விலைப்பட்டியல்", "விலைப்பட்டியல் எண்", "தேதி", "விற்பனையாளர்", "வாங்குபவர்", "விவரம்", "அளவு", "விலை", "தொகை", "மொத்தம்", "உங்கள் வணிகத்திற்கு நன்றி!"),
            "முருகன் ஜவுளி மாளிகை", "டி. நகர், சென்னை 600017",
            "செல்வம் ஸ்டோர்ஸ்", "மேல மாசி வீதி, மதுரை 625001",
            [("பருத்தி வேட்டி", 2, 600m), ("பட்டுப் புடவை", 1, 4500m), ("கைத்தறி துண்டு", 3, 250m)]),
        new("french", "French",
            new InvoiceLabels("Facture", "N° de facture", "Date", "Vendeur", "Client", "Désignation", "Qté", "Prix unitaire", "Montant", "Total TTC", "Merci pour votre confiance !"),
            "Brûlerie Saint-Éloi", "12 rue de la Paix, 69002 Lyon",
            "Café Lumière", "4 place Bellecour, 69002 Lyon",
            [("Café en grains « Moka »", 3, 18.50m), ("Thé vert Sencha", 2, 9.00m), ("Crème de marrons", 1, 6.80m)],
            Euro: true),
        new("mixed", "Mixed",
            new InvoiceLabels("Tax invoice", "Invoice number", "Date", "Seller", "Buyer", "Description", "Qty", "Rate", "Amount", "Total", "Thank you · ధన్యవాదాలు · धन्यवाद"),
            "Lakshmi Textiles · శ్రీ లక్ష్మి టెక్స్‌టైల్స్", "Besant Road, Vijayawada 520002",
            "Sharma Traders · शर्मा ट्रेडर्स", "M. I. Road, Jaipur 302001",
            [("Cotton saree · కాటన్ చీర", 2, 1500m), ("Silk dupatta · रेशमी दुपट्टा", 1, 1200m)])
    ];

    #endregion
}
