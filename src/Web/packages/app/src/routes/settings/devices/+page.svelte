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
  import { Badge } from "$lib/components/ui/badge";
  import { Switch } from "$lib/components/ui/switch";
  import { Label } from "$lib/components/ui/label";
  import { Separator } from "$lib/components/ui/separator";
  import { AlertCircle } from "lucide-svelte";
  import {
    Select,
    SelectContent,
    SelectItem,
    SelectTrigger,
  } from "$lib/components/ui/select";
  import {
    Smartphone,
    Activity,
    Bluetooth,
    Plus,
    Settings,
    Trash2,
    RefreshCw,
    Battery,
    Loader2,
  } from "lucide-svelte";

  const store = getSettingsStore();

  function getStatusBadge(status: string | undefined) {
    switch (status) {
      case "connected":
        return { variant: "default" as const, text: "Connected" };
      case "disconnected":
        return { variant: "secondary" as const, text: "Disconnected" };
      case "error":
        return { variant: "destructive" as const, text: "Error" };
      default:
        return { variant: "secondary" as const, text: status ?? "Unknown" };
    }
  }

  function formatLastSync(date: Date | undefined): string {
    if (!date) return "Never";
    const diff = Date.now() - new Date(date).getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return "Just now";
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return new Date(date).toLocaleDateString();
  }

  function removeDevice(deviceId: string | undefined) {
    if (!deviceId || !store.devices?.connectedDevices) return;
    const index = store.devices.connectedDevices.findIndex(
      (d) => d.id === deviceId
    );
    if (index !== -1) {
      store.devices.connectedDevices.splice(index, 1);
      store.markChanged();
    }
  }
</script>

