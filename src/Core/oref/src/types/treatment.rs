//! Treatment types representing insulin deliveries and carb entries

use chrono::{DateTime, Utc};

#[cfg(feature = "serde")]
use serde::{Deserialize, Serialize};

/// Represents any treatment event (bolus, temp basal, carbs, etc.)
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct Treatment {
    /// ISO timestamp string
    #[cfg_attr(feature = "serde", serde(default))]
    pub timestamp: Option<String>,

    /// Unix milliseconds
    #[cfg_attr(feature = "serde", serde(default))]
    pub date: i64,

    /// Started at timestamp (for temp basals)
    #[cfg_attr(feature = "serde", serde(default))]
    pub started_at: Option<String>,

    /// Created at timestamp
    #[cfg_attr(feature = "serde", serde(default))]
    pub created_at: Option<String>,

    /// Insulin amount (units) - for boluses
    #[cfg_attr(feature = "serde", serde(default))]
    pub insulin: Option<f64>,

    /// Carb amount (grams)
    #[cfg_attr(feature = "serde", serde(default))]
    pub carbs: Option<f64>,

    /// Nightscout carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub ns_carbs: Option<f64>,

    /// Bolus Wizard carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub bw_carbs: Option<f64>,

    /// Journal carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub journal_carbs: Option<f64>,

    /// Temp basal rate (U/hr)
    #[cfg_attr(feature = "serde", serde(default))]
    pub rate: Option<f64>,

    /// Duration in minutes
    #[cfg_attr(feature = "serde", serde(default))]
    pub duration: Option<f64>,

    /// Event type string
    #[cfg_attr(feature = "serde", serde(rename = "_type", default))]
    pub event_type: Option<String>,
}

impl Default for Treatment {
    fn default() -> Self {
        Self {
            timestamp: None,
            date: 0,
            started_at: None,
            created_at: None,
            insulin: None,
            carbs: None,
            ns_carbs: None,
            bw_carbs: None,
            journal_carbs: None,
            rate: None,
            duration: None,
            event_type: None,
        }
    }
}

impl Treatment {
    /// Create a bolus treatment
    pub fn bolus(insulin: f64, timestamp: DateTime<Utc>) -> Self {
        Self {
            insulin: Some(insulin),
            date: timestamp.timestamp_millis(),
            timestamp: Some(timestamp.to_rfc3339()),
            started_at: Some(timestamp.to_rfc3339()),
            event_type: Some("Bolus".to_string()),
            ..Default::default()
        }
    }

    /// Create a temp basal treatment
    pub fn temp_basal(rate: f64, duration: f64, timestamp: DateTime<Utc>) -> Self {
        Self {
            rate: Some(rate),
            duration: Some(duration),
            date: timestamp.timestamp_millis(),
            timestamp: Some(timestamp.to_rfc3339()),
            started_at: Some(timestamp.to_rfc3339()),
            event_type: Some("TempBasal".to_string()),
            ..Default::default()
        }
    }

    /// Create a carb entry
    pub fn carbs(carbs: f64, timestamp: DateTime<Utc>) -> Self {
        Self {
            carbs: Some(carbs),
            ns_carbs: Some(carbs),
            date: timestamp.timestamp_millis(),
            timestamp: Some(timestamp.to_rfc3339()),
            event_type: Some("Carbs".to_string()),
            ..Default::default()
        }
    }

    /// Check if this is a bolus
    pub fn is_bolus(&self) -> bool {
        self.insulin.map_or(false, |i| i > 0.0)
    }

    /// Check if this is a temp basal
    pub fn is_temp_basal(&self) -> bool {
        self.rate.is_some() && self.duration.is_some()
    }

    /// Check if this is a carb entry
    pub fn has_carbs(&self) -> bool {
        self.carbs.map_or(false, |c| c >= 1.0)
    }

    /// Get the effective date as Unix millis
    pub fn effective_date(&self) -> i64 {
        if self.date != 0 {
            return self.date;
        }

        // Try to parse from timestamp strings
        if let Some(ref ts) = self.started_at {
            if let Ok(dt) = DateTime::parse_from_rfc3339(ts) {
                return dt.timestamp_millis();
            }
        }
        if let Some(ref ts) = self.timestamp {
            if let Ok(dt) = DateTime::parse_from_rfc3339(ts) {
                return dt.timestamp_millis();
            }
        }

        0
    }
}

