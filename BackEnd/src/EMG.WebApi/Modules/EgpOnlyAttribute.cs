using System.ComponentModel.DataAnnotations;

namespace EMG.Api.Modules;

[AttributeUsage(AttributeTargets.Parameter | AttributeTargets.Property)]
public sealed class EgpOnlyAttribute() : ValidationAttribute("هذه العملية تدعم الجنيه المصري (EGP) فقط.")
{
    public override bool IsValid(object? value) =>
        value is null || value is string code && string.Equals(code.Trim(), "EGP", StringComparison.OrdinalIgnoreCase);
}
