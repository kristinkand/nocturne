/**
 * Remote functions for Retrospective data (IOB, COB, glucose, basal at specific times)
 */
import { getRequestEvent, query } from '$app/server';
import { z } from 'zod';
import { error } from '@sveltejs/kit';

/**
 * Get retrospective data at a specific point in time
 */
export const getRetrospectiveAt = query(
	z.object({
		time: z.number(), // Unix timestamp in milliseconds
	}),
	async ({ time }) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		try {
			return await apiClient.retrospective.getRetrospectiveData(time);
		} catch (err) {
			console.error('Error fetching retrospective data:', err);
			throw error(500, 'Failed to fetch retrospective data');
		}
	}
);

/**
 * Get retrospective timeline for an entire day
 */
export const getRetrospectiveTimeline = query(
	z.object({
		date: z.string(), // Date in YYYY-MM-DD format
		intervalMinutes: z.number().optional().default(5),
	}),
	async ({ date, intervalMinutes }) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		try {
			return await apiClient.retrospective.getRetrospectiveTimeline(date, intervalMinutes);
		} catch (err) {
			console.error('Error fetching retrospective timeline:', err);
			throw error(500, 'Failed to fetch retrospective timeline');
		}
	}
);

/**
 * Get basal rate timeline for a day
 */
export const getBasalTimeline = query(
	z.object({
		date: z.string(), // Date in YYYY-MM-DD format
		intervalMinutes: z.number().optional().default(5),
	}),
	async ({ date, intervalMinutes }) => {
		const { locals } = getRequestEvent();
		const { apiClient } = locals;

		try {
			return await apiClient.retrospective.getBasalTimeline(date, intervalMinutes);
		} catch (err) {
			console.error('Error fetching basal timeline:', err);
			throw error(500, 'Failed to fetch basal timeline');
		}
	}
);
