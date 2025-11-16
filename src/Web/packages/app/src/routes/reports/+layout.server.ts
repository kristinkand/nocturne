import type { LayoutServerLoad } from "./$types";
import { error } from "@sveltejs/kit";

export const load: LayoutServerLoad = async ({ url, locals }) => {
  console.log("Loading reports layout...");
  // Extract date parameters from URL
  const daysParam = url.searchParams.get("days");
  const fromParam = url.searchParams.get("from");
  const toParam = url.searchParams.get("to");
  const typeParam = url.searchParams.get("type");
  const eventTypeParam = url.searchParams.get("eventType");

  // Store raw params for child routes to access
  const rawParams = {
    days: daysParam || undefined,
    from: fromParam || undefined,
    to: toParam || undefined,
    type: typeParam || undefined,
    eventType: eventTypeParam || undefined,
  };

  // Calculate date range
  let startDate: Date;
  let endDate: Date;

  if (fromParam && toParam) {
    // Use explicit date range
    startDate = new Date(fromParam);
    endDate = new Date(toParam);
  } else if (daysParam) {
    // Use explicit days parameter
    const days = parseInt(daysParam);
    endDate = new Date();
    startDate = new Date(endDate);
    startDate.setDate(endDate.getDate() - (days - 1));
  } else {
    // Default to last month
    endDate = new Date();
    startDate = new Date(endDate);
    startDate.setMonth(endDate.getMonth() - 1);
  }

  // Validate dates
  if (isNaN(startDate.getTime()) || isNaN(endDate.getTime())) {
    throw error(400, "Invalid date parameters provided");
  }

  // Set to full day boundaries
  startDate.setHours(0, 0, 0, 0);
  endDate.setHours(23, 59, 59, 999);

  // Build find query for SGV/entries data as JSON string
  // Build find query for SGV/entries data
  const entriesQuery = `find[date][$gte]=${startDate.toISOString()}&find[date][$lte]=${endDate.toISOString()}`;
  const numberOfEntries = Math.ceil(
    (endDate.getTime() - startDate.getTime()) / (1000 * 60 * 5)
  );
  console.log("expected", numberOfEntries);
  const [treatments, entries] = await Promise.all([
    locals.apiClient.treatments.getTreatments2({}),
    locals.apiClient.entries.getEntries2({
      find: entriesQuery,
    }),
  ]);

  console.log("Fetched entries:", entries.length);
  return {
    treatments,
    entries,
    summary: locals.apiClient.statistics.getMultiPeriodStatistics(),
    analysis: locals.apiClient.statistics.analyzeGlucoseData({
      request: {
        entries,
        treatments,
      },
    }),
    /** All ISO strings */
    dateRange: {
      start: startDate.toISOString(),
      end: endDate.toISOString(),
      lastUpdated: new Date().toISOString(),
    },
    rawParams,
  };
};
