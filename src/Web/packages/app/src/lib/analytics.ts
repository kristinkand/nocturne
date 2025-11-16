/**
 * Privacy-first frontend analytics service
 * Tracks anonymous page views and feature usage to help improve Nocturne
 * Contains no medical or personal data
 */

export interface AnalyticsEvent {
	sessionId: string;
	eventType: string;
	category: string;
	action: string;
	label: string;
	value?: number;
	timestamp: number;
	metadata: Record<string, any>;
}

export interface AnalyticsConfig {
	enabled: boolean;
	batchSize: number;
	flushInterval: number; // milliseconds
	collectPageViews: boolean;
	collectFeatureUsage: boolean;
	collectInteractions: boolean;
	apiEndpoint: string;
}

class AnalyticsService {
	private config: AnalyticsConfig;
	private sessionId: string;
	private pendingEvents: AnalyticsEvent[] = [];
	private flushTimer: number | null = null;
	private isEnabled = false;

	constructor() {
		this.sessionId = this.generateSessionId();
		this.config = {
			enabled: true,
			batchSize: 10,
			flushInterval: 30000, // 30 seconds
			collectPageViews: true,
			collectFeatureUsage: true,
			collectInteractions: true,
			apiEndpoint: '/api/analytics/events'
		};

		// Check if analytics is enabled via API
		this.initializeFromServer();
		
		// Set up periodic flush
		this.setupPeriodicFlush();
		
		// Set up beforeunload handler to flush pending events
		if (typeof window !== 'undefined') {
			window.addEventListener('beforeunload', () => {
				this.flush();
			});
		}
	}

	private async initializeFromServer(): Promise<void> {
		try {
			const response = await fetch('/api/analytics/status');
			if (response.ok) {
				const data = await response.json();
				this.isEnabled = data.enabled && data.configuration?.collectUiUsage !== false;
			}
		} catch (error) {
			console.debug('Analytics: Failed to check server status, defaulting to enabled');
			this.isEnabled = true;
		}
	}

	private generateSessionId(): string {
		return Math.random().toString(36).substring(2) + Date.now().toString(36);
	}

	private setupPeriodicFlush(): void {
		if (typeof window === 'undefined') return;

		this.flushTimer = window.setInterval(() => {
			this.flush();
		}, this.config.flushInterval);
	}

