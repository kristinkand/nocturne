using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Tests.Services;

internal static class LegacyDeviceAgeTestHelper
{
    public static LegacyDeviceAgeService CreateService(params TrackerInstanceEntity[] instances)
    {
        var dbContext = new NocturneDbContext(new DbContextOptions<NocturneDbContext>());
        var repository = new Mock<TrackerRepository>(dbContext);
        repository
            .Setup(r => r.GetActiveInstancesAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(instances);

        return new LegacyDeviceAgeService(repository.Object, NullLogger<LegacyDeviceAgeService>.Instance);
    }

    public static TrackerInstanceEntity CreateInstance(
        string[] eventTypes,
        double ageHours,
        string? notes = null,
        IEnumerable<TrackerNotificationThresholdEntity>? thresholds = null)
    {
        var definition = new TrackerDefinitionEntity
        {
            Id = Guid.NewGuid(),
            TriggerEventTypes = JsonSerializer.Serialize(eventTypes),
            NotificationThresholds = thresholds?.ToList()
                ?? new List<TrackerNotificationThresholdEntity>()
        };

        return new TrackerInstanceEntity
        {
            Id = Guid.NewGuid(),
            UserId = "user-1",
            DefinitionId = definition.Id,
            Definition = definition,
            StartedAt = DateTime.UtcNow.AddHours(-ageHours),
            StartNotes = notes
        };
    }

    public static TrackerNotificationThresholdEntity CreateThreshold(
        NotificationUrgency urgency,
        int hours)
    {
        return new TrackerNotificationThresholdEntity
        {
            Id = Guid.NewGuid(),
            Urgency = urgency,
            Hours = hours,
            DisplayOrder = 0
        };
    }
}
