using System.Text.Json.Serialization;

#nullable enable

namespace Nocturne.Connectors.Glooko.Models
{
    public class GlookoBatchData
    {
        [JsonPropertyName("foods")]
        public GlookoFood[]? Foods { get; set; }

        [JsonPropertyName("insulins")]
        public GlookoInsulin[]? Insulins { get; set; }

        [JsonPropertyName("scheduledBasals")]
        public GlookoBasal[]? ScheduledBasals { get; set; }

        [JsonPropertyName("normalBoluses")]
        public GlookoBolus[]? NormalBoluses { get; set; }

        [JsonPropertyName("readings")]
        public GlookoCgmReading[]? Readings { get; set; }

        [JsonPropertyName("bloodGlucose")]
        public GlookoBloodGlucoseReading[]? BloodGlucose { get; set; }

        [JsonPropertyName("ketones")]
        public GlookoKetoneReading[]? Ketones { get; set; }

        [JsonPropertyName("bloodPressure")]
        public GlookoBloodPressureReading[]? BloodPressure { get; set; }

        [JsonPropertyName("weight")]
        public GlookoWeightReading[]? Weight { get; set; }

        [JsonPropertyName("sleep")]
        public GlookoSleepReading[]? Sleep { get; set; }

        [JsonPropertyName("activity")]
        public GlookoActivityReading[]? Activity { get; set; }

        [JsonPropertyName("medications")]
        public GlookoMedicationReading[]? Medications { get; set; }

        [JsonPropertyName("insulinPens")]
        public GlookoInsulinPenReading[]? InsulinPens { get; set; }
        [JsonPropertyName("extendedBoluses")]
        public GlookoExtendedBolus[]? ExtendedBoluses { get; set; }

        [JsonPropertyName("suspendBasals")]
        public GlookoSuspendBasal[]? SuspendBasals { get; set; }

        [JsonPropertyName("temporaryBasals")]
        public GlookoTempBasal[]? TempBasals { get; set; }

        [JsonPropertyName("pumpSettings")]
        public GlookoPumpSettings[]? PumpSettings { get; set; }

        [JsonPropertyName("pumpAlarms")]
        public GlookoPumpAlarm[]? PumpAlarms { get; set; }

        [JsonPropertyName("pumpEvents")]
        public GlookoPumpEvent[]? PumpEvents { get; set; }

        [JsonPropertyName("reservoirChange")]
        public GlookoReservoirChange[]? ReservoirChanges { get; set; }

        public GlookoDevice[]? Devices { get; set; }
        public GlookoDevicesAndSettings? DeviceSettings { get; set; }
    }

