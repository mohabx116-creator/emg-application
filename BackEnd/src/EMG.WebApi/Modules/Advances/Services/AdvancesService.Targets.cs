using EMG.Api.Modules.Advances.Dtos;
using EMG.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace EMG.Api.Modules.Advances.Services;

public sealed partial class AdvancesService
{
    public async Task<IReadOnlyList<AllocationTarget>?> GetAllocationTargetsAsync(int advanceId, int? userId, bool isAdmin, CancellationToken ct)
    {
        var advance = await GetAccessibleAdvanceAsync(advanceId, userId, isAdmin, ct);
        if (advance is null) return null;
        return advance.Status is "مرحّل" or "معتمد" ? await ReadTargetsAsync(advance, ct) : [];
    }

    private async Task<List<AllocationTarget>> ReadTargetsAsync(AdvancePayment advance, CancellationToken ct)
    {
        var movements = await db.SiteMovements.AsNoTracking().Where(m => m.ProjectId == advance.ProjectId && m.Currency == advance.Currency &&
                (m.Status == "معتمد" || m.Status == "مرحّل") && m.MovementType != "وارد")
            .OrderByDescending(m => m.MovementDate).ThenByDescending(m => m.Id)
            .Select(m => new { m.Id, m.Description, m.TotalAmount, m.FundingSource, m.IsPaid }).ToListAsync(ct);
        // Supplier payments are denominated in EGP. Invoices connect a settlement to the corresponding site cost.
        var payments = await db.SupplierPayments.AsNoTracking().Where(p => advance.Currency == "EGP" && p.ProjectId == advance.ProjectId &&
                (p.Status == "معتمد" || p.Status == "مرحّل"))
            .OrderByDescending(p => p.PaymentDate).ThenByDescending(p => p.Id)
            .Select(p => new { p.Id, p.InvoiceId, p.Amount, p.PaymentSource, Name = p.Supplier.Name }).ToListAsync(ct);
        var invoiceLinks = await db.SupplierInvoices.Where(i => i.ProjectId == advance.ProjectId && i.Currency == advance.Currency && i.SiteMovementId != null)
            .Select(i => new { i.Id, Movement = i.SiteMovementId!.Value }).ToDictionaryAsync(i => i.Movement, i => i.Id, ct);
        var allocated = await db.AdvanceAllocations.Where(a => a.AdvancePayment.ProjectId == advance.ProjectId &&
                (a.SiteMovementId != null || a.SupplierPaymentId != null))
            .GroupBy(a => new { a.SiteMovementId, a.SupplierPaymentId, a.AdvancePayment.Currency })
            .Select(g => new { g.Key, Amount = g.Sum(a => a.Amount) }).ToListAsync(ct);
        var byMovement = allocated.Where(a => a.Key.SiteMovementId.HasValue).ToLookup(a => a.Key.SiteMovementId!.Value);
        var byPayment = allocated.Where(a => a.Key.SupplierPaymentId.HasValue).ToLookup(a => a.Key.SupplierPaymentId!.Value);
        var invoicePayments = payments.Where(p => p.InvoiceId.HasValue).ToLookup(p => p.InvoiceId!.Value);
        var invoiceCapacity = new Dictionary<int, decimal>();
        var targets = new List<AllocationTarget>();
        foreach (var movement in movements)
        {
            var allocations = byMovement[movement.Id];
            var remaining = movement.TotalAmount - allocations.Sum(a => a.Amount);
            var conflictingCurrency = allocations.Any(a => a.Key.Currency != advance.Currency);
            if (invoiceLinks.TryGetValue(movement.Id, out var invoice))
            {
                foreach (var payment in invoicePayments[invoice])
                {
                    if (payment.PaymentSource == "مالك مباشر") remaining -= payment.Amount;
                    var assigned = byPayment[payment.Id];
                    conflictingCurrency |= assigned.Any(a => a.Key.Currency != advance.Currency);
                    remaining -= assigned.Sum(a => a.Amount);
                }
                invoiceCapacity[invoice] = conflictingCurrency ? 0 : remaining;
            }
            else if (movement.FundingSource == "مالك مباشر" || movement.IsPaid && movement.FundingSource == "المالك")
                continue;
            // An existing allocation in another currency cannot establish a remaining amount in this currency.
            if (!conflictingCurrency && remaining > 0)
                targets.Add(new("movement", movement.Id, movement.Description, advance.Currency, movement.TotalAmount, remaining));
        }
        foreach (var payment in payments.Where(p => p.PaymentSource != "مالك مباشر"))
        {
            var allocations = byPayment[payment.Id];
            if (allocations.Any(a => a.Key.Currency != advance.Currency)) continue;
            var remaining = payment.Amount - allocations.Sum(a => a.Amount);
            if (payment.InvoiceId is int invoice && invoiceCapacity.TryGetValue(invoice, out var room))
                remaining = Math.Min(remaining, room);
            if (remaining > 0) targets.Add(new("supplier", payment.Id, payment.Name, advance.Currency, payment.Amount, remaining));
        }
        return targets;
    }
}