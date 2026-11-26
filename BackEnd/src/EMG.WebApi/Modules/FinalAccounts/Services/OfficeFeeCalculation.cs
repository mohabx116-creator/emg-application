namespace EMG.Api.Modules.FinalAccounts.Services;

internal readonly record struct OfficeFeeCalculation(decimal Fee, decimal Outstanding)
{
    public static OfficeFeeCalculation Calculate(decimal totalOutgoing, decimal percentage, decimal payments)
    {
        var fee = decimal.Round(totalOutgoing * percentage / 100m, 2);
        return new(fee, fee - payments);
    }
}
