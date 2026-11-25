using EMG.Api.Modules.Intelligence.Dtos;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Intelligence.Services;

public sealed class EntryIntelligenceService(EmgDbContext db) : IEntryIntelligenceService
{
    private static readonly (string Category, string[] Terms)[] Categories =
    [
        ("الخرسانات", ["خرسان", "خرسانة", "خرسا", "صب", "بامب", "مسلحه", "مسلحة", "عاديه", "عادية", "مكعب", "م3", "اجهاد", "محتوي"]),
        ("الحفر", ["حفر", "ردم", "لودر", "نقلة رمل", "نقلات", "عربات", "تشوين", "تسوية", "تكسير", "ترحيل"]),
        ("الحديد", ["حديد", "تسليح", "كانات", "شبك", "سيخ", "طن حديد", "توريد الحديد", "رباط", "سلك رباط"]),
        ("المباني", ["طوب", "مباني", "مباني", "بلوك", "قوايم", "كانات مباني", "مصمت", "مفرغ"]),
        ("المحارة", ["محارة", "لياسة", "بياض", "طرطشة", "مونة", "بؤج", "أوتار"]),
        ("العزل", ["عزل", "بيتومين", "سيلر", "هارد توب", "فايبر", "برايمر", "رولات عزل"]),
        ("الكهرباء", ["كهرباء", "كابل", "لوحة", "لمبة", "سلك", "مفتاح", "كونتاكتور", "كشاف"]),
        ("السباكة", ["سباكة", "ماسورة", "محبس", "صرف", "خلاط", "كوع", "تيه", "سيفون"]),
        ("أعمال الموقع", ["عامل", "عمال", "معدات", "خلاطة", "ونش", "نقل", "أسمنت", "رمل", "زلط", "شكارة", "بنزين", "أجرة", "اكرامية", "مسمار", "خرطوم", "مصاريف"])
    ];

    private static readonly string[] SupplyTerms = ["توريد", "ورد", "خامة", "مادة", "شكارة", "طن", "كيس", "متر", "كمية"];
    private static readonly string[] PaymentTerms = ["دفعة", "دفع", "تحويل", "تحت الحساب", "سداد", "مدفوع"];
    private static readonly string[] EquipmentTerms = ["لودر", "خلاطة", "ونش", "هزاز", "كمبروسر", "حفار", "معدات", "ساعة تشغيل"];
    private static readonly string[] LabourTerms = ["عامل", "عمال", "يومية", "يوميه", "مصنعية", "صنايعي", "فني", "مقاول"];

