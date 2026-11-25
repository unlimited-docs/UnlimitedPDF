namespace UnlimitedPDF.Demos.BlazorWasm.Samples;

/// <summary>
/// The sample invoices of the Fonts and languages demo (D-024; WR-039; QF-26112401): Hindi, Telugu, Tamil, Bengali,
/// Gujarati, Punjabi, Odia, Kannada, Malayalam, French, Polish, Czech, Turkish, Filipino, Greek, Russian, Ukrainian,
/// Armenian, Georgian, the right-to-left Arabic, Persian, Urdu, Hebrew, and Dhivehi (WR-041), and a mixed one with
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
        new("bengali", "Bengali",
            new InvoiceLabels("কর চালান", "চালান নম্বর", "তারিখ", "বিক্রেতা", "ক্রেতা", "বিবরণ", "পরিমাণ", "দর", "মূল্য", "সর্বমোট", "আপনার ব্যবসার জন্য ধন্যবাদ!"),
            "দাস বস্ত্রালয়", "কলেজ স্ট্রিট, কলকাতা 700073",
            "শর্মা ট্রেডার্স", "পল্টন বাজার, গুয়াহাটি 781008",
            [("তাঁতের শাড়ি", 2, 1800m), ("রেশমি চাদর", 1, 2200m), ("সুতির গামছা", 3, 150m)]),
        new("gujarati", "Gujarati",
            new InvoiceLabels("કર ભરતિયું", "ભરતિયા નંબર", "તારીખ", "વેચનાર", "ખરીદનાર", "વિગત", "જથ્થો", "દર", "રકમ", "કુલ રકમ", "તમારા વ્યવસાય બદલ આભાર!"),
            "પટેલ કાપડ ભંડાર", "રતનપોળ, અમદાવાદ 380001",
            "શાહ ટ્રેડર્સ", "સ્ટેશન રોડ, સુરત 395003",
            [("સુતરાઉ કુર્તો", 2, 900m), ("બાંધણી દુપટ્ટો", 1, 1400m), ("પટોળા સાડી", 1, 5200m)]),
        new("punjabi", "Punjabi",
            new InvoiceLabels("ਟੈਕਸ ਬਿੱਲ", "ਬਿੱਲ ਨੰਬਰ", "ਮਿਤੀ", "ਵਿਕਰੇਤਾ", "ਖਰੀਦਦਾਰ", "ਵੇਰਵਾ", "ਮਾਤਰਾ", "ਦਰ", "ਰਕਮ", "ਕੁੱਲ ਜੋੜ", "ਤੁਹਾਡੇ ਕਾਰੋਬਾਰ ਲਈ ਧੰਨਵਾਦ!"),
            "ਸਿੰਘ ਕੱਪੜਾ ਭੰਡਾਰ", "ਹਾਲ ਬਾਜ਼ਾਰ, ਅੰਮ੍ਰਿਤਸਰ 143006",
            "ਗਿੱਲ ਟ੍ਰੇਡਰਜ਼", "ਘੰਟਾ ਘਰ ਚੌਕ, ਲੁਧਿਆਣਾ 141008",
            [("ਸੂਤੀ ਕੁੜਤਾ", 2, 800m), ("ਫੁਲਕਾਰੀ ਦੁਪੱਟਾ", 1, 1600m), ("ਪਸ਼ਮੀਨਾ ਸ਼ਾਲ", 1, 3500m)]),
        new("odia", "Odia",
            new InvoiceLabels("କର ଚାଲାଣ", "ଚାଲାଣ ନମ୍ବର", "ତାରିଖ", "ବିକ୍ରେତା", "କ୍ରେତା", "ବିବରଣୀ", "ପରିମାଣ", "ଦର", "ରାଶି", "ସର୍ବମୋଟ", "ଆପଣଙ୍କ ବ୍ୟବସାୟ ପାଇଁ ଧନ୍ୟବାଦ!"),
            "ମହାପାତ୍ର ବସ୍ତ୍ରାଳୟ", "ମାର୍କେଟ ବିଲ୍ଡିଂ, ଭୁବନେଶ୍ୱର 751001",
            "ସାହୁ ଷ୍ଟୋର୍ସ", "ବଡ଼ ଦାଣ୍ଡ, ପୁରୀ 752001",
            [("ସମ୍ବଲପୁରୀ ଶାଢ଼ୀ", 1, 4800m), ("କଟକୀ ଧୋତି", 2, 950m), ("ସୂତା ଗାମୁଛା", 3, 180m)]),
        new("kannada", "Kannada",
            new InvoiceLabels("ತೆರಿಗೆ ಸರಕುಪಟ್ಟಿ", "ಸರಕುಪಟ್ಟಿ ಸಂಖ್ಯೆ", "ದಿನಾಂಕ", "ಮಾರಾಟಗಾರ", "ಖರೀದಿದಾರ", "ವಿವರ", "ಪ್ರಮಾಣ", "ದರ", "ಮೊತ್ತ", "ಒಟ್ಟು ಮೊತ್ತ", "ನಿಮ್ಮ ವ್ಯಾಪಾರಕ್ಕೆ ಧನ್ಯವಾದಗಳು!"),
            "ಶ್ರೀ ಕೃಷ್ಣ ಜವಳಿ ಮಳಿಗೆ", "ಚಿಕ್ಕಪೇಟೆ, ಬೆಂಗಳೂರು 560053",
            "ಹೆಗಡೆ ಸ್ಟೋರ್ಸ್", "ಸಯ್ಯಾಜಿ ರಾವ್ ರಸ್ತೆ, ಮೈಸೂರು 570001",
            [("ಮೈಸೂರು ರೇಷ್ಮೆ ಸೀರೆ", 1, 6500m), ("ಹತ್ತಿ ಪಂಚೆ", 2, 450m), ("ಇಳಕಲ್ ಸೀರೆ", 1, 2800m)]),
        new("malayalam", "Malayalam",
            new InvoiceLabels("നികുതി ഇൻവോയ്സ്", "ഇൻവോയ്സ് നമ്പർ", "തീയതി", "വിൽപ്പനക്കാരൻ", "വാങ്ങുന്നയാൾ", "വിവരണം", "അളവ്", "നിരക്ക്", "തുക", "ആകെ തുക", "നിങ്ങളുടെ ബിസിനസിന് നന്ദി!"),
            "ശ്രീ കൃഷ്ണ ടെക്സ്റ്റൈൽസ്", "എം. ജി. റോഡ്, കൊച്ചി 682016",
            "മേനോൻ സ്റ്റോഴ്സ്", "സ്റ്റാച്യു, തിരുവനന്തപുരം 695001",
            [("കസവ് മുണ്ട്", 2, 1200m), ("കൈത്തറി സാരി", 1, 3800m), ("പ്രിന്റഡ് തോർത്ത്", 3, 220m)]),
        new("french", "French",
            new InvoiceLabels("Facture", "N° de facture", "Date", "Vendeur", "Client", "Désignation", "Qté", "Prix unitaire", "Montant", "Total TTC", "Merci pour votre confiance !"),
            "Brûlerie Saint-Éloi", "12 rue de la Paix, 69002 Lyon",
            "Café Lumière", "4 place Bellecour, 69002 Lyon",
            [("Café en grains « Moka »", 3, 18.50m), ("Thé vert Sencha", 2, 9.00m), ("Crème de marrons", 1, 6.80m)],
            Currency: "€"),
        new("polish", "Polish",
            new InvoiceLabels("Faktura VAT", "Numer faktury", "Data", "Sprzedawca", "Nabywca", "Nazwa towaru", "Ilość", "Cena", "Wartość", "Razem", "Dziękujemy za współpracę!"),
            "Piekarnia Złoty Kłos", "ul. Piotrkowska 112, 90-006 Łódź",
            "Kawiarnia Pod Różą", "ul. Floriańska 14, 31-021 Kraków",
            [("Chleb żytni na zakwasie", 4, 12.50m), ("Pączki z różą", 12, 4.20m), ("Sernik krakowski", 1, 48.00m)],
            Currency: "zł"),
        new("czech", "Czech",
            new InvoiceLabels("Faktura", "Číslo faktury", "Datum", "Dodavatel", "Odběratel", "Popis", "Množství", "Cena", "Částka", "Celkem", "Děkujeme za spolupráci!"),
            "Pekárna U Zlatého klasu", "Náměstí Míru 12, 120 00 Praha 2",
            "Kavárna Šťastná hvězda", "Masarykova 8, 602 00 Brno",
            [("Žitný chléb", 4, 45.00m), ("Koláče s mákem", 12, 18.00m), ("Medovník", 1, 320.00m)],
            Currency: "Kč"),
        new("turkish", "Turkish",
            new InvoiceLabels("Fatura", "Fatura numarası", "Tarih", "Satıcı", "Alıcı", "Açıklama", "Miktar", "Birim fiyat", "Tutar", "Genel toplam", "Bizi tercih ettiğiniz için teşekkür ederiz!"),
            "Işık Tekstil Ltd. Şti.", "İstiklal Caddesi 45, 34433 İstanbul",
            "Güneş Mağazaları", "Atatürk Bulvarı 12, 06420 Ankara",
            [("Pamuklu havlu", 6, 85.00m), ("İpek eşarp", 2, 450.00m), ("Yün battaniye", 1, 1250.00m)],
            Currency: "₺"),
        new("filipino", "Filipino",
            new InvoiceLabels("Kuwenta", "Numero ng kuwenta", "Petsa", "Nagbebenta", "Mamimili", "Paglalarawan", "Dami", "Presyo", "Halaga", "Kabuuan", "Salamat sa inyong pagtangkilik!"),
            "Panaderya ng Sto. Niño", "Kalye Mabini 21, Lungsod ng Cebu 6000",
            "Kapihan sa Bayan", "Abenida Rizal 88, Maynila 1003",
            [("Pandesal", 30, 5.00m), ("Ensaymada", 6, 45.00m), ("Bibingka", 2, 120.00m)],
            Currency: "₱", SignFirst: true),
        new("greek", "Greek",
            new InvoiceLabels("Τιμολόγιο", "Αριθμός τιμολογίου", "Ημερομηνία", "Πωλητής", "Αγοραστής", "Περιγραφή", "Ποσότητα", "Τιμή μονάδας", "Ποσό", "Σύνολο", "Ευχαριστούμε για την προτίμησή σας!"),
            "Ελαιουργείο Ήλιος", "Ερμού 25, 105 63 Αθήνα",
            "Παντοπωλείο Το Λιμάνι", "Ακτή Μιαούλη 8, 185 38 Πειραιάς",
            [("Ελαιόλαδο παρθένο", 3, 12.50m), ("Μέλι θυμαρίσιο", 2, 9.00m), ("Φέτα ΠΟΠ", 1, 7.80m)],
            Currency: "€"),
        new("russian", "Russian",
            new InvoiceLabels("Счёт-фактура", "Номер счёта", "Дата", "Продавец", "Покупатель", "Наименование", "Кол-во", "Цена", "Сумма", "Итого", "Благодарим за сотрудничество!"),
            "ООО «Северный хлеб»", "ул. Тверская, 12, Москва, 125009",
            "Кафе «Белые ночи»", "Невский пр., 28, Санкт-Петербург, 191186",
            [("Хлеб ржаной", 10, 65.00m), ("Пряники тульские", 5, 180.00m), ("Мёд липовый", 2, 450.00m)],
            Currency: "₽"),
        new("ukrainian", "Ukrainian",
            new InvoiceLabels("Рахунок-фактура", "Номер рахунку", "Дата", "Продавець", "Покупець", "Найменування", "Кількість", "Ціна", "Сума", "Разом", "Дякуємо за співпрацю!"),
            "ТОВ «Київська пекарня»", "вул. Хрещатик, 22, Київ, 01001",
            "Кав'ярня «Їжачок»", "вул. Дерибасівська, 5, Одеса, 65026",
            [("Паляниця", 8, 38.00m), ("Вареники з вишнею", 4, 95.00m), ("Узвар", 6, 42.00m)],
            Currency: "₴"),
        new("armenian", "Armenian",
            new InvoiceLabels("Հաշիվ-ապրանքագիր", "Հաշվի համար", "Ամսաթիվ", "Վաճառող", "Գնորդ", "Անվանում", "Քանակ", "Գին", "Գումար", "Ընդամենը", "Շնորհակալություն համագործակցության համար։"),
            "«Արարատ» ՍՊԸ", "Աբովյան փողոց 10, Երևան 0001",
            "«Գյումրի» սրճարան", "Վարդանանց փողոց 5, Գյումրի 3101",
            [("Ծիրանի չիր", 5, 2500m), ("Լավաշ", 20, 300m), ("Հայկական կոնյակ", 1, 15000m)],
            Currency: "֏"),
        new("georgian", "Georgian",
            new InvoiceLabels("ანგარიშ-ფაქტურა", "ანგარიშის ნომერი", "თარიღი", "გამყიდველი", "მყიდველი", "დასახელება", "რაოდენობა", "ფასი", "თანხა", "სულ", "გმადლობთ თანამშრომლობისთვის!"),
            "შპს „ქართული პური“", "რუსთაველის გამზ. 15, თბილისი 0108",
            "კაფე „ბათუმი“", "გორგილაძის ქუჩა 20, ბათუმი 6000",
            [("შოთის პური", 10, 1.50m), ("ჩურჩხელა", 6, 4.00m), ("ხაჭაპური", 3, 12.00m)],
            Currency: "₾"),
        new("arabic", "Arabic",
            new InvoiceLabels("فاتورة ضريبية", "رقم الفاتورة", "التاريخ", "البائع", "المشتري", "الوصف", "الكمية", "السعر", "المبلغ", "الإجمالي", "شكراً لتعاملكم معنا!"),
            "مؤسسة النخيل للتمور", "شارع الملك فهد، الرياض 12211",
            "مقهى الواحة", "شارع الكورنيش، جدة 23521",
            [("تمر سكري", 5, 45.00m), ("قهوة عربية", 2, 60.00m), ("عسل سدر", 1, 150.00m)],
            Currency: "ر.س", RightToLeft: true),
        new("persian", "Persian",
            new InvoiceLabels("صورتحساب فروش", "شماره صورتحساب", "تاریخ", "فروشنده", "خریدار", "شرح کالا", "تعداد", "قیمت واحد", "مبلغ", "جمع کل", "از خرید شما سپاسگزاریم!"),
            "فروشگاه فرش پارس", "خیابان ولیعصر، تهران ۱۵۹۴۷",
            "کافه گلستان", "خیابان چهارباغ، اصفهان ۸۱۳۳۷",
            [("زعفران ممتاز", 2, 850000m), ("پسته اکبری", 3, 420000m), ("گز اصفهان", 4, 180000m)],
            Currency: "ریال", RightToLeft: true),
        new("urdu", "Urdu",
            new InvoiceLabels("ٹیکس انوائس", "انوائس نمبر", "تاریخ", "فروخت کنندہ", "خریدار", "تفصیل", "مقدار", "نرخ", "رقم", "کل رقم", "آپ کے کاروبار کا شکریہ!"),
            "رحمان کپڑا گھر", "انارکلی بازار، لاہور 54000",
            "صدیقی ٹریڈرز", "صدر، کراچی 74400",
            [("لان کا سوٹ", 2, 3500m), ("ریشمی دوپٹہ", 1, 1800m), ("کاٹن شلوار قمیض", 1, 2600m)],
            Currency: "روپے", RightToLeft: true),
        new("hebrew", "Hebrew",
            new InvoiceLabels("חשבונית מס", "מספר חשבונית", "תאריך", "מוכר", "קונה", "תיאור", "כמות", "מחיר", "סכום", "סה״כ לתשלום", "תודה על שיתוף הפעולה!"),
            "מאפיית הכרמל בע״מ", "רחוב הרצל 12, חיפה 3303112",
            "בית קפה הגפן", "שדרות רוטשילד 40, תל אביב 6688312",
            [("חלה", 10, 18.00m), ("עוגת שמרים", 2, 45.00m), ("בורקס גבינה", 12, 9.50m)],
            Currency: "₪", RightToLeft: true),
        new("dhivehi", "Dhivehi",
            new InvoiceLabels("ބިލު", "ބިލު ނަންބަރު", "ތާރީޚު", "ވިއްކާ ފަރާތް", "ގަންނަ ފަރާތް", "ތަފުސީލު", "ޢަދަދު", "އަގު", "ޖުމްލަ", "ޖުމްލަ އަގު", "ޝުކުރިއްޔާ!"),
            "މާލެ ބޭކަރީ", "މަޖީދީ މަގު، މާލެ 20026",
            "ހިރިޔާ ކެފޭ", "ބޮޑުތަކުރުފާނު މަގު، މާލެ 20077",
            [("ބޯކިބާ", 6, 15.00m), ("ގުލަ", 20, 2.50m), ("ބިސްކޯދު", 3, 25.00m)],
            Currency: "ރ.", RightToLeft: true),
        new("mixed", "Mixed",
            new InvoiceLabels("Tax invoice", "Invoice number", "Date", "Seller", "Buyer", "Description", "Qty", "Rate", "Amount", "Total", "Thank you · ధన్యవాదాలు · धन्यवाद"),
            "Lakshmi Textiles · శ్రీ లక్ష్మి టెక్స్‌టైల్స్", "Besant Road, Vijayawada 520002",
            "Sharma Traders · शर्मा ट्रेडर्स", "M. I. Road, Jaipur 302001",
            [("Cotton saree · కాటన్ చీర", 2, 1500m), ("Silk dupatta · रेशमी दुपट्टा", 1, 1200m)])
    ];

    #endregion
}
