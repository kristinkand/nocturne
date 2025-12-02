<script lang="ts">
  import { getSettingsStore } from "$lib/stores/settings-store.svelte";
  import type { AvailableService } from "$lib/api/api-client";
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
  import { Badge } from "$lib/components/ui/badge";
  import {
    Plug,
    Plus,
    Settings,
    Trash2,
    RefreshCw,
    CheckCircle,
    AlertCircle,
    Clock,
    ExternalLink,
    Key,
    Cloud,
    Database,
    Smartphone,
    Loader2,
  } from "lucide-svelte";

  const store = getSettingsStore();
  let showAddConnector = $state(false);

  function getStatusBadge(status: string | undefined): {
    variant: "default" | "secondary" | "destructive";
    text: string;
    class: string;
  } {
    switch (status) {
      case "connected":
        return {
          variant: "default" as const,
          text: "Connected",
          class:
            "bg-green-100 text-green-800 dark:bg-green-900 dark:text-green-100",
        };
      case "syncing":
        return {
          variant: "secondary" as const,
          text: "Syncing...",
          class:
            "bg-blue-100 text-blue-800 dark:bg-blue-900 dark:text-blue-100",
        };
      case "error":
        return { variant: "destructive" as const, text: "Error", class: "" };
      default:
        return {
          variant: "secondary" as const,
          text: "Disconnected",
          class: "",
        };
    }
  }

  function formatLastSync(date?: Date): string {
    if (!date) return "Never";
    const d = new Date(date);
    const diff = Date.now() - d.getTime();
    const minutes = Math.floor(diff / 60000);
    if (minutes < 1) return "Just now";
    if (minutes < 60) return `${minutes}m ago`;
    const hours = Math.floor(minutes / 60);
    if (hours < 24) return `${hours}h ago`;
    return d.toLocaleDateString();
  }

  function syncConnector(id: string | undefined) {
    if (!id || !store.services?.connectedServices) return;
    const connector = store.services.connectedServices.find((c) => c.id === id);
    if (connector) {
      connector.status = "syncing";
      store.markChanged();
      // Simulate sync - in production this would call the API
      setTimeout(() => {
        connector.status = "connected";
        connector.lastSync = new Date();
        store.markChanged();
      }, 2000);
    }
  }

  function removeConnector(id: string | undefined) {
    if (!id) return;
    store.removeConnectedService(id);
  }

  function toggleConnector(id: string | undefined, enabled: boolean) {
    if (!id || !store.services?.connectedServices) return;
    const connector = store.services.connectedServices.find((c) => c.id === id);
    if (connector) {
      connector.enabled = enabled;
      store.markChanged();
    }
  }

  function addConnector(connectorType: AvailableService) {
    // In a real implementation, this would open a configuration modal
    showAddConnector = false;
    console.log("Add connector:", connectorType);
  }
</script>

<svelte:head>
  <title>Services - Settings - Nocturne</title>
</svelte:head>

