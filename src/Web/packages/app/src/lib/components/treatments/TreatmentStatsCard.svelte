<script lang="ts">
  import * as Card from "$lib/components/ui/card";
  import { Badge } from "$lib/components/ui/badge";
  import type { TreatmentStats } from "$lib/constants/treatment-categories";
  import { Syringe, Apple, Activity, TrendingUp, Clock } from "lucide-svelte";

  interface Props {
    stats: TreatmentStats;
    dateRange: { from: string; to: string };
  }

  let { stats, dateRange }: Props = $props();

  // Calculate days in range
  let daysInRange = $derived.by(() => {
    const from = new Date(dateRange.from);
    const to = new Date(dateRange.to);
    return Math.max(
      1,
      Math.ceil((to.getTime() - from.getTime()) / (1000 * 60 * 60 * 24))
    );
  });

  // Calculate daily averages
  let dailyAvgBoluses = $derived(stats.withInsulin / daysInRange);
  let dailyAvgCarbs = $derived(stats.totalCarbs / daysInRange);
  let dailyAvgInsulin = $derived(stats.totalInsulin / daysInRange);

  // Get top event types (sorted by count)
  let topEventTypes = $derived(
    Object.entries(stats.byEventTypeCount)
      .sort((a, b) => b[1] - a[1])
      .slice(0, 5)
  );
</script>

<div class="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-5 gap-4">
  <!-- Total Treatments -->
  <Card.Root class="bg-card">
    <Card.Content class="p-4">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-sm font-medium text-muted-foreground">Total</p>
          <p class="text-2xl font-bold tabular-nums">{stats.total}</p>
        </div>
        <div
          class="h-10 w-10 rounded-lg bg-primary/10 flex items-center justify-center"
        >
          <Activity class="h-5 w-5 text-primary" />
        </div>
      </div>
      <div class="mt-2 flex flex-wrap gap-1">
        {#each topEventTypes as [eventType, count]}
          <Badge variant="secondary" class="text-[10px] px-1.5 py-0">
            {eventType}
            <span class="opacity-70">{count}</span>
          </Badge>
        {/each}
      </div>
    </Card.Content>
  </Card.Root>

  <!-- Total Insulin -->
  <Card.Root class="bg-card">
    <Card.Content class="p-4">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-sm font-medium text-muted-foreground">Insulin</p>
          <p
            class="text-2xl font-bold tabular-nums text-blue-600 dark:text-blue-400"
          >
            {stats.totalInsulin.toFixed(1)}U
          </p>
        </div>
        <div
          class="h-10 w-10 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center"
        >
          <Syringe class="h-5 w-5 text-blue-600 dark:text-blue-400" />
        </div>
      </div>
      <p class="text-xs text-muted-foreground mt-2">
        {dailyAvgInsulin.toFixed(1)}U/day avg
      </p>
    </Card.Content>
  </Card.Root>

  <!-- Bolus Count -->
  <Card.Root class="bg-card">
    <Card.Content class="p-4">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-sm font-medium text-muted-foreground">Boluses</p>
          <p class="text-2xl font-bold tabular-nums">{stats.withInsulin}</p>
        </div>
        <div
          class="h-10 w-10 rounded-lg bg-purple-100 dark:bg-purple-900/30 flex items-center justify-center"
        >
          <TrendingUp class="h-5 w-5 text-purple-600 dark:text-purple-400" />
        </div>
      </div>
      <p class="text-xs text-muted-foreground mt-2">
        {dailyAvgBoluses.toFixed(1)}/day • {stats.avgInsulinPerBolus.toFixed(
          1
        )}U avg
      </p>
    </Card.Content>
  </Card.Root>

  <!-- Total Carbs -->
  <Card.Root class="bg-card">
    <Card.Content class="p-4">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-sm font-medium text-muted-foreground">Carbs</p>
          <p
            class="text-2xl font-bold tabular-nums text-green-600 dark:text-green-400"
          >
            {stats.totalCarbs.toFixed(0)}g
          </p>
        </div>
        <div
          class="h-10 w-10 rounded-lg bg-green-100 dark:bg-green-900/30 flex items-center justify-center"
        >
          <Apple class="h-5 w-5 text-green-600 dark:text-green-400" />
        </div>
      </div>
      <p class="text-xs text-muted-foreground mt-2">
        {dailyAvgCarbs.toFixed(0)}g/day avg
      </p>
    </Card.Content>
  </Card.Root>

  <!-- Carb Entries -->
  <Card.Root class="bg-card">
    <Card.Content class="p-4">
      <div class="flex items-center justify-between">
        <div>
          <p class="text-sm font-medium text-muted-foreground">Meals</p>
          <p class="text-2xl font-bold tabular-nums">{stats.withCarbs}</p>
        </div>
        <div
          class="h-10 w-10 rounded-lg bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center"
        >
          <Clock class="h-5 w-5 text-amber-600 dark:text-amber-400" />
        </div>
      </div>
      <p class="text-xs text-muted-foreground mt-2">
        {stats.avgCarbsPerEntry.toFixed(0)}g avg/meal
      </p>
    </Card.Content>
  </Card.Root>
</div>
