using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.ValueGeneration;

namespace Nocturne.Infrastructure.Data.ValueGenerators;

/// <summary>
/// Value generator for creating UUID Version 7 GUIDs using Guid.CreateVersion7()
/// Provides time-ordered, globally unique identifiers with improved database performance
/// </summary>
public class GuidV7ValueGenerator : ValueGenerator<Guid>
{
    /// <summary>
    /// Gets a value indicating whether this generator creates stable values across saves
    /// </summary>
    public override bool GeneratesTemporaryValues => false;

    /// <summary>
    /// Gets a value indicating whether the generator can create values for the given property
    /// </summary>
    public override bool GeneratesStableValues => true;

    /// <summary>
    /// Generates a new UUID Version 7 using the current timestamp
    /// </summary>
    /// <param name="entry">The entity entry for which the value is being generated</param>
    /// <returns>A new UUID Version 7 with embedded timestamp for natural ordering</returns>
    public override Guid Next(EntityEntry entry)
    {
        return Guid.CreateVersion7();
    }

    /// <summary>
    /// Generates a new UUID Version 7 using a specific timestamp (for testing scenarios)
    /// </summary>
    /// <param name="timestamp">The timestamp to embed in the UUID</param>
    /// <returns>A new UUID Version 7 with the specified timestamp</returns>
    public virtual Guid NextWithTimestamp(DateTimeOffset timestamp)
    {
        return Guid.CreateVersion7(timestamp);
    }
}