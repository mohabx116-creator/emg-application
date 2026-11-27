using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;

namespace EMG.Api.Modules.Imports.Services;

internal static class WorkbookSourceCurrency
{
    private const string Unknown = "UNKNOWN";
    private static readonly HashSet<string> CurrencyCodes = CultureInfo.GetCultures(CultureTypes.SpecificCultures)
        .Select(culture => new RegionInfo(culture.Name).ISOCurrencySymbol).ToHashSet(StringComparer.Ordinal);

    // Longer phrases precede the generic names that occur inside them.
    private static readonly (string Label, string Code)[] Labels =
    [
        ("جنيه استرليني", "GBP"), ("جنيه سوداني", "SDG"), ("جنيه مصري", "EGP"),
        ("دولار امريكي", "USD"), ("دولار كندي", "CAD"), ("دولار استرالي", "AUD"),
        ("درهم اماراتي", "AED"), ("درهم مغربي", "MAD"), ("ريال سعودي", "SAR"), ("ريال قطري", "QAR"),
        ("ريال عماني", "OMR"), ("ريال يمني", "YER"), ("فرنك سويسري", "CHF"), ("ين ياباني", "JPY"),
        ("يوان صيني", "CNY"), ("ج.م.", "EGP"), ("ج.م", "EGP"), ("د.ا", "AED"),
        ("استرليني", "GBP"), ("جنيه", "EGP"), ("دولار", "USD"), ("يورو", "EUR"),
        ("درهم", Unknown), ("ريال", Unknown), ("دينار", Unknown), ("ليرة", Unknown), ("فرنك", Unknown),
        ("روبيه", Unknown), ("روبية", Unknown), ("يوان", Unknown), ("روبل", Unknown),
        ("€", "EUR"), ("£", "GBP"), ("$", "USD")
    ];

    public static string ParseExplicit(string value)
    {
        var text = Normalize(value);
        if (text.Length == 3 && text.All(char.IsAsciiLetter)) return text.ToUpperInvariant();
        foreach (var (label, code) in Labels)
            if (text == label) return code;
        return Unknown;
    }

    public static string Resolve(IXLCell? currencyCell, IXLCell? amountCell, params string[] headings) =>
        ResolveWithDeclarations([], currencyCell, amountCell, headings);

    public static string ResolveWithDeclarations(IReadOnlyCollection<string> declarations, IXLCell? currencyCell,
        IXLCell? amountCell, params string[] headings)
    {
        var signals = new HashSet<string>(declarations, StringComparer.Ordinal);
        if (currencyCell is not null)
            signals.Add(!currencyCell.HasFormula && currencyCell.DataType == XLDataType.Text
                ? ParseExplicit(currencyCell.GetString()) : Unknown);

        foreach (var heading in headings)
            AddSignals(heading, signals, isFormat: false);

        var numberFormat = amountCell?.Style.NumberFormat;
        var format = numberFormat?.Format ?? string.Empty;
        // In [$symbol-locale], the first dollar sign is syntax. A locale-only directive supplies no currency.
        format = Regex.Replace(format, @"\[\$(?<symbol>[^\]\-]*)(?:-[^\]]*)?\]", match =>
        {
            var symbol = match.Groups["symbol"].Value.Trim();
            if (symbol.Length > 0) signals.Add(ParseExplicit(symbol));
            return " ";
        });
        AddSignals(Regex.Replace(format, @"\[[^\]]*\]", " "), signals, isFormat: true);
        if (signals.Count == 1) return signals.Single();
        // These built-in formats select a locale-dependent currency symbol.
        // They need another source field to identify the denomination.
        return signals.Count > 1 || numberFormat?.NumberFormatId is >= 5 and <= 8 or 42 or 44 ? Unknown : "EGP";
    }

    private static void AddSignals(string input, HashSet<string> signals, bool isFormat)
    {
        var text = Normalize(input);
        if (text.Length == 0) return;
        var currencyHeading = isFormat || Regex.IsMatch(text, @"\b(amount|currency|price|rate|total)\b|المبلغ|العملة|السعر|القيمة", RegexOptions.IgnoreCase);
        foreach (Match match in Regex.Matches(text, @"(?<![A-Za-z])[A-Za-z]{3}(?![A-Za-z])"))
        {
            var code = match.Value.ToUpperInvariant();
            if (CurrencyCodes.Contains(code) && (match.Value == code || currencyHeading) ||
                currencyHeading && match.Value == code && code is not ("VAT" or "QTY"))
                signals.Add(code);
        }
        foreach (var (label, code) in Labels)
        {
            if (!text.Contains(label, StringComparison.Ordinal)) continue;
            signals.Add(code);
            text = text.Replace(label, " ", StringComparison.Ordinal);
        }
        if (text.Any(character => char.GetUnicodeCategory(character) == UnicodeCategory.CurrencySymbol))
            signals.Add(Unknown);
    }

    private static string Normalize(string value) => Regex.Replace(value.Trim()
        .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ى', 'ي').Replace("ـ", "")
        .Replace("جنية", "جنيه"), @"\s+", " ");
}
