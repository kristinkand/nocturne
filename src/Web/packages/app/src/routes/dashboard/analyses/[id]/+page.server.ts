import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ locals, params }) => {
  try {
    const apiClient = locals.apiClient;
    const { id } = params;
    
    // Fetch specific analysis
    const response = await fetch(`${apiClient.baseUrl}/api/v3/discrepancy/analyses/${id}`);

    if (!response.ok) {
      if (response.status === 404) {
        throw error(404, 'Analysis not found');
      }
      throw error(500, 'Failed to fetch analysis');
    }

    const analysis = await response.json();

    return {
      analysis
    };
  } catch (err) {
    console.error('Error loading analysis:', err);
    if (err.status) {
      throw err; // Re-throw SvelteKit errors
    }
    throw error(500, 'Failed to load analysis');
  }
};