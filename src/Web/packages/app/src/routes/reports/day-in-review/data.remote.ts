/**
 * Remote functions for day-in-review report
 * Fetches entries and treatments for a specific day
 */
import { z } from 'zod';
import { getRequestEvent, query } from '$app/server';
import { error } from '@sveltejs/kit';
import type { Entry, Treatment } from '$lib/api';

/**
 * Get day-in-review data for a specific date
 */
export const getDayInReviewData = query(
	z.string(), // date string in ISO format (YYYY-MM-DD)
	async (dateParam) => {
		if (!dateParam) {
			throw error(400, 'Date parameter is required');
		}

		const date = new Date(dateParam);
		if (isNaN(date.getTime())) {
			throw error(400, 'Invalid date parameter');
		}

		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		// Calculate day boundaries in local time
		// Using the date string directly to avoid timezone confusion -
		// if someone in Australia wants to see Dec 10th, they should see Dec 10th data
		const dayStart = new Date(date);
		dayStart.setHours(0, 0, 0, 0);

		const dayEnd = new Date(date);
		dayEnd.setHours(23, 59, 59, 999);

		// Build the direct query strings for the API
		// The backend expects find[field][$op]=value directly in the URL, not as a find= parameter value
		const entriesQueryParams = new URLSearchParams();
		entriesQueryParams.set('find[date][$gte]', String(dayStart.getTime()));
		entriesQueryParams.set('find[date][$lte]', String(dayEnd.getTime()));
		entriesQueryParams.set('count', '10000'); // Get all day's entries

		const treatmentsQueryParams = new URLSearchParams();
		treatmentsQueryParams.set('find[mills][$gte]', String(dayStart.getTime()));
		treatmentsQueryParams.set('find[mills][$lte]', String(dayEnd.getTime()));
		treatmentsQueryParams.set('count', '1000'); // Get all day's treatments

		// Fetch using the underlying fetch with properly formatted URLs
		const baseUrl = apiClient.baseUrl;
		// Cast to any to access the private http client which has the auth headers injection
		const authenticatedFetch = (apiClient.client as any).http.fetch;

		const [entriesResponse, treatmentsResponse] = await Promise.all([
			authenticatedFetch(`${baseUrl}/api/v1/Entries?${entriesQueryParams.toString()}`, {
				method: 'GET',
				headers: { 'Accept': 'application/json' }
			}),
			authenticatedFetch(`${baseUrl}/api/v1/Treatments?${treatmentsQueryParams.toString()}`, {
				method: 'GET',
				headers: { 'Accept': 'application/json' }
			}),
		]);

		if (!entriesResponse.ok || !treatmentsResponse.ok) {
			const errorMsg = !entriesResponse.ok
				? `Entries: ${entriesResponse.statusText}`
				: `Treatments: ${treatmentsResponse.statusText}`;
			throw error(500, `Failed to fetch data: ${errorMsg}`);
		}

		const [entries, treatments] = await Promise.all([
			entriesResponse.json() as Promise<Entry[]>,
			treatmentsResponse.json() as Promise<Treatment[]>,
		]);

		// Calculate analysis from the backend - this includes treatmentSummary
		const analysis = entries.length > 0
			? await apiClient.statistics.analyzeGlucoseDataExtended({
					entries,
					treatments,
					population: 0 as const, // Type1Adult
				})
			: null;

		// Use the treatmentSummary from analysis (if available) to avoid redundant API call
		// The backend AnalyzeGlucoseDataExtended already calculates TreatmentSummary
		// If no entries but we have treatments, calculate treatmentSummary directly
		const treatmentSummary = analysis?.treatmentSummary
			?? (treatments.length > 0 ? await apiClient.statistics.calculateTreatmentSummary(treatments) : null);

		return {
			date: dateParam,
			entries,
			treatments,
			analysis,
			treatmentSummary,
			dateRange: {
				from: dayStart.toISOString(),
				to: dayEnd.toISOString(),
			},
		};
	}
);

