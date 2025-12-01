//! WebAssembly bindings for Nocturne integration
//!
//! This module provides WASM-compatible interfaces to the oref algorithms,
//! using JSON for data exchange with JavaScript/TypeScript callers.

use wasm_bindgen::prelude::*;
use serde::{Deserialize, Serialize};
use crate::types::{
    AutosensData, CurrentTemp, GlucoseReading,
    GlucoseStatus, IOBData, MealData, Profile, Treatment,
};
use crate::determine_basal::DetermineBasalInputs;
use chrono::DateTime;

// ============================================================================
// IOB Calculation
// ============================================================================

/// Calculate Insulin on Board (IOB) from treatment history
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
/// * `current_only` - If true, only calculate current IOB (faster)
///
/// # Returns
/// JSON string containing IOBData array, or error message
#[wasm_bindgen]
pub fn calculate_iob(
    profile_json: &str,
    treatments_json: &str,
    time_millis: i64,
    current_only: bool,
) -> Result<String, JsValue> {
    let profile: Profile = serde_json::from_str(profile_json)
        .map_err(|e| JsValue::from_str(&format!("Profile parse error: {}", e)))?;

    let treatments: Vec<Treatment> = serde_json::from_str(treatments_json)
        .map_err(|e| JsValue::from_str(&format!("Treatments parse error: {}", e)))?;

    let time = DateTime::from_timestamp_millis(time_millis)
        .ok_or_else(|| JsValue::from_str("Invalid timestamp"))?;

    let iob_array = crate::iob::calculate(&profile, &treatments, time, current_only)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    serde_json::to_string(&iob_array)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

/// Calculate current IOB only (optimized for single-point calculation)
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
///
/// # Returns
/// JSON string containing single IOBData object
#[wasm_bindgen]
pub fn calculate_iob_current(
    profile_json: &str,
    treatments_json: &str,
    time_millis: i64,
) -> Result<String, JsValue> {
    calculate_iob(profile_json, treatments_json, time_millis, true)
}

// ============================================================================
// COB Calculation
// ============================================================================

/// Calculate Carbs on Board (COB) from glucose and treatment history
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `glucose_json` - JSON string containing array of GlucoseReading objects
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
///
/// # Returns
/// JSON string containing COBResult
#[wasm_bindgen]
pub fn calculate_cob(
    profile_json: &str,
    glucose_json: &str,
    treatments_json: &str,
    time_millis: i64,
) -> Result<String, JsValue> {
    let profile: Profile = serde_json::from_str(profile_json)
        .map_err(|e| JsValue::from_str(&format!("Profile parse error: {}", e)))?;

    let glucose: Vec<GlucoseReading> = serde_json::from_str(glucose_json)
        .map_err(|e| JsValue::from_str(&format!("Glucose parse error: {}", e)))?;

    let treatments: Vec<Treatment> = serde_json::from_str(treatments_json)
        .map_err(|e| JsValue::from_str(&format!("Treatments parse error: {}", e)))?;

    let time = DateTime::from_timestamp_millis(time_millis)
        .ok_or_else(|| JsValue::from_str("Invalid timestamp"))?;

    let cob = crate::cob::calculate(&profile, &glucose, &treatments, time)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    serde_json::to_string(&cob)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

// ============================================================================
// Autosens Calculation
// ============================================================================

/// Calculate autosens ratio from glucose and treatment history
///
/// Detects changes in insulin sensitivity over time by analyzing
/// glucose deviations from expected values.
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `glucose_json` - JSON string containing array of GlucoseReading objects (24 hours)
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
///
/// # Returns
/// JSON string containing AutosensData with sensitivity ratio
#[wasm_bindgen]
pub fn calculate_autosens(
    profile_json: &str,
    glucose_json: &str,
    treatments_json: &str,
    time_millis: i64,
) -> Result<String, JsValue> {
    let profile: Profile = serde_json::from_str(profile_json)
        .map_err(|e| JsValue::from_str(&format!("Profile parse error: {}", e)))?;

    let glucose: Vec<GlucoseReading> = serde_json::from_str(glucose_json)
        .map_err(|e| JsValue::from_str(&format!("Glucose parse error: {}", e)))?;

    let treatments: Vec<Treatment> = serde_json::from_str(treatments_json)
        .map_err(|e| JsValue::from_str(&format!("Treatments parse error: {}", e)))?;

    let time = DateTime::from_timestamp_millis(time_millis)
        .ok_or_else(|| JsValue::from_str("Invalid timestamp"))?;

    let autosens = crate::autosens::detect_sensitivity(&profile, &glucose, &treatments, time)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    serde_json::to_string(&autosens)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

// ============================================================================
// Determine Basal (Main Algorithm)
// ============================================================================

/// Combined inputs for determine_basal as a JSON-serializable struct
#[derive(Debug, Clone, Serialize, Deserialize)]
#[serde(rename_all = "camelCase")]
pub struct DetermineBasalInputsJson {
    /// Current glucose status
    pub glucose_status: GlucoseStatus,

    /// Current temp basal
    pub current_temp: CurrentTemp,

    /// Current IOB data
    pub iob_data: IOBData,

    /// User profile
    pub profile: Profile,

    /// Autosens data
    #[serde(default)]
    pub autosens_data: AutosensData,

    /// Meal data
    #[serde(default)]
    pub meal_data: MealData,

    /// Whether micro bolus (SMB) is allowed
    #[serde(default)]
    pub micro_bolus_allowed: bool,

    /// Current time in milliseconds (optional, defaults to now)
    #[serde(default)]
    pub current_time_millis: Option<i64>,
}

/// Run the determine-basal algorithm
///
/// This is the main dosing algorithm that determines optimal temp basals and SMBs.
///
/// # Arguments
/// * `inputs_json` - JSON string containing DetermineBasalInputsJson
///
/// # Returns
/// JSON string containing DetermineBasalResult
#[wasm_bindgen]
pub fn determine_basal(inputs_json: &str) -> Result<String, JsValue> {
    let inputs: DetermineBasalInputsJson = serde_json::from_str(inputs_json)
        .map_err(|e| JsValue::from_str(&format!("Inputs parse error: {}", e)))?;

    let current_time = inputs.current_time_millis
        .and_then(DateTime::from_timestamp_millis);

    let algo_inputs = DetermineBasalInputs {
        glucose_status: &inputs.glucose_status,
        current_temp: &inputs.current_temp,
        iob_data: &inputs.iob_data,
        profile: &inputs.profile,
        autosens_data: &inputs.autosens_data,
        meal_data: &inputs.meal_data,
        micro_bolus_allowed: inputs.micro_bolus_allowed,
        current_time,
    };

    let result = crate::determine_basal::determine_basal(&algo_inputs)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    serde_json::to_string(&result)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

/// Convenience function to run determine_basal with individual parameters
///
/// This allows calling without constructing the full inputs JSON
#[wasm_bindgen]
pub fn determine_basal_simple(
    profile_json: &str,
    glucose_status_json: &str,
    iob_data_json: &str,
    current_temp_json: &str,
    autosens_ratio: f64,
    meal_cob: f64,
    micro_bolus_allowed: bool,
) -> Result<String, JsValue> {
    let profile: Profile = serde_json::from_str(profile_json)
        .map_err(|e| JsValue::from_str(&format!("Profile parse error: {}", e)))?;

    let glucose_status: GlucoseStatus = serde_json::from_str(glucose_status_json)
        .map_err(|e| JsValue::from_str(&format!("GlucoseStatus parse error: {}", e)))?;

    let iob_data: IOBData = serde_json::from_str(iob_data_json)
        .map_err(|e| JsValue::from_str(&format!("IOBData parse error: {}", e)))?;

    let current_temp: CurrentTemp = serde_json::from_str(current_temp_json)
        .map_err(|e| JsValue::from_str(&format!("CurrentTemp parse error: {}", e)))?;

    let autosens_data = AutosensData::with_ratio(autosens_ratio);
    let meal_data = MealData::with_cob(meal_cob, 0.0);

    let inputs = DetermineBasalInputs {
        glucose_status: &glucose_status,
        current_temp: &current_temp,
        iob_data: &iob_data,
        profile: &profile,
        autosens_data: &autosens_data,
        meal_data: &meal_data,
        micro_bolus_allowed,
        current_time: None,
    };

    let result = crate::determine_basal::determine_basal(&inputs)
        .map_err(|e| JsValue::from_str(&e.to_string()))?;

    serde_json::to_string(&result)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

// ============================================================================
// Glucose Status Helper
// ============================================================================

/// Calculate glucose status from readings
///
/// # Arguments
/// * `glucose_json` - JSON string containing array of GlucoseReading objects (most recent first)
///
/// # Returns
/// JSON string containing GlucoseStatus
#[wasm_bindgen]
pub fn calculate_glucose_status(glucose_json: &str) -> Result<String, JsValue> {
    let readings: Vec<GlucoseReading> = serde_json::from_str(glucose_json)
        .map_err(|e| JsValue::from_str(&format!("Glucose parse error: {}", e)))?;

    let status = GlucoseStatus::from_readings(&readings)
        .ok_or_else(|| JsValue::from_str("No valid glucose readings"))?;

    serde_json::to_string(&status)
        .map_err(|e| JsValue::from_str(&format!("Serialization error: {}", e)))
}

// ============================================================================
// Version and Info
// ============================================================================

/// Get the oref library version
#[wasm_bindgen]
pub fn oref_version() -> String {
    env!("CARGO_PKG_VERSION").to_string()
}

/// Check if the WASM module is loaded correctly
#[wasm_bindgen]
pub fn oref_health_check() -> String {
    r#"{"status":"ok","version":""#.to_string()
        + env!("CARGO_PKG_VERSION")
        + r#"","features":["iob","cob","autosens","determine_basal"]}"#
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_health_check() {
        let result = oref_health_check();
        assert!(result.contains("ok"));
        assert!(result.contains("iob"));
    }

    #[test]
    fn test_version() {
        let version = oref_version();
        assert!(!version.is_empty());
    }
}
