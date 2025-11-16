using Nocturne.Tests.Shared.Attributes;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.Tests.Shared.Infrastructure;

/// <summary>
/// Base class for integration tests that require PostgreSQL containers
/// Uses shared container infrastructure to avoid the container management anti-pattern
/// </summary>
[Collection("SharedTestContainers")]
[Parity]
public abstract class SharedIntegrationTestBase : IAsyncLifetime
{
    protected readonly SharedTestContainerFixture ContainerFixture;
    protected readonly ITestOutputHelper Output;
    protected NpgsqlConnection Database => ContainerFixture.Database;
    protected string PostgreSqlConnectionString => ContainerFixture.PostgreSqlConnectionString;

    protected SharedIntegrationTestBase(
        SharedTestContainerFixture containerFixture,
        ITestOutputHelper output
    )
    {
        ContainerFixture = containerFixture;
        Output = output;
    }

    public virtual async Task InitializeAsync()
    {
        // Clean slate for each test
        await ContainerFixture.CleanupAsync();
    }

    public virtual Task DisposeAsync()
    {
        // Cleanup is handled by the fixture
        return Task.CompletedTask;
    }
}
