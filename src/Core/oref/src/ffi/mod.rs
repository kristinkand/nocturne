//! C FFI bindings for iOS/Swift (Trio) integration
//!
//! This module provides C-compatible function interfaces that can be called from
//! Swift or Objective-C code. All functions use null-terminated C strings for
//! JSON input/output.
//!
//! # Memory Management
//!
//! Strings returned by these functions are allocated by Rust and must be freed
//! by calling `oref_free_string()`. Failure to do so will result in memory leaks.
//!
//! # Thread Safety
//!
//! All functions are thread-safe and can be called from multiple threads concurrently.
//!
//! # Error Handling
//!
//! On success, functions return a JSON string with the result.
//! On error, functions return a JSON string with an "error" field containing
//! the error message.

use std::ffi::{CStr, CString};
use std::os::raw::c_char;
use chrono::DateTime;

use crate::types::{
    AutosensData, CurrentTemp, GlucoseReading, GlucoseStatus,
    IOBData, MealData, Profile, Treatment,
};
use crate::determine_basal::DetermineBasalInputs;

// ============================================================================
// Helper Functions
// ============================================================================

/// Convert a C string pointer to a Rust string slice, returning None if invalid
unsafe fn c_str_to_rust(ptr: *const c_char) -> Option<&'static str> {
    if ptr.is_null() {
        return None;
    }
    CStr::from_ptr(ptr).to_str().ok()
}

/// Allocate a new C string from a Rust string, returning null on failure
fn rust_to_c_string(s: String) -> *mut c_char {
    CString::new(s)
        .map(|cs| cs.into_raw())
        .unwrap_or(std::ptr::null_mut())
}

