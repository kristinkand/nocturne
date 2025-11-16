import { error } from '@sveltejs/kit';
import type { PageServerLoad } from './$types';

export const load: PageServerLoad = async ({ locals, params }) => {
	const client = locals.apiClient;

	try {
		const analysisId = params.id;

		// Fetch analysis detail using typed client
		const analysis = await client.compatibility.getAnalysisDetail(analysisId);

		return {
			analysis
		};
	} catch (err) {
		console.error('Error loading analysis detail:', err);
		if ((err as any).status === 404) {
			throw error(404, 'Analysis not found');
		}
		if ((err as any).status) {
			throw err; // Re-throw SvelteKit errors
		}
		throw error(500, 'Failed to load analysis detail');
	}
};
