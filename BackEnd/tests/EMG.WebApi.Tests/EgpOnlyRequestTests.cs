using System.Text.Json;
using EMG.Api.Modules.Concrete.Dtos;
using EMG.Api.Modules.Excavation.Dtos;
using EMG.Api.Modules.Finishing.Dtos;
using EMG.Api.Modules.Steel.Dtos;
using EMG.Api.Modules.Suppliers.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.WebApi.Tests;

public sealed class EgpOnlyRequestTests
{
    [Theory]
    [InlineData(typeof(CreateExcavationRequest))]
    [InlineData(typeof(CreateSteelRequest))]
    [InlineData(typeof(CreateConcreteRequest))]
    [InlineData(typeof(CreateFinishingRequest))]
    [InlineData(typeof(CreateSupplyRequest))]
    [InlineData(typeof(CreateInlineSupplierSupplyRequest))]
    [InlineData(typeof(UpdateSupplierInvoiceRequest))]
    [InlineData(typeof(SupplierInvoiceLineRequest))]
    public void CurrencyIsValidatedByMvcBeforePosting(Type requestType)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var validator = provider.GetRequiredService<IObjectModelValidator>();
        foreach (var currency in new string?[] { null, "EGP", "egp", "", "USD", "EUR" })
        {
            var context = new ActionContext(new DefaultHttpContext { RequestServices = provider }, new RouteData(), new ActionDescriptor());
            var json = currency is null ? "{}" : JsonSerializer.Serialize(new { Currency = currency });
            var request = JsonSerializer.Deserialize(json, requestType)!;
            validator.Validate(context, null, string.Empty, request);
            var rejected = context.ModelState.TryGetValue("Currency", out var entry) && entry.Errors.Count > 0;
            Assert.Equal(currency is "" or "USD" or "EUR", rejected);
        }
    }
}
