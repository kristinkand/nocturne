//! Glucose data types

#[cfg(feature = "serde")]
use serde::{Deserialize, Serialize};

/// Single glucose reading from CGM
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct GlucoseReading {
    /// Glucose value (mg/dL)
    #[cfg_attr(feature = "serde", serde(alias = "sgv"))]
    pub glucose: f64,

    /// Unix milliseconds
    #[cfg_attr(feature = "serde", serde(default))]
    pub date: i64,

    /// ISO date string
    #[cfg_attr(feature = "serde", serde(default))]
    pub date_string: Option<String>,

    /// Display time string
    #[cfg_attr(feature = "serde", serde(default))]
    pub display_time: Option<String>,

    /// Noise level (0-4)
    #[cfg_attr(feature = "serde", serde(default))]
    pub noise: Option<f64>,

    /// Direction arrow
    #[cfg_attr(feature = "serde", serde(default))]
    pub direction: Option<String>,
}

impl GlucoseReading {
    /// Create a new glucose reading
    pub fn new(glucose: f64, date: i64) -> Self {
        Self {
            glucose,
            date,
            date_string: None,
            display_time: None,
            noise: None,
            direction: None,
        }
    }

    /// Check if this is a valid reading (not too low for calibration)
    pub fn is_valid(&self) -> bool {
        self.glucose >= 39.0
    }
}

/// Current glucose status with deltas
#[derive(Debug, Clone, Default)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct GlucoseStatus {
    /// Current glucose (mg/dL)
    pub glucose: f64,

    /// 5-minute delta (mg/dL)
    pub delta: f64,

    /// Short average delta (15 min)
    #[cfg_attr(feature = "serde", serde(default))]
    pub short_avgdelta: f64,

    /// Long average delta (45 min)
    #[cfg_attr(feature = "serde", serde(default))]
    pub long_avgdelta: f64,

    /// Unix milliseconds of reading
    #[cfg_attr(feature = "serde", serde(default))]
    pub date: i64,

    /// Noise level
    #[cfg_attr(feature = "serde", serde(default))]
    pub noise: Option<f64>,
}

impl GlucoseStatus {
    /// Create a new glucose status
    pub fn new(glucose: f64, delta: f64) -> Self {
        Self {
            glucose,
            delta,
            short_avgdelta: delta,
            long_avgdelta: delta,
            date: 0,
            noise: None,
        }
    }

    /// Create from glucose readings
    pub fn from_readings(readings: &[GlucoseReading]) -> Option<Self> {
        if readings.is_empty() {
            return None;
        }

        let current = &readings[0];

        // Calculate delta from most recent two readings
        let delta = if readings.len() >= 2 && readings[1].is_valid() {
            current.glucose - readings[1].glucose
        } else {
            0.0
        };

        // Calculate short average delta (last 3 readings, ~15 min)
        let short_avgdelta = if readings.len() >= 4 {
            let valid: Vec<_> = readings[0..4].iter().filter(|r| r.is_valid()).collect();
            if valid.len() >= 2 {
                (valid[0].glucose - valid[valid.len() - 1].glucose) / (valid.len() - 1) as f64
            } else {
                delta
            }
        } else {
            delta
        };

        // Calculate long average delta (last 9 readings, ~45 min)
        let long_avgdelta = if readings.len() >= 10 {
            let valid: Vec<_> = readings[0..10].iter().filter(|r| r.is_valid()).collect();
            if valid.len() >= 2 {
                (valid[0].glucose - valid[valid.len() - 1].glucose) / (valid.len() - 1) as f64
            } else {
                delta
            }
        } else {
            short_avgdelta
        };

        Some(Self {
            glucose: current.glucose,
            delta,
            short_avgdelta,
            long_avgdelta,
            date: current.date,
            noise: current.noise,
        })
    }

    /// Check if glucose is below a threshold
    pub fn is_below(&self, threshold: f64) -> bool {
        self.glucose < threshold
    }

    /// Check if glucose is rising
    pub fn is_rising(&self) -> bool {
        self.delta > 0.0
    }

    /// Check if glucose is falling
    pub fn is_falling(&self) -> bool {
        self.delta < 0.0
    }

    /// Get the trend based on delta
    pub fn trend(&self) -> GlucoseTrend {
        if self.delta > 3.0 {
            GlucoseTrend::RisingFast
        } else if self.delta > 1.0 {
            GlucoseTrend::Rising
        } else if self.delta > -1.0 {
            GlucoseTrend::Flat
        } else if self.delta > -3.0 {
            GlucoseTrend::Falling
        } else {
            GlucoseTrend::FallingFast
        }
    }
}

/// Glucose trend direction
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum GlucoseTrend {
    RisingFast,
    Rising,
    Flat,
    Falling,
    FallingFast,
}

impl GlucoseTrend {
    /// Get the trend arrow symbol
    pub fn arrow(&self) -> &'static str {
        match self {
            GlucoseTrend::RisingFast => "↑↑",
            GlucoseTrend::Rising => "↑",
            GlucoseTrend::Flat => "→",
            GlucoseTrend::Falling => "↓",
            GlucoseTrend::FallingFast => "↓↓",
        }
    }
}
