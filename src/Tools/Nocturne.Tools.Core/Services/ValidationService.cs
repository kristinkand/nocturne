using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Core.Services;

/// <summary>
/// Implementation of validation services for tools.
/// </summary>
public class ValidationService : IValidationService
{
    private readonly ILogger<ValidationService> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidationService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ValidationService(ILogger<ValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public Abstractions.Services.ValidationResult ValidateObject(object instance)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new ValidationContext(instance);

        var isValid = Validator.TryValidateObject(
            instance,
            context,
            results,
            validateAllProperties: true
        );

        if (isValid)
        {
            _logger.LogDebug("Object validation passed for {Type}", instance.GetType().Name);
            return Abstractions.Services.ValidationResult.Success();
        }

        var errors = results
            .Select(r => new ValidationError(
                r.MemberNames.FirstOrDefault() ?? "Unknown",
                r.ErrorMessage ?? "Unknown error",
                null
            ))
            .ToArray();

        _logger.LogDebug(
            "Object validation failed for {Type} with {ErrorCount} errors",
            instance.GetType().Name,
            errors.Length
        );

        return Abstractions.Services.ValidationResult.Failure(errors);
    }

    #region New Comprehensive Validation Methods (Not Implemented in Core)

    /// <summary>
    /// Validates the database schema
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Schema validation result</returns>
    public virtual async Task<Abstractions.Services.SchemaValidationResult> ValidateSchemaAsync(
        string connectionString,
        Abstractions.Services.ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        throw new NotImplementedException(
            "Schema validation requires SchemaValidationService implementation"
        );
    }

