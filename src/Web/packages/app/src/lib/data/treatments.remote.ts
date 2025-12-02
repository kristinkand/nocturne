import { getRequestEvent, query } from "$app/server";
import type { Treatment } from "$lib/api";
import { z } from "zod";

// Schema for fetching treatments with pagination and filtering
const treatmentsQuerySchema = z.object({
  dateRange: z.object({
    from: z.date().optional(),
    to: z.date().optional(),
  }),
  category: z.enum(["all", "bolus", "basal", "carbs", "device", "notes"]).optional(),
  eventTypes: z.array(z.string()).optional(),
  page: z.number().optional().default(0),
  pageSize: z.number().optional().default(100),
});

/**
 * Treatment category definitions for UI organization
 * Each category groups related event types together
 */
export const TREATMENT_CATEGORIES = {
  bolus: {
    id: "bolus",
    name: "Bolus & Insulin",
    description: "Insulin doses, corrections, and combo boluses",
    eventTypes: ["Snack Bolus", "Meal Bolus", "Correction Bolus", "Combo Bolus"],
    icon: "💉",
    color: "blue",
  },
  basal: {
    id: "basal",
    name: "Basal & Profiles",
    description: "Temp basals and profile switches",
    eventTypes: ["Temp Basal Start", "Temp Basal End", "Temp Basal", "Profile Switch"],
    icon: "📊",
    color: "purple",
  },
  carbs: {
    id: "carbs",
    name: "Carbs & Nutrition",
    description: "Meals, snacks, and carb corrections",
    eventTypes: ["Carb Correction"],
    icon: "🍽️",
    color: "green",
  },
  device: {
    id: "device",
    name: "Device Events",
    description: "Sensor, pump, and site changes",
    eventTypes: [
      "Site Change",
      "Sensor Start",
      "Sensor Change",
      "Sensor Stop",
      "Pump Battery Change",
      "Insulin Change",
    ],
    icon: "📱",
    color: "orange",
  },
  notes: {
    id: "notes",
    name: "Notes & Alerts",
    description: "Notes, announcements, and BG checks",
    eventTypes: ["BG Check", "Note", "Announcement", "Question", "D.A.D. Alert"],
    icon: "📝",
    color: "gray",
  },
} as const;

export type TreatmentCategoryId = keyof typeof TREATMENT_CATEGORIES;

/**
 * Get all treatments within a date range with optional filtering
 * Uses pagination for large datasets
 */
export const getTreatments = query(treatmentsQuerySchema, async (props) => {
  const { locals } = getRequestEvent();
  const { apiClient } = locals;

  const { from = new Date(), to = new Date() } = props.dateRange;
  if (!from || !to) throw new Error("Invalid date range");

  // Build the find query for the API
  const treatmentsQuery = `find[created_at][$gte]=${from.toISOString()}&find[created_at][$lte]=${to.toISOString()}`;

  // Fetch treatments with pagination
  const treatments = await apiClient.treatments.getTreatments2(
    treatmentsQuery,
    props.pageSize,
    props.page * props.pageSize
  );

  // Apply category filter if specified
  let filtered = treatments;
  if (props.category && props.category !== "all") {
    const categoryConfig = TREATMENT_CATEGORIES[props.category];
    if (categoryConfig) {
      const eventTypes = categoryConfig.eventTypes as readonly string[];
      filtered = treatments.filter((t) =>
        eventTypes.includes(t.eventType || "")
      );
    }
  }

  // Apply event type filter if specified
  if (props.eventTypes && props.eventTypes.length > 0) {
    filtered = filtered.filter((t) =>
      props.eventTypes!.includes(t.eventType || "")
    );
  }

  return filtered;
});

/**
 * Get all treatments for a date range (paginated fetch for large datasets)
 */
export const getAllTreatments = query(
  z.object({
    dateRange: z.object({
      from: z.date(),
      to: z.date(),
    }),
  }),
  async (props) => {
    const { locals } = getRequestEvent();
    const { apiClient } = locals;

    const { from, to } = props.dateRange;
    const treatmentsQuery = `find[created_at][$gte]=${from.toISOString()}&find[created_at][$lte]=${to.toISOString()}`;

    const pageSize = 1000;
    let allTreatments: Treatment[] = [];
    let offset = 0;
    let hasMore = true;

    while (hasMore) {
      const batch = await apiClient.treatments.getTreatments2(
        treatmentsQuery,
        pageSize,
        offset
      );
      allTreatments = allTreatments.concat(batch);

      if (batch.length < pageSize) {
        hasMore = false;
      } else {
        offset += pageSize;
      }

      // Safety limit
      if (offset >= 50000) {
        console.warn("Treatment fetch reached safety limit of 50,000 records");
        hasMore = false;
      }
    }

    return allTreatments;
  }
);

