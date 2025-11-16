using System.ComponentModel.DataAnnotations;
using MongoDB.Bson;

namespace Nocturne.Tools.Abstractions.Services;

/// <summary>
/// Represents a validation error.
/// </summary>
public record ValidationError(
    string PropertyName,
    string ErrorMessage,
    object? AttemptedValue = null
);

/// <summary>
/// Represents a conflict that needs resolution during migration.
/// </summary>
public record ValidationConflict(
    string ConflictType,
    string Description,
    object? ConflictingValue = null,
    IReadOnlyList<ConflictResolutionOption>? ResolutionOptions = null
);

/// <summary>
/// Represents an option for resolving a conflict.
/// </summary>
public record ConflictResolutionOption(
    string StrategyName,
    string Description,
    object? ResolvedValue = null
);

/// <summary>
/// Represents the result of a validation operation.
/// </summary>
public record ValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationConflict> Conflicts
)
{
    /// <summary>
    /// Creates a successful validation result.
    /// </summary>
    /// <returns>A successful validation result.</returns>
    public static ValidationResult Success() =>
        new(true, Array.Empty<ValidationError>(), Array.Empty<ValidationConflict>());

    /// <summary>
    /// Creates a failed validation result.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(params ValidationError[] errors) =>
        new(false, errors, Array.Empty<ValidationConflict>());

    /// <summary>
    /// Creates a failed validation result from a single error.
    /// </summary>
    /// <param name="propertyName">The property name.</param>
    /// <param name="errorMessage">The error message.</param>
    /// <param name="attemptedValue">The attempted value.</param>
    /// <returns>A failed validation result.</returns>
    public static ValidationResult Failure(
        string propertyName,
        string errorMessage,
        object? attemptedValue = null
    ) =>
        new(
            false,
            new[] { new ValidationError(propertyName, errorMessage, attemptedValue) },
            Array.Empty<ValidationConflict>()
        );

    /// <summary>
    /// Creates a validation result with conflicts.
    /// </summary>
    /// <param name="conflicts">The validation conflicts.</param>
    /// <returns>A validation result with conflicts.</returns>
    public static ValidationResult WithConflicts(params ValidationConflict[] conflicts) =>
        new(true, Array.Empty<ValidationError>(), conflicts);

    /// <summary>
    /// Creates a validation result with both errors and conflicts.
    /// </summary>
    /// <param name="errors">The validation errors.</param>
    /// <param name="conflicts">The validation conflicts.</param>
    /// <returns>A validation result with errors and conflicts.</returns>
    public static ValidationResult FailureWithConflicts(
        ValidationError[] errors,
        ValidationConflict[] conflicts
    ) => new(false, errors, conflicts);
}

/// <summary>
/// Configuration options for validation operations.
/// </summary>
public record ValidationOptions(
    bool EnableSchemaValidation = true,
    bool EnableDataValidation = true,
    bool EnableConflictDetection = true,
    bool StrictMode = false,
    bool DryRunMode = false,
    int? MaxErrorsPerCollection = null,
    IReadOnlyList<string>? IgnoredTables = null,
    IReadOnlyList<string>? RequiredTables = null
);

/// <summary>
/// Schema validation result for database structures.
/// </summary>
public record SchemaValidationResult(
    bool IsValid,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ValidationConflict> Conflicts,
    IReadOnlyDictionary<string, TableValidationResult> TableResults
)
{
    /// <summary>
    /// Creates a successful schema validation result.
    /// </summary>
    public static SchemaValidationResult Success(
        IReadOnlyDictionary<string, TableValidationResult> tableResults
    ) => new(true, Array.Empty<ValidationError>(), Array.Empty<ValidationConflict>(), tableResults);

    /// <summary>
    /// Creates a failed schema validation result.
    /// </summary>
    public static SchemaValidationResult Failure(
        ValidationError[] errors,
        ValidationConflict[] conflicts,
        IReadOnlyDictionary<string, TableValidationResult> tableResults
    ) => new(false, errors, conflicts, tableResults);
}

/// <summary>
/// Table-specific validation result.
/// </summary>
public record TableValidationResult(
    string TableName,
    bool Exists,
    IReadOnlyList<ValidationError> Errors,
    IReadOnlyList<ColumnValidationResult> ColumnResults,
    IReadOnlyList<IndexValidationResult> IndexResults
);

/// <summary>
/// Column-specific validation result.
/// </summary>
public record ColumnValidationResult(
    string ColumnName,
    bool Exists,
    string? ExpectedType,
    string? ActualType,
    bool IsNullable,
    IReadOnlyList<ValidationError> Errors
);

