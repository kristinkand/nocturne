using Nocturne.Core.Contracts;

namespace Nocturne.API.Services.Auth;

/// <summary>
/// Background service that initializes default authorization entities on startup.
/// This includes default roles and the Public system subject.
/// </summary>
public class AuthorizationSeedService : IHostedService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<AuthorizationSeedService> _logger;

    public AuthorizationSeedService(
        IServiceProvider serviceProvider,
        ILogger<AuthorizationSeedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var roleService = scope.ServiceProvider.GetRequiredService<IRoleService>();
        var subjectService = scope.ServiceProvider.GetRequiredService<ISubjectService>();

        try
        {
            // Initialize default roles (admin, readable, public, api, careportal, denied)
            var rolesCreated = await roleService.InitializeDefaultRolesAsync();
            if (rolesCreated > 0)
            {
                _logger.LogInformation("Initialized {Count} default role(s)", rolesCreated);
            }

            // Initialize the Public system subject for unauthenticated access
            var publicSubject = await subjectService.InitializePublicSubjectAsync();
            if (publicSubject != null)
            {
                _logger.LogDebug("Public subject initialized: {SubjectId}", publicSubject.Id);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error initializing authorization defaults");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