    /// <summary>
    /// Validates data compatibility between MongoDB and PostgreSQL
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string</param>
    /// <param name="mongoDatabaseName">MongoDB database name</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    public virtual async Task<Abstractions.Services.ValidationResult> ValidateDataCompatibilityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        Abstractions.Services.ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        throw new NotImplementedException(
            "Data compatibility validation requires SchemaValidationService implementation"
        );
    }

    /// <summary>
    /// Validates a BSON document
    /// </summary>
    /// <param name="document">BSON document to validate</param>
    /// <param name="collectionName">Collection name</param>
    /// <param name="options">Validation options</param>
    /// <returns>Validation result</returns>
    public virtual Abstractions.Services.ValidationResult ValidateDocument(
        MongoDB.Bson.BsonDocument document,
        string collectionName,
        Abstractions.Services.ValidationOptions? options = null
    )
    {
        throw new NotImplementedException(
            "Document validation requires SchemaValidationService implementation"
        );
    }

    /// <summary>
    /// Detects conflicts between MongoDB and PostgreSQL data
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string</param>
    /// <param name="mongoDatabaseName">MongoDB database name</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    public virtual async Task<Abstractions.Services.ValidationResult> DetectConflictsAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        Abstractions.Services.ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        throw new NotImplementedException(
            "Conflict detection requires SchemaValidationService implementation"
        );
    }

    /// <summary>
    /// Validates referential integrity between databases
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string</param>
    /// <param name="mongoDatabaseName">MongoDB database name</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    public virtual async Task<Abstractions.Services.ValidationResult> ValidateReferentialIntegrityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        Abstractions.Services.ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask;
        throw new NotImplementedException(
            "Referential integrity validation requires SchemaValidationService implementation"
        );
    }

    /// <summary>
    /// Validates JSON structure against expected schema
    /// </summary>
    /// <param name="jsonValue">JSON value to validate</param>
    /// <param name="fieldName">Field name</param>
    /// <param name="expectedSchema">Expected schema</param>
    /// <returns>Validation result</returns>
    public virtual Abstractions.Services.ValidationResult ValidateJsonStructure(
        string? jsonValue,
        string fieldName,
        object? expectedSchema = null
    )
    {
        if (string.IsNullOrWhiteSpace(jsonValue))
        {
            return Abstractions.Services.ValidationResult.Success();
        }

        try
        {
            System.Text.Json.JsonDocument.Parse(jsonValue);
            return Abstractions.Services.ValidationResult.Success();
        }
        catch (System.Text.Json.JsonException ex)
        {
            return Abstractions.Services.ValidationResult.Failure(
                fieldName,
                $"Invalid JSON: {ex.Message}",
                jsonValue
            );
        }
    }

    /// <summary>
    /// Validates identifier against reserved keywords
    /// </summary>
    /// <param name="identifier">Identifier to validate</param>
    /// <param name="identifierType">Type of identifier</param>
    /// <returns>Validation result</returns>
    public virtual Abstractions.Services.ValidationResult ValidateReservedKeywords(
        string identifier,
        string identifierType
    )
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return Abstractions.Services.ValidationResult.Failure(
                identifierType,
                "Identifier cannot be null or empty",
                identifier
            );
        }

        // Basic implementation - more comprehensive checking is in SchemaValidationService
        return Abstractions.Services.ValidationResult.Success();
    }

    /// <summary>
    /// Validates date format and range
    /// </summary>
    /// <param name="dateValue">Date value to validate</param>
    /// <param name="fieldName">Field name</param>
    /// <param name="allowedRange">Allowed date range</param>
    /// <returns>Validation result</returns>
    public virtual Abstractions.Services.ValidationResult ValidateDateFormat(
        object? dateValue,
        string fieldName,
        (DateTime? Min, DateTime? Max)? allowedRange = null
    )
    {
        if (dateValue == null)
        {
            return Abstractions.Services.ValidationResult.Success();
        }

        try
        {
            var parsedDate = dateValue switch
            {
                DateTime dt => dt,
                DateTimeOffset dto => dto.DateTime,
                long unixMills => DateTimeOffset.FromUnixTimeMilliseconds(unixMills).DateTime,
                string dateStr => DateTime.Parse(dateStr),
                _ => throw new ArgumentException($"Unsupported date type: {dateValue.GetType()}"),
            };

            if (allowedRange.HasValue)
            {
                var (min, max) = allowedRange.Value;

                if (min.HasValue && parsedDate < min.Value)
                {
                    return Abstractions.Services.ValidationResult.Failure(
                        fieldName,
                        $"Date {parsedDate} is before minimum allowed date {min.Value}",
                        dateValue
                    );
                }

                if (max.HasValue && parsedDate > max.Value)
                {
                    return Abstractions.Services.ValidationResult.Failure(
                        fieldName,
                        $"Date {parsedDate} is after maximum allowed date {max.Value}",
                        dateValue
                    );
                }
            }

            return Abstractions.Services.ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return Abstractions.Services.ValidationResult.Failure(
                fieldName,
                $"Invalid date format: {ex.Message}",
                dateValue
            );
        }
    }

    /// <summary>
    /// Validates type compatibility between BSON and PostgreSQL types
    /// </summary>
    /// <param name="bsonValue">BSON value to validate</param>
    /// <param name="expectedPostgreSqlType">Expected PostgreSQL type</param>
    /// <param name="fieldName">Field name</param>
    /// <returns>Validation result</returns>
    public virtual Abstractions.Services.ValidationResult ValidateTypeCompatibility(
        object? bsonValue,
        string expectedPostgreSqlType,
        string fieldName
    )
    {
        // Basic implementation - more comprehensive checking is in SchemaValidationService
        return Abstractions.Services.ValidationResult.Success();
    }

    #endregion

    /// <inheritdoc/>
    public Abstractions.Services.ValidationResult ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Abstractions.Services.ValidationResult.Failure(
                "ConnectionString",
                "Connection string cannot be null or empty",
                connectionString
            );
        }

        try
        {
            // Basic validation - check if it contains key-value pairs
            var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                return Abstractions.Services.ValidationResult.Failure(
                    "ConnectionString",
                    "Connection string format is invalid",
                    connectionString
                );
            }

            foreach (var part in parts)
            {
                if (!part.Contains('='))
                {
                    return Abstractions.Services.ValidationResult.Failure(
                        "ConnectionString",
                        $"Invalid connection string part: {part}",
                        connectionString
                    );
                }
            }

            _logger.LogDebug("Connection string validation passed");
            return Abstractions.Services.ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection string validation failed");
            return Abstractions.Services.ValidationResult.Failure(
                "ConnectionString",
                $"Connection string validation failed: {ex.Message}",
                connectionString
            );
        }
    }

    /// <inheritdoc/>
    public Abstractions.Services.ValidationResult ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return Abstractions.Services.ValidationResult.Failure(
                "URL",
                "URL cannot be null or empty",
                url
            );
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return Abstractions.Services.ValidationResult.Failure(
                "URL",
                "URL format is invalid",
                url
            );
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return Abstractions.Services.ValidationResult.Failure(
                "URL",
                "URL must use HTTP or HTTPS scheme",
                url
            );
        }

        _logger.LogDebug("URL validation passed for {URL}", url);
        return Abstractions.Services.ValidationResult.Success();
    }

    /// <inheritdoc/>
    public Abstractions.Services.ValidationResult ValidateFilePath(
        string filePath,
        bool mustExist = false
    )
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return Abstractions.Services.ValidationResult.Failure(
                "FilePath",
                "File path cannot be null or empty",
                filePath
            );
        }

        try
        {
            var fullPath = Path.GetFullPath(filePath);

            if (mustExist && !File.Exists(fullPath))
            {
                return Abstractions.Services.ValidationResult.Failure(
                    "FilePath",
                    $"File does not exist: {fullPath}",
                    filePath
                );
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                return Abstractions.Services.ValidationResult.Failure(
                    "FilePath",
                    $"Directory does not exist: {directory}",
                    filePath
                );
            }

            _logger.LogDebug("File path validation passed for {FilePath}", filePath);
            return Abstractions.Services.ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "File path validation failed for {FilePath}", filePath);
            return Abstractions.Services.ValidationResult.Failure(
                "FilePath",
                $"File path validation failed: {ex.Message}",
                filePath
            );
        }
    }
}
