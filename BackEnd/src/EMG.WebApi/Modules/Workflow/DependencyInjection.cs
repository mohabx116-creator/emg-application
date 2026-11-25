using EMG.Api.Modules.Workflow.Services;
using Microsoft.Extensions.DependencyInjection;

namespace EMG.Api.Modules.Workflow;

public static class DependencyInjection
{
    public static IServiceCollection AddWorkflowModule(this IServiceCollection services)
    {
        services.AddScoped<IWorkflowService, WorkflowService>();
        return services;
    }
}
