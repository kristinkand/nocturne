//! Determine Basal algorithm
//!
//! The main dosing algorithm that determines optimal temp basals and SMBs.

mod algorithm;
mod smb;
mod predictions;

pub use algorithm::determine_basal;
pub use smb::should_enable_smb;
pub use predictions::predict_glucose;

use crate::types::{
    AutosensData, CurrentTemp, GlucoseStatus,
    IOBData, MealData, Profile,
};

/// Inputs for the determine basal algorithm
pub struct DetermineBasalInputs<'a> {
    /// Current glucose status
    pub glucose_status: &'a GlucoseStatus,

    /// Current temp basal
    pub current_temp: &'a CurrentTemp,

    /// Current IOB data
    pub iob_data: &'a IOBData,

    /// User profile
    pub profile: &'a Profile,

    /// Autosens data
    pub autosens_data: &'a AutosensData,

    /// Meal data
    pub meal_data: &'a MealData,

    /// Whether micro bolus is allowed
    pub micro_bolus_allowed: bool,

    /// Current time (for testing)
    pub current_time: Option<chrono::DateTime<chrono::Utc>>,
}
