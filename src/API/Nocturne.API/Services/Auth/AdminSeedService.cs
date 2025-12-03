using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Background service that seeds the initial admin account on startup
/// The admin account is created from configuration if it doesn't already exist
/// </summary>
public class AdminSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly LocalIdentityOptions _options;
    private readonly ILogger<AdminSeedService> _logger;

    public AdminSeedService(
        IServiceProvider serviceProvider,
        IOptions<LocalIdentityOptions> options,
        ILogger<AdminSeedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _options = options.Value;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (_options.AdminSeed == null)
        {
            _logger.LogDebug("No admin seed configuration found, skipping admin account seeding");
            return;
        }

        if (
            string.IsNullOrEmpty(_options.AdminSeed.Email)
            || string.IsNullOrEmpty(_options.AdminSeed.Password)
        )
        {
            _logger.LogWarning(
                "Admin seed configuration is incomplete (missing email or password), skipping"
            );
            return;
        }

        // Validate password meets minimum length
        if (_options.AdminSeed.Password.Length < _options.Password.MinLength)
        {
            _logger.LogError(
                "Admin seed password does not meet minimum length requirement ({MinLength} characters)",
                _options.Password.MinLength
            );
            return;
        }

        using var scope = _serviceProvider.CreateScope();
        var localIdentityService =
            scope.ServiceProvider.GetRequiredService<ILocalIdentityService>();
        var subjectService = scope.ServiceProvider.GetRequiredService<ISubjectService>();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();

        try
        {
            // Check if admin already exists
            var existingUser = await localIdentityService.GetUserByEmailAsync(
                _options.AdminSeed.Email
            );
            if (existingUser != null)
            {
                _logger.LogDebug(
                    "Admin account already exists for {Email}",
                    _options.AdminSeed.Email
                );
                return;
            }

            _logger.LogInformation("Creating admin account for {Email}", _options.AdminSeed.Email);

            // Register the admin user (bypassing allowlist checks)
            var result = await localIdentityService.RegisterAsync(
                _options.AdminSeed.Email,
                _options.AdminSeed.Password,
                _options.AdminSeed.DisplayName,
                skipAllowlistCheck: true,
                autoVerifyEmail: true
            );

            if (!result.Success)
            {
                _logger.LogError("Failed to create admin account: {Error}", result.Error);
                return;
            }

            // Assign admin role
            if (result.SubjectId.HasValue)
            {
                // Ensure admin role exists
                var adminRole = await roleService.GetRoleByNameAsync("admin");
                if (adminRole == null)
                {
                    _logger.LogWarning("Admin role does not exist, creating it");
                    adminRole = await roleService.CreateRoleAsync(
                        new Role
                        {
                            Name = "admin",
                            Description = "Full administrative access",
                            Permissions = new List<string> { "*" },
                        }
                    );
                }

                await subjectService.AssignRoleAsync(result.SubjectId.Value, "admin");
                _logger.LogInformation("Admin account created and assigned admin role");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error seeding admin account");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