/// <summary>
/// Index-specific validation result.
/// </summary>
public record IndexValidationResult(
    string IndexName,
    bool Exists,
    IReadOnlyList<string> ExpectedColumns,
    IReadOnlyList<string> ActualColumns,
    IReadOnlyList<ValidationError> Errors
);

/// <summary>
/// Service for validating objects and configurations.
/// </summary>
public interface IValidationService
{
    /// <summary>
    /// Validates an object using data annotations.
    /// </summary>
    /// <param name="instance">The object to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateObject(object instance);

    /// <summary>
    /// Validates a connection string.
    /// </summary>
    /// <param name="connectionString">The connection string to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateConnectionString(string connectionString);

    /// <summary>
    /// Validates a URL.
    /// </summary>
    /// <param name="url">The URL to validate.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateUrl(string url);

    /// <summary>
    /// Validates a file path.
    /// </summary>
    /// <param name="filePath">The file path to validate.</param>
    /// <param name="mustExist">Whether the file must exist.</param>
    /// <returns>The validation result.</returns>
    ValidationResult ValidateFilePath(string filePath, bool mustExist = false);

    /// <summary>
    /// Validates PostgreSQL database schema structure.
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string.</param>
    /// <param name="options">Validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Schema validation result.</returns>
    Task<SchemaValidationResult> ValidateSchemaAsync(
        string connectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates data compatibility between MongoDB and PostgreSQL.
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string.</param>
    /// <param name="mongoDatabaseName">MongoDB database name.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string.</param>
    /// <param name="options">Validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Data validation result.</returns>
    Task<ValidationResult> ValidateDataCompatibilityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates a MongoDB document for PostgreSQL compatibility.
    /// </summary>
    /// <param name="document">MongoDB document to validate.</param>
    /// <param name="collectionName">Source collection name.</param>
    /// <param name="options">Validation options.</param>
    /// <returns>Document validation result.</returns>
    ValidationResult ValidateDocument(
        BsonDocument document,
        string collectionName,
        ValidationOptions? options = null
    );

    /// <summary>
    /// Detects conflicts in data that would prevent successful migration.
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string.</param>
    /// <param name="mongoDatabaseName">MongoDB database name.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string.</param>
    /// <param name="options">Validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Conflict detection result.</returns>
    Task<ValidationResult> DetectConflictsAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates referential integrity between related collections/tables.
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string.</param>
    /// <param name="mongoDatabaseName">MongoDB database name.</param>
    /// <param name="postgresConnectionString">PostgreSQL connection string.</param>
    /// <param name="options">Validation options.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Referential integrity validation result.</returns>
    Task<ValidationResult> ValidateReferentialIntegrityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates JSON structure for JSONB fields.
    /// </summary>
    /// <param name="jsonValue">JSON value to validate.</param>
    /// <param name="fieldName">Field name for error reporting.</param>
    /// <param name="expectedSchema">Expected JSON schema (optional).</param>
    /// <returns>JSON validation result.</returns>
    ValidationResult ValidateJsonStructure(
        string? jsonValue,
        string fieldName,
        object? expectedSchema = null
    );

    /// <summary>
    /// Checks for PostgreSQL reserved keyword conflicts.
    /// </summary>
    /// <param name="identifier">Identifier to check (table name, column name, etc.).</param>
    /// <param name="identifierType">Type of identifier for error reporting.</param>
    /// <returns>Reserved keyword validation result.</returns>
    ValidationResult ValidateReservedKeywords(string identifier, string identifierType);

    /// <summary>
    /// Validates date ranges and formats for migration.
    /// </summary>
    /// <param name="dateValue">Date value to validate.</param>
    /// <param name="fieldName">Field name for error reporting.</param>
    /// <param name="allowedRange">Allowed date range (optional).</param>
    /// <returns>Date validation result.</returns>
    ValidationResult ValidateDateFormat(
        object? dateValue,
        string fieldName,
        (DateTime? Min, DateTime? Max)? allowedRange = null
    );

    /// <summary>
    /// Validates data type compatibility between MongoDB and PostgreSQL types.
    /// </summary>
    /// <param name="bsonValue">MongoDB BSON value.</param>
    /// <param name="expectedPostgreSqlType">Expected PostgreSQL type.</param>
    /// <param name="fieldName">Field name for error reporting.</param>
    /// <returns>Type compatibility validation result.</returns>
    ValidationResult ValidateTypeCompatibility(
        object? bsonValue,
        string expectedPostgreSqlType,
        string fieldName
    );
}
