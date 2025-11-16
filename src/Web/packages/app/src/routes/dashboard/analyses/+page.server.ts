import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ locals, url }) => {
  try {
    const apiClient = locals.apiClient;
    
    // Parse query parameters for filtering
    const requestPath = url.searchParams.get('requestPath') || '';
    const overallMatch = url.searchParams.get('overallMatch') ? parseInt(url.searchParams.get('overallMatch')!) : undefined;
    const fromDate = url.searchParams.get('fromDate') ? new Date(url.searchParams.get('fromDate')!) : undefined;
    const toDate = url.searchParams.get('toDate') ? new Date(url.searchParams.get('toDate')!) : undefined;
    const count = parseInt(url.searchParams.get('count') || '50');
    const skip = parseInt(url.searchParams.get('skip') || '0');

    // Build query parameters
    const queryParams = new URLSearchParams({
      count: count.toString(),
      skip: skip.toString(),
      ...(requestPath && { requestPath }),
      ...(overallMatch !== undefined && { overallMatch: overallMatch.toString() }),
      ...(fromDate && { fromDate: fromDate.toISOString() }),
      ...(toDate && { toDate: toDate.toISOString() })
    });

    // Fetch analyses
    const response = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/analyses?${queryParams}`);

    if (!response.ok) {
      throw error(500, 'Failed to fetch discrepancy analyses');
    }

    const analyses = await response.json();

    return {
      analyses,
      filters: {
        requestPath,
        overallMatch,
        fromDate: fromDate?.toISOString(),
        toDate: toDate?.toISOString(),
        count,
        skip
      }
    };
  } catch (err) {
    console.error('Error loading analyses:', err);
    throw error(500, 'Failed to load analyses');
  }
};