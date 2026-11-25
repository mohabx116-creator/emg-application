using Microsoft.Extensions.DependencyInjection;
using EMG.Domain.Security;
using EMG.Api.Modules.Security.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using EMG.Api.Modules.Security.Authorization;
using EMG.Api.Modules.Security.Authentication;

namespace EMG.Api.Modules.Security;

public static class DependencyInjection
{
    public static IServiceCollection AddSecurityModule(this IServiceCollection services)
    {
        services.AddScoped<IPasswordHasher<AppUser>, PasswordHasher<AppUser>>();
        services.AddScoped<IAccountService, AccountService>();
        services.AddScoped<IAuthSessionService, AuthSessionService>();
        services.AddSingleton<IPasswordRecoveryDelivery, UnavailablePasswordRecoveryDelivery>();
        services.AddScoped<IUsersService, UsersService>();
        services.AddScoped<IProfileService, ProfileService>();
        services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
        services.AddScoped<IAuthorizationHandler, PermissionAuthorizationHandler>();
        return services;
    }
}
