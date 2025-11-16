using System.Collections.Concurrent;
using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Migration.Data;
using Npgsql;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Comprehensive schema validation service for MongoDB to PostgreSQL migration.
/// </summary>
public class SchemaValidationService : IValidationService
{
    private readonly ILogger<SchemaValidationService> _logger;
    private readonly IServiceProvider _serviceProvider;

    // PostgreSQL reserved keywords (subset of most common ones)
    private static readonly HashSet<string> PostgresReservedKeywords = new(
        StringComparer.OrdinalIgnoreCase
    )
    {
        "select",
        "from",
        "where",
        "order",
        "group",
        "having",
        "insert",
        "update",
        "delete",
        "create",
        "drop",
        "alter",
        "table",
        "column",
        "index",
        "primary",
        "foreign",
        "key",
        "constraint",
        "unique",
        "not",
        "null",
        "default",
        "check",
        "references",
        "and",
        "or",
        "in",
        "like",
        "between",
        "exists",
        "case",
        "when",
        "then",
        "else",
        "end",
        "union",
        "join",
        "inner",
        "left",
        "right",
        "full",
        "outer",
        "on",
        "as",
        "distinct",
        "all",
        "user",
        "role",
        "grant",
        "revoke",
        "commit",
        "rollback",
        "transaction",
        "begin",
    };

    private readonly IDatabaseSchemaIntrospectionService _schemaIntrospectionService;

    public SchemaValidationService(
        ILogger<SchemaValidationService> logger,
        IServiceProvider serviceProvider,
        IDatabaseSchemaIntrospectionService schemaIntrospectionService
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _schemaIntrospectionService =
            schemaIntrospectionService
            ?? throw new ArgumentNullException(nameof(schemaIntrospectionService));
    }

    #region Basic Validation Methods (Inherited)

    public ValidationResult ValidateObject(object instance)
    {
        var results = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
        var context = new System.ComponentModel.DataAnnotations.ValidationContext(instance);

        var isValid = System.ComponentModel.DataAnnotations.Validator.TryValidateObject(
            instance,
            context,
            results,
            validateAllProperties: true
        );

        if (isValid)
        {
            _logger.LogDebug("Object validation passed for {Type}", instance.GetType().Name);
            return ValidationResult.Success();
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

        return ValidationResult.Failure(errors);
    }

    public ValidationResult ValidateConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return ValidationResult.Failure(
                "ConnectionString",
                "Connection string cannot be null or empty",
                connectionString
            );
        }

