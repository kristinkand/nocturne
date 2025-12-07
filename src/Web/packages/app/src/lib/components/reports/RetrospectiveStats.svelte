<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import {
    Droplet,
    Syringe,
    Apple,
    TrendingUp,
    TrendingDown,
    Minus,
    Activity,
  } from "lucide-svelte";
  import type { RetrospectiveDataResponse } from "$lib/api";
  import { glucoseUnitsState } from "$lib/stores/glucose-units-store.svelte";
  import {
    formatGlucoseValue,
    getUnitLabel,
  } from "$lib/utils/glucose-formatting";

  interface Props {
    /** Data from the retrospective API */
    data: RetrospectiveDataResponse | null;
    /** Whether data is loading */
    isLoading?: boolean;
    /** Current scrub time for display */
    currentTime: Date;
  }

  let { data, isLoading = false, currentTime }: Props = $props();

  // Get units preference
  const units = $derived(glucoseUnitsState.units);
  const unitLabel = $derived(getUnitLabel(units));

  // Trend arrow component
  function getTrendIcon(direction: string | null | undefined) {
    if (!direction) return Minus;
    const dir = direction.toLowerCase();
    if (dir.includes("up") || dir.includes("rising")) return TrendingUp;
    if (dir.includes("down") || dir.includes("falling")) return TrendingDown;
    return Minus;
  }

  function getTrendColor(direction: string | null | undefined): string {
    if (!direction) return "text-muted-foreground";
    const dir = direction.toLowerCase();
    if (dir.includes("up") || dir.includes("rising")) return "text-yellow-500";
    if (dir.includes("down") || dir.includes("falling")) return "text-red-500";
    return "text-green-500";
  }

  // Format time for display
  const timeDisplay = $derived(
    currentTime.toLocaleTimeString(undefined, {
      hour: "2-digit",
      minute: "2-digit",
    })
  );
</script>

<Card.Root class="border-2 border-primary/20 bg-primary/5">
  <Card.Header class="pb-2">
    <Card.Title class="flex items-center gap-2 text-base">
      <Activity class="h-4 w-4" />
      Status at {timeDisplay}
    </Card.Title>
  </Card.Header>
  <Card.Content>
    {#if isLoading}
      <!-- Skeleton loading state - matches the layout below -->
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
        {#each [1, 2, 3, 4] as _}
          <div
            class="flex flex-col items-center gap-1 p-3 rounded-lg bg-background/50"
          >
            <div class="flex items-center gap-1">
              <div
                class="h-4 w-4 rounded bg-muted-foreground/20 animate-pulse"
              ></div>
              <div
                class="h-3 w-12 rounded bg-muted-foreground/20 animate-pulse"
              ></div>
            </div>
            <div
              class="h-8 w-16 rounded bg-muted-foreground/20 animate-pulse mt-1"
            ></div>
            <div
              class="h-3 w-20 rounded bg-muted-foreground/20 animate-pulse mt-1"
            ></div>
          </div>
        {/each}
      </div>
    {:else}
      <div class="grid grid-cols-2 md:grid-cols-4 gap-4">
        <!-- Glucose -->
        <div
          class="flex flex-col items-center gap-1 p-3 rounded-lg bg-background/50"
        >
          <div class="flex items-center gap-1 text-muted-foreground">
            <Droplet class="h-4 w-4" />
            <span class="text-xs font-medium">Glucose</span>
          </div>
          {#if data?.glucose}
            {@const TrendIcon = getTrendIcon(data.glucose.direction)}
            <div class="flex items-center gap-1">
              <span class="text-2xl font-bold tabular-nums">
                {formatGlucoseValue(data.glucose.value ?? 0, units)}
              </span>
              <span class="text-sm text-muted-foreground">{unitLabel}</span>
            </div>
            <div
              class="flex items-center gap-1 text-sm {getTrendColor(
                data.glucose.direction
              )}"
            >
              <TrendIcon class="h-4 w-4" />
              {#if data.glucose.delta !== null && data.glucose.delta !== undefined}
                <span class="tabular-nums">
                  {data.glucose.delta > 0 ? "+" : ""}{formatGlucoseValue(
                    Math.abs(data.glucose.delta),
                    units
                  )}
                </span>
              {/if}
            </div>
          {:else}
            <span class="text-2xl font-bold text-muted-foreground">—</span>
            <span class="text-xs text-muted-foreground">No data</span>
          {/if}
        </div>

        <!-- IOB -->
        <div
          class="flex flex-col items-center gap-1 p-3 rounded-lg bg-background/50"
        >
          <div class="flex items-center gap-1 text-muted-foreground">
            <Syringe class="h-4 w-4 text-blue-500" />
            <span class="text-xs font-medium">IOB</span>
          </div>
          {#if data?.iob}
            <div class="flex items-center gap-1">
              <span
                class="text-2xl font-bold tabular-nums text-blue-600 dark:text-blue-400"
              >
                {(data.iob.total ?? 0).toFixed(2)}
              </span>
              <span class="text-sm text-muted-foreground">U</span>
            </div>
            <div class="flex items-center gap-2 text-xs text-muted-foreground">
              <span>Bolus: {(data.iob.bolus ?? 0).toFixed(1)}U</span>
              {#if (data.iob.basal ?? 0) > 0}
                <span>Basal: {(data.iob.basal ?? 0).toFixed(1)}U</span>
              {/if}
            </div>
          {:else}
            <span class="text-2xl font-bold text-muted-foreground">—</span>
            <span class="text-xs text-muted-foreground">No data</span>
          {/if}
        </div>

        <!-- COB -->
        <div
          class="flex flex-col items-center gap-1 p-3 rounded-lg bg-background/50"
        >
          <div class="flex items-center gap-1 text-muted-foreground">
            <Apple class="h-4 w-4 text-orange-500" />
            <span class="text-xs font-medium">COB</span>
          </div>
          {#if data?.cob}
            <div class="flex items-center gap-1">
              <span
                class="text-2xl font-bold tabular-nums text-orange-600 dark:text-orange-400"
              >
                {(data.cob.total ?? 0).toFixed(0)}
              </span>
              <span class="text-sm text-muted-foreground">g</span>
            </div>
            <div class="text-xs text-muted-foreground">Carbs on Board</div>
          {:else}
            <span class="text-2xl font-bold text-muted-foreground">—</span>
            <span class="text-xs text-muted-foreground">No data</span>
          {/if}
        </div>

        <!-- Basal Rate -->
        <div
          class="flex flex-col items-center gap-1 p-3 rounded-lg bg-background/50"
        >
          <div class="flex items-center gap-1 text-muted-foreground">
            <Activity class="h-4 w-4 text-cyan-500" />
            <span class="text-xs font-medium">Basal</span>
          </div>
          {#if data?.basal}
            <div class="flex items-center gap-1">
              <span
                class="text-2xl font-bold tabular-nums text-cyan-600 dark:text-cyan-400"
              >
                {(data.basal.rate ?? 0).toFixed(2)}
              </span>
              <span class="text-sm text-muted-foreground">U/hr</span>
            </div>
            {#if data.basal.isTemp}
              <Badge variant="secondary" class="text-xs">Temp</Badge>
            {:else}
              <span class="text-xs text-muted-foreground">Scheduled</span>
            {/if}
          {:else}
            <span class="text-2xl font-bold text-muted-foreground">—</span>
            <span class="text-xs text-muted-foreground">No data</span>
          {/if}
        </div>
      </div>
    {/if}
  </Card.Content>
</Card.Root>
