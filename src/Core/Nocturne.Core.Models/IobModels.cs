namespace Nocturne.Core.Models;

/// <summary>
/// IOB contribution from a single treatment
/// </summary>
public class IobContribution
{
    public double IobContrib { get; set; }
    public double ActivityContrib { get; set; }
}

/// <summary>
/// Insulin on Board (IOB) calculation result
/// </summary>
public class IobResult
{
    public double Iob { get; set; }
    public double? Activity { get; set; }
    public Treatment? LastBolus { get; set; }
    public string? Source { get; set; }
    public string? Device { get; set; }
    public long? Mills { get; set; }
    public double? BasalIob { get; set; }
    public double? TreatmentIob { get; set; }
    public string? Display { get; set; }
    public string? DisplayLine { get; set; }
}

/// <summary>
/// Temporary basal calculation result
/// </summary>
public class TempBasalResult
{
    public double Basal { get; set; }
    public Treatment? Treatment { get; set; }
    public Treatment? ComboBolusTreatment { get; set; }
    public double TempBasal { get; set; }
    public double ComboBolusBasal { get; set; }
    public double TotalBasal { get; set; }
}
