using Xunit;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Test collection to ensure tests share the same test infrastructure
/// and run in isolation from each other
/// </summary>
[CollectionDefinition("Integration")]
public class IntegrationTestCollection : ICollectionFixture<TestDatabaseFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
