using ClosedXML.Excel;
using EMG.Infrastructure.Db;
using Microsoft.EntityFrameworkCore;

if (args.Length != 2 || !int.TryParse(args[1], out var projectId) || projectId <= 0)
    throw new ArgumentException("Supply a workbook path and a positive project ID.");
var source = args[0];
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__EmgDb")
    ?? throw new InvalidOperationException("ConnectionStrings__EmgDb must be supplied through the environment.");
var options = new DbContextOptionsBuilder<EmgDbContext>().UseNpgsql(connectionString).Options;
await using var db = new EmgDbContext(options);
using var workbook = new XLWorkbook(source);
var project = await db.Projects.SingleAsync(x => x.Id == projectId);
var failures = new List<string>();

static decimal D(IXLRow row, int col) => row.Cell(col).TryGetValue<decimal>(out var value) ? Math.Round(value, 2) : 0m;
static string S(IXLRow row, int col) => row.Cell(col).GetString().Trim();
static IEnumerable<IXLRow> Rows(IXLWorksheet sheet, int header) => sheet.RowsUsed().Where(row => row.RowNumber() > header);
void Check(string label, int sourceCount, decimal sourceTotal, int targetCount, decimal targetTotal)
{
    var ok = sourceCount == targetCount && Math.Round(sourceTotal, 2) == Math.Round(targetTotal, 2);
    Console.WriteLine($"{(ok ? "OK" : "FAIL")}|{label}|source_count={sourceCount}|target_count={targetCount}|source_total={sourceTotal:0.00}|target_total={targetTotal:0.00}");
    if (!ok) failures.Add(label);
}

void RowFail(string label, string detail)
{
    failures.Add(label);
    Console.WriteLine($"FAIL|{label}|{detail}");
}

static bool Same(decimal left, decimal right) => Math.Abs(left - right) <= 0.01m;
static bool SameDate(DateTime left, DateTime right) => left.Date == right.Date;
static DateTime DateCell(IXLRow row, int column)
{
    var cell = row.Cell(column);
    if (cell.TryGetValue<DateTime>(out var value)) return value.Date;
    return DateTime.TryParse(cell.GetString(), out value) ? value.Date : DateTime.UtcNow.Date;
}

var site = workbook.Worksheet("خاص باعمال الموقع");
var clientCell = site.Cell("A5").GetString().Trim();
var expectedClient = clientCell.Length == 0 ? null : clientCell.Split(':').Last().Trim();
var finalSheet = workbook.Worksheets.FirstOrDefault(x => x.Name == "كشف حساب نهائي");
var expectedOfficeShare = finalSheet is not null && finalSheet.Cell("H13").TryGetValue<decimal>(out var officeShare) ? (officeShare <= 1 ? officeShare * 100 : officeShare) : 0m;
if ((project.ClientName ?? string.Empty).Trim() != (expectedClient ?? string.Empty).Trim()) RowFail("بيانات المشروع", $"اسم العميل مختلف | source={expectedClient} | target={project.ClientName}");
if (!Same(project.OfficeSharePercentage, expectedOfficeShare)) RowFail("بيانات المشروع", $"نسبة نصيب المكتب مختلفة | source={expectedOfficeShare} | target={project.OfficeSharePercentage}");
Console.WriteLine($"OK|بيانات المشروع|اسم العميل={project.ClientName}|نسبة المكتب={project.OfficeSharePercentage:0.##}%");
var siteRows = Rows(site, 9).Where(row => !string.IsNullOrWhiteSpace(S(row, 3)) && D(row, 10) != 0).ToList();
var siteTarget = await db.SiteMovements.Where(x => x.ProjectId == projectId).ToListAsync();
Check("أعمال الموقع", siteRows.Count, siteRows.Sum(row => D(row, 10)), siteTarget.Count, siteTarget.Sum(x => x.TotalAmount));
foreach (var row in siteRows)
{
    var key = $"استيراد: {site.Name} صف {row.RowNumber()}";
    var target = siteTarget.SingleOrDefault(x => x.Reference == key);
    if (target is null) RowFail("أعمال الموقع", $"صف {row.RowNumber()} غير موجود في الهدف");
    else if (!Same(target.TotalAmount, D(row, 10)) || target.Description != S(row, 3) || !Same(target.Quantity ?? 0, D(row, 8)) || !Same(target.UnitPrice ?? 0, D(row, 6)) || target.Unit != S(row, 5) || !SameDate(target.MovementDate, DateCell(row, 2)))
        RowFail("أعمال الموقع", $"صف {row.RowNumber()} قيمته مختلفة");
}

