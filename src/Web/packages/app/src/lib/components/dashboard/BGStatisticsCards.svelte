<script lang="ts">
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { formatTime, timeAgo } from "$lib/utils";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";
  import WebSocketStatus from "$lib/components/WebSocketStatus.svelte";

  let { 
    bgDelta, 
    lastUpdated,
    showWebSocketStatus = true 
  }: {
    bgDelta?: number;
    lastUpdated?: number;
    showWebSocketStatus?: boolean;
  } = $props();

  const realtimeStore = getRealtimeStore();

  // Use realtime store values as fallback when props not provided
  const displayBgDelta = $derived(bgDelta ?? realtimeStore.bgDelta);
  const displayLastUpdated = $derived(lastUpdated ?? realtimeStore.lastUpdated);
</script>

<div class="grid grid-cols-1 md:grid-cols-4 gap-4">
  <Card>
    <CardHeader class="pb-2">
      <CardTitle class="text-sm font-medium">BG Delta</CardTitle>
    </CardHeader>
    <CardContent>
      <div class="text-2xl font-bold">
        {displayBgDelta > 0 ? "+" : ""}{displayBgDelta}
      </div>
      <p class="text-xs text-muted-foreground">mg/dL</p>
    </CardContent>
  </Card>

  <Card>
    <CardHeader class="pb-2">
      <CardTitle class="text-sm font-medium">Last Updated</CardTitle>
    </CardHeader>
    <CardContent>
      <div class="text-2xl font-bold">
        {timeAgo(displayLastUpdated)}
      </div>
      <p class="text-xs text-muted-foreground">
        {formatTime(displayLastUpdated)}
      </p>
    </CardContent>
  </Card>

  {#if showWebSocketStatus}
    <WebSocketStatus />
  {/if}
</div>