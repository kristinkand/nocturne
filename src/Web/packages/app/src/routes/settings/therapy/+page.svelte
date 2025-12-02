<script lang="ts">
  import {
    getSettingsStore,
    formatTime,
  } from "$lib/stores/settings-store.svelte";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import { Badge } from "$lib/components/ui/badge";
  import {
    Tabs,
    TabsContent,
    TabsList,
    TabsTrigger,
  } from "$lib/components/ui/tabs";
  import {
    Syringe,
    Target,
    Clock,
    Plus,
    Trash2,
    Edit,
    AlertCircle,
    Info,
    Loader2,
  } from "lucide-svelte";

  const store = getSettingsStore();

  // Track which period is being edited (for inline time editing)
  let editingPeriodIndex = $state<number | null>(null);
  let editingPeriodType = $state<"carb" | "isf" | "basal" | null>(null);

  function updateCarbRatioValue(index: number, value: number) {
    if (store.therapy?.carbRatios) {
      store.therapy.carbRatios[index].value = value;
      store.markChanged();
    }
  }

  function updateCarbRatioTime(index: number, time: string) {
    if (store.therapy?.carbRatios) {
      store.therapy.carbRatios[index].time = time;
      store.markChanged();
    }
  }

  function updateIsfValue(index: number, value: number) {
    if (store.therapy?.insulinSensitivity) {
      store.therapy.insulinSensitivity[index].value = value;
      store.markChanged();
    }
  }

  function updateIsfTime(index: number, time: string) {
    if (store.therapy?.insulinSensitivity) {
      store.therapy.insulinSensitivity[index].time = time;
      store.markChanged();
    }
  }

  function updateBasalValue(index: number, value: number) {
    if (store.therapy?.basalRates) {
      store.therapy.basalRates[index].value = value;
      store.markChanged();
    }
  }

  function updateBasalTime(index: number, time: string) {
    if (store.therapy?.basalRates) {
      store.therapy.basalRates[index].time = time;
      store.markChanged();
    }
  }

  function calculateTotalDailyBasal(): number {
    if (!store.therapy?.basalRates || store.therapy.basalRates.length === 0) {
      return 0;
    }

    // Sort by time
    const sorted = [...store.therapy.basalRates].sort((a, b) =>
      (a.time ?? "00:00").localeCompare(b.time ?? "00:00")
    );

    let total = 0;
    for (let i = 0; i < sorted.length; i++) {
      const current = sorted[i];
      const next = sorted[(i + 1) % sorted.length];

      const currentMinutes = timeToMinutes(current.time ?? "00:00");
      let nextMinutes = timeToMinutes(next.time ?? "00:00");

      // Handle wrap around midnight
      if (nextMinutes <= currentMinutes) {
        nextMinutes += 24 * 60;
      }

      const duration = (nextMinutes - currentMinutes) / 60; // hours
      total += (current.value ?? 0) * duration;
    }

    return total;
  }

  function timeToMinutes(time: string): number {
    const [hours, minutes] = time.split(":").map(Number);
    return hours * 60 + minutes;
  }
</script>

