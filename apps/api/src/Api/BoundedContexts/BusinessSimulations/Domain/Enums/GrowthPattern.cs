namespace Api.BoundedContexts.BusinessSimulations.Domain.Enums;

/// <summary>
/// Growth pattern for resource forecasting projections.
/// Issue #3726: Resource Forecasting Simulator (Epic #3688)
/// </summary>
public enum GrowthPattern
{
    /// <summary>Constant growth rate per month</summary>
    Linear = 0,

    /// <summary>Compounding growth rate per month</summary>
    Exponential = 1,

    /// <summary>Rapid initial growth that slows over time</summary>
    Logarithmic = 2,

    /// <summary>S-curve: slow start, rapid middle, plateau</summary>
    SCurve = 3
}
