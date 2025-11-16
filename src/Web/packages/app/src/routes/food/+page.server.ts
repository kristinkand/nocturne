import type { PageServerLoad } from "./$types";
import type { FoodRecord, QuickPickRecord } from "./types";
import { error } from "@sveltejs/kit";

export const load: PageServerLoad = async ({ locals }) => {
  try {
    // Use the centralized API client to fetch food data
    // This automatically handles demo mode and authentication
    const records = await locals.apiClient.food.getFood2();

    // Separate food records and quickpicks, and build categories
    const foodList: FoodRecord[] = [];
    const quickPickList: QuickPickRecord[] = [];
    const categories: Record<string, Record<string, boolean>> = {};

    records.forEach((record) => {
      if (record.type === "food") {
        foodList.push(record);

        // Build categories structure
        const foodRecord = record;
        if (foodRecord.category && !categories[foodRecord.category]) {
          categories[foodRecord.category] = {};
        }
        if (foodRecord.category && foodRecord.subcategory) {
          categories[foodRecord.category][foodRecord.subcategory] = true;
        }
      } else if (record.type === "quickpick") {
        const quickPickRecord = record;
        // Calculate carbs for quickpick
        quickPickRecord.carbs = 0;
        if (quickPickRecord.foods) {
          quickPickRecord.foods.forEach((food) => {
            quickPickRecord.carbs += food.carbs * (food.portions || 1);
          });
        } else {
          quickPickRecord.foods = [];
        }
        quickPickList.push(quickPickRecord);
      }
    });

    // Sort quickpicks by position
    quickPickList.sort((a, b) => (a.position || 99999) - (b.position || 99999));

    return {
      foodList,
      quickPickList,
      categories,
      nightscoutUrl: process.env.NIGHTSCOUT_URL || "http://localhost:1612",
    };
  } catch (err) {
    console.error("Error loading food database:", err);
    throw error(500, "Failed to load food database");
  }
};
