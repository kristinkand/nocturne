using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for AlertProcessingService
/// </summary>
public class AlertProcessingServiceTests
{
    private readonly Mock<AlertHistoryRepository> _mockAlertHistoryRepository;
    private readonly Mock<AlertRuleRepository> _mockAlertRuleRepository;
    private readonly Mock<NotificationPreferencesRepository> _mockNotificationPreferencesRepository;
    private readonly Mock<ISignalRBroadcastService> _mockSignalRBroadcastService;
    private readonly Mock<IOptions<AlertMonitoringOptions>> _mockOptions;
    private readonly Mock<ILogger<AlertProcessingService>> _mockLogger;
    private readonly AlertProcessingService _alertProcessingService;
    private readonly AlertMonitoringOptions _defaultOptions;

    public AlertProcessingServiceTests()
    {
        var db = Nocturne.Tests.Shared.Infrastructure.TestDbContextFactory.CreateInMemoryContext();
        _mockAlertHistoryRepository = new Mock<AlertHistoryRepository>(db) { CallBase = true };
        _mockAlertRuleRepository = new Mock<AlertRuleRepository>(db) { CallBase = true };
        _mockNotificationPreferencesRepository = new Mock<NotificationPreferencesRepository>(db)
        {
            CallBase = true,
        };
        _mockSignalRBroadcastService = new Mock<ISignalRBroadcastService>();
        _mockOptions = new Mock<IOptions<AlertMonitoringOptions>>();
        _mockLogger = new Mock<ILogger<AlertProcessingService>>();

        _defaultOptions = new AlertMonitoringOptions
        {
            AlertCooldownMinutes = 15,
            MaxActiveAlertsPerUser = 10,
            HysteresisPercentage = 0.1,
        };

        _mockOptions.Setup(x => x.Value).Returns(_defaultOptions);

        _alertProcessingService = new AlertProcessingService(
            _mockAlertHistoryRepository.Object,
            _mockAlertRuleRepository.Object,
            _mockNotificationPreferencesRepository.Object,
            _mockSignalRBroadcastService.Object,
            _mockOptions.Object,
            _mockLogger.Object
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessAlertEvent_WithValidAlert_CreatesHistoryAndSendsNotification()
    {
        // Arrange
        var alertEvent = CreateAlertEvent(AlertType.Low, "test-user", 65, 70);
        var createdAlert = CreateAlertHistoryEntity(alertEvent);

        _mockAlertHistoryRepository
            .Setup(x =>
                x.CreateAlertAsync(It.IsAny<AlertHistoryEntity>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(createdAlert);

        // Act
        await _alertProcessingService.ProcessAlertEvent(alertEvent, CancellationToken.None);

        // Assert
        _mockAlertHistoryRepository.Verify(
            x => x.CreateAlertAsync(It.IsAny<AlertHistoryEntity>(), It.IsAny<CancellationToken>()),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessAlertEvent_WithUrgentAlert_SendsUrgentNotification()
    {
        // Arrange
        var alertEvent = CreateAlertEvent(AlertType.UrgentLow, "test-user", 45, 55);
        var createdAlert = CreateAlertHistoryEntity(alertEvent);

        _mockAlertHistoryRepository
            .Setup(x =>
                x.CreateAlertAsync(It.IsAny<AlertHistoryEntity>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(createdAlert);

        // Act
        await _alertProcessingService.ProcessAlertEvent(alertEvent, CancellationToken.None);

        // Assert
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastUrgentAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessAlertEvent_WithDeviceWarning_SendsNotification()
    {
        // Arrange
        var alertEvent = CreateAlertEvent(AlertType.DeviceWarning, "test-user", 100, 0);
        var createdAlert = CreateAlertHistoryEntity(alertEvent);

        _mockAlertHistoryRepository
            .Setup(x =>
                x.CreateAlertAsync(It.IsAny<AlertHistoryEntity>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(createdAlert);

        // Act
        await _alertProcessingService.ProcessAlertEvent(alertEvent, CancellationToken.None);

        // Assert
        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastNotificationAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAlert_WithValidId_UpdatesStatusAndSendsClearNotification()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var resolvedAlert = new AlertHistoryEntity
        {
            Id = alertId,
            UserId = "test-user",
            AlertType = "Low",
            Status = "RESOLVED",
            ResolvedAt = DateTime.UtcNow,
        };

        _mockAlertHistoryRepository
            .Setup(x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "RESOLVED",
                    null,
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(resolvedAlert);

        // Act
        await _alertProcessingService.ResolveAlert(alertId, CancellationToken.None);

        // Assert
        _mockAlertHistoryRepository.Verify(
            x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "RESOLVED",
                    null,
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );

        _mockSignalRBroadcastService.Verify(
            x => x.BroadcastClearAlarmAsync(It.IsAny<NotificationBase>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AcknowledgeAlert_WithSnooze_UpdatesStatusWithSnoozeTime()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var snoozeMinutes = 30;
        var acknowledgedAlert = new AlertHistoryEntity
        {
            Id = alertId,
            UserId = "test-user",
            AlertType = "Low",
            Status = "SNOOZED",
            AcknowledgedAt = DateTime.UtcNow,
            SnoozeUntil = DateTime.UtcNow.AddMinutes(snoozeMinutes),
        };

        _mockAlertHistoryRepository
            .Setup(x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "SNOOZED",
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acknowledgedAlert);

        // Act
        await _alertProcessingService.AcknowledgeAlert(
            alertId,
            snoozeMinutes,
            CancellationToken.None
        );

        // Assert
        _mockAlertHistoryRepository.Verify(
            x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "SNOOZED",
                    It.IsAny<DateTime>(),
                    null,
                    It.IsAny<DateTime>(),
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task AcknowledgeAlert_WithoutSnooze_UpdatesStatusToAcknowledged()
    {
        // Arrange
        var alertId = Guid.NewGuid();
        var acknowledgedAlert = new AlertHistoryEntity
        {
            Id = alertId,
            UserId = "test-user",
            AlertType = "Low",
            Status = "ACKNOWLEDGED",
            AcknowledgedAt = DateTime.UtcNow,
        };

        _mockAlertHistoryRepository
            .Setup(x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "ACKNOWLEDGED",
                    It.IsAny<DateTime>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(acknowledgedAlert);

        // Act
        await _alertProcessingService.AcknowledgeAlert(alertId, 0, CancellationToken.None);

        // Assert
        _mockAlertHistoryRepository.Verify(
            x =>
                x.UpdateAlertStatusAsync(
                    alertId,
                    "ACKNOWLEDGED",
                    It.IsAny<DateTime>(),
                    null,
                    null,
                    It.IsAny<CancellationToken>()
                ),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ResolveAlertsForUser_WithValidUserAndType_CallsRepositoryResolveMethod()
    {
        // Arrange
        var userId = "test-user";
        var alertType = AlertType.Low;
        var resolvedCount = 2;

        _mockAlertHistoryRepository
            .Setup(x =>
                x.ResolveAlertsAsync(userId, alertType.ToString(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(resolvedCount);

        // Act
        await _alertProcessingService.ResolveAlertsForUser(
            userId,
            alertType,
            CancellationToken.None
        );

        // Assert
        _mockAlertHistoryRepository.Verify(
            x => x.ResolveAlertsAsync(userId, alertType.ToString(), It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task CleanupOldAlerts_WithDefaultDays_CallsRepositoryCleanupMethod()
    {
        // Arrange
        var deletedCount = 5;
        _mockAlertHistoryRepository
            .Setup(x => x.CleanupOldAlertsAsync(30, It.IsAny<CancellationToken>()))
            .ReturnsAsync(deletedCount);

        // Act
        await _alertProcessingService.CleanupOldAlerts(30, CancellationToken.None);

        // Assert
        _mockAlertHistoryRepository.Verify(
            x => x.CleanupOldAlertsAsync(30, It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task ProcessAlertEvent_WithHighAlert_ResolvesLowAlerts()
    {
        // Arrange
        var alertEvent = CreateAlertEvent(AlertType.High, "test-user", 200, 180);
        var createdAlert = CreateAlertHistoryEntity(alertEvent);

        _mockAlertHistoryRepository
            .Setup(x =>
                x.CreateAlertAsync(It.IsAny<AlertHistoryEntity>(), It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(createdAlert);

        _mockAlertHistoryRepository
            .Setup(x => x.ResolveAlertsAsync("test-user", "Low", It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        _mockAlertHistoryRepository
            .Setup(x =>
                x.ResolveAlertsAsync("test-user", "UrgentLow", It.IsAny<CancellationToken>())
            )
            .ReturnsAsync(0);

        // Act
        await _alertProcessingService.ProcessAlertEvent(alertEvent, CancellationToken.None);

        // Assert
        _mockAlertHistoryRepository.Verify(
            x => x.ResolveAlertsAsync("test-user", "Low", It.IsAny<CancellationToken>()),
            Times.Once
        );

        _mockAlertHistoryRepository.Verify(
            x => x.ResolveAlertsAsync("test-user", "UrgentLow", It.IsAny<CancellationToken>()),
            Times.Once
        );
    }

    private static AlertEvent CreateAlertEvent(
        AlertType alertType,
        string userId,
        decimal glucoseValue,
        decimal threshold
    )
    {
        return new AlertEvent
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            AlertRuleId = Guid.NewGuid(),
            AlertType = alertType,
            GlucoseValue = glucoseValue,
            Threshold = threshold,
            TriggerTime = DateTime.UtcNow,
            Context = new Dictionary<string, object>
            {
                { "EntryId", Guid.NewGuid().ToString() },
                { "Direction", "Flat" },
                { "Delta", 0 },
            },
        };
    }

    private static AlertHistoryEntity CreateAlertHistoryEntity(AlertEvent alertEvent)
    {
        return new AlertHistoryEntity
        {
            Id = Guid.NewGuid(),
            UserId = alertEvent.UserId,
            AlertRuleId = alertEvent.AlertRuleId,
            AlertType = alertEvent.AlertType.ToString(),
            GlucoseValue = alertEvent.GlucoseValue,
            Threshold = alertEvent.Threshold,
            Status = "ACTIVE",
            TriggerTime = alertEvent.TriggerTime,
            EscalationLevel = 0,
            NotificationsSent = "[]",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }
}
