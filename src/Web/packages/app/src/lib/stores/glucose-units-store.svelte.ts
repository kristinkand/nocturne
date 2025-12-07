/**
 * Glucose Units Store
 *
 * Provides a reactive store for accessing the user's glucose unit preference
 * throughout the application. This simplifies consuming the units setting
 * from the main settings store.
 */

import { browser } from "$app/environment";
import type { GlucoseUnits } from "$lib/utils/glucose-formatting";

/** Default units when no setting is configured */
const DEFAULT_UNITS: GlucoseUnits = "mg/dl";

/** LocalStorage key for units setting */
const UNITS_STORAGE_KEY = "nocturne-glucose-units";

/**
 * Get the current glucose units from settings
 * Checks localStorage for the UI settings and extracts the units preference
 */
export function getGlucoseUnits(): GlucoseUnits {
  if (!browser) {
    return DEFAULT_UNITS;
  }

  try {
    // First try to get from UI settings
    const uiSettings = localStorage.getItem("nocturne-ui-settings");
    if (uiSettings) {
      const parsed = JSON.parse(uiSettings);
      const units = parsed?.features?.display?.units;
      if (units === "mmol" || units === "mg/dl") {
        return units;
      }
    }

    // Fallback to dedicated units key
    const units = localStorage.getItem(UNITS_STORAGE_KEY);
    if (units === "mmol" || units === "mg/dl") {
      return units;
    }
  } catch {
    // Ignore parse errors
  }

  return DEFAULT_UNITS;
}

/**
 * Reactive units value for use in Svelte components
 * This creates a simple reactive state that can be imported
 */
class GlucoseUnitsState {
  private _units = $state<GlucoseUnits>(DEFAULT_UNITS);

  constructor() {
    if (browser) {
      this._units = getGlucoseUnits();

      // Listen for storage changes to sync across tabs
      window.addEventListener("storage", this.handleStorageChange.bind(this));
    }
  }

  private handleStorageChange(event: StorageEvent): void {
    if (event.key === "nocturne-ui-settings" || event.key === UNITS_STORAGE_KEY) {
      this._units = getGlucoseUnits();
    }
  }

  get units(): GlucoseUnits {
    return this._units;
  }

  set units(value: GlucoseUnits) {
    this._units = value;
    if (browser) {
      localStorage.setItem(UNITS_STORAGE_KEY, value);
    }
  }

  /** Refresh units from storage */
  refresh(): void {
    if (browser) {
      this._units = getGlucoseUnits();
    }
  }
}

/** Singleton instance of the glucose units state */
export const glucoseUnitsState = new GlucoseUnitsState();