/// Create an error JSON response
fn error_json(message: &str) -> *mut c_char {
    rust_to_c_string(format!(r#"{{"error":"{}"}}"#, message.replace('"', "\\\"")))
}

// ============================================================================
// Memory Management
// ============================================================================

/// Free a string that was returned by an oref function.
///
/// # Safety
///
/// The pointer must have been returned by one of the oref functions,
/// and must not be used after this call.
#[no_mangle]
pub unsafe extern "C" fn oref_free_string(ptr: *mut c_char) {
    if !ptr.is_null() {
        drop(CString::from_raw(ptr));
    }
}

// ============================================================================
// Version and Info
// ============================================================================

/// Get the oref library version.
///
/// # Returns
/// A null-terminated string containing the version. Must be freed with `oref_free_string`.
#[no_mangle]
pub extern "C" fn oref_version() -> *mut c_char {
    rust_to_c_string(env!("CARGO_PKG_VERSION").to_string())
}

/// Health check to verify the library is loaded correctly.
///
/// # Returns
/// A JSON string containing status information. Must be freed with `oref_free_string`.
#[no_mangle]
pub extern "C" fn oref_health_check() -> *mut c_char {
    rust_to_c_string(format!(
        r#"{{"status":"ok","version":"{}","features":["iob","cob","autosens","determine_basal"]}}"#,
        env!("CARGO_PKG_VERSION")
    ))
}

// ============================================================================
// IOB Calculation
// ============================================================================

/// Calculate Insulin on Board (IOB) from treatment history.
///
/// # Safety
///
/// - `profile_json` must be a valid null-terminated UTF-8 string containing Profile JSON
/// - `treatments_json` must be a valid null-terminated UTF-8 string containing Treatment[] JSON
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
/// * `current_only` - If 1, only calculate current IOB (faster); if 0, calculate full array
///
/// # Returns
/// JSON string containing IOBData array. Must be freed with `oref_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oref_calculate_iob(
    profile_json: *const c_char,
    treatments_json: *const c_char,
    time_millis: i64,
    current_only: i32,
) -> *mut c_char {
    let Some(profile_str) = c_str_to_rust(profile_json) else {
        return error_json("Invalid profile_json pointer");
    };
    let Some(treatments_str) = c_str_to_rust(treatments_json) else {
        return error_json("Invalid treatments_json pointer");
    };

    let profile: Profile = match serde_json::from_str(profile_str) {
        Ok(p) => p,
        Err(e) => return error_json(&format!("Profile parse error: {}", e)),
    };

    let treatments: Vec<Treatment> = match serde_json::from_str(treatments_str) {
        Ok(t) => t,
        Err(e) => return error_json(&format!("Treatments parse error: {}", e)),
    };

    let Some(time) = DateTime::from_timestamp_millis(time_millis) else {
        return error_json("Invalid timestamp");
    };

    match crate::iob::calculate(&profile, &treatments, time, current_only != 0) {
        Ok(iob_array) => match serde_json::to_string(&iob_array) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        Err(e) => error_json(&e.to_string()),
    }
}

/// Calculate current IOB only (optimized single-point calculation).
///
/// # Safety
///
/// Same requirements as `oref_calculate_iob`.
#[no_mangle]
pub unsafe extern "C" fn oref_calculate_iob_current(
    profile_json: *const c_char,
    treatments_json: *const c_char,
    time_millis: i64,
) -> *mut c_char {
    oref_calculate_iob(profile_json, treatments_json, time_millis, 1)
}

// ============================================================================
// COB Calculation
// ============================================================================

/// Calculate Carbs on Board (COB) from glucose and treatment history.
///
/// # Safety
///
/// - `profile_json` must be a valid null-terminated UTF-8 string
/// - `glucose_json` must be a valid null-terminated UTF-8 string containing GlucoseReading[] JSON
/// - `treatments_json` must be a valid null-terminated UTF-8 string containing Treatment[] JSON
///
/// # Arguments
/// * `profile_json` - JSON string containing Profile data
/// * `glucose_json` - JSON string containing array of GlucoseReading objects
/// * `treatments_json` - JSON string containing array of Treatment objects
/// * `time_millis` - Current time as Unix milliseconds
///
/// # Returns
/// JSON string containing COBResult. Must be freed with `oref_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oref_calculate_cob(
    profile_json: *const c_char,
    glucose_json: *const c_char,
    treatments_json: *const c_char,
    time_millis: i64,
) -> *mut c_char {
    let Some(profile_str) = c_str_to_rust(profile_json) else {
        return error_json("Invalid profile_json pointer");
    };
    let Some(glucose_str) = c_str_to_rust(glucose_json) else {
        return error_json("Invalid glucose_json pointer");
    };
    let Some(treatments_str) = c_str_to_rust(treatments_json) else {
        return error_json("Invalid treatments_json pointer");
    };

    let profile: Profile = match serde_json::from_str(profile_str) {
        Ok(p) => p,
        Err(e) => return error_json(&format!("Profile parse error: {}", e)),
    };

    let glucose: Vec<GlucoseReading> = match serde_json::from_str(glucose_str) {
        Ok(g) => g,
        Err(e) => return error_json(&format!("Glucose parse error: {}", e)),
    };

    let treatments: Vec<Treatment> = match serde_json::from_str(treatments_str) {
        Ok(t) => t,
        Err(e) => return error_json(&format!("Treatments parse error: {}", e)),
    };

    let Some(time) = DateTime::from_timestamp_millis(time_millis) else {
        return error_json("Invalid timestamp");
    };

    match crate::cob::calculate(&profile, &glucose, &treatments, time) {
        Ok(cob) => match serde_json::to_string(&cob) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        Err(e) => error_json(&e.to_string()),
    }
}

// ============================================================================
// Autosens Calculation
// ============================================================================

/// Calculate autosens ratio from glucose and treatment history.
///
/// Detects changes in insulin sensitivity over time by analyzing
/// glucose deviations from expected values.
///
/// # Safety
///
/// All string pointers must be valid null-terminated UTF-8 strings.
///
/// # Returns
/// JSON string containing AutosensData. Must be freed with `oref_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oref_calculate_autosens(
    profile_json: *const c_char,
    glucose_json: *const c_char,
    treatments_json: *const c_char,
    time_millis: i64,
) -> *mut c_char {
    let Some(profile_str) = c_str_to_rust(profile_json) else {
        return error_json("Invalid profile_json pointer");
    };
    let Some(glucose_str) = c_str_to_rust(glucose_json) else {
        return error_json("Invalid glucose_json pointer");
    };
    let Some(treatments_str) = c_str_to_rust(treatments_json) else {
        return error_json("Invalid treatments_json pointer");
    };

    let profile: Profile = match serde_json::from_str(profile_str) {
        Ok(p) => p,
        Err(e) => return error_json(&format!("Profile parse error: {}", e)),
    };

    let glucose: Vec<GlucoseReading> = match serde_json::from_str(glucose_str) {
        Ok(g) => g,
        Err(e) => return error_json(&format!("Glucose parse error: {}", e)),
    };

    let treatments: Vec<Treatment> = match serde_json::from_str(treatments_str) {
        Ok(t) => t,
        Err(e) => return error_json(&format!("Treatments parse error: {}", e)),
    };

    let Some(time) = DateTime::from_timestamp_millis(time_millis) else {
        return error_json("Invalid timestamp");
    };

    match crate::autosens::detect_sensitivity(&profile, &glucose, &treatments, time) {
        Ok(autosens) => match serde_json::to_string(&autosens) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        Err(e) => error_json(&e.to_string()),
    }
}

