import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ locals, url }) => {
  try {
    const apiClient = locals.apiClient;
    
    // Parse date filters from query parameters
    const fromDate = url.searchParams.get('fromDate') ? new Date(url.searchParams.get('fromDate')!) : undefined;
    const toDate = url.searchParams.get('toDate') ? new Date(url.searchParams.get('toDate')!) : undefined;

    // Fetch compatibility metrics
    const metricsResponse = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/metrics?${new URLSearchParams({
      ...(fromDate && { fromDate: fromDate.toISOString() }),
      ...(toDate && { toDate: toDate.toISOString() })
    })}`);

    if (!metricsResponse.ok) {
      throw error(500, 'Failed to fetch compatibility metrics');
    }

    // Fetch endpoint metrics  
    const endpointResponse = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/endpoints?${new URLSearchParams({
      ...(fromDate && { fromDate: fromDate.toISOString() }),
      ...(toDate && { toDate: toDate.toISOString() })
    })}`);

    if (!endpointResponse.ok) {
      throw error(500, 'Failed to fetch endpoint metrics');
    }

    // Fetch recent analyses
    const analysesResponse = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/analyses?count=20&skip=0`);

    if (!analysesResponse.ok) {
      throw error(500, 'Failed to fetch recent analyses');
    }

    // Fetch compatibility status
    const statusResponse = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/status`);

    if (!statusResponse.ok) {
      throw error(500, 'Failed to fetch compatibility status');
    }

    const [metrics, endpoints, analyses, status] = await Promise.all([
      metricsResponse.json(),
      endpointResponse.json(),
      analysesResponse.json(),
      statusResponse.json()
    ]);

    return {
      metrics,
      endpoints,
      analyses,
      status,
      filters: {
        fromDate: fromDate?.toISOString(),
        toDate: toDate?.toISOString()
      }
    };
  } catch (err) {
    console.error('Error loading dashboard data:', err);
    throw error(500, 'Failed to load dashboard data');
  }
};