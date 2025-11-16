import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';
import type { ResponseMatchType } from '$lib/api/generated/nocturne-api-client';

export const load: PageServerLoad = async ({ locals, url }) => {
	const client = locals.apiClient;

	try {
		// Parse query parameters for filtering
		const requestPath = url.searchParams.get('requestPath') || undefined;
		const overallMatchStr = url.searchParams.get('overallMatch');
		const overallMatch = overallMatchStr ? (parseInt(overallMatchStr) as ResponseMatchType) : undefined;
		const requestMethod = url.searchParams.get('requestMethod') || undefined;
		const count = parseInt(url.searchParams.get('count') || '100', 10);
		const skip = parseInt(url.searchParams.get('skip') || '0', 10);

		// Fetch data from compatibility API using typed client
		const [config, metrics, endpoints, analysesData] = await Promise.all([
			client.compatibility.getConfiguration(),
			client.compatibility.getMetrics(undefined, undefined),
			client.compatibility.getEndpointMetrics(undefined, undefined),
			client.compatibility.getAnalyses(requestPath, overallMatch, requestMethod, undefined, undefined, count, skip)
		]);

		return {
			config,
			metrics,
			endpoints,
			analyses: analysesData.analyses || [],
			total: analysesData.total || 0,
			filters: {
				requestPath: requestPath || '',
				overallMatch: overallMatchStr || '',
				requestMethod: requestMethod || '',
				count,
				skip
			}
		};
	} catch (err) {
		console.error('Error loading compatibility data:', err);
		if ((err as any).status) {
			throw err; // Re-throw SvelteKit errors
		}
		throw error(500, 'Failed to load compatibility data');
	}
};
