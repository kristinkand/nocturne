using System.Text.RegularExpressions;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service for handling bash-style brace expansion for time pattern matching
/// Provides 1:1 compatibility with the legacy JavaScript braces.expand() functionality
/// </summary>
public interface IBraceExpansionService
{
    /// <summary>
    /// Expand bash-style brace patterns into a list of concrete strings
    /// Examples:
    /// - "20{14..15}" -> ["2014", "2015"]
    /// - "T{13..18}:{00..15}" -> ["T13:00", "T13:01", ..., "T18:15"]
    /// </summary>
    /// <param name="pattern">The pattern with brace expansions</param>
    /// <returns>List of expanded pattern strings</returns>
    IEnumerable<string> ExpandBraces(string pattern);

    /// <summary>
    /// Convert expanded patterns into regular expressions suitable for MongoDB queries
    /// </summary>
    /// <param name="patterns">List of expanded patterns</param>
    /// <param name="prefix">Optional prefix to prepend to each pattern</param>
    /// <param name="suffix">Optional suffix to append to each pattern</param>
    /// <returns>List of regular expressions</returns>
    IEnumerable<Regex> PatternsToRegex(
        IEnumerable<string> patterns,
        string? prefix = null,
        string? suffix = null
    );

    /// <summary>
    /// Prepare MongoDB query patterns for time-based searches
    /// </summary>
    /// <param name="prefix">The prefix parameter from the route (e.g., "2015-04", "20{14..15}")</param>
    /// <param name="regex">The regex parameter from the route (e.g., "T{13..18}:{00..15}")</param>
    /// <param name="fieldName">The field name to query against (default "dateString")</param>
    /// <returns>MongoDB query patterns</returns>
    TimePatternQuery PrepareTimePatterns(
        string? prefix,
        string? regex,
        string fieldName = "dateString"
    );
}

/// <summary>
/// Result of time pattern preparation for MongoDB queries
/// </summary>
public class TimePatternQuery
{
    /// <summary>
    /// List of expanded patterns for debugging/echo
    /// </summary>
    public IEnumerable<string> Patterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Field name being queried
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// MongoDB query filter for $in operation
    /// </summary>
    public IEnumerable<string> InPatterns { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Single regex pattern for optimization when there's only one prefix
    /// </summary>
    public string? SingleRegexPattern { get; set; }

    /// <summary>
    /// Indicates if MongoDB can optimize this query with an index
    /// </summary>
    public bool CanOptimizeWithIndex { get; set; }
}
