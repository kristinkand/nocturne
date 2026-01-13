import type { Handle, HandleFetch } from '@sveltejs/kit';
import { env } from '$env/dynamic/private';
import { env as publicEnv } from '$env/dynamic/public';
import { dev } from '$app/environment';
import { runWithLocale, loadLocales } from 'wuchale/load-utils/server';
import { sequence } from '@sveltejs/kit/hooks';
import * as main from '../../../locales/main.loader.server.svelte.js'
import * as js from '../../../locales/js.loader.server.js'
import { locales } from '../../../locales/data.js'
import supportedLocales from '../../../supportedLocales.json';

// load at server startup
loadLocales(main.key, main.loadIDs, main.loadCatalog, locales)
loadLocales(js.key, js.loadIDs, js.loadCatalog, locales)

// Turn off SSL validation during development for self-signed certs
if (dev) {
	process.env.NODE_TLS_REJECT_UNAUTHORIZED = '0';
}

export const proxy: Handle = async ({ event, resolve }) => {
	// Proxy /api requests to the backend
	if (event.url.pathname.startsWith('/api')) {
		const apiUrl = env.VITE_PORTAL_API_URL;
		if (!apiUrl) {
			console.error('VITE_PORTAL_API_URL is not defined');
			return new Response('Configuration error: VITE_PORTAL_API_URL is missing', { status: 500 });
		}

		const targetUrl = new URL(event.url.pathname + event.url.search, apiUrl);
		console.log(`[PROXY] ${event.url.pathname} -> ${targetUrl.toString()}`);

		try {
			const response = await fetch(targetUrl, {
				method: event.request.method,
				headers: event.request.headers,
				body: event.request.method !== 'GET' && event.request.method !== 'HEAD'
					? await event.request.blob()
					: undefined,
				// Important: dupe logic for signals if needed, but usually simple fetch is enough
			});

			return new Response(response.body, {
				status: response.status,
				statusText: response.statusText,
				headers: response.headers
			});
		} catch (err) {
			console.error('Proxy error:', err);
			return new Response('Proxy error', { status: 502 });
		}
	}

	return resolve(event);
};

export const handleFetch: HandleFetch = async ({ event, request, fetch }) => {
	if (request.url.startsWith('http')) {
		const url = new URL(request.url);
		if (url.pathname.startsWith('/api')) {
			const apiUrl = env.VITE_PORTAL_API_URL;
			if (apiUrl) {
				const targetUrl = new URL(url.pathname + url.search, apiUrl).toString();
				console.log(`[FETCH-PROXY] ${url.pathname} -> ${targetUrl}`);
				// Forward the request to the backend
				return fetch(targetUrl, {
					method: request.method,
					headers: request.headers,
					body: request.body,
					duplex: 'half'
				} as any);
			}
		}
	}
	return fetch(request);
};

/** Cookie name for language preference - must match app store */
const LANGUAGE_COOKIE_NAME = 'nocturne-language';

/**
 * Parse Accept-Language header and find the best matching supported locale
 */
function parseAcceptLanguage(header: string | null, supported: Set<string>): string | null {
	if (!header) return null;

	const languages = header.split(',').map((lang) => {
		const [code, qValue] = lang.trim().split(';q=');
		return {
			code: code.split('-')[0].toLowerCase(),
			quality: qValue ? parseFloat(qValue) : 1.0,
		};
	});

	languages.sort((a, b) => b.quality - a.quality);

	for (const { code } of languages) {
		if (supported.has(code)) {
			return code;
		}
	}

	return null;
}

/**
 * Resolve locale using priority cascade (portal has no user auth):
 * 1. Query param override (?locale=fr)
 * 2. Cookie (nocturne-language) - synced from client localStorage
 * 3. Environment default (PUBLIC_DEFAULT_LANGUAGE)
 * 4. Browser Accept-Language header
 * 5. Ultimate fallback: 'en'
 */
function resolveLocale(event: Parameters<Handle>[0]['event']): string {
	const supported = new Set(supportedLocales);

	// 1. Query param override
	const queryLocale = event.url.searchParams.get('locale');
	if (queryLocale && supported.has(queryLocale)) {
		return queryLocale;
	}

	// 2. Cookie (set by client from localStorage)
	const cookieLocale = event.cookies.get(LANGUAGE_COOKIE_NAME);
	if (cookieLocale && supported.has(cookieLocale)) {
		return cookieLocale;
	}

	// 3. Environment default
	const envDefault = publicEnv.PUBLIC_DEFAULT_LANGUAGE;
	if (envDefault && supported.has(envDefault)) {
		return envDefault;
	}

	// 4. Browser Accept-Language header
	const acceptLang = event.request.headers.get('accept-language');
	const browserLocale = parseAcceptLanguage(acceptLang, supported);
	if (browserLocale) {
		return browserLocale;
	}

	// 5. Ultimate fallback
	return 'en';
}

export const locale: Handle = async ({ event, resolve }) => {
	const locale = resolveLocale(event);
	return await runWithLocale(locale, () => resolve(event));
};

export const handle: Handle = sequence(proxy, locale);