// ============================================================================
// Determine Basal (Main Algorithm)
// ============================================================================

/// JSON input structure for determine_basal (matches WASM version)
#[derive(serde::Deserialize)]
#[serde(rename_all = "camelCase")]
struct DetermineBasalInputsJson {
    glucose_status: GlucoseStatus,
    current_temp: CurrentTemp,
    iob_data: IOBData,
    profile: Profile,
    #[serde(default)]
    autosens_data: AutosensData,
    #[serde(default)]
    meal_data: MealData,
    #[serde(default)]
    micro_bolus_allowed: bool,
    #[serde(default)]
    current_time_millis: Option<i64>,
}

/// Run the determine-basal algorithm.
///
/// This is the main dosing algorithm that determines optimal temp basals and SMBs.
///
/// # Safety
///
/// `inputs_json` must be a valid null-terminated UTF-8 string containing DetermineBasalInputs JSON.
///
/// # Returns
/// JSON string containing DetermineBasalResult. Must be freed with `oref_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oref_determine_basal(inputs_json: *const c_char) -> *mut c_char {
    let Some(inputs_str) = c_str_to_rust(inputs_json) else {
        return error_json("Invalid inputs_json pointer");
    };

    let inputs: DetermineBasalInputsJson = match serde_json::from_str(inputs_str) {
        Ok(i) => i,
        Err(e) => return error_json(&format!("Inputs parse error: {}", e)),
    };

    let current_time = inputs.current_time_millis.and_then(DateTime::from_timestamp_millis);

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

    match crate::determine_basal::determine_basal(&algo_inputs) {
        Ok(result) => match serde_json::to_string(&result) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        Err(e) => error_json(&e.to_string()),
    }
}

/// Convenience function to run determine_basal with individual parameters.
///
/// # Safety
///
/// All string pointers must be valid null-terminated UTF-8 strings.
#[no_mangle]
pub unsafe extern "C" fn oref_determine_basal_simple(
    profile_json: *const c_char,
    glucose_status_json: *const c_char,
    iob_data_json: *const c_char,
    current_temp_json: *const c_char,
    autosens_ratio: f64,
    meal_cob: f64,
    micro_bolus_allowed: i32,
) -> *mut c_char {
    let Some(profile_str) = c_str_to_rust(profile_json) else {
        return error_json("Invalid profile_json pointer");
    };
    let Some(glucose_status_str) = c_str_to_rust(glucose_status_json) else {
        return error_json("Invalid glucose_status_json pointer");
    };
    let Some(iob_data_str) = c_str_to_rust(iob_data_json) else {
        return error_json("Invalid iob_data_json pointer");
    };
    let Some(current_temp_str) = c_str_to_rust(current_temp_json) else {
        return error_json("Invalid current_temp_json pointer");
    };

    let profile: Profile = match serde_json::from_str(profile_str) {
        Ok(p) => p,
        Err(e) => return error_json(&format!("Profile parse error: {}", e)),
    };

    let glucose_status: GlucoseStatus = match serde_json::from_str(glucose_status_str) {
        Ok(g) => g,
        Err(e) => return error_json(&format!("GlucoseStatus parse error: {}", e)),
    };

    let iob_data: IOBData = match serde_json::from_str(iob_data_str) {
        Ok(i) => i,
        Err(e) => return error_json(&format!("IOBData parse error: {}", e)),
    };

    let current_temp: CurrentTemp = match serde_json::from_str(current_temp_str) {
        Ok(c) => c,
        Err(e) => return error_json(&format!("CurrentTemp parse error: {}", e)),
    };

    let autosens_data = AutosensData::with_ratio(autosens_ratio);
    let meal_data = MealData::with_cob(meal_cob, 0.0);

    let inputs = DetermineBasalInputs {
        glucose_status: &glucose_status,
        current_temp: &current_temp,
        iob_data: &iob_data,
        profile: &profile,
        autosens_data: &autosens_data,
        meal_data: &meal_data,
        micro_bolus_allowed: micro_bolus_allowed != 0,
        current_time: None,
    };

    match crate::determine_basal::determine_basal(&inputs) {
        Ok(result) => match serde_json::to_string(&result) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        Err(e) => error_json(&e.to_string()),
    }
}