        try
        {
            // Try parsing as PostgreSQL connection string
            var connStringBuilder = new NpgsqlConnectionStringBuilder(connectionString);

            if (string.IsNullOrWhiteSpace(connStringBuilder.Host))
            {
                return ValidationResult.Failure(
                    "ConnectionString",
                    "Host is required in connection string",
                    connectionString
                );
            }

            if (string.IsNullOrWhiteSpace(connStringBuilder.Database))
            {
                return ValidationResult.Failure(
                    "ConnectionString",
                    "Database is required in connection string",
                    connectionString
                );
            }

            _logger.LogDebug("PostgreSQL connection string validation passed");
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Connection string validation failed");
            return ValidationResult.Failure(
                "ConnectionString",
                $"Invalid PostgreSQL connection string: {ex.Message}",
                connectionString
            );
        }
    }

    public ValidationResult ValidateUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return ValidationResult.Failure("URL", "URL cannot be null or empty", url);
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return ValidationResult.Failure("URL", "URL format is invalid", url);
        }

        if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
        {
            return ValidationResult.Failure("URL", "URL must use HTTP or HTTPS scheme", url);
        }

        _logger.LogDebug("URL validation passed for {URL}", url);
        return ValidationResult.Success();
    }

    public ValidationResult ValidateFilePath(string filePath, bool mustExist = false)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return ValidationResult.Failure(
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
                return ValidationResult.Failure(
                    "FilePath",
                    $"File does not exist: {fullPath}",
                    filePath
                );
            }

            var directory = Path.GetDirectoryName(fullPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                return ValidationResult.Failure(
                    "FilePath",
                    $"Directory does not exist: {directory}",
                    filePath
                );
            }

            _logger.LogDebug("File path validation passed for {FilePath}", filePath);
            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "File path validation failed for {FilePath}", filePath);
            return ValidationResult.Failure(
                "FilePath",
                $"File path validation failed: {ex.Message}",
                filePath
            );
        }
    }

    #endregion

    #region Comprehensive Schema and Data Validation

    public async Task<SchemaValidationResult> ValidateSchemaAsync(
        string connectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ValidationOptions();
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();
        var tableResults = new Dictionary<string, TableValidationResult>();

        _logger.LogInformation("Starting PostgreSQL schema validation");

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Discover actual schema from database
            var discoveredSchema = await _schemaIntrospectionService.DiscoverAllTablesAsync(
                connectionString,
                cancellationToken
            );
            var tablesToValidate =
                options.RequiredTables?.ToList() ?? discoveredSchema.Keys.ToList();

            foreach (var tableName in tablesToValidate)
            {
                if (options.IgnoredTables?.Contains(tableName) == true)
                    continue;

                var tableResult = await ValidateTableAsync(
                    connection,
                    tableName,
                    connectionString,
                    cancellationToken
                );
                tableResults[tableName] = tableResult;

                errors.AddRange(tableResult.Errors);
            }

            _logger.LogInformation(
                "Schema validation completed. {ErrorCount} errors found",
                errors.Count
            );

            return errors.Count == 0
                ? SchemaValidationResult.Success(tableResults)
                : SchemaValidationResult.Failure(
                    errors.ToArray(),
                    conflicts.ToArray(),
                    tableResults
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Schema validation failed: {Error}", ex.Message);
            errors.Add(
                new ValidationError(
                    "Schema",
                    $"Schema validation failed: {ex.Message}",
                    connectionString
                )
            );
            return SchemaValidationResult.Failure(
                errors.ToArray(),
                conflicts.ToArray(),
                tableResults
            );
        }
    }

    public async Task<ValidationResult> ValidateDataCompatibilityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ValidationOptions();
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();

        _logger.LogInformation(
            "Starting data compatibility validation between MongoDB and PostgreSQL"
        );

        try
        {
            var mongoClient = new MongoClient(mongoConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

            // Get collections to validate
            var collections = await mongoDatabase.ListCollectionNamesAsync(
                cancellationToken: cancellationToken
            );
            var collectionList = await collections.ToListAsync(cancellationToken);

            // Discover supported collections from PostgreSQL schema
            var discoveredSchema = await _schemaIntrospectionService.DiscoverAllTablesAsync(
                postgresConnectionString,
                cancellationToken
            );
            var supportedCollections = discoveredSchema.Keys.ToHashSet();
            var collectionsToValidate = collectionList
                .Where(c => supportedCollections.Contains(c))
                .ToList();

            foreach (var collectionName in collectionsToValidate)
            {
                if (options.IgnoredTables?.Contains(collectionName) == true)
                    continue;

                var collectionResult = await ValidateCollectionDataAsync(
                    mongoDatabase,
                    collectionName,
                    options,
                    cancellationToken
                );
                errors.AddRange(collectionResult.Errors);
                conflicts.AddRange(collectionResult.Conflicts);

                if (
                    options.MaxErrorsPerCollection.HasValue
                    && errors.Count >= options.MaxErrorsPerCollection.Value
                )
                {
                    _logger.LogWarning(
                        "Maximum errors per collection reached ({MaxErrors}), stopping validation for {Collection}",
                        options.MaxErrorsPerCollection.Value,
                        collectionName
                    );
                    break;
                }
            }

            _logger.LogInformation(
                "Data compatibility validation completed. {ErrorCount} errors, {ConflictCount} conflicts found",
                errors.Count,
                conflicts.Count
            );

            return errors.Count == 0
                ? (
                    conflicts.Count == 0
                        ? ValidationResult.Success()
                        : ValidationResult.WithConflicts(conflicts.ToArray())
                )
                : ValidationResult.FailureWithConflicts(errors.ToArray(), conflicts.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Data compatibility validation failed: {Error}", ex.Message);
            errors.Add(
                new ValidationError(
                    "DataCompatibility",
                    $"Data compatibility validation failed: {ex.Message}"
                )
            );
            return ValidationResult.Failure(errors.ToArray());
        }
    }

    public async Task<ValidationResult> ValidateDocumentAsync(
        BsonDocument document,
        string collectionName,
        string connectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ValidationOptions();
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();

        try
        {
            // Discover schema for the specific table
            var schema = await _schemaIntrospectionService.DiscoverTableSchemaAsync(
                connectionString,
                collectionName,
                cancellationToken
            );

            if (schema == null)
            {
                return ValidationResult.Failure(
                    "Collection",
                    $"Table '{collectionName}' does not exist in database",
                    collectionName
                );
            }

            // Fields that are generated during transformation and don't exist in MongoDB
            var generatedFields = new HashSet<string>
            {
                "id",
                "created_at",
                "updated_at",
                "sys_created_at",
                "sys_updated_at",
                "date",
            };

            // Validate required fields and data types for each expected column
            foreach (var (columnName, columnSchema) in schema.Columns)
            {
                var fieldValue = document.Contains(columnName) ? document[columnName] : null;

                // Skip validation for fields that are generated during transformation
                if (generatedFields.Contains(columnName))
                {
                    continue;
                }

                // Check for required non-nullable fields
                if (!columnSchema.IsNullable && (fieldValue == null || fieldValue.IsBsonNull))
                {
                    errors.Add(
                        new ValidationError(
                            columnName,
                            $"Required field '{columnName}' is missing or null",
                            fieldValue
                        )
                    );
                    continue;
                }

                // Validate data type compatibility
                if (fieldValue != null && !fieldValue.IsBsonNull)
                {
                    var typeValidation = ValidateTypeCompatibility(
                        fieldValue,
                        columnSchema.DataType,
                        columnName
                    );
                    errors.AddRange(typeValidation.Errors);
                    conflicts.AddRange(typeValidation.Conflicts);
                }
            }

            // Check for potential conflicts with reserved keywords
            foreach (var element in document.Elements)
            {
                var keywordValidation = ValidateReservedKeywords(element.Name, "field");
                if (!keywordValidation.IsValid)
                {
                    conflicts.Add(
                        new ValidationConflict(
                            "ReservedKeyword",
                            $"Field name '{element.Name}' conflicts with PostgreSQL reserved keyword",
                            element.Name,
                            new[]
                            {
                                new ConflictResolutionOption(
                                    "Rename",
                                    $"Rename to '{element.Name}_field'",
                                    $"{element.Name}_field"
                                ),
                            }
                        )
                    );
                }
            }

            return errors.Count == 0
                ? (
                    conflicts.Count == 0
                        ? ValidationResult.Success()
                        : ValidationResult.WithConflicts(conflicts.ToArray())
                )
                : ValidationResult.FailureWithConflicts(errors.ToArray(), conflicts.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Document validation failed for collection {Collection}: {Error}",
                collectionName,
                ex.Message
            );
            return ValidationResult.Failure(
                "Document",
                $"Document validation failed: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Legacy synchronous validation method - deprecated in favor of ValidateDocumentAsync
    /// </summary>
    public ValidationResult ValidateDocument(
        BsonDocument document,
        string collectionName,
        ValidationOptions? options = null
    )
    {
        // For backwards compatibility, return success for now
        // Callers should use ValidateDocumentAsync for proper validation
        _logger.LogWarning(
            "ValidateDocument called without connection string - validation skipped. Use ValidateDocumentAsync instead."
        );
        return ValidationResult.Success();
    }

    public async Task<ValidationResult> DetectConflictsAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ValidationOptions();
        var conflicts = new List<ValidationConflict>();

        _logger.LogInformation("Starting conflict detection");

        try
        {
            var mongoClient = new MongoClient(mongoConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

            using var postgresConnection = new NpgsqlConnection(postgresConnectionString);
            await postgresConnection.OpenAsync(cancellationToken);

            // Discover schema to get collection names
            var discoveredSchema = await _schemaIntrospectionService.DiscoverAllTablesAsync(
                postgresConnectionString,
                cancellationToken
            );

            foreach (var collectionName in discoveredSchema.Keys)
            {
                if (options.IgnoredTables?.Contains(collectionName) == true)
                    continue;

                var collection = mongoDatabase.GetCollection<BsonDocument>(collectionName);

                // Check for duplicate IDs that would cause primary key conflicts
                var duplicateIds = await DetectDuplicateIdsAsync(collection, cancellationToken);
                foreach (var duplicateId in duplicateIds)
                {
                    conflicts.Add(
                        new ValidationConflict(
                            "DuplicateId",
                            $"Duplicate ID found in collection '{collectionName}': {duplicateId}",
                            duplicateId,
                            new[]
                            {
                                new ConflictResolutionOption(
                                    "Skip",
                                    "Skip duplicate documents",
                                    "skip"
                                ),
                                new ConflictResolutionOption(
                                    "GenerateNew",
                                    "Generate new UUID for duplicates",
                                    "generate_uuid"
                                ),
                            }
                        )
                    );
                }

                // Check for data type conflicts
                var typeConflicts = await DetectTypeConflictsAsync(
                    collection,
                    collectionName,
                    postgresConnectionString,
                    cancellationToken
                );
                conflicts.AddRange(typeConflicts);
            }

            _logger.LogInformation(
                "Conflict detection completed. {ConflictCount} conflicts found",
                conflicts.Count
            );
            return ValidationResult.WithConflicts(conflicts.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conflict detection failed: {Error}", ex.Message);
            return ValidationResult.Failure(
                "ConflictDetection",
                $"Conflict detection failed: {ex.Message}"
            );
        }
    }

    public async Task<ValidationResult> ValidateReferentialIntegrityAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgresConnectionString,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= new ValidationOptions();
        var errors = new List<ValidationError>();

        _logger.LogInformation("Starting referential integrity validation");

        try
        {
            var mongoClient = new MongoClient(mongoConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(mongoDatabaseName);

            // For now, we'll implement basic referential integrity checks
            // This could be expanded to check foreign key relationships once they're defined

            _logger.LogInformation(
                "Referential integrity validation completed. {ErrorCount} errors found",
                errors.Count
            );
            return errors.Count == 0
                ? ValidationResult.Success()
                : ValidationResult.Failure(errors.ToArray());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Referential integrity validation failed: {Error}", ex.Message);
            return ValidationResult.Failure(
                "ReferentialIntegrity",
                $"Referential integrity validation failed: {ex.Message}"
            );
        }
    }

    public ValidationResult ValidateJsonStructure(
        string? jsonValue,
        string fieldName,
        object? expectedSchema = null
    )
    {
        if (string.IsNullOrWhiteSpace(jsonValue))
        {
            return ValidationResult.Success(); // Null/empty JSON is valid for nullable JSONB fields
        }

        try
        {
            var jsonDocument = JsonDocument.Parse(jsonValue);

            // Basic JSON structure validation - ensure it's valid JSON
            if (jsonDocument.RootElement.ValueKind == JsonValueKind.Undefined)
            {
                return ValidationResult.Failure(fieldName, "Invalid JSON structure", jsonValue);
            }

            _logger.LogDebug("JSON structure validation passed for field {FieldName}", fieldName);
            return ValidationResult.Success();
        }
        catch (JsonException ex)
        {
            _logger.LogDebug(
                "JSON validation failed for field {FieldName}: {Error}",
                fieldName,
                ex.Message
            );
            return ValidationResult.Failure(fieldName, $"Invalid JSON: {ex.Message}", jsonValue);
        }
    }

    public ValidationResult ValidateReservedKeywords(string identifier, string identifierType)
    {
        if (string.IsNullOrWhiteSpace(identifier))
        {
            return ValidationResult.Failure(
                identifierType,
                "Identifier cannot be null or empty",
                identifier
            );
        }

        if (PostgresReservedKeywords.Contains(identifier))
        {
            return ValidationResult.Failure(
                identifierType,
                $"'{identifier}' is a PostgreSQL reserved keyword",
                identifier
            );
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateDateFormat(
        object? dateValue,
        string fieldName,
        (DateTime? Min, DateTime? Max)? allowedRange = null
    )
    {
        if (dateValue == null)
        {
            return ValidationResult.Success(); // Null dates are handled by nullable field validation
        }

        DateTime parsedDate;

        try
        {
            parsedDate = dateValue switch
            {
                DateTime dt => dt,
                DateTimeOffset dto => dto.DateTime,
                long unixMills => DateTimeOffset.FromUnixTimeMilliseconds(unixMills).DateTime,
                string dateStr => DateTime.Parse(dateStr),
                BsonValue bsonValue when bsonValue.IsValidDateTime => bsonValue.ToUniversalTime(),
                _ => throw new ArgumentException($"Unsupported date type: {dateValue.GetType()}"),
            };
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure(
                fieldName,
                $"Invalid date format: {ex.Message}",
                dateValue
            );
        }

        // Validate date range if specified
        if (allowedRange.HasValue)
        {
            var (min, max) = allowedRange.Value;

            if (min.HasValue && parsedDate < min.Value)
            {
                return ValidationResult.Failure(
                    fieldName,
                    $"Date {parsedDate} is before minimum allowed date {min.Value}",
                    dateValue
                );
            }

            if (max.HasValue && parsedDate > max.Value)
            {
                return ValidationResult.Failure(
                    fieldName,
                    $"Date {parsedDate} is after maximum allowed date {max.Value}",
                    dateValue
                );
            }
        }

        return ValidationResult.Success();
    }

    public ValidationResult ValidateTypeCompatibility(
        object? bsonValue,
        string expectedPostgreSqlType,
        string fieldName
    )
    {
        if (bsonValue == null)
        {
            return ValidationResult.Success(); // Null values are handled by nullable field validation
        }

        try
        {
            var isCompatible = expectedPostgreSqlType.ToLowerInvariant() switch
            {
                "uuid" => bsonValue is string || bsonValue is Guid,
                var t when t.StartsWith("varchar(") => bsonValue is string,
                "varchar" => bsonValue is string,
                "text" => bsonValue is string,
                "integer" => bsonValue is int
                    || bsonValue is long
                    || (bsonValue is BsonValue bsonInt && bsonInt.IsInt32),
                "bigint" => bsonValue is long
                    || bsonValue is int
                    || (bsonValue is BsonValue bsonLong && (bsonLong.IsInt32 || bsonLong.IsInt64)),
                "double precision" => bsonValue is double
                    || bsonValue is float
                    || bsonValue is int
                    || bsonValue is long
                    || (bsonValue is BsonValue bsonNum && bsonNum.IsNumeric),
                "boolean" => bsonValue is bool
                    || (bsonValue is BsonValue bsonBool && bsonBool.IsBoolean),
                "timestamp without time zone" => bsonValue is DateTime
                    || bsonValue is DateTimeOffset
                    || bsonValue is long
                    || bsonValue is string
                    || (bsonValue is BsonValue bsonDate && bsonDate.IsValidDateTime),
                "jsonb" => true, // JSONB can accept any structured data
                _ => false,
            };

            if (!isCompatible)
            {
                var conflicts = new List<ValidationConflict>
                {
                    new(
                        "TypeMismatch",
                        $"Type mismatch for field '{fieldName}': {bsonValue.GetType().Name} cannot be converted to {expectedPostgreSqlType}",
                        bsonValue,
                        new[]
                        {
                            new ConflictResolutionOption(
                                "Convert",
                                $"Attempt automatic conversion to {expectedPostgreSqlType}",
                                "convert"
                            ),
                            new ConflictResolutionOption(
                                "Skip",
                                "Skip this field during migration",
                                "skip"
                            ),
                            new ConflictResolutionOption(
                                "Default",
                                $"Use default value for {expectedPostgreSqlType}",
                                "default"
                            ),
                        }
                    ),
                };

                return ValidationResult.WithConflicts(conflicts.ToArray());
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure(
                fieldName,
                $"Type compatibility check failed: {ex.Message}",
                bsonValue
            );
        }
    }

    #endregion

    #region Private Helper Methods

    private async Task<TableValidationResult> ValidateTableAsync(
        NpgsqlConnection connection,
        string tableName,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<ValidationError>();
        var columnResults = new List<ColumnValidationResult>();
        var indexResults = new List<IndexValidationResult>();

        try
        {
            // Check if table exists
            var tableExistsQuery =
                @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = @tableName
                )";

            using var tableCmd = new NpgsqlCommand(tableExistsQuery, connection);
            tableCmd.Parameters.AddWithValue("tableName", tableName);
            var tableExists = (bool)(await tableCmd.ExecuteScalarAsync(cancellationToken) ?? false);

            if (!tableExists)
            {
                errors.Add(
                    new ValidationError(tableName, $"Table '{tableName}' does not exist", tableName)
                );
                return new TableValidationResult(
                    tableName,
                    false,
                    errors,
                    columnResults,
                    indexResults
                );
            }

            // Discover schema for this table
            var discoveredTableSchema = await _schemaIntrospectionService.DiscoverTableSchemaAsync(
                connectionString,
                tableName,
                cancellationToken
            );
            if (discoveredTableSchema != null)
            {
                columnResults = await ValidateTableColumnsAsync(
                    connection,
                    tableName,
                    discoveredTableSchema,
                    cancellationToken
                );
                indexResults = await ValidateTableIndexesAsync(
                    connection,
                    tableName,
                    discoveredTableSchema,
                    cancellationToken
                );

                errors.AddRange(columnResults.SelectMany(c => c.Errors));
                errors.AddRange(indexResults.SelectMany(i => i.Errors));
            }

            return new TableValidationResult(tableName, true, errors, columnResults, indexResults);
        }
        catch (Exception ex)
        {
            errors.Add(
                new ValidationError(tableName, $"Table validation failed: {ex.Message}", tableName)
            );
            return new TableValidationResult(tableName, false, errors, columnResults, indexResults);
        }
    }

    private async Task<List<ColumnValidationResult>> ValidateTableColumnsAsync(
        NpgsqlConnection connection,
        string tableName,
        TableSchema expectedSchema,
        CancellationToken cancellationToken
    )
    {
        var results = new List<ColumnValidationResult>();

        var columnsQuery =
            @"
            SELECT column_name, data_type, is_nullable, column_default
            FROM information_schema.columns
            WHERE table_schema = 'public' AND table_name = @tableName";

        using var cmd = new NpgsqlCommand(columnsQuery, connection);
        cmd.Parameters.AddWithValue("tableName", tableName);

        var actualColumns = new Dictionary<string, (string DataType, bool IsNullable)>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString("column_name");
            var dataType = reader.GetString("data_type");
            var isNullable = reader.GetString("is_nullable") == "YES";

            actualColumns[columnName] = (dataType, isNullable);
        }

        // Validate each expected column
        foreach (var (columnName, expectedColumn) in expectedSchema.Columns)
        {
            var errors = new List<ValidationError>();

            if (!actualColumns.TryGetValue(columnName, out var actualColumn))
            {
                errors.Add(
                    new ValidationError(
                        columnName,
                        $"Column '{columnName}' does not exist in table '{tableName}'",
                        columnName
                    )
                );
                results.Add(
                    new ColumnValidationResult(
                        columnName,
                        false,
                        expectedColumn.DataType,
                        null,
                        expectedColumn.IsNullable,
                        errors
                    )
                );
                continue;
            }

            // Validate data type (simplified comparison)
            if (!IsDataTypeCompatible(expectedColumn.DataType, actualColumn.DataType))
            {
                errors.Add(
                    new ValidationError(
                        columnName,
                        $"Column '{columnName}' has incompatible data type. Expected: {expectedColumn.DataType}, Actual: {actualColumn.DataType}",
                        actualColumn.DataType
                    )
                );
            }

            // Validate nullability
            if (expectedColumn.IsNullable != actualColumn.IsNullable)
            {
                errors.Add(
                    new ValidationError(
                        columnName,
                        $"Column '{columnName}' nullability mismatch. Expected: {(expectedColumn.IsNullable ? "nullable" : "not nullable")}, Actual: {(actualColumn.IsNullable ? "nullable" : "not nullable")}",
                        actualColumn.IsNullable
                    )
                );
            }

            results.Add(
                new ColumnValidationResult(
                    columnName,
                    true,
                    expectedColumn.DataType,
                    actualColumn.DataType,
                    actualColumn.IsNullable,
                    errors
                )
            );
        }

        return results;
    }

    private async Task<List<IndexValidationResult>> ValidateTableIndexesAsync(
        NpgsqlConnection connection,
        string tableName,
        TableSchema expectedSchema,
        CancellationToken cancellationToken
    )
    {
        var results = new List<IndexValidationResult>();

        var indexesQuery =
            @"
            SELECT indexname, indexdef
            FROM pg_indexes
            WHERE tablename = @tableName AND schemaname = 'public'";

        using var cmd = new NpgsqlCommand(indexesQuery, connection);
        cmd.Parameters.AddWithValue("tableName", tableName);

        var actualIndexes = new HashSet<string>();

        using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var indexName = reader.GetString("indexname");
            actualIndexes.Add(indexName);
        }

        // Validate each expected index
        foreach (var expectedIndex in expectedSchema.ExpectedIndexes)
        {
            var errors = new List<ValidationError>();
            var exists = actualIndexes.Contains(expectedIndex);

            if (!exists)
            {
                errors.Add(
                    new ValidationError(
                        expectedIndex,
                        $"Expected index '{expectedIndex}' does not exist on table '{tableName}'",
                        expectedIndex
                    )
                );
            }

            results.Add(
                new IndexValidationResult(
                    expectedIndex,
                    exists,
                    Array.Empty<string>(),
                    Array.Empty<string>(),
                    errors
                )
            );
        }

        return results;
    }

    private static bool IsDataTypeCompatible(string expected, string actual)
    {
        // Normalize type names for comparison
        expected = NormalizeDataType(expected);
        actual = NormalizeDataType(actual);

        return expected.Equals(actual, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDataType(string dataType)
    {
        return dataType.ToLowerInvariant() switch
        {
            "character varying" => "varchar",
            "timestamp without time zone" => "timestamp",
            "double precision" => "double precision",
            _ => dataType.ToLowerInvariant(),
        };
    }

    private async Task<ValidationResult> ValidateCollectionDataAsync(
        IMongoDatabase mongoDatabase,
        string collectionName,
        ValidationOptions options,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();

        try
        {
            var collection = mongoDatabase.GetCollection<BsonDocument>(collectionName);
            var sampleSize = Math.Min(1000, options.MaxErrorsPerCollection ?? 1000);

            var documents = await collection
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Limit(sampleSize)
                .ToListAsync(cancellationToken);

            foreach (var document in documents)
            {
                var docResult = ValidateDocument(document, collectionName, options);
                errors.AddRange(docResult.Errors);
                conflicts.AddRange(docResult.Conflicts);

                if (
                    options.MaxErrorsPerCollection.HasValue
                    && errors.Count >= options.MaxErrorsPerCollection.Value
                )
                    break;
            }

            return errors.Count == 0
                ? (
                    conflicts.Count == 0
                        ? ValidationResult.Success()
                        : ValidationResult.WithConflicts(conflicts.ToArray())
                )
                : ValidationResult.FailureWithConflicts(errors.ToArray(), conflicts.ToArray());
        }
        catch (Exception ex)
        {
            errors.Add(
                new ValidationError(collectionName, $"Collection validation failed: {ex.Message}")
            );
            return ValidationResult.Failure(errors.ToArray());
        }
    }

    private async Task<List<string>> DetectDuplicateIdsAsync(
        IMongoCollection<BsonDocument> collection,
        CancellationToken cancellationToken
    )
    {
        var duplicates = new List<string>();

        try
        {
            var pipeline = new[]
            {
                new BsonDocument(
                    "$group",
                    new BsonDocument { { "_id", "$_id" }, { "count", new BsonDocument("$sum", 1) } }
                ),
                new BsonDocument("$match", new BsonDocument("count", new BsonDocument("$gt", 1))),
                new BsonDocument("$limit", 100), // Limit to prevent memory issues
            };

            var results = await collection.AggregateAsync<BsonDocument>(
                pipeline,
                cancellationToken: cancellationToken
            );
            while (await results.MoveNextAsync(cancellationToken))
            {
                foreach (var result in results.Current)
                {
                    duplicates.Add(result["_id"]?.ToString() ?? string.Empty);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to detect duplicate IDs in collection");
        }

        return duplicates;
    }

    private async Task<List<ValidationConflict>> DetectTypeConflictsAsync(
        IMongoCollection<BsonDocument> collection,
        string collectionName,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var conflicts = new List<ValidationConflict>();

        try
        {
            // Discover schema for this collection
            var schema = await _schemaIntrospectionService.DiscoverTableSchemaAsync(
                connectionString,
                collectionName,
                cancellationToken
            );
            if (schema == null)
                return conflicts;

            var sampleSize = 100;
            var documents = await collection
                .Find(FilterDefinition<BsonDocument>.Empty)
                .Limit(sampleSize)
                .ToListAsync(cancellationToken);

            var fieldTypes = new Dictionary<string, HashSet<Type>>();

            // Collect actual field types from sample documents
            foreach (var document in documents)
            {
                foreach (var element in document.Elements)
                {
                    if (!fieldTypes.ContainsKey(element.Name))
                        fieldTypes[element.Name] = new HashSet<Type>();

                    fieldTypes[element.Name].Add(element.Value.GetType());
                }
            }

            // Check for type conflicts
            foreach (var (fieldName, types) in fieldTypes)
            {
                if (types.Count > 1)
                {
                    conflicts.Add(
                        new ValidationConflict(
                            "InconsistentFieldType",
                            $"Field '{fieldName}' has inconsistent types across documents: {string.Join(", ", types.Select(t => t.Name))}",
                            types,
                            new[]
                            {
                                new ConflictResolutionOption(
                                    "ConvertToString",
                                    "Convert all values to string",
                                    "string"
                                ),
                                new ConflictResolutionOption(
                                    "UseFirstType",
                                    "Use the first encountered type",
                                    types.First().Name
                                ),
                                new ConflictResolutionOption(
                                    "Skip",
                                    "Skip documents with conflicting types",
                                    "skip"
                                ),
                            }
                        )
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Failed to detect type conflicts in collection {Collection}",
                collectionName
            );
        }

        return conflicts;
    }

    #endregion
}