	/**
	 * Track a page view
	 */
	public trackPageView(page: string, metadata: Record<string, any> = {}): void {
		if (!this.isEnabled || !this.config.collectPageViews) return;

		const userAgent = typeof navigator !== 'undefined' ? navigator.userAgent : '';
		
		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'page_view',
			category: 'navigation',
			action: 'view',
			label: page,
			timestamp: Date.now(),
			metadata: {
				...metadata,
				device_type: this.detectDeviceType(userAgent),
				path: typeof window !== 'undefined' ? window.location.pathname : page
			}
		});
	}

	/**
	 * Track feature usage
	 */
	public trackFeatureUsage(feature: string, action: string, metadata: Record<string, any> = {}): void {
		if (!this.isEnabled || !this.config.collectFeatureUsage) return;

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'feature_usage',
			category: 'feature',
			action: action,
			label: feature,
			timestamp: Date.now(),
			metadata: metadata
		});
	}

	/**
	 * Track user interactions (clicks, form submissions, etc.)
	 */
	public trackInteraction(element: string, action: string, metadata: Record<string, any> = {}): void {
		if (!this.isEnabled || !this.config.collectInteractions) return;

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'interaction',
			category: 'ui',
			action: action,
			label: element,
			timestamp: Date.now(),
			metadata: metadata
		});
	}

	/**
	 * Track report generation
	 */
	public trackReportGeneration(reportType: string, parameters: Record<string, any> = {}): void {
		if (!this.isEnabled) return;

		// Only track report type and parameters, not actual data
		const sanitizedParams = this.sanitizeReportParameters(parameters);

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'report_generation',
			category: 'reports',
			action: 'generate',
			label: reportType,
			timestamp: Date.now(),
			metadata: sanitizedParams
		});
	}

	/**
	 * Track chart/visualization usage
	 */
	public trackChartView(chartType: string, metadata: Record<string, any> = {}): void {
		if (!this.isEnabled) return;

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'chart_view',
			category: 'visualization',
			action: 'view',
			label: chartType,
			timestamp: Date.now(),
			metadata: metadata
		});
	}

	/**
	 * Track settings changes (anonymized)
	 */
	public trackSettingsChange(settingCategory: string, action: string): void {
		if (!this.isEnabled) return;

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'settings_change',
			category: 'settings',
			action: action,
			label: settingCategory,
			timestamp: Date.now(),
			metadata: {}
		});
	}

	/**
	 * Track error events (anonymized)
	 */
	public trackError(errorType: string, context: string, metadata: Record<string, any> = {}): void {
		if (!this.isEnabled) return;

		// Remove any potentially sensitive data from error metadata
		const sanitizedMetadata = this.sanitizeErrorMetadata(metadata);

		this.trackEvent({
			sessionId: this.sessionId,
			eventType: 'error',
			category: 'system',
			action: errorType,
			label: context,
			timestamp: Date.now(),
			metadata: sanitizedMetadata
		});
	}

	private trackEvent(event: AnalyticsEvent): void {
		this.pendingEvents.push(event);

		// Flush if we've reached the batch size
		if (this.pendingEvents.length >= this.config.batchSize) {
			this.flush();
		}
	}

	private async flush(): Promise<void> {
		if (this.pendingEvents.length === 0) return;

		const events = [...this.pendingEvents];
		this.pendingEvents = [];

		try {
			const response = await fetch(this.config.apiEndpoint, {
				method: 'POST',
				headers: {
					'Content-Type': 'application/json'
				},
				body: JSON.stringify(events[0]) // Send one event at a time for now
			});

			if (!response.ok) {
				console.debug('Analytics: Failed to send events, status:', response.status);
			}
		} catch (error) {
			console.debug('Analytics: Failed to send events:', error);
			// Re-queue events on failure (up to a limit)
			if (this.pendingEvents.length < 100) {
				this.pendingEvents.unshift(...events);
			}
		}
	}

	private detectDeviceType(userAgent: string): string {
		const ua = userAgent.toLowerCase();
		
		if (ua.includes('mobile') || ua.includes('android') || ua.includes('iphone')) {
			return 'mobile';
		}
		
		if (ua.includes('tablet') || ua.includes('ipad')) {
			return 'tablet';
		}
		
		return 'desktop';
	}

	private sanitizeReportParameters(params: Record<string, any>): Record<string, any> {
		// Only include safe, non-personal parameters
		const safeParams: Record<string, any> = {};
		
		const allowedKeys = [
			'period', 'range_type', 'chart_type', 'display_units', 
			'include_predictions', 'aggregation', 'timezone_offset'
		];
		
		for (const key of allowedKeys) {
			if (params[key] !== undefined) {
				safeParams[key] = params[key];
			}
		}
		
		return safeParams;
	}

	private sanitizeErrorMetadata(metadata: Record<string, any>): Record<string, any> {
		// Remove potentially sensitive information from error metadata
		const sanitized: Record<string, any> = {};
		
		const allowedKeys = [
			'error_code', 'http_status', 'error_type', 'component',
			'browser', 'viewport_width', 'viewport_height'
		];
		
		for (const key of allowedKeys) {
			if (metadata[key] !== undefined) {
				sanitized[key] = metadata[key];
			}
		}
		
		return sanitized;
	}

	/**
	 * Enable or disable analytics collection
	 */
	public setEnabled(enabled: boolean): void {
		this.isEnabled = enabled;
		
		if (!enabled) {
			// Clear pending events when disabled
			this.pendingEvents = [];
		}
	}

	/**
	 * Check if analytics is enabled
	 */
	public getEnabled(): boolean {
		return this.isEnabled;
	}

	/**
	 * Update analytics configuration
	 */
	public updateConfig(newConfig: Partial<AnalyticsConfig>): void {
		this.config = { ...this.config, ...newConfig };
		
		// Reset flush timer if interval changed
		if (newConfig.flushInterval && this.flushTimer) {
			clearInterval(this.flushTimer);
			this.setupPeriodicFlush();
		}
	}

	/**
	 * Clear all pending analytics data
	 */
	public clearData(): void {
		this.pendingEvents = [];
	}
}

// Export singleton instance
export const analytics = new AnalyticsService();

// Convenience functions for common tracking scenarios
export function trackPageView(page: string, metadata?: Record<string, any>) {
	analytics.trackPageView(page, metadata);
}

export function trackFeatureUsage(feature: string, action: string, metadata?: Record<string, any>) {
	analytics.trackFeatureUsage(feature, action, metadata);
}

export function trackInteraction(element: string, action: string, metadata?: Record<string, any>) {
	analytics.trackInteraction(element, action, metadata);
}

export function trackReportGeneration(reportType: string, parameters?: Record<string, any>) {
	analytics.trackReportGeneration(reportType, parameters);
}

export function trackChartView(chartType: string, metadata?: Record<string, any>) {
	analytics.trackChartView(chartType, metadata);
}

export function trackSettingsChange(settingCategory: string, action: string) {
	analytics.trackSettingsChange(settingCategory, action);
}

export function trackError(errorType: string, context: string, metadata?: Record<string, any>) {
	analytics.trackError(errorType, context, metadata);
}