// ============================================================================
// Glucose Status Helper
// ============================================================================

/// Calculate glucose status from readings.
///
/// # Safety
///
/// `glucose_json` must be a valid null-terminated UTF-8 string containing GlucoseReading[] JSON.
///
/// # Returns
/// JSON string containing GlucoseStatus. Must be freed with `oref_free_string`.
#[no_mangle]
pub unsafe extern "C" fn oref_calculate_glucose_status(glucose_json: *const c_char) -> *mut c_char {
    let Some(glucose_str) = c_str_to_rust(glucose_json) else {
        return error_json("Invalid glucose_json pointer");
    };

    let readings: Vec<GlucoseReading> = match serde_json::from_str(glucose_str) {
        Ok(r) => r,
        Err(e) => return error_json(&format!("Glucose parse error: {}", e)),
    };

    match GlucoseStatus::from_readings(&readings) {
        Some(status) => match serde_json::to_string(&status) {
            Ok(json) => rust_to_c_string(json),
            Err(e) => error_json(&format!("Serialization error: {}", e)),
        },
        None => error_json("No valid glucose readings"),
    }
}

// ============================================================================
// Tests
// ============================================================================

#[cfg(test)]
mod tests {
    use super::*;
    use std::ffi::CString;

    #[test]
    fn test_health_check() {
        let result = oref_health_check();
        assert!(!result.is_null());

        let c_str = unsafe { CStr::from_ptr(result) };
        let json = c_str.to_str().unwrap();
        assert!(json.contains("ok"));
        assert!(json.contains("iob"));

        unsafe { oref_free_string(result) };
    }

    #[test]
    fn test_version() {
        let result = oref_version();
        assert!(!result.is_null());

        let c_str = unsafe { CStr::from_ptr(result) };
        let version = c_str.to_str().unwrap();
        assert!(!version.is_empty());

        unsafe { oref_free_string(result) };
    }

    #[test]
    fn test_null_pointer_handling() {
        unsafe {
            let result = oref_calculate_iob(
                std::ptr::null(),
                std::ptr::null(),
                0,
                1,
            );
            assert!(!result.is_null());

            let c_str = CStr::from_ptr(result);
            let json = c_str.to_str().unwrap();
            assert!(json.contains("error"));

            oref_free_string(result);
        }
    }

    #[test]
    fn test_iob_calculation() {
        let profile_json = CString::new(r#"{
            "dia": 3.0,
            "currentBasal": 1.0,
            "maxIob": 10.0,
            "maxDailyBasal": 2.0,
            "maxBasal": 4.0,
            "minBg": 100.0,
            "maxBg": 120.0,
            "sens": 50.0,
            "carbRatio": 10.0
        }"#).unwrap();

        let treatments_json = CString::new("[]").unwrap();

        unsafe {
            let result = oref_calculate_iob(
                profile_json.as_ptr(),
                treatments_json.as_ptr(),
                chrono::Utc::now().timestamp_millis(),
                1,
            );
            assert!(!result.is_null());

            let c_str = CStr::from_ptr(result);
            let json = c_str.to_str().unwrap();
            // With no treatments, we should get an IOB of 0
            assert!(json.contains("iob"));
            assert!(!json.contains("error"));

            oref_free_string(result);
        }
    }
}
