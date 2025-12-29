<script lang="ts">
  import { Button } from "$lib/components/ui/button";
  import {
    Card,
    CardContent,
    CardHeader,
    CardTitle,
  } from "$lib/components/ui/card";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import * as Command from "$lib/components/ui/command";
  import * as Popover from "$lib/components/ui/popover";
  import { Check, ChevronsUpDown } from "lucide-svelte";
  import { tick } from "svelte";
  import { cn } from "$lib/utils";
  import { getFoodState } from "./food-context";
  import { CategorySubcategoryCombobox } from "$lib/components/food";

  const foodStore = getFoodState();

  // Combobox state for unit and GI
  let unitOpen = $state(false);
  let giOpen = $state(false);
  let unitTriggerRef = $state<HTMLButtonElement>(null!);
  let giTriggerRef = $state<HTMLButtonElement>(null!);

  const foodUnits = ["g", "ml", "pcs", "oz"];
  const giOptions = [
    { value: 1, label: "Low" },
    { value: 2, label: "Medium" },
    { value: 3, label: "High" },
  ];

  // Selected labels for display
  let selectedUnitLabel = $derived(
    foodStore.currentFood.unit || "Select unit..."
  );
  let selectedGiLabel = $derived(
    giOptions.find((opt) => opt.value === foodStore.currentFood.gi)?.label ||
      "Select GI..."
  );

  // Helper functions for combobox
  function closeUnitAndFocus() {
    unitOpen = false;
    tick().then(() => unitTriggerRef.focus());
  }

  function closeGiAndFocus() {
    giOpen = false;
    tick().then(() => giTriggerRef.focus());
  }

  function selectUnit(unit: string) {
    foodStore.currentFood.unit = unit;
    closeUnitAndFocus();
  }

  function selectGi(gi: number) {
    foodStore.currentFood.gi = gi;
    closeGiAndFocus();
  }

  function handleCategoryChange(category: string) {
    foodStore.currentFood.category = category;
  }

  function handleSubcategoryChange(subcategory: string) {
    foodStore.currentFood.subcategory = subcategory;
  }

  function handleCategoryCreate(category: string) {
    if (!foodStore.categories[category]) {
      foodStore.categories[category] = {};
    }
  }

  function handleSubcategoryCreate(category: string, subcategory: string) {
    if (!foodStore.categories[category]) {
      foodStore.categories[category] = {};
    }
    foodStore.categories[category][subcategory] = true;
  }

  function handleSaveFood() {
    foodStore.saveFood();
  }

  function handleClearForm() {
    foodStore.clearForm();
  }
</script>

<Card>
  <CardHeader>
    <CardTitle>
      Record {#if foodStore.currentFood._id}(ID: {foodStore.currentFood
          ._id}){/if}
    </CardTitle>
  </CardHeader>
  <CardContent class="space-y-4">
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-5 gap-4">
      <div class="space-y-2">
        <Label for="food-name">Name</Label>
        <Input id="food-name" bind:value={foodStore.currentFood.name} />
      </div>
      <div class="space-y-2">
        <Label for="food-portion">Portion</Label>
        <Input
          id="food-portion"
          type="number"
          bind:value={foodStore.currentFood.portion}
        />
      </div>
      <div class="space-y-2">
        <Label for="food-unit">Unit</Label>
        <Popover.Root bind:open={unitOpen}>
          <Popover.Trigger bind:ref={unitTriggerRef}>
            {#snippet child({ props })}
              <Button
                variant="outline"
                class="w-full justify-between"
                {...props}
                role="combobox"
                aria-expanded={unitOpen}
              >
                {selectedUnitLabel}
                <ChevronsUpDown class="ml-2 size-4 shrink-0 opacity-50" />
              </Button>
            {/snippet}
          </Popover.Trigger>
          <Popover.Content class="w-[var(--bits-popover-anchor-width)] p-0">
            <Command.Root>
              <Command.Input placeholder="Search units..." />
              <Command.List>
                <Command.Empty>No unit found.</Command.Empty>
                <Command.Group>
                  {#each foodUnits as unit}
                    <Command.Item
                      value={unit}
                      onSelect={() => selectUnit(unit)}
                    >
                      <Check
                        class={cn(
                          "mr-2 size-4",
                          foodStore.currentFood.unit !== unit &&
                            "text-transparent"
                        )}
                      />
                      {unit}
                    </Command.Item>
                  {/each}
                </Command.Group>
              </Command.List>
            </Command.Root>
          </Popover.Content>
        </Popover.Root>
      </div>
      <div class="space-y-2">
        <Label for="food-carbs">Carbs (g)</Label>
        <Input
          id="food-carbs"
          type="number"
          bind:value={foodStore.currentFood.carbs}
        />
      </div>
      <div class="space-y-2">
        <Label for="food-gi">GI</Label>
        <Popover.Root bind:open={giOpen}>
          <Popover.Trigger bind:ref={giTriggerRef}>
            {#snippet child({ props })}
              <Button
                variant="outline"
                class="w-full justify-between"
                {...props}
                role="combobox"
                aria-expanded={giOpen}
              >
                {selectedGiLabel}
                <ChevronsUpDown class="ml-2 size-4 shrink-0 opacity-50" />
              </Button>
            {/snippet}
          </Popover.Trigger>
          <Popover.Content class="w-[var(--bits-popover-anchor-width)] p-0">
            <Command.Root>
              <Command.Input placeholder="Search GI levels..." />
              <Command.List>
                <Command.Empty>No GI level found.</Command.Empty>
                <Command.Group>
                  {#each giOptions as option}
                    <Command.Item
                      value={option.label}
                      onSelect={() => selectGi(option.value)}
                    >
                      <Check
                        class={cn(
                          "mr-2 size-4",
                          foodStore.currentFood.gi !== option.value &&
                            "text-transparent"
                        )}
                      />
                      {option.label}
                    </Command.Item>
                  {/each}
                </Command.Group>
              </Command.List>
            </Command.Root>
          </Popover.Content>
        </Popover.Root>
      </div>
    </div>
    <div class="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
      <div class="space-y-2 col-span-2">
        <Label>Category & Subcategory</Label>
        <CategorySubcategoryCombobox
          bind:category={foodStore.currentFood.category}
          bind:subcategory={foodStore.currentFood.subcategory}
          categories={foodStore.categories}
          onCategoryChange={handleCategoryChange}
          onSubcategoryChange={handleSubcategoryChange}
          onCategoryCreate={handleCategoryCreate}
          onSubcategoryCreate={handleSubcategoryCreate}
        />
      </div>
      <div class="space-y-2">
        <Label for="food-fat">Fat (g)</Label>
        <Input
          id="food-fat"
          type="number"
          bind:value={foodStore.currentFood.fat}
        />
      </div>
      <div class="space-y-2">
        <Label for="food-protein">Protein (g)</Label>
        <Input
          id="food-protein"
          type="number"
          bind:value={foodStore.currentFood.protein}
        />
      </div>
      <div class="space-y-2">
        <Label for="food-energy">Energy (kJ)</Label>
        <Input
          id="food-energy"
          type="number"
          bind:value={foodStore.currentFood.energy}
        />
      </div>
    </div>
    <div class="flex gap-2">
      <Button onclick={handleSaveFood}>
        {foodStore.currentFood._id ? "Save record" : "Create new record"}
      </Button>
      <Button variant="outline" onclick={handleClearForm}>Clear</Button>
    </div>
  </CardContent>
</Card>
