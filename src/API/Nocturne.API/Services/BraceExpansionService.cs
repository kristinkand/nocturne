using System.Text.RegularExpressions;
using Nocturne.Core.Contracts;

namespace Nocturne.API.Services;

/// <summary>
/// Implementation of bash-style brace expansion for time pattern matching
/// Replicates the functionality of the legacy JavaScript braces.expand() library
/// </summary>
public class BraceExpansionService : IBraceExpansionService
{
    /// <summary>
    /// Expand bash-style brace patterns into a list of concrete strings
    /// </summary>
    public IEnumerable<string> ExpandBraces(string pattern)
    {
        if (string.IsNullOrEmpty(pattern))
        {
            return new[] { "" };
        }

        // Handle numeric ranges like {14..16} or {00..15}
        var numericRangeRegex = new Regex(@"\{(\d+)\.\.(\d+)\}");
        var match = numericRangeRegex.Match(pattern);

        if (match.Success)
        {
            var startStr = match.Groups[1].Value;
            var endStr = match.Groups[2].Value;

            if (int.TryParse(startStr, out var start) && int.TryParse(endStr, out var end))
            {
                var results = new List<string>();
                var padLength = Math.Max(startStr.Length, endStr.Length);

                for (var i = start; i <= end; i++)
                {
                    var paddedNumber = i.ToString().PadLeft(padLength, '0');
                    var expandedPattern = pattern.Replace(match.Value, paddedNumber);

                    // Recursively expand any remaining braces
                    results.AddRange(ExpandBraces(expandedPattern));
                }

                return results;
            }
        }

        // Handle sequence patterns like {a,b,c}
        var sequenceRegex = new Regex(@"\{([^}]+)\}");
        match = sequenceRegex.Match(pattern);

        if (match.Success)
        {
            var content = match.Groups[1].Value;
            var parts = content.Split(',');
            var results = new List<string>();

            foreach (var part in parts)
            {
                var expandedPattern = pattern.Replace(match.Value, part.Trim());

                // Recursively expand any remaining braces
                results.AddRange(ExpandBraces(expandedPattern));
            }

            return results;
        }

        // No braces found, return the pattern as-is
        return new[] { pattern };
    }

    /// <summary>
    /// Convert expanded patterns into regular expressions suitable for MongoDB queries
    /// </summary>
    public IEnumerable<Regex> PatternsToRegex(
        IEnumerable<string> patterns,
        string? prefix = null,
        string? suffix = null
    )
    {
        return patterns.Select(pattern =>
        {
            var regexPattern = (prefix ?? "") + Regex.Escape(pattern) + (suffix ?? "");
            return new Regex(regexPattern, RegexOptions.Compiled);
        });
    }

    /// <summary>
    /// Prepare MongoDB query patterns for time-based searches
    /// Replicates the legacy prep_patterns middleware functionality
    /// </summary>
    public TimePatternQuery PrepareTimePatterns(
        string? prefix,
        string? regex,
        string fieldName = "dateString"
    )
    {
        var pattern = new List<string>();

        // Initialize a basic prefix and perform bash brace/glob-style expansion
        var expandedPrefix = ExpandBraces(prefix ?? ".*").ToList();

        // If expansion leads to more than one prefix
        if (expandedPrefix.Count > 1)
        {
            // Pre-pend the prefix to the pattern list and wait to expand it as
            // part of the full pattern
            pattern.Add($"^{prefix}");
        }

        // Append any regex parameters
        if (!string.IsNullOrEmpty(regex))
        {
            // Prepend "match any" rule to their rule
            pattern.Add($".*{regex}");
        }

        // Create a single pattern with all inputs considered
        // Expand the pattern using bash/glob style brace expansion to generate
        // an array of patterns.
        var combinedPattern = string.Join("", pattern);
        var expandedPatterns = ExpandBraces(combinedPattern).ToList();

        if (expandedPatterns.Count == 0)
        {
            expandedPatterns = new List<string> { "" };
        }

        // Prepare the MongoDB query
        var result = new TimePatternQuery
        {
            Patterns = expandedPatterns,
            FieldName = fieldName,
            InPatterns = expandedPatterns,
        };

        // If there is a single prefix pattern, MongoDB can optimize this against
        // an indexed field
        if (expandedPrefix.Count == 1)
        {
            result.SingleRegexPattern = $"^{expandedPrefix.First()}";
            result.CanOptimizeWithIndex = true;
        }

        return result;
    }
}