    public class GlookoFood
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("pumpTimestamp")]
        public string PumpTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("carbs")]
        public double Carbs { get; set; }

        [JsonPropertyName("carbohydrateGrams")]
        public double CarbohydrateGrams { get; set; }

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;
    }

    public class GlookoInsulin
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("pumpTimestamp")]
        public string PumpTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("units")]
        public double Units { get; set; }

        [JsonPropertyName("insulinType")]
        public string InsulinType { get; set; } = string.Empty;
    }

    public class GlookoBasal
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("pumpTimestamp")]
        public string PumpTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("rate")]
        public double Rate { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("startTime")]
        public int? StartTime { get; set; }

        [JsonPropertyName("pumpTimestampUtcOffset")]
        public string? PumpTimestampUtcOffset { get; set; }
    }

    public class GlookoBolus
    {
        [JsonPropertyName("pumpTimestamp")]
        public string PumpTimestamp { get; set; } = string.Empty;

        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("insulinDelivered")]
        public double InsulinDelivered { get; set; }

        [JsonPropertyName("carbsInput")]
        public double CarbsInput { get; set; }

        [JsonPropertyName("deliveredUnits")]
        public double DeliveredUnits { get; set; }

        [JsonPropertyName("programmedUnits")]
        public double ProgrammedUnits { get; set; }
    }

    public class GlookoCgmReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("trend")]
        public string? Trend { get; set; }
    }

    public class GlookoBloodGlucoseReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("units")]
        public string Units { get; set; } = "mg/dL";

        [JsonPropertyName("meterType")]
        public string MeterType { get; set; } = "fingerstick";

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }

        [JsonPropertyName("testStripLot")]
        public string? TestStripLot { get; set; }

        [JsonPropertyName("qualityFlag")]
        public string? QualityFlag { get; set; }
    }

    public class GlookoKetoneReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("ketoneType")]
        public string KetoneType { get; set; } = "blood";

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }

        [JsonPropertyName("testStripType")]
        public string? TestStripType { get; set; }

        [JsonPropertyName("qualityFlag")]
        public string? QualityFlag { get; set; }
    }

    public class GlookoBloodPressureReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("systolic")]
        public int Systolic { get; set; }

        [JsonPropertyName("diastolic")]
        public int Diastolic { get; set; }

        [JsonPropertyName("pulse")]
        public int? Pulse { get; set; }

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }

        [JsonPropertyName("cuffSize")]
        public string? CuffSize { get; set; }

        [JsonPropertyName("irregularHeartbeat")]
        public bool? IrregularHeartbeat { get; set; }
    }

    public class GlookoWeightReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("weight")]
        public double Weight { get; set; }

        [JsonPropertyName("units")]
        public string Units { get; set; } = "kg";

        [JsonPropertyName("bmi")]
        public double? BMI { get; set; }

        [JsonPropertyName("bodyFatPercentage")]
        public double? BodyFatPercentage { get; set; }

        [JsonPropertyName("muscleMass")]
        public double? MuscleMass { get; set; }

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }
    }

    public class GlookoSleepReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("sleepStart")]
        public string SleepStart { get; set; } = string.Empty;

        [JsonPropertyName("sleepEnd")]
        public string SleepEnd { get; set; } = string.Empty;

        [JsonPropertyName("totalSleepMinutes")]
        public int TotalSleepMinutes { get; set; }

        [JsonPropertyName("lightSleepMinutes")]
        public int? LightSleepMinutes { get; set; }

        [JsonPropertyName("deepSleepMinutes")]
        public int? DeepSleepMinutes { get; set; }

        [JsonPropertyName("remSleepMinutes")]
        public int? RemSleepMinutes { get; set; }

        [JsonPropertyName("sleepEfficiency")]
        public double? SleepEfficiency { get; set; }

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }
    }

    public class GlookoActivityReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("steps")]
        public int Steps { get; set; }

        [JsonPropertyName("distance")]
        public double Distance { get; set; }

        [JsonPropertyName("caloriesBurned")]
        public double CaloriesBurned { get; set; }

        [JsonPropertyName("activeMinutes")]
        public int ActiveMinutes { get; set; }

        [JsonPropertyName("averageHeartRate")]
        public int? AverageHeartRate { get; set; }

        [JsonPropertyName("activityType")]
        public string? ActivityType { get; set; }

        [JsonPropertyName("deviceModel")]
        public string? DeviceModel { get; set; }
    }

    public class GlookoMedicationReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("medicationName")]
        public string MedicationName { get; set; } = string.Empty;

        [JsonPropertyName("dosage")]
        public double Dosage { get; set; }

        [JsonPropertyName("dosageUnit")]
        public string DosageUnit { get; set; } = "mg";

        [JsonPropertyName("wasTaken")]
        public bool WasTaken { get; set; }

        [JsonPropertyName("scheduledTime")]
        public string? ScheduledTime { get; set; }

        [JsonPropertyName("actualTime")]
        public string? ActualTime { get; set; }
    }

    public class GlookoInsulinPenReading
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("unitsDelivered")]
        public double UnitsDelivered { get; set; }

        [JsonPropertyName("insulinType")]
        public string? InsulinType { get; set; }

        [JsonPropertyName("insulinBrand")]
        public string? InsulinBrand { get; set; }

        [JsonPropertyName("penModel")]
        public string? PenModel { get; set; }

        [JsonPropertyName("cartridgeRemaining")]
        public double? CartridgeRemaining { get; set; }

        [JsonPropertyName("injectionSite")]
        public string? InjectionSite { get; set; }

        [JsonPropertyName("temperature")]
        public double? Temperature { get; set; }
    }

    public class GlookoExtendedBolus
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("bolusAmount")]
        public double BolusAmount { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("extendedAmount")]
        public double ExtendedAmount { get; set; }
    }

    public class GlookoSuspendBasal
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("suspendReason")]
        public string? SuspendReason { get; set; }
    }

    public class GlookoTempBasal
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("rate")]
        public double Rate { get; set; }

        [JsonPropertyName("duration")]
        public int Duration { get; set; }

        [JsonPropertyName("percent")]
        public int? Percent { get; set; }

        [JsonPropertyName("tempBasalType")]
        public string? TempBasalType { get; set; }
    }

    public class GlookoPumpSettings
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("settings")]
        public object? Settings { get; set; }
    }

    public class GlookoPumpAlarm
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("alarmCode")]
        public string? AlarmCode { get; set; }

        [JsonPropertyName("alarmDescription")]
        public string? AlarmDescription { get; set; }
    }

    public class GlookoPumpEvent
    {
        [JsonPropertyName("timestamp")]
        public string Timestamp { get; set; } = string.Empty;

        [JsonPropertyName("eventType")]
        public string EventType { get; set; } = string.Empty;

        [JsonPropertyName("eventData")]
        public object? EventData { get; set; }
    }
}
