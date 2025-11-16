using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Models;

public class ProcessableDocumentInterfaceTests
{
    [Fact]
    public void Treatment_ImplementsIProcessableDocument_CorrectSanitizableFields()
    {
        // Arrange
        var treatment = new Treatment
        {
            EventType = "Meal Bolus",
            Notes = "Test notes",
            EnteredBy = "TestUser",
            Reason = "High BG",
            FoodType = "Pizza",
        };

        // Act
        var sanitizableFields = treatment.GetSanitizableFields();

        // Assert
        Assert.Contains("EventType", sanitizableFields.Keys);
        Assert.Contains("Notes", sanitizableFields.Keys);
        Assert.Contains("EnteredBy", sanitizableFields.Keys);
        Assert.Contains("Reason", sanitizableFields.Keys);
        Assert.Contains("FoodType", sanitizableFields.Keys);

        Assert.Equal("Meal Bolus", sanitizableFields["EventType"]);
        Assert.Equal("Test notes", sanitizableFields["Notes"]);
        Assert.Equal("TestUser", sanitizableFields["EnteredBy"]);
        Assert.Equal("High BG", sanitizableFields["Reason"]);
        Assert.Equal("Pizza", sanitizableFields["FoodType"]);
    }

    [Fact]
    public void Treatment_SetSanitizedField_UpdatesCorrectProperties()
    {
        // Arrange
        var treatment = new Treatment();

        // Act
        treatment.SetSanitizedField("EventType", "Clean Event");
        treatment.SetSanitizedField("Notes", "Clean Notes");
        treatment.SetSanitizedField("EnteredBy", "Clean User");
        treatment.SetSanitizedField("Reason", "Clean Reason");
        treatment.SetSanitizedField("FoodType", "Clean Food");

        // Assert
        Assert.Equal("Clean Event", treatment.EventType);
        Assert.Equal("Clean Notes", treatment.Notes);
        Assert.Equal("Clean User", treatment.EnteredBy);
        Assert.Equal("Clean Reason", treatment.Reason);
        Assert.Equal("Clean Food", treatment.FoodType);
    }

    [Fact]
    public void DeviceStatus_ImplementsIProcessableDocument_CorrectSanitizableFields()
    {
        // Arrange
        var deviceStatus = new DeviceStatus { Device = "MyDevice" };

        // Act
        var sanitizableFields = deviceStatus.GetSanitizableFields();

        // Assert
        Assert.Contains("Device", sanitizableFields.Keys);
        Assert.Equal("MyDevice", sanitizableFields["Device"]);
    }

    [Fact]
    public void DeviceStatus_SetSanitizedField_UpdatesDeviceProperty()
    {
        // Arrange
        var deviceStatus = new DeviceStatus();

        // Act
        deviceStatus.SetSanitizedField("Device", "Clean Device");

        // Assert
        Assert.Equal("Clean Device", deviceStatus.Device);
    }

    [Fact]
    public void Entry_ImplementsIProcessableDocument_CorrectSanitizableFields()
    {
        // Arrange
        var entry = new Entry { Device = "CGM Device", Type = "sgv" };

        // Act
        var sanitizableFields = entry.GetSanitizableFields();

        // Assert
        Assert.Contains("Device", sanitizableFields.Keys);
        Assert.Contains("Type", sanitizableFields.Keys);

        Assert.Equal("CGM Device", sanitizableFields["Device"]);
        Assert.Equal("sgv", sanitizableFields["Type"]);
    }

    [Fact]
    public void Entry_SetSanitizedField_UpdatesCorrectProperties()
    {
        // Arrange
        var entry = new Entry();

        // Act
        entry.SetSanitizedField("Device", "Clean Device");
        entry.SetSanitizedField("Type", "Clean Type");

        // Assert
        Assert.Equal("Clean Device", entry.Device);
        Assert.Equal("Clean Type", entry.Type);
    }