var excavation = workbook.Worksheet("اعمال الحفر");
var excavationRows = Rows(excavation, 8).Where(row => D(row, 10) != 0).ToList();
var excavationTarget = await db.ExcavationRecords.Where(x => x.ProjectId == projectId).ToListAsync();
Check("الحفر", excavationRows.Count, excavationRows.Sum(row => D(row, 10)), excavationTarget.Count, excavationTarget.Sum(x => x.TotalAmount));
foreach (var row in excavationRows)
{
    var target = excavationTarget.SingleOrDefault(x => x.Notes?.Contains($"صف {row.RowNumber()}") == true);
    if (target is null) RowFail("الحفر", $"صف {row.RowNumber()} غير موجود في الهدف");
    else if (!Same(target.TotalAmount, D(row, 10)) || !Same(target.CalculatedQuantity, D(row, 8)) || !Same(target.UnitPrice, D(row, 9)) || target.Unit != S(row, 7) || target.Description != (string.IsNullOrWhiteSpace(S(row, 6)) ? "أعمال حفر" : S(row, 6)) || !SameDate(target.ExcavationDate, DateCell(row, 5)))
        RowFail("الحفر", $"صف {row.RowNumber()} قيمته مختلفة | src={D(row, 8)}/{D(row, 9)}/{D(row, 10)}/{S(row, 7)}/{S(row, 6)}/{DateCell(row, 5):yyyy-MM-dd} | dst={target.CalculatedQuantity}/{target.UnitPrice}/{target.TotalAmount}/{target.Unit}/{target.Description}/{target.ExcavationDate:yyyy-MM-dd}");
}

var concrete = workbook.Worksheet("اعمال الخرسانات");
var concreteRows = Rows(concrete, 8).Where(row => (D(row, 21) != 0 && D(row, 17) != 0) || (D(row, 8) != 0 && D(row, 4) != 0)).ToList();
var concreteTarget = await db.ConcreteRecords.Where(x => x.ProjectId == projectId).ToListAsync();
Check("الخرسانات", concreteRows.Count, concreteRows.Sum(row => D(row, 21) != 0 ? D(row, 21) : D(row, 8)), concreteTarget.Count, concreteTarget.Sum(x => x.TotalAmount));
foreach (var row in concreteRows)
{
    var target = concreteTarget.SingleOrDefault(x => x.Notes?.Contains($"صف {row.RowNumber()}") == true);
    if (target is null) RowFail("الخرسانات", $"صف {row.RowNumber()} غير موجود في الهدف");
    var standardConcrete = D(row, 21) != 0 && D(row, 17) != 0;
    var expectedConcreteDescription = standardConcrete ? S(row, 14) : S(row, 3);
    var expectedConcreteQuantity = standardConcrete ? D(row, 17) : D(row, 4);
    var expectedConcreteUnitPrice = standardConcrete ? D(row, 19) : D(row, 7);
    var expectedConcreteTotal = standardConcrete ? D(row, 21) : D(row, 8);
    var expectedConcreteUnit = standardConcrete ? S(row, 15) : string.Empty;
    if (target is not null && (!Same(target.TotalAmount, expectedConcreteTotal) || !Same(target.Quantity, expectedConcreteQuantity) || !Same(target.UnitPrice, expectedConcreteUnitPrice) || target.Unit != expectedConcreteUnit || target.ElementType != expectedConcreteDescription || !SameDate(target.PourDate, DateCell(row, 2))))
        RowFail("الخرسانات", $"صف {row.RowNumber()} قيمته مختلفة");
}

