// Store common type definitions for report components

import type { Entry, Treatment } from '$lib';
import type { TreatmentSummary } from '$lib/utils/calculate/treatment-stats.ts';
import type { GlucoseAnalytics } from '$lib/utils/glucose-analytics.ts';

export interface DayToDayDailyData {
  date: string; // YYYY-MM-DD format
  readingsCount: number;
  analytics: GlucoseAnalytics; // Comprehensive glucose analytics
  trend: "rising" | "falling" | "stable";
  glucoseData: Entry[]; // Array of glucose readings for the day
  treatments: Treatment[]; // Array of treatments for the day
  treatmentSummary: TreatmentSummary;
}