    [Fact]
    public void Activity_ImplementsIProcessableDocument_CorrectSanitizableFields()
    {
        // Arrange
        var activity = new Activity
        {
            Type = "Exercise",
            Description = "Running",
            Notes = "Good workout",
            EnteredBy = "User123",
        };

        // Act
        var sanitizableFields = activity.GetSanitizableFields();

        // Assert
        Assert.Contains("Type", sanitizableFields.Keys);
        Assert.Contains("Description", sanitizableFields.Keys);
        Assert.Contains("Notes", sanitizableFields.Keys);
        Assert.Contains("EnteredBy", sanitizableFields.Keys);

        Assert.Equal("Exercise", sanitizableFields["Type"]);
        Assert.Equal("Running", sanitizableFields["Description"]);
        Assert.Equal("Good workout", sanitizableFields["Notes"]);
        Assert.Equal("User123", sanitizableFields["EnteredBy"]);
    }

    [Fact]
    public void Activity_SetSanitizedField_UpdatesCorrectProperties()
    {
        // Arrange
        var activity = new Activity();

        // Act
        activity.SetSanitizedField("Type", "Clean Type");
        activity.SetSanitizedField("Description", "Clean Description");
        activity.SetSanitizedField("Notes", "Clean Notes");
        activity.SetSanitizedField("EnteredBy", "Clean User");

        // Assert
        Assert.Equal("Clean Type", activity.Type);
        Assert.Equal("Clean Description", activity.Description);
        Assert.Equal("Clean Notes", activity.Notes);
        Assert.Equal("Clean User", activity.EnteredBy);
    }

    [Fact]
    public void AllDocumentTypes_HaveRequiredInterfaceProperties()
    {
        // Arrange & Act & Assert
        var treatment = new Treatment();
        var deviceStatus = new DeviceStatus();
        var entry = new Entry();
        var activity = new Activity();

        // All should have Id property
        Assert.True(treatment is IProcessableDocument);
        Assert.True(deviceStatus is IProcessableDocument);
        Assert.True(entry is IProcessableDocument);
        Assert.True(activity is IProcessableDocument);

        // All should have CreatedAt property
        Assert.NotNull(treatment.GetType().GetProperty("CreatedAt"));
        Assert.NotNull(deviceStatus.GetType().GetProperty("CreatedAt"));
        Assert.NotNull(entry.GetType().GetProperty("CreatedAt"));
        Assert.NotNull(activity.GetType().GetProperty("CreatedAt"));

        // All should have Mills property
        Assert.NotNull(treatment.GetType().GetProperty("Mills"));
        Assert.NotNull(deviceStatus.GetType().GetProperty("Mills"));
        Assert.NotNull(entry.GetType().GetProperty("Mills"));
        Assert.NotNull(activity.GetType().GetProperty("Mills"));

        // All should have UtcOffset property
        Assert.NotNull(treatment.GetType().GetProperty("UtcOffset"));
        Assert.NotNull(deviceStatus.GetType().GetProperty("UtcOffset"));
        Assert.NotNull(entry.GetType().GetProperty("UtcOffset"));
        Assert.NotNull(activity.GetType().GetProperty("UtcOffset"));
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    public void DocumentTypes_WithEmptyFields_DoNotIncludeInSanitizableFields(string? emptyValue)
    {
        // Arrange
        var treatment = new Treatment { EventType = emptyValue, Notes = "Valid notes" };
        var deviceStatus = new DeviceStatus { Device = emptyValue! };
        var entry = new Entry { Device = emptyValue, Type = "sgv" };
        var activity = new Activity { Type = emptyValue, Description = "Valid description" };

        // Act
        var treatmentFields = treatment.GetSanitizableFields();
        var deviceStatusFields = deviceStatus.GetSanitizableFields();
        var entryFields = entry.GetSanitizableFields();
        var activityFields = activity.GetSanitizableFields();

        // Assert
        Assert.DoesNotContain("EventType", treatmentFields.Keys);
        Assert.Contains("Notes", treatmentFields.Keys);

        Assert.Empty(deviceStatusFields); // Device is empty, so no fields to sanitize

        Assert.DoesNotContain("Device", entryFields.Keys);
        Assert.Contains("Type", entryFields.Keys);

        Assert.DoesNotContain("Type", activityFields.Keys);
        Assert.Contains("Description", activityFields.Keys);
    }
}