    public async Task<EntrySuggestionResponse> SuggestAsync(EntrySuggestionRequest request, CancellationToken cancellationToken)
    {
        var description = request.Description?.Trim() ?? string.Empty;
        var normalized = Normalize(description);
        var matches = Categories
            .Select(item => (item.Category, Score: item.Terms.Count(term => normalized.Contains(Normalize(term), StringComparison.Ordinal))))
            .Where(item => item.Score > 0)
            .OrderByDescending(item => item.Score)
            .ToList();

        var selected = matches.FirstOrDefault();
        var category = selected.Category ?? "أعمال الموقع";
        var entryKind = InferEntryKind(normalized);
        var movementType = string.IsNullOrWhiteSpace(request.MovementType) ? InferMovementType(entryKind) : request.MovementType.Trim();
        var unit = string.IsNullOrWhiteSpace(request.Unit) ? InferUnit(normalized) : request.Unit.Trim();
        var confidence = matches.Count == 0 ? 0.25m : selected.Score >= 2 && (matches.Count == 1 || selected.Score > matches[1].Score) ? 0.92m : 0.68m;
        var alternatives = matches.Skip(1).Select(item => item.Category).Distinct(StringComparer.Ordinal).Take(3).ToList();
        var checks = new List<string>();
        if (request.Amount is < 0 || request.Quantity is < 0 || request.UnitPrice is < 0) checks.Add("القيمة أو الكمية أو سعر الوحدة لا يمكن أن تكون سالبة.");
        if (request.Amount is null && (request.Quantity is null || request.UnitPrice is null)) checks.Add("أدخل القيمة مباشرة أو الكمية وسعر الوحدة.");
        if (request.Quantity is not null && request.UnitPrice is not null && request.Amount is not null && decimal.Round(request.Quantity.Value * request.UnitPrice.Value, 2) != decimal.Round(request.Amount.Value, 2))
            checks.Add("القيمة لا تساوي الكمية × سعر الوحدة؛ راجع البيانات قبل الحفظ.");

        string? matchedSupplier = null;
        if (!string.IsNullOrWhiteSpace(request.SupplierName))
        {
            var supplierText = Normalize(request.SupplierName);
            var supplierNames = await db.Suppliers.AsNoTracking()
                .Where(item => item.IsActive)
                .Select(item => item.Name)
                .ToListAsync(cancellationToken);
            matchedSupplier = supplierNames.FirstOrDefault(name => Normalize(name).Contains(supplierText) || supplierText.Contains(Normalize(name)));
            if (matchedSupplier is null) checks.Add("المورد غير موجود بنفس الاسم؛ راجع قبل إنشاء مورد جديد.");
        }

        var needsConfirmation = confidence < 0.85m || alternatives.Count > 0 || checks.Count > 0 || matchedSupplier is null && !string.IsNullOrWhiteSpace(request.SupplierName);
        var explanation = matches.Count == 0
            ? "لم يتم العثور على مؤشر كافٍ؛ تم اقتراح أعمال الموقع كخيار آمن ويجب تأكيده."
            : $"تم الاقتراح بناءً على كلمات مفتاحية من وصف الحركة: {category}.";
        return new EntrySuggestionResponse(movementType, entryKind, category, unit, matchedSupplier, confidence, needsConfirmation, alternatives, checks, explanation);
    }

    private static string InferEntryKind(string text)
    {
        if (PaymentTerms.Any(term => text.Contains(Normalize(term), StringComparison.Ordinal))) return "دفعة/سداد";
        if (LabourTerms.Any(term => text.Contains(Normalize(term), StringComparison.Ordinal))) return "عمالة/مصنعية";
        if (EquipmentTerms.Any(term => text.Contains(Normalize(term), StringComparison.Ordinal))) return "معدات";
        if (SupplyTerms.Any(term => text.Contains(Normalize(term), StringComparison.Ordinal))) return "توريدات";
        return "مصروفات";
    }

    private static string InferMovementType(string entryKind) => entryKind switch
    {
        "توريدات" => "توريدات",
        "عمالة/مصنعية" => "عمالة",
        "معدات" => "معدات",
        _ => "مصروفات"
    };

    private static string InferUnit(string text) =>
        text.Contains("عامل", StringComparison.Ordinal) || text.Contains("عمال", StringComparison.Ordinal) ? "عامل" :
        text.Contains("لودر", StringComparison.Ordinal) || text.Contains("ساعة", StringComparison.Ordinal) ? "ساعة" :
        text.Contains("نقلة", StringComparison.Ordinal) || text.Contains("نقلات", StringComparison.Ordinal) ? "نقلة" :
        text.Contains("طن", StringComparison.Ordinal) || text.Contains("حديد", StringComparison.Ordinal) ? "طن" :
        text.Contains("متر", StringComparison.Ordinal) ? "متر" : string.Empty;

    private static string Normalize(string value) => string.Join(' ', value.Trim().ToLowerInvariant()
        .Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه').Replace('ى', 'ي')
        .Replace('ـ', ' ').Replace(':', ' ').Replace('-', ' ').Replace('_', ' ').Replace('/', ' ')
        .Split(' ', StringSplitOptions.RemoveEmptyEntries));
}
