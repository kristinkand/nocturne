//! IOB (Insulin on Board) data types

use chrono::{DateTime, Utc};

#[cfg(feature = "serde")]
use serde::{Deserialize, Serialize};

/// Complete IOB data for a point in time
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct IOBData {
    /// Total IOB (units)
    pub iob: f64,

    /// Insulin activity (units/minute)
    pub activity: f64,

    /// IOB from basal adjustments
    #[cfg_attr(feature = "serde", serde(default))]
    pub basal_iob: f64,

    /// IOB from boluses
    #[cfg_attr(feature = "serde", serde(default))]
    pub bolus_iob: f64,

    /// Net basal insulin delivered
    #[cfg_attr(feature = "serde", serde(default))]
    pub net_basal_insulin: f64,

    /// Total bolus insulin delivered
    #[cfg_attr(feature = "serde", serde(default))]
    pub bolus_insulin: f64,

    /// Time of calculation (Unix milliseconds, defaults to current time)
    #[cfg_attr(feature = "serde", serde(
        default = "default_time",
        with = "chrono::serde::ts_milliseconds"
    ))]
    pub time: DateTime<Utc>,

    /// IOB if zero temp is continued
    #[cfg_attr(feature = "serde", serde(default))]
    pub iob_with_zero_temp: Option<Box<IOBData>>,

    /// Time of last bolus (Unix millis)
    #[cfg_attr(feature = "serde", serde(default))]
    pub last_bolus_time: Option<i64>,

    /// Last temp basal state
    #[cfg_attr(feature = "serde", serde(default))]
    pub last_temp: Option<TempBasalState>,
}

fn default_time() -> DateTime<Utc> {
    Utc::now()
}

impl Default for IOBData {
    fn default() -> Self {
        Self {
            iob: 0.0,
            activity: 0.0,
            basal_iob: 0.0,
            bolus_iob: 0.0,
            net_basal_insulin: 0.0,
            bolus_insulin: 0.0,
            time: Utc::now(),
            iob_with_zero_temp: None,
            last_bolus_time: None,
            last_temp: None,
        }
    }
}

impl IOBData {
    /// Create a zero IOB state
    pub fn zero(time: DateTime<Utc>) -> Self {
        Self {
            time,
            ..Default::default()
        }
    }

    /// Round values to 3/4 decimal places (matching JS implementation)
    pub fn rounded(mut self) -> Self {
        self.iob = (self.iob * 1000.0).round() / 1000.0;
        self.activity = (self.activity * 10000.0).round() / 10000.0;
        self.basal_iob = (self.basal_iob * 1000.0).round() / 1000.0;
        self.bolus_iob = (self.bolus_iob * 1000.0).round() / 1000.0;
        self.net_basal_insulin = (self.net_basal_insulin * 1000.0).round() / 1000.0;
        self.bolus_insulin = (self.bolus_insulin * 1000.0).round() / 1000.0;
        self
    }
}

/// Contribution from a single treatment to IOB/activity
#[derive(Debug, Clone, Default)]
pub struct IOBContrib {
    /// IOB contribution (units)
    pub iob_contrib: f64,

    /// Activity contribution (units/minute)
    pub activity_contrib: f64,
}

impl IOBContrib {
    /// Create a zero contribution
    pub fn zero() -> Self {
        Self::default()
    }

    /// Create a new contribution
    pub fn new(iob_contrib: f64, activity_contrib: f64) -> Self {
        Self {
            iob_contrib,
            activity_contrib,
        }
    }
}

/// State of last temp basal
#[derive(Debug, Clone, Default)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
pub struct TempBasalState {
    /// Start time (Unix millis)
    pub date: i64,

    /// Duration (minutes)
    pub duration: f64,

    /// Rate (U/hr), if absolute
    #[cfg_attr(feature = "serde", serde(default))]
    pub rate: Option<f64>,
}

impl TempBasalState {
    /// Create a new temp basal state
    pub fn new(date: i64, duration: f64, rate: Option<f64>) -> Self {
        Self { date, duration, rate }
    }
}