/// Pump history event types (matching Medtronic format)
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct PumpHistoryEvent {
    /// Event type
    #[cfg_attr(feature = "serde", serde(rename = "_type"))]
    pub event_type: String,

    /// Timestamp
    pub timestamp: String,

    /// Bolus amount (for Bolus events)
    #[cfg_attr(feature = "serde", serde(default))]
    pub amount: Option<f64>,

    /// Programmed amount
    #[cfg_attr(feature = "serde", serde(default))]
    pub programmed: Option<f64>,

    /// Duration in minutes (for TempBasal events)
    #[cfg_attr(feature = "serde", serde(default))]
    pub duration: Option<f64>,

    /// Rate (for TempBasal events)
    #[cfg_attr(feature = "serde", serde(default))]
    pub rate: Option<f64>,

    /// Temp type (absolute or percent)
    #[cfg_attr(feature = "serde", serde(default))]
    pub temp: Option<String>,
}

impl PumpHistoryEvent {
    /// Convert to a Treatment
    pub fn to_treatment(&self) -> Option<Treatment> {
        let date = DateTime::parse_from_rfc3339(&self.timestamp)
            .ok()?
            .timestamp_millis();

        Some(match self.event_type.as_str() {
            "Bolus" => Treatment {
                insulin: self.amount.or(self.programmed),
                date,
                timestamp: Some(self.timestamp.clone()),
                started_at: Some(self.timestamp.clone()),
                event_type: Some("Bolus".to_string()),
                ..Default::default()
            },
            "TempBasal" | "TempBasalDuration" => Treatment {
                rate: self.rate,
                duration: self.duration,
                date,
                timestamp: Some(self.timestamp.clone()),
                started_at: Some(self.timestamp.clone()),
                event_type: Some("TempBasal".to_string()),
                ..Default::default()
            },
            _ => return None,
        })
    }
}

/// Current temp basal state
#[derive(Debug, Clone, Default)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
pub struct CurrentTemp {
    /// Duration remaining (minutes)
    pub duration: f64,

    /// Rate (U/hr)
    pub rate: f64,

    /// Type: "absolute" or "percent"
    #[cfg_attr(feature = "serde", serde(default = "default_temp_type"))]
    pub temp: String,
}

fn default_temp_type() -> String {
    "absolute".to_string()
}

impl CurrentTemp {
    /// Create a new absolute temp basal
    pub fn absolute(rate: f64, duration: f64) -> Self {
        Self {
            rate,
            duration,
            temp: "absolute".to_string(),
        }
    }

    /// Create an inactive/zero temp
    pub fn none() -> Self {
        Self {
            rate: 0.0,
            duration: 0.0,
            temp: "absolute".to_string(),
        }
    }

    /// Check if a temp is currently active
    pub fn is_active(&self) -> bool {
        self.duration > 0.0
    }
}

/// Temporary target for blood glucose
#[derive(Debug, Clone, Default)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct TempTarget {
    /// Created at timestamp (Unix millis)
    pub created_at: i64,

    /// Duration in minutes (0 = cancelled)
    pub duration: u32,

    /// Lower bound of target range (mg/dL)
    #[cfg_attr(feature = "serde", serde(alias = "targetBottom"))]
    pub target_bottom: f64,

    /// Upper bound of target range (mg/dL)
    #[cfg_attr(feature = "serde", serde(alias = "targetTop"))]
    pub target_top: f64,

    /// Reason for temp target
    #[cfg_attr(feature = "serde", serde(default))]
    pub reason: Option<String>,
}

impl TempTarget {
    /// Create a new temp target
    pub fn new(target_bottom: f64, target_top: f64, duration: u32, created_at: i64) -> Self {
        Self {
            created_at,
            duration,
            target_bottom,
            target_top,
            reason: None,
        }
    }

    /// Get the midpoint of the target range
    pub fn midpoint(&self) -> f64 {
        (self.target_top + self.target_bottom) / 2.0
    }

    /// Check if this is a high temp target (eating soon, exercise)
    pub fn is_high(&self) -> bool {
        self.midpoint() > 100.0
    }

    /// Check if this is a low temp target
    pub fn is_low(&self) -> bool {
        self.midpoint() < 100.0
    }

    /// Check if this cancels temp targets
    pub fn is_cancelled(&self) -> bool {
        self.duration == 0
    }
}
