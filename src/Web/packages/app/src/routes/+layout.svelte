<script lang="ts">
  import "../app.css";
  import { page } from "$app/state";
  import { createRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import { onMount } from "svelte";
  import {
    PUBLIC_WEBSOCKET_RECONNECT_ATTEMPTS,
    PUBLIC_WEBSOCKET_RECONNECT_DELAY,
    PUBLIC_WEBSOCKET_MAX_RECONNECT_DELAY,
    PUBLIC_WEBSOCKET_PING_TIMEOUT,
    PUBLIC_WEBSOCKET_PING_INTERVAL,
  } from "$env/static/public";
  import * as Sidebar from "$lib/components/ui/sidebar";
  import { AppSidebar, MobileHeader } from "$lib/components/layout";
  import type { LayoutData } from "./$types";

  // Check if we're on a reports sub-page (not the main /reports page)
  const isReportsSubpage = $derived(page.url.pathname.startsWith("/reports/"));

  const { data, children } = $props<{ data: LayoutData; children: any }>();

  // WebSocket bridge is integrated into the SvelteKit dev server
  const config = {
    url: typeof window !== "undefined" ? window.location.origin : "",
    reconnectAttempts: parseInt(PUBLIC_WEBSOCKET_RECONNECT_ATTEMPTS) || 10,
    reconnectDelay: parseInt(PUBLIC_WEBSOCKET_RECONNECT_DELAY) || 5000,
    maxReconnectDelay: parseInt(PUBLIC_WEBSOCKET_MAX_RECONNECT_DELAY) || 30000,
    pingTimeout: parseInt(PUBLIC_WEBSOCKET_PING_TIMEOUT) || 60000,
    pingInterval: parseInt(PUBLIC_WEBSOCKET_PING_INTERVAL) || 25000,
  };

  const realtimeStore = createRealtimeStore(config);

  onMount(async () => {
    await realtimeStore.initialize();
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
