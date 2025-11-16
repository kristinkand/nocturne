<script lang="ts">
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Separator } from "$lib/components/ui/separator";
  import { BarChart3, Activity, Calendar, AlertTriangle } from "lucide-svelte";
  import { AmbulatoryGlucoseProfile } from "$lib/components/ambulatory-glucose-profile";
  import TIRStackedChart from "$lib/components/reports/TIRStackedChart.svelte";
  import { type Entry } from "$lib/api";
  import { GlucoseChart } from "$lib/components/glucose-chart";
  import * as Pagination from "$lib/components/ui/pagination/index.js";
  import ChevronLeftIcon from "@lucide/svelte/icons/chevron-left";
  import ChevronRightIcon from "@lucide/svelte/icons/chevron-right";
  import { MediaQuery } from "svelte/reactivity";
  import CardFooter from "$lib/components/ui/card/card-footer.svelte";
  const isDesktop = new MediaQuery("(min-width: 768px)");
  const perPage = $derived(isDesktop.current ? 4 : 8);
  const siblingCount = $derived(isDesktop.current ? 1 : 0);

  let { data } = $props();

  const { entries, analysis: analysisPromise, dateRange } = data;

  const entriesByDay = $derived(
    Object.entries(
      entries.reduce(
        (acc, entry) => {
          const date = new Date(entry.mills ?? 0).toLocaleDateString();
          if (!date) {
            return acc;
          }
          if (!acc[date]) {
            acc[date] = [];
          }
          acc[date].push(entry);
          return acc;
        },
        {} as Record<string, Entry[]>
      )
    ).sort((a, b) => {
      // a[0] and b[0] are date strings produced by toLocaleDateString(); fall back to lexical sort if parsing fails
      const timeA = Date.parse(a[0]);
      const timeB = Date.parse(b[0]);
      if (isNaN(timeA) || isNaN(timeB)) {
        return a[0].localeCompare(b[0]);
      }
      return timeA - timeB;
    })
  );

  let dayByDayPage = $state(1);

  // analysisPromise resolves to GlucoseAnalytics in the template using an await block.
</script>

<svelte:head>
  <title>Ambulatory Glucose Profile - Nocturne Reports</title>
  <meta
    name="description"
    content="14-day glucose profile overlay with percentile bands and time-in-range breakdown"
  />
</svelte:head>

