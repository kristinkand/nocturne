using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Extensions;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Controllers.V4;

/// <summary>
/// V4 Treatments controller with authentication and tracker integration.
/// Unlike V1-V3 endpoints, this requires authentication and automatically
/// triggers tracker instances when matching treatments are created.
/// </summary>
[ApiController]
[Route("api/v4/treatments")]
[Tags("V4 Treatments")]
[Authorize]
public class TreatmentsController : ControllerBase
{
    private readonly TreatmentRepository _repository;
    private readonly IDocumentProcessingService _documentProcessingService;
    private readonly ITrackerTriggerService _trackerTriggerService;
    private readonly ITrackerSuggestionService _trackerSuggestionService;
    private readonly ISignalRBroadcastService _broadcast;
    private readonly ILogger<TreatmentsController> _logger;

    public TreatmentsController(
        TreatmentRepository repository,
        IDocumentProcessingService documentProcessingService,
        ITrackerTriggerService trackerTriggerService,
        ITrackerSuggestionService trackerSuggestionService,
        ISignalRBroadcastService broadcast,
        ILogger<TreatmentsController> logger
    )
    {
        _repository = repository;
        _documentProcessingService = documentProcessingService;
        _trackerTriggerService = trackerTriggerService;
        _trackerSuggestionService = trackerSuggestionService;
        _broadcast = broadcast;
        _logger = logger;
    }

    /// <summary>
    /// Create a treatment with tracker integration.
    /// If the treatment's event type matches a tracker's trigger event types,
    /// the tracker instance will be automatically started/restarted.
    /// </summary>
    [HttpPost]
    [ProducesResponseType(typeof(Treatment), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Treatment>> CreateTreatment(
        [FromBody] Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        if (treatment == null)
            return BadRequest("Treatment data is required");

        var userId = HttpContext.GetSubjectIdString()!;

        // Process the treatment (adds timestamps, etc.)
        var processedTreatment = _documentProcessingService.ProcessTreatment(treatment);

        // Save to database
        var created = await _repository.CreateTreatmentAsync(processedTreatment, cancellationToken);

        if (created == null)
            return StatusCode(500, "Failed to create treatment");

        _logger.LogInformation(
            "Created V4 treatment {Id} ({EventType}) for user {UserId}",
            created.Id,
            created.EventType,
            userId
        );

        // Trigger any matching trackers
        await _trackerTriggerService.ProcessTreatmentAsync(created, userId, cancellationToken);

        // Evaluate for tracker suggestions (e.g., Site Change -> suggest resetting Cannula tracker)
        await _trackerSuggestionService.EvaluateTreatmentForTrackerSuggestionAsync(created, userId, cancellationToken);

        // Broadcast via SignalR
        await _broadcast.BroadcastStorageCreateAsync("treatments", created);

        return CreatedAtAction(nameof(GetTreatment), new { id = created.Id }, created);
    }

    /// <summary>
    /// Create multiple treatments with tracker integration.
    /// </summary>
    [HttpPost("bulk")]
    [ProducesResponseType(typeof(Treatment[]), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<Treatment[]>> CreateTreatments(
        [FromBody] Treatment[] treatments,
        CancellationToken cancellationToken = default
    )
    {
        if (treatments == null || treatments.Length == 0)
            return BadRequest("Treatment data is required");

        if (treatments.Length > 1000)
            return BadRequest("Bulk operations are limited to 1000 treatments per request");

        var userId = HttpContext.GetSubjectIdString()!;

        // Process all treatments
        var processedTreatments = treatments
            .Select(t => _documentProcessingService.ProcessTreatment(t))
            .ToList();

        // Save to database
        var created = await _repository.CreateTreatmentsAsync(
            processedTreatments,
            cancellationToken
        );
        var createdArray = created.ToArray();

        _logger.LogInformation(
            "Created {Count} V4 treatments for user {UserId}",
            createdArray.Length,
            userId
        );

        // Trigger any matching trackers
        await _trackerTriggerService.ProcessTreatmentsAsync(
            createdArray,
            userId,
            cancellationToken
        );

        // Evaluate for tracker suggestions (e.g., Site Change -> suggest resetting Cannula tracker)
        foreach (var treatment in createdArray)
        {
            await _trackerSuggestionService.EvaluateTreatmentForTrackerSuggestionAsync(treatment, userId, cancellationToken);
        }

        // Broadcast via SignalR
        foreach (var treatment in createdArray)
        {
            await _broadcast.BroadcastStorageCreateAsync("treatments", treatment);
        }

        return StatusCode(201, createdArray);
    }

    /// <summary>
    /// Get a specific treatment by ID
    /// </summary>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(Treatment), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<Treatment>> GetTreatment(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        var treatment = await _repository.GetTreatmentByIdAsync(id, cancellationToken);

        if (treatment == null)
            return NotFound();

        return Ok(treatment);
    }
}
