using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.API.Tests.Integration.Infrastructure;
using Nocturne.API.Tests.Integration.Infrastructure;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Base class for integration tests providing common setup and utilities
/// </summary>
[Collection("Integration")]
[Parity]
public abstract class IntegrationTestBase
    : IClassFixture<CustomWebApplicationFactory>,
        IAsyncLifetime
{
    protected readonly CustomWebApplicationFactory Factory;
    protected readonly TestDatabaseFixture DatabaseFixture;
    protected readonly Xunit.Abstractions.ITestOutputHelper Output;
    protected readonly List<HubConnection> HubConnections = new();

    protected IntegrationTestBase(
        CustomWebApplicationFactory factory,
        Xunit.Abstractions.ITestOutputHelper output
    )
    {
        Factory = factory;
        DatabaseFixture = factory.DatabaseFixture;
        Output = output;
    }

    public virtual async Task InitializeAsync()
    {
        using var _ = TestPerformanceTracker.MeasureTest($"{GetType().Name}.Initialize");

        await Factory.InitializeAsync();
        await DatabaseFixture.CleanupAsync(); // Clean slate for each test
    }

    public virtual async Task DisposeAsync()
    {
        using var _ = TestPerformanceTracker.MeasureTest($"{GetType().Name}.Dispose");

        // Clean up SignalR connections
        foreach (var connection in HubConnections)
        {
            if (connection.State == HubConnectionState.Connected)
            {
                await connection.StopAsync();
            }
            await connection.DisposeAsync();
        }
        HubConnections.Clear();

        // Clean up test data
        await DatabaseFixture.CleanupAsync();
    }

    /// <summary>
    /// Creates a SignalR connection to the Data Hub
    /// </summary>
    protected async Task<HubConnection> CreateDataHubConnectionAsync()
    {
        var client = Factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl(
                $"{client.BaseAddress}hubs/data",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                }
            )
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        HubConnections.Add(connection);
        return connection;
    }

    /// <summary>
    /// Creates a SignalR connection to the Alarm Hub
    /// </summary>
    protected async Task<HubConnection> CreateAlarmHubConnectionAsync()
    {
        var client = Factory.CreateClient();
        var connection = new HubConnectionBuilder()
            .WithUrl(
                $"{client.BaseAddress}hubs/alarms",
                options =>
                {
                    options.HttpMessageHandlerFactory = _ => Factory.Server.CreateHandler();
                }
            )
            .ConfigureLogging(logging =>
            {
                logging.SetMinimumLevel(LogLevel.Warning);
            })
            .Build();

        HubConnections.Add(connection);
        return connection;
    }

    /// <summary>
    /// Authorizes a SignalR connection with test credentials
    /// </summary>
    protected async Task AuthorizeConnectionAsync(HubConnection connection)
    {
        var authData = new
        {
            client = "test-client",
            secret = "test-secret-for-integration-tests",
            history = 24,
        };

        try
        {
            await connection.InvokeAsync("Authorize", authData);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Authorization failed (expected in some tests): {ex.Message}");
        }
    }

    /// <summary>
    /// Subscribes to storage collections on a SignalR connection
    /// </summary>
    protected async Task SubscribeToCollectionsAsync(HubConnection connection, string[] collections)
    {
        var subscribeData = new { collections };

        try
        {
            await connection.InvokeAsync("Subscribe", subscribeData);
        }
        catch (Exception ex)
        {
            Output.WriteLine($"Subscription failed (expected in some tests): {ex.Message}");
        }
    }

    /// <summary>
    /// Gets a service from the test application's DI container
    /// </summary>
    protected T GetService<T>()
        where T : notnull
    {
        return Factory.Services.GetRequiredService<T>();
    }

    /// <summary>
    /// Creates a service scope for dependency injection
    /// </summary>
    protected IServiceScope CreateServiceScope()
    {
        return Factory.Services.CreateScope();
    }

    /// <summary>
    /// Waits for SignalR events with timeout
    /// </summary>
    protected async Task<bool> WaitForEventAsync(Func<bool> condition, TimeSpan? timeout = null)
    {
        timeout ??= TimeSpan.FromSeconds(5);
        var start = DateTime.UtcNow;

        while (DateTime.UtcNow - start < timeout)
        {
            if (condition())
                return true;

            await Task.Delay(100);
        }

        return false;
    }
}
