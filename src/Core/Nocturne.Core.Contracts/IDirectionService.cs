using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service interface for direction and delta calculations with 1:1 legacy JavaScript compatibility
/// Implements the exact algorithms from ClientApp/lib/plugins/direction.js and ClientApp/lib/plugins/bgnow.js
/// </summary>
public interface IDirectionService
{
    /// <summary>
    /// Get direction information for display - exact legacy algorithm
    /// </summary>
    /// <param name="entry">SGV entry with direction</param>
    /// <returns>Direction info with label and entity</returns>
    DirectionInfo GetDirectionInfo(Entry? entry);

    /// <summary>
    /// Calculate glucose delta between current and previous readings - exact legacy algorithm
    /// </summary>
    /// <param name="entries">List of SGV entries</param>
    /// <param name="units">Units (mg/dl or mmol)</param>
    /// <returns>Delta calculation result</returns>
    DeltaInfo? CalculateDelta(IList<Entry> entries, string units);

    /// <summary>
    /// Calculate direction from slope and delta values
    /// </summary>
    /// <param name="current">Current glucose value</param>
    /// <param name="previous">Previous glucose value</param>
    /// <param name="deltaMinutes">Time difference in minutes</param>
    /// <returns>Calculated direction</returns>
    Direction CalculateDirection(double current, double previous, double deltaMinutes);

    /// <summary>
    /// Get direction character mapping - exact legacy mapping
    /// </summary>
    /// <param name="direction">Direction enum value</param>
    /// <returns>Unicode character for direction</returns>
    string DirectionToChar(Direction direction);

    /// <summary>
    /// Convert character to HTML entity - exact legacy method
    /// </summary>
    /// <param name="character">Unicode character</param>
    /// <returns>HTML entity representation</returns>
    string CharToEntity(string character);
}