<div class="container mx-auto px-4 py-6 space-y-8">
  <!-- Header -->
  <div class="text-center space-y-3">
    <h1 class="text-4xl font-bold">Ambulatory Glucose Profile</h1>
    <p class="text-muted-foreground text-lg max-w-2xl mx-auto">
      Visualise your typical daily glucose pattern with percentile bands and key
      targets.
    </p>
  </div>

  <!-- Overview statistics & Time-in-Range -->
  {#await analysisPromise then analysis}
    {@const tir = analysis.timeInRange?.percentages ?? {}}
    <div class="grid grid-cols-1 md:grid-cols-3 gap-6">
      <!-- Glucose Statistics & Targets -->
      <Card class="border-2 md:col-span-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <Activity class="w-5 h-5" />
            Glucose Statistics &amp; Targets
          </CardTitle>
          <CardDescription>
            Snapshot of key metrics for the selected period
          </CardDescription>
        </CardHeader>
        <CardContent>
          <div class="grid grid-cols-2 sm:grid-cols-3 gap-4 text-sm">
            <div>
              <p class="text-muted-foreground">Average Glucose</p>
              <p class="text-2xl font-bold">
                {analysis.basicStats?.mean?.toFixed(1)}
                <span class="text-base font-normal">mg/dL</span>
              </p>
            </div>
            <div>
              <p class="text-muted-foreground">Estimated HbA1c</p>
              <p class="text-2xl font-bold text-red-600">
                {analysis.glycemicVariability?.estimatedA1c?.toFixed(2)}%
              </p>
            </div>
            <div>
              <p class="text-muted-foreground">Glucose CV</p>
              <p class="text-2xl font-bold text-purple-600">
                {analysis.glycemicVariability?.coefficientOfVariation?.toFixed(
                  1
                )}%
              </p>
            </div>
            <div>
              <p class="text-muted-foreground">Target</p>
              <p class="text-2xl font-bold text-green-600">
                {tir.target?.toFixed(1)}%
              </p>
            </div>
            <div>
              <p class="text-muted-foreground">Below Range</p>
              <p class="text-2xl font-bold text-red-600">
                {((tir.low ?? 0) + (tir.severeLow ?? 0)).toFixed(1)}%
              </p>
            </div>
            <div>
              <p class="text-muted-foreground">Above Range</p>
              <p class="text-2xl font-bold text-orange-600">
                {((tir.high ?? 0) + (tir.severeHigh ?? 0)).toFixed(1)}%
              </p>
            </div>
          </div>
        </CardContent>
      </Card>

      <!-- Time in Range chart -->
      <Card class="border-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <BarChart3 class="w-5 h-5" />
            Time in Range
          </CardTitle>
          <CardDescription>Percentage distribution of readings</CardDescription>
        </CardHeader>
        <CardContent class="h-72 md:h-96">
          <TIRStackedChart {entries} />
        </CardContent>
      </Card>
    </div>
  {:catch error}
    <Card class="border-2 border-destructive">
      <CardHeader>
        <CardTitle class="flex items-center gap-2 text-destructive">
          <AlertTriangle class="w-5 h-5" />
          Error Loading Statistics
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-destructive-foreground">{error.message}</p>
      </CardContent>
    </Card>
  {/await}

  <!-- AGP Chart -->
  <Card class="border-2">
    <CardHeader>
      <CardTitle class="flex items-center gap-2">
        <Calendar class="w-5 h-5" />
        Ambulatory Glucose Profile (Median &amp; Percentiles)
      </CardTitle>
    </CardHeader>
    <CardContent class="h-72 md:h-96">
      <AmbulatoryGlucoseProfile {entries} />
    </CardContent>
  </Card>

  <!-- Daily Chart -->
  <Card class="@container border-2">
    <CardHeader>
      <CardTitle class="flex items-center gap-2">
        <BarChart3 class="w-5 h-5" />
        Daily Chart
      </CardTitle>
    </CardHeader>
    <CardContent class="@min-md:grid-cols-4 grid">
      {#each entriesByDay.slice(dayByDayPage - 1 * perPage, dayByDayPage * perPage) as [date, entries]}
        <div class="p-2 h-48">
          {date}
          <GlucoseChart {entries} treatments={[]} />
        </div>
      {/each}
    </CardContent>
    <CardFooter>
      <Pagination.Root
        count={entriesByDay.length}
        {perPage}
        {siblingCount}
        bind:page={dayByDayPage}
      >
        {#snippet children({ pages, currentPage })}
          <Pagination.Content>
            <Pagination.Item>
              <Pagination.PrevButton>
                <ChevronLeftIcon class="size-4" />
                <span class="hidden sm:block">Previous</span>
              </Pagination.PrevButton>
            </Pagination.Item>
            {#each pages as page (page.key)}
              {#if page.type === "ellipsis"}
                <Pagination.Item>
                  <Pagination.Ellipsis />
                </Pagination.Item>
              {:else}
                <Pagination.Item>
                  <Pagination.Link {page} isActive={currentPage === page.value}>
                    {page.value}
                  </Pagination.Link>
                </Pagination.Item>
              {/if}
            {/each}
            <Pagination.Item>
              <Pagination.NextButton>
                <span class="hidden sm:block">Next</span>
                <ChevronRightIcon class="size-4" />
              </Pagination.NextButton>
            </Pagination.Item>
          </Pagination.Content>
        {/snippet}
      </Pagination.Root>
    </CardFooter>
  </Card>

  <Separator />

  <div class="text-xs text-muted-foreground text-center">
    Data from {new Date(dateRange.start).toLocaleDateString()} – {new Date(
      dateRange.end
    ).toLocaleDateString()}. Last updated {new Date(
      dateRange.lastUpdated
    ).toLocaleString()}.
  </div>
</div>

<!-- Fallback if top-level analysisPromise fails entirely (should be caught above) -->
{#await analysisPromise catch error}
  <Card class="border-2 border-destructive mt-6">
    <CardHeader>
      <CardTitle class="flex items-center gap-2 text-destructive">
        <AlertTriangle class="w-5 h-5" /> Error Loading AGP
      </CardTitle>
    </CardHeader>
    <CardContent>
      <p class="text-destructive-foreground">
        There was an error generating your AGP report. This usually means there
        is not enough data in the selected time range to perform the necessary
        calculations.
      </p>
      <p class="text-sm text-muted-foreground mt-2">
        Please select a larger date range or ensure you have sufficient glucose
        readings.
      </p>
      <pre
        class="mt-4 p-2 bg-muted rounded-md text-xs overflow-auto">{error.message}</pre>
    </CardContent>
  </Card>
{/await}