<svelte:head>
  <title>Therapy - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Therapy Settings</h1>
    <p class="text-muted-foreground">
      Configure your insulin ratios, sensitivity factors, and glucose targets
    </p>
  </div>

  {#if store.isLoading}
    <div class="flex items-center justify-center py-12">
      <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
    </div>
  {:else if store.hasError}
    <Card class="border-destructive">
      <CardContent class="flex items-center gap-3 pt-6">
        <AlertCircle class="h-5 w-5 text-destructive" />
        <div>
          <p class="font-medium">Failed to load settings</p>
          <p class="text-sm text-muted-foreground">{store.error}</p>
        </div>
      </CardContent>
    </Card>
  {:else if store.therapy}
    <!-- Alert about profile sync -->
    <Card
      class="border-blue-200 bg-blue-50/50 dark:border-blue-900 dark:bg-blue-950/20"
    >
      <CardContent class="flex items-start gap-3 pt-6">
        <Info
          class="h-5 w-5 text-blue-600 dark:text-blue-400 shrink-0 mt-0.5"
        />
        <div>
          <p class="font-medium text-blue-900 dark:text-blue-100">
            Profile Sync
          </p>
          <p class="text-sm text-blue-800 dark:text-blue-200">
            These settings are synced with your Nightscout profile. Changes here
            will update your profile.
          </p>
        </div>
      </CardContent>
    </Card>

    <Tabs value="ratios" class="w-full">
      <TabsList class="grid w-full grid-cols-4">
        <TabsTrigger value="ratios">Carb Ratios</TabsTrigger>
        <TabsTrigger value="sensitivity">Sensitivity</TabsTrigger>
        <TabsTrigger value="targets">Targets</TabsTrigger>
        <TabsTrigger value="basal">Basal</TabsTrigger>
      </TabsList>

      <!-- Carb Ratios Tab -->
      <TabsContent value="ratios" class="space-y-4 mt-4">
        <Card>
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle class="flex items-center gap-2">
                  <Syringe class="h-5 w-5" />
                  Insulin-to-Carb Ratios
                </CardTitle>
                <CardDescription>
                  How many grams of carbs one unit of insulin covers
                </CardDescription>
              </div>
              <Button
                size="sm"
                variant="outline"
                class="gap-2"
                onclick={() => store.addCarbRatio()}
              >
                <Plus class="h-4 w-4" />
                Add Period
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <div class="space-y-3">
              {#each store.therapy.carbRatios ?? [] as period, index}
                <div class="flex items-center gap-4 p-3 rounded-lg border">
                  <div class="flex items-center gap-2 min-w-[140px]">
                    <Clock class="h-4 w-4 text-muted-foreground" />
                    {#if editingPeriodType === "carb" && editingPeriodIndex === index}
                      <Input
                        type="time"
                        value={period.time ?? "00:00"}
                        class="w-28"
                        onchange={(e) => {
                          updateCarbRatioTime(index, e.currentTarget.value);
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                        onblur={() => {
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                      />
                    {:else}
                      <button
                        class="font-medium hover:underline"
                        onclick={() => {
                          editingPeriodIndex = index;
                          editingPeriodType = "carb";
                        }}
                      >
                        {formatTime(period.time ?? "00:00")}
                      </button>
                    {/if}
                  </div>
                  <div class="flex items-center gap-2 flex-1">
                    <Label class="text-muted-foreground">1u :</Label>
                    <Input
                      type="number"
                      value={period.value ?? 0}
                      class="w-20"
                      min="1"
                      max="100"
                      onchange={(e) =>
                        updateCarbRatioValue(
                          index,
                          parseFloat(e.currentTarget.value)
                        )}
                    />
                    <span class="text-muted-foreground">g carbs</span>
                  </div>
                  <div class="flex items-center gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => {
                        editingPeriodIndex = index;
                        editingPeriodType = "carb";
                      }}
                    >
                      <Edit class="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      class="text-destructive"
                      disabled={(store.therapy?.carbRatios?.length ?? 0) <= 1}
                      onclick={() => store.removeCarbRatio(index)}
                    >
                      <Trash2 class="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              {/each}
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- Sensitivity Tab -->
      <TabsContent value="sensitivity" class="space-y-4 mt-4">
        <Card>
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle class="flex items-center gap-2">
                  <Target class="h-5 w-5" />
                  Insulin Sensitivity Factor (ISF)
                </CardTitle>
                <CardDescription>
                  How much one unit of insulin lowers your blood glucose
                </CardDescription>
              </div>
              <Button
                size="sm"
                variant="outline"
                class="gap-2"
                onclick={() => store.addInsulinSensitivity()}
              >
                <Plus class="h-4 w-4" />
                Add Period
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <div class="space-y-3">
              {#each store.therapy.insulinSensitivity ?? [] as period, index}
                <div class="flex items-center gap-4 p-3 rounded-lg border">
                  <div class="flex items-center gap-2 min-w-[140px]">
                    <Clock class="h-4 w-4 text-muted-foreground" />
                    {#if editingPeriodType === "isf" && editingPeriodIndex === index}
                      <Input
                        type="time"
                        value={period.time ?? "00:00"}
                        class="w-28"
                        onchange={(e) => {
                          updateIsfTime(index, e.currentTarget.value);
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                        onblur={() => {
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                      />
                    {:else}
                      <button
                        class="font-medium hover:underline"
                        onclick={() => {
                          editingPeriodIndex = index;
                          editingPeriodType = "isf";
                        }}
                      >
                        {formatTime(period.time ?? "00:00")}
                      </button>
                    {/if}
                  </div>
                  <div class="flex items-center gap-2 flex-1">
                    <Label class="text-muted-foreground">1u :</Label>
                    <Input
                      type="number"
                      value={period.value ?? 0}
                      class="w-20"
                      min="1"
                      max="500"
                      onchange={(e) =>
                        updateIsfValue(
                          index,
                          parseFloat(e.currentTarget.value)
                        )}
                    />
                    <span class="text-muted-foreground">
                      {store.therapy.units ?? "mg/dl"}
                    </span>
                  </div>
                  <div class="flex items-center gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => {
                        editingPeriodIndex = index;
                        editingPeriodType = "isf";
                      }}
                    >
                      <Edit class="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      class="text-destructive"
                      disabled={(store.therapy?.insulinSensitivity?.length ??
                        0) <= 1}
                      onclick={() => store.removeInsulinSensitivity(index)}
                    >
                      <Trash2 class="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              {/each}
            </div>
          </CardContent>
        </Card>

        <Card>
          <CardHeader>
            <CardTitle>Active Insulin Time</CardTitle>
            <CardDescription>
              How long insulin remains active in your body
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-4">
            <div class="grid gap-4 sm:grid-cols-2">
              <div class="space-y-2">
                <Label>Duration (DIA)</Label>
                <div class="flex items-center gap-2">
                  <Input
                    type="number"
                    value={store.therapy.activeInsulin?.duration ?? 4}
                    class="w-24"
                    min="2"
                    max="8"
                    step="0.5"
                    onchange={(e) => {
                      if (store.therapy?.activeInsulin) {
                        store.therapy.activeInsulin.duration = parseFloat(
                          e.currentTarget.value
                        );
                        store.markChanged();
                      }
                    }}
                  />
                  <span class="text-muted-foreground">hours</span>
                </div>
              </div>
              <div class="space-y-2">
                <Label>Peak activity</Label>
                <div class="flex items-center gap-2">
                  <Input
                    type="number"
                    value={store.therapy.activeInsulin?.peak ?? 75}
                    class="w-24"
                    min="30"
                    max="180"
                    onchange={(e) => {
                      if (store.therapy?.activeInsulin) {
                        store.therapy.activeInsulin.peak = parseInt(
                          e.currentTarget.value
                        );
                        store.markChanged();
                      }
                    }}
                  />
                  <span class="text-muted-foreground">minutes</span>
                </div>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- Targets Tab -->
      <TabsContent value="targets" class="space-y-4 mt-4">
        <Card>
          <CardHeader>
            <CardTitle>Blood Glucose Targets</CardTitle>
            <CardDescription>
              Target range and urgent thresholds for alerts
            </CardDescription>
          </CardHeader>
          <CardContent class="space-y-6">
            <div class="space-y-4">
              <Label class="text-base font-medium">Target Range</Label>
              <div class="grid gap-4 sm:grid-cols-2">
                <div class="space-y-2">
                  <Label class="text-muted-foreground">Low target</Label>
                  <div class="flex items-center gap-2">
                    <Input
                      type="number"
                      value={store.therapy.bgTargets?.targetLow ?? 80}
                      class="w-24"
                      onchange={(e) => {
                        if (store.therapy?.bgTargets) {
                          store.therapy.bgTargets.targetLow = parseInt(
                            e.currentTarget.value
                          );
                          store.markChanged();
                        }
                      }}
                    />
                    <span class="text-muted-foreground">
                      {store.therapy.units ?? "mg/dl"}
                    </span>
                  </div>
                </div>
                <div class="space-y-2">
                  <Label class="text-muted-foreground">High target</Label>
                  <div class="flex items-center gap-2">
                    <Input
                      type="number"
                      value={store.therapy.bgTargets?.targetHigh ?? 120}
                      class="w-24"
                      onchange={(e) => {
                        if (store.therapy?.bgTargets) {
                          store.therapy.bgTargets.targetHigh = parseInt(
                            e.currentTarget.value
                          );
                          store.markChanged();
                        }
                      }}
                    />
                    <span class="text-muted-foreground">
                      {store.therapy.units ?? "mg/dl"}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <Separator />

            <div class="space-y-4">
              <Label class="text-base font-medium flex items-center gap-2">
                <AlertCircle class="h-4 w-4 text-destructive" />
                Urgent Thresholds
              </Label>
              <div class="grid gap-4 sm:grid-cols-2">
                <div class="space-y-2">
                  <Label class="text-muted-foreground">Urgent low</Label>
                  <div class="flex items-center gap-2">
                    <Input
                      type="number"
                      value={store.therapy.bgTargets?.urgentLow ?? 55}
                      class="w-24 border-red-200 focus:border-red-500"
                      onchange={(e) => {
                        if (store.therapy?.bgTargets) {
                          store.therapy.bgTargets.urgentLow = parseInt(
                            e.currentTarget.value
                          );
                          store.markChanged();
                        }
                      }}
                    />
                    <span class="text-muted-foreground">
                      {store.therapy.units ?? "mg/dl"}
                    </span>
                  </div>
                </div>
                <div class="space-y-2">
                  <Label class="text-muted-foreground">Urgent high</Label>
                  <div class="flex items-center gap-2">
                    <Input
                      type="number"
                      value={store.therapy.bgTargets?.urgentHigh ?? 250}
                      class="w-24 border-orange-200 focus:border-orange-500"
                      onchange={(e) => {
                        if (store.therapy?.bgTargets) {
                          store.therapy.bgTargets.urgentHigh = parseInt(
                            e.currentTarget.value
                          );
                          store.markChanged();
                        }
                      }}
                    />
                    <span class="text-muted-foreground">
                      {store.therapy.units ?? "mg/dl"}
                    </span>
                  </div>
                </div>
              </div>
            </div>

            <!-- Visual range indicator -->
            <div class="mt-6 p-4 bg-muted/50 rounded-lg">
              <div class="text-sm text-muted-foreground mb-2">
                Current ranges:
              </div>
              <div class="flex flex-wrap items-center gap-2 text-sm">
                <Badge variant="destructive">
                  Urgent Low &lt;{store.therapy.bgTargets?.urgentLow ?? 55}
                </Badge>
                <Badge variant="secondary">
                  Low {store.therapy.bgTargets?.urgentLow ?? 55}-{store.therapy
                    .bgTargets?.targetLow ?? 80}
                </Badge>
                <Badge
                  class="bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100"
                >
                  Target {store.therapy.bgTargets?.targetLow ?? 80}-{store
                    .therapy.bgTargets?.targetHigh ?? 120}
                </Badge>
                <Badge variant="secondary">
                  High {store.therapy.bgTargets?.targetHigh ?? 120}-{store
                    .therapy.bgTargets?.urgentHigh ?? 250}
                </Badge>
                <Badge
                  class="bg-orange-100 text-orange-800 dark:bg-orange-900 dark:text-orange-100"
                >
                  Urgent High &gt;{store.therapy.bgTargets?.urgentHigh ?? 250}
                </Badge>
              </div>
            </div>
          </CardContent>
        </Card>
      </TabsContent>

      <!-- Basal Tab -->
      <TabsContent value="basal" class="space-y-4 mt-4">
        <Card>
          <CardHeader>
            <div class="flex items-center justify-between">
              <div>
                <CardTitle>Basal Rates</CardTitle>
                <CardDescription>
                  Your background insulin delivery schedule
                </CardDescription>
              </div>
              <Button
                size="sm"
                variant="outline"
                class="gap-2"
                onclick={() => store.addBasalRate()}
              >
                <Plus class="h-4 w-4" />
                Add Period
              </Button>
            </div>
          </CardHeader>
          <CardContent>
            <div class="space-y-3">
              {#each store.therapy.basalRates ?? [] as period, index}
                <div class="flex items-center gap-4 p-3 rounded-lg border">
                  <div class="flex items-center gap-2 min-w-[140px]">
                    <Clock class="h-4 w-4 text-muted-foreground" />
                    {#if editingPeriodType === "basal" && editingPeriodIndex === index}
                      <Input
                        type="time"
                        value={period.time ?? "00:00"}
                        class="w-28"
                        onchange={(e) => {
                          updateBasalTime(index, e.currentTarget.value);
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                        onblur={() => {
                          editingPeriodIndex = null;
                          editingPeriodType = null;
                        }}
                      />
                    {:else}
                      <button
                        class="font-medium hover:underline"
                        onclick={() => {
                          editingPeriodIndex = index;
                          editingPeriodType = "basal";
                        }}
                      >
                        {formatTime(period.time ?? "00:00")}
                      </button>
                    {/if}
                  </div>
                  <div class="flex items-center gap-2 flex-1">
                    <Input
                      type="number"
                      value={period.value ?? 0}
                      class="w-24"
                      min="0"
                      max="10"
                      step="0.05"
                      onchange={(e) =>
                        updateBasalValue(
                          index,
                          parseFloat(e.currentTarget.value)
                        )}
                    />
                    <span class="text-muted-foreground">U/hr</span>
                  </div>
                  <div class="flex items-center gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      onclick={() => {
                        editingPeriodIndex = index;
                        editingPeriodType = "basal";
                      }}
                    >
                      <Edit class="h-4 w-4" />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      class="text-destructive"
                      disabled={(store.therapy?.basalRates?.length ?? 0) <= 1}
                      onclick={() => store.removeBasalRate(index)}
                    >
                      <Trash2 class="h-4 w-4" />
                    </Button>
                  </div>
                </div>
              {/each}
            </div>

            <Separator class="my-4" />

            <div class="flex items-center justify-between text-sm">
              <span class="text-muted-foreground">Total daily basal:</span>
              <span class="font-medium">
                {calculateTotalDailyBasal().toFixed(2)} U
                <span class="text-muted-foreground font-normal">
                  (approximate)
                </span>
              </span>
            </div>
          </CardContent>
        </Card>
      </TabsContent>
    </Tabs>
  {/if}
</div>
