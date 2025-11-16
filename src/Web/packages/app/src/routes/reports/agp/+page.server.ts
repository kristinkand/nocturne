import type { PageServerLoad } from "./$types";
import { error } from "@sveltejs/kit";
export const load: PageServerLoad = async ({ parent }) => {
  // Re-use analytics data fetched in the reports layout
  const parentData = await parent();

  if (!parentData || !parentData.entries) {
    throw error(500, "Entries data unavailable for AGP report");
  }

  return {
    entries: parentData.entries,
    treatments: parentData.treatments,
    analysis: parentData.analysis,
    dateRange: parentData.dateRange,
  };
};