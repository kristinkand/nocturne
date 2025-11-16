using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for food operations with WebSocket broadcasting
/// </summary>
public class FoodService : IFoodService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ISignalRBroadcastService _signalRBroadcastService;
    private readonly ILogger<FoodService> _logger;

    public FoodService(
        IPostgreSqlService postgreSqlService,
        IDocumentProcessingService documentProcessingService,
        ISignalRBroadcastService signalRBroadcastService,
        ILogger<FoodService> logger
    )
    {
        _postgreSqlService =
            postgreSqlService ?? throw new ArgumentNullException(nameof(postgreSqlService));
        _documentProcessingService =
            documentProcessingService
            ?? throw new ArgumentNullException(nameof(documentProcessingService));
        _signalRBroadcastService =
            signalRBroadcastService
            ?? throw new ArgumentNullException(nameof(signalRBroadcastService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Getting food records with find: {Find}, count: {Count}, skip: {Skip}",
                find,
                count,
                skip
            );
            // Note: MongoDB service doesn't support find/count/skip parameters for food yet
            return await _postgreSqlService.GetFoodAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting food records");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Food?> GetFoodByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug("Getting food record by ID: {Id}", id);
            return await _postgreSqlService.GetFoodByIdAsync(id, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting food record by ID: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> CreateFoodAsync(
        IEnumerable<Food> foods,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var foodList = foods.ToList();
            _logger.LogDebug("Creating {Count} food records", foodList.Count);

            // TODO: Process documents (sanitization, timestamp conversion, deduplication)
            // Currently Food model doesn't implement IProcessableDocument
            // var processedFoods = _documentProcessingService.ProcessDocuments(foodList, enableDeduplication: true);

            // Create in database
            var createdFoods = await _postgreSqlService.CreateFoodAsync(
                foodList,
                cancellationToken
            );
            var resultList = createdFoods.ToList();

            // Broadcast WebSocket event for storage create
            await _signalRBroadcastService.BroadcastStorageCreateAsync(
                "food",
                new
                {
                    collection = "food",
                    data = resultList,
                    count = resultList.Count,
                }
            );

            _logger.LogDebug("Successfully created {Count} food records", resultList.Count);
            return resultList;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating food records");
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<Food?> UpdateFoodAsync(
        string id,
        Food food,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug("Updating food record with ID: {Id}", id);

            var updatedFood = await _postgreSqlService.UpdateFoodAsync(id, food, cancellationToken);

            if (updatedFood != null)
            {
                // Broadcast WebSocket event for storage update
                await _signalRBroadcastService.BroadcastStorageUpdateAsync(
                    "food",
                    new
                    {
                        collection = "food",
                        data = updatedFood,
                        id = id,
                    }
                );

                _logger.LogDebug("Successfully updated food record with ID: {Id}", id);
            }

            return updatedFood;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating food record with ID: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFoodAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug("Deleting food record with ID: {Id}", id);

            var deleted = await _postgreSqlService.DeleteFoodAsync(id, cancellationToken);

            if (deleted)
            {
                // Broadcast WebSocket event for storage delete
                await _signalRBroadcastService.BroadcastStorageDeleteAsync(
                    "food",
                    new { collection = "food", id = id }
                );

                _logger.LogDebug("Successfully deleted food record with ID: {Id}", id);
            }

            return deleted;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting food record with ID: {Id}", id);
            throw;
        }
    }

    /// <inheritdoc />
    public async Task<long> DeleteMultipleFoodAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug("Bulk deleting food records with filter: {Find}", find);

            var deletedCount = await _postgreSqlService.BulkDeleteFoodAsync(
                find ?? "{}",
                cancellationToken
            );

            if (deletedCount > 0)
            {
                // Broadcast WebSocket event for bulk storage delete
                await _signalRBroadcastService.BroadcastStorageDeleteAsync(
                    "food",
                    new
                    {
                        collection = "food",
                        filter = find,
                        deletedCount = deletedCount,
                    }
                );

                _logger.LogDebug("Successfully bulk deleted {Count} food records", deletedCount);
            }

            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error bulk deleting food records with filter: {Find}", find);
            throw;
        }
    }
}
