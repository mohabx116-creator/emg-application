using System.Text.Json;
using EMG.Domain.Entities;

namespace EMG.Api.Modules.Advances.Services;

// Controls identify the underlying signed components. Only those components carry financial effects.
internal sealed class HistoricalFundingBasis
{
    private readonly HashSet<(int Batch,string Currency)> disputed;
    private readonly HashSet<(int Batch,string Currency,string Sheet,string Cell)> fundingCells = [];

    public HistoricalFundingBasis(IReadOnlyList<HistoricalFundingLine> lines)
    {
        disputed = lines.Where(l => l.Basis == "conflicting_ownership").Select(l => (l.BatchId,l.Currency)).ToHashSet();
        foreach (var control in lines.Where(l => l.Basis is "verified_allocation_total" or "verified_office_supplement"))
        {
            using var proof = JsonDocument.Parse(control.SourceJson);
            foreach (var cell in proof.RootElement.GetProperty("members").EnumerateObject())
                fundingCells.Add((control.BatchId,control.Currency,control.Sheet,cell.Name));
        }
    }

    public bool Disputed(HistoricalFundingLine line) => disputed.Contains((line.BatchId,line.Currency));
    public bool Allocates(HistoricalFundingLine line) => line.Basis == "project_allocation" && !Disputed(line);
    public bool AdditionalFunding(HistoricalFundingLine line) => line.Basis == "signed_schedule_adjustment"
        || Allocates(line) && fundingCells.Contains((line.BatchId,line.Currency,line.Sheet,line.Cell));
}
