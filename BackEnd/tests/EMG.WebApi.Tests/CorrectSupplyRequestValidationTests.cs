using EMG.Api.Modules.Workflow.Dtos;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.WebApi.Tests;

public sealed class CorrectSupplyRequestValidationTests
{
    [Theory]
    [InlineData(null, 0)]
    [InlineData(nameof(CorrectSupplyRequest.SupplyType), 101)]
    [InlineData(nameof(CorrectSupplyRequest.ItemName), 301)]
    [InlineData(nameof(CorrectSupplyRequest.Unit), 81)]
    [InlineData(nameof(CorrectSupplyRequest.InvoiceNumber), 121)]
    public void MvcValidatesTheRecordWithoutThrowing(string? field, int length)
    {
        var valid = new CorrectSupplyRequest("materials", "corrected delivery", "unit", 2m, 50m, null);
        var overLimit = new string('x', length);
        var request = field switch
        {
            nameof(CorrectSupplyRequest.SupplyType) => valid with { SupplyType = overLimit },
            nameof(CorrectSupplyRequest.ItemName) => valid with { ItemName = overLimit },
            nameof(CorrectSupplyRequest.Unit) => valid with { Unit = overLimit },
            nameof(CorrectSupplyRequest.InvoiceNumber) => valid with { InvoiceNumber = overLimit },
            _ => valid
        };

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddControllers();
        using var provider = services.BuildServiceProvider();
        var context = new ActionContext(new DefaultHttpContext { RequestServices = provider }, new RouteData(), new ActionDescriptor());
        provider.GetRequiredService<IObjectModelValidator>().Validate(context, null, string.Empty, request);

        if (field is null)
            Assert.True(context.ModelState.IsValid);
        else
        {
            Assert.False(context.ModelState.IsValid);
            Assert.Single(context.ModelState[field]!.Errors);
        }
    }
}