var steelSheets = workbook.Worksheets.Where(x => x.Name.Contains("حديد", StringComparison.OrdinalIgnoreCase)).ToList();
var steelRows = steelSheets.SelectMany(sheet => Rows(sheet, 6).Where(row => D(row, 3) != 0 && D(row, 6) != 0).Select(row => (sheet, row))).ToList();
var steelTarget = await db.SteelRecords.Where(x => x.ProjectId == projectId).ToListAsync();
Check("الحديد", steelRows.Count, steelRows.Sum(item => D(item.row, 6)), steelTarget.Count, steelTarget.Sum(x => x.TotalAmount));
foreach (var item in steelRows)
{
    var steelIndex = steelRows.IndexOf(item);
    var target = steelIndex >= 0 && steelIndex < steelTarget.Count ? steelTarget.OrderBy(x => x.Id).ElementAt(steelIndex) : null;
    if (target is null) RowFail("الحديد", $"صف {item.row.RowNumber()} من {item.sheet.Name} غير موجود في الهدف");
    else if (!Same(target.TotalAmount, D(item.row, 6)) || !Same(target.Quantity, D(item.row, 3)) || !Same(target.UnitPrice, D(item.row, 5)) || target.BarDiameter != S(item.row, 4) || !SameDate(target.OperationDate, DateCell(item.row, 2)))
        RowFail("الحديد", $"صف {item.row.RowNumber()} من {item.sheet.Name} قيمته مختلفة");
}

var finishNames = new[] { ("اعمال المباني", "المباني"), ("اعمال المحاره", "المحارة"), ("اعمال العزل", "العزل"), ("اعمال الكهرباء", "الكهرباء"), ("اعمال السباكه", "السباكة") };
var suppliesTarget = await db.SupplierSupplies.Include(x => x.Supplier).Where(x => x.ProjectId == projectId).ToListAsync();
var paymentsTarget = await db.SupplierPayments.Include(x => x.Supplier).Where(x => x.ProjectId == projectId).ToListAsync();
foreach (var (sheetName, category) in finishNames)
{
    var sheet = workbook.Worksheet(sheetName);
    var rows = Rows(sheet, 9).Where(row => !string.IsNullOrWhiteSpace(S(row, 3)) && D(row, 9) != 0 && D(row, 10) != 0).ToList();
    var target = await db.FinishingRecords.Where(x => x.ProjectId == projectId && x.AccountCategory == category).ToListAsync();
    Check(category, rows.Count, rows.Sum(row => D(row, 10)), target.Count, target.Sum(x => x.TotalAmount));
    foreach (var row in rows)
    {
        var record = target.SingleOrDefault(x => x.Notes?.Contains($"صف {row.RowNumber()}") == true);
        if (record is null) RowFail(category, $"صف {row.RowNumber()} غير موجود في الهدف");
        else if (!Same(record.TotalAmount, D(row, 10)) || !Same(record.Quantity, D(row, 9)) || !Same(record.UnitPrice, D(row, 6)) || record.Unit != S(row, 5) || record.WorkType != S(row, 3) || !SameDate(record.WorkDate, DateCell(row, 2)))
            RowFail(category, $"صف {row.RowNumber()} قيمته مختلفة");
        var supply = suppliesTarget.SingleOrDefault(x => x.Notes?.Contains($"صف {row.RowNumber()}") == true && x.SupplyType == category);
        var supplierName = S(row, 4);
        if (supply is null) RowFail(category, $"صف {row.RowNumber()} توريده غير موجود");
        else if (supply.ItemName != S(row, 3) || supply.Supplier.Name != supplierName || !Same(supply.Quantity, D(row, 9)) || supply.Unit != S(row, 5) || !Same(supply.UnitPrice, D(row, 6)) || !Same(supply.TotalAmount, D(row, 10)) || !SameDate(supply.SupplyDate, DateCell(row, 2)))
            RowFail(category, $"صف {row.RowNumber()} بيانات التوريد مختلفة");
        var paid = D(row, 11);
        if (paid > 0)
        {
            var payment = paymentsTarget.SingleOrDefault(x => x.Notes?.Contains($"صف {row.RowNumber()}") == true);
            if (payment is null) RowFail(category, $"صف {row.RowNumber()} دفعته غير موجودة");
            else if (!Same(payment.Amount, paid) || payment.Supplier.Name != supplierName || !SameDate(payment.PaymentDate, DateCell(row, 2)))
                RowFail(category, $"صف {row.RowNumber()} بيانات الدفع مختلفة");
        }
    }
}