<svelte:head>
  <title>Devices - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Devices</h1>
    <p class="text-muted-foreground">
      Manage your CGM, insulin pump, and other connected devices
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
  {:else if store.devices}
    <!-- Connected Devices -->
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle>Connected Devices</CardTitle>
            <CardDescription>
              Devices currently paired with Nocturne
            </CardDescription>
          </div>
          <Button size="sm" class="gap-2">
            <Plus class="h-4 w-4" />
            Add Device
          </Button>
        </div>
      </CardHeader>
      <CardContent class="space-y-4">
        {#if !store.devices.connectedDevices || store.devices.connectedDevices.length === 0}
          <div class="text-center py-8 text-muted-foreground">
            <Bluetooth class="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p class="font-medium">No devices connected</p>
            <p class="text-sm">Add a CGM or pump to get started</p>
          </div>
        {:else}
          {#each store.devices.connectedDevices as device}
            <div
              class="flex items-center justify-between p-4 rounded-lg border"
            >
              <div class="flex items-center gap-4">
                <div
                  class="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10"
                >
                  {#if device.type === "cgm"}
                    <Activity class="h-6 w-6 text-primary" />
                  {:else}
                    <Smartphone class="h-6 w-6 text-primary" />
                  {/if}
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-medium">{device.name}</span>
                    <Badge variant={getStatusBadge(device.status).variant}>
                      {getStatusBadge(device.status).text}
                    </Badge>
                  </div>
                  <div
                    class="text-sm text-muted-foreground flex items-center gap-3"
                  >
                    {#if device.battery != null}
                      <span class="flex items-center gap-1">
                        <Battery class="h-3 w-3" />
                        {device.battery}%
                      </span>
                      <span>•</span>
                    {/if}
                    <span>Last sync: {formatLastSync(device.lastSync)}</span>
                  </div>
                  {#if device.serialNumber}
                    <div class="text-xs text-muted-foreground mt-1">
                      S/N: {device.serialNumber}
                    </div>
                  {/if}
                </div>
              </div>
              <div class="flex items-center gap-2">
                <Button variant="ghost" size="icon">
                  <RefreshCw class="h-4 w-4" />
                </Button>
                <Button variant="ghost" size="icon">
                  <Settings class="h-4 w-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  class="text-destructive"
                  onclick={() => removeDevice(device.id)}
                >
                  <Trash2 class="h-4 w-4" />
                </Button>
              </div>
            </div>
          {/each}
        {/if}
      </CardContent>
    </Card>

    <!-- Device Settings -->
    <Card>
      <CardHeader>
        <CardTitle>Device Settings</CardTitle>
        <CardDescription>
          Configure how devices connect and sync data
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Auto-connect on startup</Label>
            <p class="text-sm text-muted-foreground">
              Automatically reconnect to known devices when the app starts
            </p>
          </div>
          <Switch
            checked={store.devices.autoConnect ?? true}
            onCheckedChange={(checked) => {
              if (store.devices) {
                store.devices.autoConnect = checked;
                store.markChanged();
              }
            }}
          />
        </div>

        <Separator />

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Cloud upload</Label>
            <p class="text-sm text-muted-foreground">
              Upload device data to your Nightscout site
            </p>
          </div>
          <Switch
            checked={store.devices.uploadEnabled ?? true}
            onCheckedChange={(checked) => {
              if (store.devices) {
                store.devices.uploadEnabled = checked;
                store.markChanged();
              }
            }}
          />
        </div>

        <Separator />

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Show raw sensor data</Label>
            <p class="text-sm text-muted-foreground">
              Display unfiltered readings alongside calibrated values
            </p>
          </div>
          <Switch
            checked={store.devices.showRawData ?? false}
            onCheckedChange={(checked) => {
              if (store.devices) {
                store.devices.showRawData = checked;
                store.markChanged();
              }
            }}
          />
        </div>
      </CardContent>
    </Card>

    <!-- CGM Settings -->
    <Card>
      <CardHeader>
        <CardTitle>CGM Configuration</CardTitle>
        <CardDescription>
          Settings specific to continuous glucose monitors
        </CardDescription>
      </CardHeader>
      <CardContent class="space-y-4">
        <div class="space-y-2">
          <Label>Data source priority</Label>
          <Select
            type="single"
            value={store.devices.cgmConfiguration?.dataSourcePriority ?? "cgm"}
            onValueChange={(value) => {
              if (store.devices?.cgmConfiguration) {
                store.devices.cgmConfiguration.dataSourcePriority = value;
                store.markChanged();
              }
            }}
          >
            <SelectTrigger>
              <span>
                {#if store.devices.cgmConfiguration?.dataSourcePriority === "meter"}
                  Meter readings preferred
                {:else if store.devices.cgmConfiguration?.dataSourcePriority === "average"}
                  Average both sources
                {:else}
                  CGM readings preferred
                {/if}
              </span>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="cgm">CGM readings preferred</SelectItem>
              <SelectItem value="meter">Meter readings preferred</SelectItem>
              <SelectItem value="average">Average both sources</SelectItem>
            </SelectContent>
          </Select>
          <p class="text-sm text-muted-foreground">
            Choose which data source to prioritize when multiple are available
          </p>
        </div>

        <Separator />

        <div class="space-y-2">
          <Label>Sensor warmup period</Label>
          <Select
            type="single"
            value={`${store.devices.cgmConfiguration?.sensorWarmupHours ?? 2}h`}
            onValueChange={(value) => {
              if (store.devices?.cgmConfiguration) {
                const hours = parseInt(value.replace("h", ""));
                if (!isNaN(hours)) {
                  store.devices.cgmConfiguration.sensorWarmupHours = hours;
                  store.markChanged();
                }
              }
            }}
          >
            <SelectTrigger>
              <span>
                {store.devices.cgmConfiguration?.sensorWarmupHours ?? 2} hours
              </span>
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="1h">1 hour</SelectItem>
              <SelectItem value="2h">2 hours</SelectItem>
              <SelectItem value="4h">4 hours</SelectItem>
            </SelectContent>
          </Select>
          <p class="text-sm text-muted-foreground">
            Time to wait before using readings from a new sensor
          </p>
        </div>
      </CardContent>
    </Card>
  {/if}
</div>
