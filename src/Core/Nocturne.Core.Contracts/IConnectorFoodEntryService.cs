using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service interface for connector food entry imports.
/// </summary>
public interface IConnectorFoodEntryService
{
    /// <summary>
    /// Import connector food entries, deduplicating foods and entries as needed.
    /// </summary>
    Task<IReadOnlyList<ConnectorFoodEntry>> ImportAsync(
        IEnumerable<ConnectorFoodEntryImport> imports,
        CancellationToken cancellationToken = default
    );
}