<div class="container mx-auto p-6 max-w-3xl space-y-6">
  <!-- Header -->
  <div>
    <h1 class="text-2xl font-bold tracking-tight">Services & Connectors</h1>
    <p class="text-muted-foreground">
      Connect data sources and sync with external services
    </p>
  </div>

  {#if store.isLoading}
    <div class="flex items-center justify-center py-12">
      <Loader2 class="h-8 w-8 animate-spin text-muted-foreground" />
    </div>
  {:else if store.hasError}
    <Card class="border-destructive">
      <CardContent class="py-8">
        <div class="text-center">
          <AlertCircle class="h-12 w-12 mx-auto mb-4 text-destructive" />
          <p class="font-medium">Failed to load settings</p>
          <p class="text-sm text-muted-foreground mt-1">{store.error}</p>
        </div>
      </CardContent>
    </Card>
  {:else if store.services}
    <!-- Connected Services -->
    <Card>
      <CardHeader>
        <div class="flex items-center justify-between">
          <div>
            <CardTitle>Connected Services</CardTitle>
            <CardDescription>
              Data sources currently syncing with Nocturne
            </CardDescription>
          </div>
          <Button
            size="sm"
            class="gap-2"
            onclick={() => (showAddConnector = true)}
          >
            <Plus class="h-4 w-4" />
            Add Service
          </Button>
        </div>
      </CardHeader>
      <CardContent class="space-y-4">
        {#if !store.services.connectedServices || store.services.connectedServices.length === 0}
          <div class="text-center py-8 text-muted-foreground">
            <Plug class="h-12 w-12 mx-auto mb-4 opacity-50" />
            <p class="font-medium">No services connected</p>
            <p class="text-sm">Add a data source to start syncing</p>
            <Button class="mt-4" onclick={() => (showAddConnector = true)}>
              <Plus class="h-4 w-4 mr-2" />
              Add Your First Service
            </Button>
          </div>
        {:else}
          {#each store.services.connectedServices as connector}
            <div
              class="flex items-center justify-between p-4 rounded-lg border"
            >
              <div class="flex items-center gap-4">
                <div
                  class="flex h-12 w-12 items-center justify-center rounded-lg bg-primary/10"
                >
                  {#if connector.type === "cgm"}
                    <Smartphone class="h-6 w-6 text-primary" />
                  {:else if connector.type === "data"}
                    <Cloud class="h-6 w-6 text-primary" />
                  {:else}
                    <Database class="h-6 w-6 text-primary" />
                  {/if}
                </div>
                <div>
                  <div class="flex items-center gap-2">
                    <span class="font-medium">
                      {connector.name ?? "Unknown"}
                    </span>
                    <Badge
                      variant={getStatusBadge(connector.status).variant}
                      class={getStatusBadge(connector.status).class}
                    >
                      {#if connector.status === "connected"}
                        <CheckCircle class="h-3 w-3 mr-1" />
                      {:else if connector.status === "syncing"}
                        <RefreshCw class="h-3 w-3 mr-1 animate-spin" />
                      {:else if connector.status === "error"}
                        <AlertCircle class="h-3 w-3 mr-1" />
                      {/if}
                      {getStatusBadge(connector.status).text}
                    </Badge>
                  </div>
                  <div class="text-sm text-muted-foreground">
                    {connector.description ?? ""}
                  </div>
                  <div
                    class="text-xs text-muted-foreground flex items-center gap-1 mt-1"
                  >
                    <Clock class="h-3 w-3" />
                    Last sync: {formatLastSync(connector.lastSync)}
                  </div>
                </div>
              </div>
              <div class="flex items-center gap-2">
                <Switch
                  checked={connector.enabled ?? false}
                  onCheckedChange={(checked) =>
                    toggleConnector(connector.id, checked)}
                />
                <Button
                  variant="ghost"
                  size="icon"
                  onclick={() => syncConnector(connector.id)}
                  disabled={connector.status === "syncing"}
                >
                  <RefreshCw
                    class="h-4 w-4 {connector.status === 'syncing'
                      ? 'animate-spin'
                      : ''}"
                  />
                </Button>
                <Button variant="ghost" size="icon">
                  <Settings class="h-4 w-4" />
                </Button>
                <Button
                  variant="ghost"
                  size="icon"
                  class="text-destructive"
                  onclick={() => removeConnector(connector.id)}
                >
                  <Trash2 class="h-4 w-4" />
                </Button>
              </div>
            </div>
          {/each}
        {/if}
      </CardContent>
    </Card>

    <!-- Add Connector Dialog/Section -->
    {#if showAddConnector && store.services.availableServices}
      <Card>
        <CardHeader>
          <div class="flex items-center justify-between">
            <div>
              <CardTitle>Add a Service</CardTitle>
              <CardDescription>Choose a data source to connect</CardDescription>
            </div>
            <Button
              variant="ghost"
              size="sm"
              onclick={() => (showAddConnector = false)}
            >
              Cancel
            </Button>
          </div>
        </CardHeader>
        <CardContent>
          <div class="grid gap-4 sm:grid-cols-2">
            {#each store.services.availableServices as connector}
              <button
                class="flex items-start gap-4 p-4 rounded-lg border hover:border-primary/50 hover:bg-accent/50 transition-colors text-left"
                onclick={() => addConnector(connector)}
              >
                <div
                  class="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-primary/10"
                >
                  {#if connector.type === "cgm"}
                    <Smartphone class="h-5 w-5 text-primary" />
                  {:else if connector.type === "data"}
                    <Cloud class="h-5 w-5 text-primary" />
                  {:else if connector.type === "pump"}
                    <Database class="h-5 w-5 text-primary" />
                  {:else}
                    <Plug class="h-5 w-5 text-primary" />
                  {/if}
                </div>
                <div class="flex-1">
                  <div class="font-medium">{connector.name ?? "Unknown"}</div>
                  <div class="text-sm text-muted-foreground">
                    {connector.description ?? ""}
                  </div>
                  <Badge variant="secondary" class="mt-2 text-xs">
                    {(connector.type ?? "unknown").toUpperCase()}
                  </Badge>
                </div>
              </button>
            {/each}
          </div>
        </CardContent>
      </Card>
    {/if}

    <!-- Sync Settings -->
    <Card>
      <CardHeader>
        <CardTitle>Sync Settings</CardTitle>
        <CardDescription>Configure how data is synchronized</CardDescription>
      </CardHeader>
      <CardContent class="space-y-6">
        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Auto-sync</Label>
            <p class="text-sm text-muted-foreground">
              Automatically sync data in the background
            </p>
          </div>
          <Switch
            checked={store.services.syncSettings?.autoSync ?? true}
            onCheckedChange={(checked) => {
              if (store.services?.syncSettings) {
                store.services.syncSettings.autoSync = checked;
                store.markChanged();
              }
            }}
          />
        </div>

        <Separator />

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Sync on app open</Label>
            <p class="text-sm text-muted-foreground">
              Fetch latest data when opening the app
            </p>
          </div>
          <Switch
            checked={store.services.syncSettings?.syncOnAppOpen ?? true}
            onCheckedChange={(checked) => {
              if (store.services?.syncSettings) {
                store.services.syncSettings.syncOnAppOpen = checked;
                store.markChanged();
              }
            }}
          />
        </div>

        <Separator />

        <div class="flex items-center justify-between">
          <div class="space-y-0.5">
            <Label>Background refresh</Label>
            <p class="text-sm text-muted-foreground">
              Keep data updated even when app is closed
            </p>
          </div>
          <Switch
            checked={store.services.syncSettings?.backgroundRefresh ?? true}
            onCheckedChange={(checked) => {
              if (store.services?.syncSettings) {
                store.services.syncSettings.backgroundRefresh = checked;
                store.markChanged();
              }
            }}
          />
        </div>
      </CardContent>
    </Card>
  {/if}

  <!-- API Access -->
  <Card>
    <CardHeader>
      <CardTitle class="flex items-center gap-2">
        <Key class="h-5 w-5" />
        API Access
      </CardTitle>
      <CardDescription>
        Configure API tokens and external access
      </CardDescription>
    </CardHeader>
    <CardContent class="space-y-4">
      <div class="p-4 rounded-lg border bg-muted/50">
        <div class="flex items-center justify-between">
          <div>
            <Label>API Token</Label>
            <p class="text-sm text-muted-foreground mt-1">
              Use this token to access your data from other apps
            </p>
          </div>
          <Button variant="outline" size="sm">Generate Token</Button>
        </div>
      </div>

      <div class="p-4 rounded-lg border">
        <div class="flex items-center justify-between">
          <div>
            <Label>API Documentation</Label>
            <p class="text-sm text-muted-foreground mt-1">
              Learn how to integrate with the Nocturne API
            </p>
          </div>
          <Button variant="ghost" size="sm" class="gap-2">
            View Docs
            <ExternalLink class="h-4 w-4" />
          </Button>
        </div>
      </div>
    </CardContent>
  </Card>
</div>