/**
 * Get treatment statistics by category
 */
export const getTreatmentStats = query(
  z.object({
    dateRange: z.object({
      from: z.date(),
      to: z.date(),
    }),
  }),
  async (props) => {
    const { locals } = getRequestEvent();
    const { apiClient } = locals;

    const { from, to } = props.dateRange;
    const treatmentsQuery = `find[created_at][$gte]=${from.toISOString()}&find[created_at][$lte]=${to.toISOString()}`;

    // Get all treatments for stats
    const treatments = await apiClient.treatments.getTreatments2(treatmentsQuery, 10000, 0);

    // Calculate category counts
    const categoryCounts: Record<string, number> = {};
    const eventTypeCounts: Record<string, number> = {};
    let totalInsulin = 0;
    let totalCarbs = 0;
    let bolusCount = 0;
    let carbEntryCount = 0;

    for (const treatment of treatments) {
      const eventType = treatment.eventType || "<none>";
      eventTypeCounts[eventType] = (eventTypeCounts[eventType] || 0) + 1;

      // Count by category
      for (const [categoryId, category] of Object.entries(TREATMENT_CATEGORIES)) {
        if ((category.eventTypes as readonly string[]).includes(eventType)) {
          categoryCounts[categoryId] = (categoryCounts[categoryId] || 0) + 1;
          break;
        }
      }

      // Aggregate insulin and carbs
      if (treatment.insulin && treatment.insulin > 0) {
        totalInsulin += treatment.insulin;
        bolusCount++;
      }
      if (treatment.carbs && treatment.carbs > 0) {
        totalCarbs += treatment.carbs;
        carbEntryCount++;
      }
    }

    return {
      total: treatments.length,
      categoryCounts,
      eventTypeCounts,
      totals: {
        insulin: totalInsulin,
        carbs: totalCarbs,
        bolusCount,
        carbEntryCount,
      },
      averages: {
        insulinPerBolus: bolusCount > 0 ? totalInsulin / bolusCount : 0,
        carbsPerEntry: carbEntryCount > 0 ? totalCarbs / carbEntryCount : 0,
      },
    };
  }
);

// Note: Mutations (update, delete) are handled via SvelteKit form actions in +page.server.ts
// This file only contains query functions for fetching data

/**
 * Identify anomalous treatments based on configurable thresholds
 */
export function identifyAnomalies(
  treatments: Treatment[],
  options: {
    highInsulinThreshold?: number;
    highCarbThreshold?: number;
    unusualTimeHours?: number[];
  } = {}
): Treatment[] {
  const {
    highInsulinThreshold = 15, // Units of insulin considered high
    highCarbThreshold = 100, // Grams of carbs considered high
    unusualTimeHours = [0, 1, 2, 3, 4, 5], // Hours considered unusual for boluses
  } = options;

  return treatments.filter((t) => {
    // High insulin bolus
    if (t.insulin && t.insulin > highInsulinThreshold) return true;

    // High carb entry
    if (t.carbs && t.carbs > highCarbThreshold) return true;

    // Unusual timing for boluses
    if (t.insulin && t.created_at) {
      const hour = new Date(t.created_at).getHours();
      if (unusualTimeHours.includes(hour)) return true;
    }

    return false;
  });
}

/**
 * Group treatments by time period for analysis
 */
export function groupTreatmentsByPeriod(
  treatments: Treatment[],
  period: "hour" | "day" | "week" | "month"
): Map<string, Treatment[]> {
  const groups = new Map<string, Treatment[]>();

  for (const treatment of treatments) {
    if (!treatment.created_at) continue;

    const date = new Date(treatment.created_at);
    let key: string;

    switch (period) {
      case "hour":
        key = date.toISOString().slice(0, 13); // YYYY-MM-DDTHH
        break;
      case "day":
        key = date.toISOString().slice(0, 10); // YYYY-MM-DD
        break;
      case "week":
        const weekStart = new Date(date);
        weekStart.setDate(date.getDate() - date.getDay());
        key = weekStart.toISOString().slice(0, 10);
        break;
      case "month":
        key = date.toISOString().slice(0, 7); // YYYY-MM
        break;
    }

    if (!groups.has(key)) {
      groups.set(key, []);
    }
    groups.get(key)!.push(treatment);
  }

  return groups;
}
