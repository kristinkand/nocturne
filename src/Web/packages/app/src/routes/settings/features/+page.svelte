<script lang="ts">
  import { getSettingsStore } from "$lib/stores/settings-store.svelte";
  import {
    Card,
    CardContent,
    CardDescription,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Button } from "$lib/components/ui/button";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
  } from "$lib/components/ui/select";
  import {
    Sparkles,
    Eye,
    Palette,
    Layout,
    BarChart3,
    Syringe,
    Activity,
    Clock,
    Moon,
    Sun,
    Pill,
    Droplets,
    Battery,
    Timer,
    Loader2,
    AlertCircle,
  } from "lucide-svelte";

  const store = getSettingsStore();

  function enableAllPlugins() {
    if (store.features?.plugins) {
      Object.keys(store.features.plugins).forEach((key) => {
        store.features!.plugins![key].enabled = true;
      });
      store.markChanged();
    }
  }

  function disableAllPlugins() {
    if (store.features?.plugins) {
      Object.keys(store.features.plugins).forEach((key) => {
        store.features!.plugins![key].enabled = false;
      });
      store.markChanged();
    }
  }
</script>

<svelte:head>
  <title>Features - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Features</h1>
    <p class="text-muted-foreground">
      Customize display options, plugins, and dashboard widgets
    </p>
  </div>

  {#if store.isLoading}
    <div class="flex items-center justify-center py-12">
      <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
    </div>
  {:else if store.hasError}
    <Card class="border-destructive">
      <CardContent class="flex items-center gap-3 py-6">
        <AlertCircle class="h-5 w-5 text-destructive" />
        <div>
          <p class="font-medium">Failed to load settings</p>
          <p class="text-sm text-muted-foreground">{store.error}</p>
        </div>
      </CardContent>
    </Card>
  {:else if store.features}
    <!-- Display Settings -->
    <Card>
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Eye class="h-5 w-5" />
          Display Settings
        </CardTitle>
        <CardDescription>
          Customize how information is displayed
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <div class="grid gap-4 sm:grid-cols-2">
          <div class="space-y-2">
            <Label>Theme</Label>
            <Select
              type="single"
              value={store.features.display?.theme ?? "system"}
              onValueChange={(value) => {
                if (store.features?.display) {
                  store.features.display.theme = value;
                  store.markChanged();
                }
              }}
            >
              <SelectTrigger>
                <span class="flex items-center gap-2">
                  {#if store.features.display?.theme === "light"}
                    <Sun class="h-4 w-4" />
                    Light
                  {:else if store.features.display?.theme === "dark"}
                    <Moon class="h-4 w-4" />
                    Dark
                  {:else}
                    <Palette class="h-4 w-4" />
                    System
                  {/if}
                </span>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="system">System</SelectItem>
                <SelectItem value="light">Light</SelectItem>
                <SelectItem value="dark">Dark</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div class="space-y-2">
            <Label>Time format</Label>
            <Select
              type="single"
              value={store.features.display?.timeFormat ?? "12"}
              onValueChange={(value) => {
                if (store.features?.display) {
                  store.features.display.timeFormat = value;
                  store.markChanged();
                }
              }}
            >
              <SelectTrigger>
                <span>
                  {store.features.display?.timeFormat === "12"
                    ? "12-hour (AM/PM)"
                    : "24-hour"}
                </span>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="12">12-hour (AM/PM)</SelectItem>
                <SelectItem value="24">24-hour</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <div class="grid gap-4 sm:grid-cols-2">
          <div class="space-y-2">
            <Label>Blood glucose units</Label>
            <Select
              type="single"
              value={store.features.display?.units ?? "mg/dl"}
              onValueChange={(value) => {
                if (store.features?.display) {
                  store.features.display.units = value;
                  store.markChanged();
                }
              }}
            >
              <SelectTrigger>
                <span>
                  {store.features.display?.units === "mg/dl"
                    ? "mg/dL"
                    : "mmol/L"}
                </span>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="mg/dl">mg/dL</SelectItem>
                <SelectItem value="mmol">mmol/L</SelectItem>
              </SelectContent>
            </Select>
          </div>

          <div class="space-y-2">
            <Label>Default chart range</Label>
            <Select
              type="single"
              value={String(store.features.display?.focusHours ?? 3)}
              onValueChange={(value) => {
                if (store.features?.display) {
                  store.features.display.focusHours = parseInt(value);
                  store.markChanged();
                }
              }}
            >
              <SelectTrigger>
                <span>{store.features.display?.focusHours ?? 3} hours</span>
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="1">1 hour</SelectItem>
                <SelectItem value="2">2 hours</SelectItem>
                <SelectItem value="3">3 hours</SelectItem>
                <SelectItem value="6">6 hours</SelectItem>
                <SelectItem value="12">12 hours</SelectItem>
                <SelectItem value="24">24 hours</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>

        <Separator />

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Night mode schedule</Label>
            <p class="text-sm text-muted-foreground">
              Automatically switch to dark theme at night
            </p>
          </div>
          <Switch
            checked={store.features.display?.nightMode ?? false}
            onCheckedChange={(checked) => {
              if (store.features?.display) {
                store.features.display.nightMode = checked;
                store.markChanged();
              }
            }}
          />
        </div>

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Show raw sensor values</Label>
            <p class="text-sm text-muted-foreground">
              Display unfiltered CGM data alongside calibrated values
            </p>
          </div>
          <Switch
            checked={store.features.display?.showRawBG ?? false}
            onCheckedChange={(checked) => {
              if (store.features?.display) {
                store.features.display.showRawBG = checked;
                store.markChanged();
              }
            }}
          />
        </div>
      </CardContent>
    </Card>

    <!-- Dashboard Widgets -->
    <Card>
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Layout class="h-5 w-5" />
          Dashboard Widgets
        </CardTitle>
        <CardDescription>
          Choose which widgets appear on your dashboard
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="grid gap-4 sm:grid-cols-2">
          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <Activity class="h-5 w-5 text-muted-foreground" />
              <Label>Glucose Chart</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.glucoseChart ?? true}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.glucoseChart = checked;
                  store.markChanged();
                }
              }}
            />
          </div>

          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <BarChart3 class="h-5 w-5 text-muted-foreground" />
              <Label>Statistics</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.statistics ?? true}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.statistics = checked;
                  store.markChanged();
                }
              }}
            />
          </div>

          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <Syringe class="h-5 w-5 text-muted-foreground" />
              <Label>Treatments</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.treatments ?? true}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.treatments = checked;
                  store.markChanged();
                }
              }}
            />
          </div>

          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <Activity class="h-5 w-5 text-muted-foreground" />
              <Label>Predictions</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.predictions ?? true}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.predictions = checked;
                  store.markChanged();
                }
              }}
            />
          </div>

          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <BarChart3 class="h-5 w-5 text-muted-foreground" />
              <Label>AGP Summary</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.agp ?? false}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.agp = checked;
                  store.markChanged();
                }
              }}
            />
          </div>

          <div class="flex items-center justify-between p-3 rounded-lg border">
            <div class="flex items-center gap-3">
              <Clock class="h-5 w-5 text-muted-foreground" />
              <Label>Daily Stats</Label>
            </div>
            <Switch
              checked={store.features.dashboardWidgets?.dailyStats ?? true}
              onCheckedChange={(checked) => {
                if (store.features?.dashboardWidgets) {
                  store.features.dashboardWidgets.dailyStats = checked;
                  store.markChanged();
                }
              }}
            />
          </div>
        </div>
      </CardContent>
    </Card>

    <!-- Plugins -->
    <Card>
      <CardHeader>
        <CardTitle class="flex items-center gap-2">
          <Sparkles class="h-5 w-5" />
          Plugins
        </CardTitle>
        <CardDescription>
          Enable or disable individual data plugins
        </CardDescription>
      </CardHeader>
      <CardContent>
        <div class="space-y-1">
          {#each Object.entries(store.features.plugins ?? {}) as [key, plugin]}
            <div
              class="flex items-center justify-between py-3 border-b last:border-0"
            >
              <div class="flex items-center gap-3">
                {#if key === "delta" || key === "direction"}
                  <Activity class="h-4 w-4 text-muted-foreground" />
                {:else if key === "timeago"}
                  <Clock class="h-4 w-4 text-muted-foreground" />
                {:else if key === "iob" || key === "basal"}
                  <Syringe class="h-4 w-4 text-muted-foreground" />
                {:else if key === "cob"}
                  <Droplets class="h-4 w-4 text-muted-foreground" />
                {:else if key === "cage" || key === "sage" || key === "iage"}
                  <Timer class="h-4 w-4 text-muted-foreground" />
                {:else if key === "bage" || key === "upbat"}
                  <Battery class="h-4 w-4 text-muted-foreground" />
                {:else}
                  <Pill class="h-4 w-4 text-muted-foreground" />
                {/if}
                <div>
                  <Label class="capitalize">{key}</Label>
                  <p class="text-sm text-muted-foreground">
                    {plugin.description ?? ""}
                  </p>
                </div>
              </div>
              <Switch
                checked={plugin.enabled ?? false}
                onCheckedChange={(checked) => {
                  if (store.features?.plugins) {
                    store.features.plugins[key].enabled = checked;
                    store.markChanged();
                  }
                }}
              />
            </div>
          {/each}
        </div>
      </CardContent>
    </Card>

    <!-- Quick Enable/Disable All -->
    <div class="flex justify-end gap-2">
      <Button variant="outline" size="sm" onclick={disableAllPlugins}>
        Disable All Plugins
      </Button>
      <Button variant="outline" size="sm" onclick={enableAllPlugins}>
        Enable All Plugins
      </Button>
    </div>
  {/if}
</div>
