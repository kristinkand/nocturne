<script lang="ts">
  import type { Entry } from "$lib/api";
  import { Badge } from "$lib/components/ui/badge";
  import { getDirectionInfo } from "$lib/utils";
  import { getRealtimeStore } from "$lib/stores/realtime-store.svelte";

  interface ComponentProps {
    entries?: Entry[];
    currentBG?: number;
    direction?: string;
    bgDelta?: number;
    demoMode?: boolean;
  }

  let { entries, currentBG, direction, bgDelta, demoMode }: ComponentProps =
    $props();

  const realtimeStore = getRealtimeStore();

  // Use realtime store values as fallback when props not provided
  const displayCurrentBG = $derived(currentBG ?? realtimeStore.currentBG);
  const displayDirection = $derived(direction ?? realtimeStore.direction);
  const displayBgDelta = $derived(bgDelta ?? realtimeStore.bgDelta);
  const displayDemoMode = $derived(demoMode ?? realtimeStore.demoMode);

  // const directionInfo = $derived(getDirectionInfo(displayDirection));
  // const Icon = $derived(directionInfo.icon);

  // Get background color based on BG value
  const getBGColor = (bg: number) => {
    if (bg < 70) return "bg-red-500";
    if (bg < 80) return "bg-yellow-500";
    if (bg > 250) return "bg-red-500";
    if (bg > 180) return "bg-orange-500";
    return "bg-green-500";
  };
</script>

<div class="flex items-center justify-between">
  <div class="flex items-center gap-4">
    <h1 class="text-3xl font-bold">Nocturne</h1>
    {#if displayDemoMode}
      <Badge variant="secondary" class="flex items-center gap-1">
        <div class="w-2 h-2 bg-blue-500 rounded-full animate-pulse"></div>
        Demo Mode
      </Badge>
    {/if}
  </div>
  <div class="flex items-center gap-4">
    <div class="flex items-center gap-2">
      <div class="relative">
        <div
          class="text-4xl font-bold {getBGColor(
            displayCurrentBG
          )} text-white px-4 py-2 rounded-lg"
        >
          {displayCurrentBG}
        </div>
      </div>
      <div class="text-center">
        <div class="text-2xl">
          <!-- {#if directionInfo.icon}
            {@const Icon = directionInfo.icon}
            <Icon class="inline w-6 h-6" />
          {/if} -->
          <!-- {directionInfo.label} -->
        </div>
        <div class="text-sm text-muted-foreground">
          {displayBgDelta > 0 ? "+" : ""}{displayBgDelta}
        </div>
      </div>
    </div>
  </div>
</div>
