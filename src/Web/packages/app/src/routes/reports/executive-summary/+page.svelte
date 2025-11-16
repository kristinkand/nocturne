<script lang="ts">
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Progress } from "$lib/components/ui/progress";
  import {
    Gauge,
    Target,
    TrendingUp,
    Shield,
    Clock,
    AlertTriangle,
  } from "lucide-svelte";

  export let data;

  const { analysis, dateRange } = data;
</script>

<svelte:head>
  <title>Executive Summary - Nocturne Reports</title>
  <meta
    name="description"
    content="High-level overview of your diabetes management metrics"
  />
</svelte:head>

<div class="container mx-auto px-4 py-6 space-y-8">
  <!-- Header -->
  <div class="text-center space-y-3">
    <h1 class="text-4xl font-bold">Executive Summary</h1>
    <p class="text-muted-foreground text-lg max-w-2xl mx-auto">
      A quick snapshot of your most important diabetes metrics. Use the links
      below each card to explore detailed reports.
    </p>
  </div>

  <!-- Key Metrics -->
  {#await data.analysis then analysis}
    {@const tir = analysis?.timeInRange?.percentages}
    <div class="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-6">
      <!-- Estimated HbA1c -->
      <Card class="border-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <Gauge class="w-5 h-5" />
            Estimated HbA1c
          </CardTitle>
          <CardDescription>
            Based on your average glucose during the selected period
          </CardDescription>
        </CardHeader>
        <CardContent class="text-center space-y-3">
          <div class="text-5xl font-bold text-red-600">
            {analysis?.glycemicVariability?.estimatedA1c?.toFixed(2) ?? "–"}%
          </div>
          <Button
            href="/reports/agp"
            size="sm"
            variant="outline"
            class="w-full"
          >
            View AGP Report
          </Button>
        </CardContent>
      </Card>

      <!-- Time in Range -->
      <Card class="border-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <Target class="w-5 h-5" />
            Time in Range
          </CardTitle>
          <CardDescription>
            Percentage of readings within your target range
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-4">
          <div class="text-center text-5xl font-bold text-green-600">
            {tir?.target?.toFixed(1) ?? "–"}%
          </div>
          <!-- TIR Breakdown -->
          <div class="space-y-2">
            <div class="flex justify-between text-xs">
              <span>Target</span>
              <span>{tir?.target?.toFixed(1) ?? "–"}%</span>
            </div>
            <Progress value={tir.target} max={100} />
            <div class="flex justify-between text-xs">
              <span>Low</span>
              <span>{(tir.low ?? 0) + (tir.severeLow ?? 0)}%</span>
            </div>
            <Progress
              value={(tir.low ?? 0) + (tir.severeLow ?? 0)}
              max={100}
              class="bg-red-200"
            />
            <div class="flex justify-between text-xs">
              <span>High</span>
              <span>{(tir.high ?? 0) + (tir.severeHigh ?? 0)}%</span>
            </div>
            <Progress
              value={(tir.high ?? 0) + (tir.severeHigh ?? 0)}
              max={100}
              class="bg-orange-200"
            />
          </div>
          <Button
            href="/reports/time-in-range"
            size="sm"
            variant="outline"
            class="w-full"
          >
            Detailed TIR Analysis
          </Button>
        </CardContent>
      </Card>

      <!-- Glycemic Variability -->
      <Card class="border-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <TrendingUp class="w-5 h-5" />
            Variability (CV)
          </CardTitle>
          <CardDescription>
            Coefficient of variation of your glucose readings
          </CardDescription>
        </CardHeader>
        <CardContent class="text-center space-y-3">
          <div class="text-5xl font-bold text-purple-600">
            {analysis?.glycemicVariability?.coefficientOfVariation?.toFixed(
              1
            ) ?? "–"}%
          </div>
          <Button
            href="/reports/variability"
            size="sm"
            variant="outline"
            class="w-full"
          >
            View Variability Report
          </Button>
        </CardContent>
      </Card>
    </div>
  {:catch error}
    <Card class="border-2 border-destructive">
      <CardHeader>
        <CardTitle class="flex items-center gap-2 text-destructive">
          <AlertTriangle class="w-5 h-5" />
          Error Loading Analytics
        </CardTitle>
      </CardHeader>
      <CardContent>
        <p class="text-destructive-foreground">
          There was an error generating your analytics report. This usually
          means there is not enough data in the selected time range to perform
          the necessary calculations.
        </p>
        <p class="text-sm text-muted-foreground mt-2">
          Please select a larger date range or ensure you have sufficient
          glucose readings.
        </p>
        <pre class="mt-4 p-2 bg-muted rounded-md text-xs overflow-auto">
            {error.message}
          </pre>
      </CardContent>
    </Card>
  {/await}

  <!-- Additional Insights -->
  <div class="grid grid-cols-1 md:grid-cols-2 gap-6 pt-6">
    {#await data.analysis then analysis}
      <!-- Data Quality -->
      <Card class="border-2">
        <CardHeader>
          <CardTitle class="flex items-center gap-2">
            <Shield class="w-5 h-5" />
            Data Quality
          </CardTitle>
          <CardDescription>
            CGM sensor uptime and completeness of your data
          </CardDescription>
        </CardHeader>
        <CardContent class="space-y-3">
          <div class="flex justify-between text-sm">
            <span>CGM Active</span>
            <span class="font-semibold">
              {analysis?.dataQuality?.cgmActivePercent?.toFixed(1) ?? "–"}%
            </span>
          </div>

          <Progress
            value={analysis?.dataQuality?.cgmActivePercent ?? 0}
            max={100}
          />
          <Button
            href="/reports/data-quality"
            size="sm"
            variant="outline"
            class="w-full mt-2"
          >
            Data Quality Report
          </Button>
        </CardContent>
      </Card>
    {/await}
    <!-- Days of Data -->
    <Card class="border-2">
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Clock class="w-5 h-5" />
          Days of Data
        </CardTitle>
        <CardDescription>Coverage in the selected period</CardDescription>
      </CardHeader>
      <CardContent class="text-center space-y-3">
        <div class="text-5xl font-bold text-blue-600">
          {Math.round(
            (new Date(dateRange.end).getTime() -
              new Date(dateRange.start).getTime()) /
              (1000 * 60 * 60 * 24) +
              1
          )}
        </div>
        <Button
          href="/reports/pattern-recognition"
          size="sm"
          variant="outline"
          class="w-full"
        >
          Explore Patterns
        </Button>
      </CardContent>
    </Card>
  </div>

  <!-- Footer -->
  <div class="text-xs text-muted-foreground text-center">
    Last updated: {new Date(dateRange.lastUpdated).toLocaleString()}
  </div>
</div>
