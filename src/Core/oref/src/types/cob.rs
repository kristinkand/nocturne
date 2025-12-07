//! COB (Carbs on Board) and meal data types

#[cfg(feature = "serde")]
use serde::{Deserialize, Serialize};

/// Meal and COB data
#[derive(Debug, Clone)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct MealData {
    /// Total carbs entered
    #[cfg_attr(feature = "serde", serde(default))]
    pub carbs: f64,

    /// Nightscout carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub ns_carbs: f64,

    /// Bolus Wizard carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub bw_carbs: f64,

    /// Journal carbs
    #[cfg_attr(feature = "serde", serde(default))]
    pub journal_carbs: f64,

    /// Current Carbs on Board (grams)
    #[cfg_attr(feature = "serde", serde(default))]
    pub meal_cob: f64,

    /// Current BG deviation from expected
    #[cfg_attr(feature = "serde", serde(default))]
    pub current_deviation: f64,

    /// Maximum deviation seen
    #[cfg_attr(feature = "serde", serde(default))]
    pub max_deviation: f64,

    /// Minimum deviation seen
    #[cfg_attr(feature = "serde", serde(default))]
    pub min_deviation: f64,

    /// Slope from maximum deviation
    #[cfg_attr(feature = "serde", serde(default))]
    pub slope_from_max_deviation: f64,

    /// Slope from minimum deviation
    #[cfg_attr(feature = "serde", serde(default))]
    pub slope_from_min_deviation: f64,

    /// All deviation values
    #[cfg_attr(feature = "serde", serde(default))]
    pub all_deviations: Vec<f64>,

    /// Time of last carb entry (Unix millis)
    #[cfg_attr(feature = "serde", serde(default))]
    pub last_carb_time: i64,

    /// Whether Bolus Wizard carbs were found
    #[cfg_attr(feature = "serde", serde(default))]
    pub bw_found: bool,
}

impl Default for MealData {
    fn default() -> Self {
        Self {
            carbs: 0.0,
            ns_carbs: 0.0,
            bw_carbs: 0.0,
            journal_carbs: 0.0,
            meal_cob: 0.0,
            current_deviation: 0.0,
            max_deviation: 0.0,
            min_deviation: 0.0,
            slope_from_max_deviation: 0.0,
            slope_from_min_deviation: 0.0,
            all_deviations: vec![],
            last_carb_time: 0,
            bw_found: false,
        }
    }
}

impl MealData {
    /// Create empty meal data
    pub fn empty() -> Self {
        Self::default()
    }

    /// Create meal data with COB
    pub fn with_cob(meal_cob: f64, carbs: f64) -> Self {
        Self {
            meal_cob,
            carbs,
            ns_carbs: carbs,
            ..Default::default()
        }
    }

    /// Round values to appropriate precision
    pub fn rounded(mut self) -> Self {
        self.carbs = (self.carbs * 1000.0).round() / 1000.0;
        self.ns_carbs = (self.ns_carbs * 1000.0).round() / 1000.0;
        self.bw_carbs = (self.bw_carbs * 1000.0).round() / 1000.0;
        self.journal_carbs = (self.journal_carbs * 1000.0).round() / 1000.0;
        self.meal_cob = self.meal_cob.round();
        self.current_deviation = (self.current_deviation * 100.0).round() / 100.0;
        self.max_deviation = (self.max_deviation * 100.0).round() / 100.0;
        self.min_deviation = (self.min_deviation * 100.0).round() / 100.0;
        self.slope_from_max_deviation = (self.slope_from_max_deviation * 1000.0).round() / 1000.0;
        self.slope_from_min_deviation = (self.slope_from_min_deviation * 1000.0).round() / 1000.0;
        self
    }
}

/// COB calculation result
#[derive(Debug, Clone, Default)]
#[cfg_attr(feature = "serde", derive(Serialize, Deserialize))]
#[cfg_attr(feature = "serde", serde(rename_all = "camelCase"))]
pub struct COBResult {
    /// Remaining carbs on board (grams)
    pub meal_cob: f64,

    /// Carbs absorbed so far (grams)
    pub carbs_absorbed: f64,

    /// Current deviation
    pub current_deviation: f64,

    /// Maximum deviation
    pub max_deviation: f64,

    /// Minimum deviation
    pub min_deviation: f64,

    /// Slope from max deviation
    pub slope_from_max: f64,

    /// Slope from min deviation
    pub slope_from_min: f64,
}
