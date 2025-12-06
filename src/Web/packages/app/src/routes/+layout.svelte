<script lang="ts">
  import "../app.css";
  import { page } from "$app/state";
  import { createRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { createSettingsStore } from "$lib/stores/settings-store.svelte";
  import { onMount, onDestroy } from "svelte";
  import * as Sidebar from "$lib/components/ui/sidebar";
  import { AppSidebar, MobileHeader } from "$lib/components/layout";
  import type { LayoutData } from "./$types";
  import { getTitleFaviconService } from "$lib/services/title-favicon-service.svelte";
  import { getDefaultSettings } from "$lib/components/settings/constants";
  import type { AlarmVisualSettings } from "$lib/types/alarm-profile";
  import type { TitleFaviconSettings } from "$lib/stores/serverSettings";
  import { browser } from "$app/environment";

  // LocalStorage key for title/favicon settings
  const SETTINGS_STORAGE_KEY = "nocturne-title-favicon-settings";

  // WebSocket config - defaults, can be overridden in production
  const config = {
    url: typeof window !== "undefined" ? window.location.origin : "",
    reconnectAttempts: 10,
    reconnectDelay: 5000,
    maxReconnectDelay: 30000,
    pingTimeout: 60000,
    pingInterval: 25000,
  };

  // Check if we're on a reports sub-page (not the main /reports page)
  const isReportsSubpage = $derived(page.url.pathname.startsWith("/reports/"));

  const { data, children } = $props<{ data: LayoutData; children: any }>();

  const realtimeStore = createRealtimeStore(config);

  // Create settings store in context for the entire app
  // This makes feature settings available on all pages including the main dashboard
  createSettingsStore();

  // Title/Favicon service for dynamic updates
  const titleFaviconService = getTitleFaviconService();
  const defaultSettings = getDefaultSettings();

  // Load settings from localStorage with defaults
  function loadTitleFaviconSettings(): TitleFaviconSettings {
    if (!browser) return defaultSettings.titleFavicon;
    try {
      const stored = localStorage.getItem(SETTINGS_STORAGE_KEY);
      if (stored) {
        return { ...defaultSettings.titleFavicon, ...JSON.parse(stored) };
      }
    } catch (e) {
      console.error("Failed to load title/favicon settings:", e);
    }
    return defaultSettings.titleFavicon;
  }

  // Reactive settings state - reloads when localStorage changes
  let titleFaviconSettings = $state<TitleFaviconSettings>(
    loadTitleFaviconSettings()
  );

  // Listen for storage changes to update settings in real-time
  function handleStorageChange(e: StorageEvent) {
    if (e.key === SETTINGS_STORAGE_KEY) {
      titleFaviconSettings = loadTitleFaviconSettings();
    }
  }

  onMount(async () => {
    await realtimeStore.initialize();
    titleFaviconService.initialize();

    // Reload settings after hydration (SSR fix)
    titleFaviconSettings = loadTitleFaviconSettings();
    console.log("[TitleFavicon] Settings loaded:", titleFaviconSettings);

    // Listen for localStorage changes (from settings page)
    if (browser) {
      window.addEventListener("storage", handleStorageChange);
    }
  });

  onDestroy(() => {
    titleFaviconService.destroy();
    if (browser) {
      window.removeEventListener("storage", handleStorageChange);
    }
  });

  // Reactive updates when glucose changes or settings change
  $effect(() => {
    const bg = realtimeStore.currentBG;
    const enabled = titleFaviconSettings.enabled;

    console.log(
      "[TitleFavicon] Effect triggered - BG:",
      bg,
      "enabled:",
      enabled
    );

    if (enabled && bg && bg > 0) {
      console.log("[TitleFavicon] Updating title/favicon with BG:", bg);
      titleFaviconService.update(
        bg,
        realtimeStore.direction,
        realtimeStore.bgDelta,
        titleFaviconSettings,
        defaultSettings.thresholds
      );
    }
  });

  // Handle alarm events for flashing
  // When an alarm is active, start flashing with the alarm's visual settings
  $effect(() => {
    // For now, we can detect alarms by checking if BG is in alarm range
    // In the future, this should integrate with the alarm system's actual events
    const bg = realtimeStore.currentBG;
    if (
      bg &&
      titleFaviconSettings.enabled &&
      titleFaviconSettings.flashOnAlarm
    ) {
      const status = titleFaviconService.getGlucoseStatus(
        bg,
        defaultSettings.thresholds
      );
      if (status === "urgent-low" || status === "urgent-high") {
        // Start flashing with default alarm visual settings if not already flashing
        if (!titleFaviconService.isFlashing) {
          const alarmVisual: AlarmVisualSettings = {
            screenFlash: true,
            flashColor: status === "urgent-low" ? "#ef4444" : "#ef4444",
            flashIntervalMs: 500,
            persistentBanner: true,
            wakeScreen: true,
          };
          titleFaviconService.startFlashing(alarmVisual);
        }
      } else {
        // Stop flashing if no longer in alarm state
        if (titleFaviconService.isFlashing) {
          titleFaviconService.stopFlashing();
        }
      }
    }
  });
</script>

<Sidebar.Provider>
  <AppSidebar user={data.user} />
  <MobileHeader />
  <Sidebar.Inset>
    <!-- Desktop header - hidden on mobile and on reports subpages (which have their own header) -->
    {#if !isReportsSubpage}
      <header
        class="hidden md:flex h-14 shrink-0 items-center gap-2 border-b border-border px-4"
      >
        <Sidebar.Trigger class="-ml-1" />
        <div class="flex-1"></div>
      </header>
    {/if}
    <main class="flex-1 overflow-auto">
      <svelte:boundary>
        {@render children()}

        {#snippet pending()}
          <div class="flex items-center justify-center h-full">
            <div class="text-muted-foreground">Loading...</div>
          </div>
        {/snippet}
        {#snippet failed(e)}
          <div class="flex items-center justify-center h-full">
            <div class="text-destructive">
              Error loading entries: {e instanceof Error
                ? e.message
                : JSON.stringify(e)}
            </div>
          </div>
        {/snippet}
      </svelte:boundary>
    </main>
  </Sidebar.Inset>
</Sidebar.Provider>
