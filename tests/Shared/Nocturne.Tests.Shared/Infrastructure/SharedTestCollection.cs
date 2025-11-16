using Xunit;

namespace Nocturne.Tests.Shared.Infrastructure;

/// <summary>
/// Shared test collection to ensure tests across assemblies can share the same test infrastructure
/// and run with shared containers while maintaining isolation from each other
/// </summary>
[CollectionDefinition("SharedTestContainers")]
public class SharedTestCollection : ICollectionFixture<SharedTestContainerFixture>
{
    // This class has no code, and is never created. Its purpose is simply
    // to be the place to apply [CollectionDefinition] and all the
    // ICollectionFixture<> interfaces.
}