var advancesSheet = workbook.Worksheet("دفعات تحت الحساب");
var categories = 11;
var advanceRows = Rows(advancesSheet, 3).Where(row => D(row, 14) > 0 || Enumerable.Range(0, categories).Sum(i => D(row, i + 2)) != 0).ToList();
var advancesTarget = await db.AdvancePayments.Where(x => x.ProjectId == projectId).ToListAsync();
var advanceIds = advancesTarget.Select(x => x.Id).ToArray();
var allocationsTarget = await db.AdvanceAllocations.Where(x => advanceIds.Contains(x.AdvancePaymentId)).ToListAsync();
Check("دفعات تحت الحساب", advanceRows.Count, advanceRows.Sum(row => D(row, 14) > 0 ? D(row, 14) : Enumerable.Range(0, categories).Sum(i => D(row, i + 2))), advancesTarget.Count, advancesTarget.Sum(x => x.Amount));
Check("توزيعات الدفعات", advanceRows.Sum(row => Enumerable.Range(0, categories).Count(i => D(row, i + 2) != 0)), advanceRows.Sum(row => Enumerable.Range(0, categories).Sum(i => D(row, i + 2))), allocationsTarget.Count, allocationsTarget.Sum(x => x.Amount));
foreach (var row in advanceRows)
{
    var advanceIndex = advanceRows.IndexOf(row);
    var advance = advanceIndex >= 0 && advanceIndex < advancesTarget.Count ? advancesTarget.OrderBy(x => x.Id).ElementAt(advanceIndex) : null;
    var expectedAmount = D(row, 14) > 0 ? D(row, 14) : Enumerable.Range(0, categories).Sum(i => D(row, i + 2));
    if (advance is null) RowFail("دفعات تحت الحساب", $"صف {row.RowNumber()} غير موجود في الهدف");
    else if (!Same(advance.Amount, expectedAmount) || advance.PayerName != S(row, 15)) RowFail("دفعات تحت الحساب", $"صف {row.RowNumber()} قيمته مختلفة");
    if (advance is not null)
    {
        var expected = Enumerable.Range(0, categories).Select((i, index) => (Category: new[] { "الموقع", "العرب", "المقاول", "المحطة", "الحديد", "المباني", "المحارة", "العزل", "الكهرباء", "السباكة", "المكتب" }[index], Amount: D(row, i + 2))).Where(x => x.Amount != 0).ToList();
        var actual = allocationsTarget.Where(x => x.AdvancePaymentId == advance.Id).ToList();
        if (expected.Count != actual.Count || expected.Sum(x => x.Amount) != actual.Sum(x => x.Amount)) RowFail("توزيعات الدفعات", $"صف {row.RowNumber()} توزيعاته مختلفة");
        foreach (var item in expected)
            if (!actual.Any(x => x.AccountCategory == item.Category && Same(x.Amount, item.Amount))) RowFail("توزيعات الدفعات", $"صف {row.RowNumber()} بند {item.Category} مختلف");
    }
}

Console.WriteLine($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}|failures={failures.Count}");
if (failures.Count > 0) Environment.ExitCode = 1;
