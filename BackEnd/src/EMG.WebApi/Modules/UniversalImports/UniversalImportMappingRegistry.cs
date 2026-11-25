namespace EMG.Api.Modules.UniversalImports;

public sealed class UniversalImportMappingRegistry
{
    public const string Version = "universal-v1.1";

    public IReadOnlyList<UniversalMappingDefinition> Definitions { get; } =
    [
        new("F01_SITE_EXPENSE", Version, "Site expenses", ["site", "expense", "مصروف", "أعمال الموقع"], true),
        new("F02_EXCAVATION", Version, "Excavation", ["excavat", "حف", "ردم"], true),
        new("F03_CONCRETE", Version, "Concrete", ["concrete", "خرسان", "صب"], true),
        new("F04_STEEL_SUPPLY", Version, "Steel supply", ["steel", "حديد", "توريد"], true),
        new("F05_FINISHING", Version, "Finishing", ["finish", "تشطيب", "محار", "مبان", "عزل"], true),
        new("F06_ADVANCE_PARALLEL", Version, "Advances and parallel ledgers", ["advance", "دفعات", "تحت الحساب", "دفع"], false),
        new("F07_FINAL_ACCOUNT", Version, "Final account", ["final", "نهائي", "مستخلص", "حساب"], false),
        new("F08_REFERENCE", Version, "Reference or supporting sheet", ["reference", "مرفق", "تقرير", "ملخص"], false)
    ];

    public UniversalMappingDefinition? Resolve(string sheetName, string evidence)
    {
        var normalized = Normalize($"{sheetName} {evidence}");
        if (normalized.Contains("دفعات تحت الحساب", StringComparison.Ordinal) || normalized.Contains("advance", StringComparison.Ordinal) ||
            normalized.Contains("تمويل المالك", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F06_ADVANCE_PARALLEL");
        if (normalized.Contains("كشف الحساب النهائي", StringComparison.Ordinal) || normalized.Contains("final account", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F07_FINAL_ACCOUNT");
        if (normalized.Contains("مرجع", StringComparison.Ordinal) || normalized.Contains("مرفق", StringComparison.Ordinal) || normalized.Contains("reference", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F08_REFERENCE");
        if (normalized.Contains("حديد", StringComparison.Ordinal) || normalized.Contains("steel", StringComparison.Ordinal) ||
            normalized.Contains("supplier receipts", StringComparison.Ordinal) || normalized.Contains("توريدات الموردين", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F04_STEEL_SUPPLY");
        if (normalized.Contains("خرسان", StringComparison.Ordinal) || normalized.Contains("concrete", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F03_CONCRETE");
        if (normalized.Contains("حفر", StringComparison.Ordinal) || normalized.Contains("excavat", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F02_EXCAVATION");
        if (normalized.Contains("محار", StringComparison.Ordinal) || normalized.Contains("مبان", StringComparison.Ordinal) || normalized.Contains("تشطيب", StringComparison.Ordinal) || normalized.Contains("عزل", StringComparison.Ordinal) || normalized.Contains("finish", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F05_FINISHING");
        if (normalized.Contains("موقع", StringComparison.Ordinal) || normalized.Contains("مصروف", StringComparison.Ordinal) || normalized.Contains("expense", StringComparison.Ordinal) || normalized.Contains("site", StringComparison.Ordinal))
            return Definitions.Single(item => item.FamilyKey == "F01_SITE_EXPENSE");
        return null;
    }

    private static string Normalize(string value) =>
        value.Trim().ToLowerInvariant().Replace('ـ', ' ').Replace('_', ' ').Replace('-', ' ');
}
