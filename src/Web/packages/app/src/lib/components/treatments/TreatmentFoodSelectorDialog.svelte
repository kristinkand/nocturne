<script lang="ts">
  import * as Dialog from "$lib/components/ui/dialog";
  import * as Command from "$lib/components/ui/command";
  import * as Popover from "$lib/components/ui/popover";
  import { Button } from "$lib/components/ui/button";
  import { Input } from "$lib/components/ui/input";
  import { Label } from "$lib/components/ui/label";
  import { Badge } from "$lib/components/ui/badge";
  import { Check, ChevronsUpDown, Plus, Star, Clock } from "lucide-svelte";
  import { cn } from "$lib/utils";
  import { tick } from "svelte";
  import { toast } from "svelte-sonner";
  import {
    TreatmentFoodInputMode,
    type Food,
    type TreatmentFoodRequest,
  } from "$lib/api";
  import {
    addFavoriteFood,
    getAllFoods,
    getFavoriteFoods,
    getRecentFoods,
    removeFavoriteFood,
    createNewFood,
    updateExistingFood,
  } from "$lib/data/treatment-foods.remote";
  import { CategorySubcategoryCombobox } from "$lib/components/food";

  interface Props {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    onSubmit: (request: TreatmentFoodRequest) => void;
  }

  let { open = $bindable(), onOpenChange, onSubmit }: Props = $props();

  // Food lists
  let favorites = $state<Food[]>([]);
  let recents = $state<Food[]>([]);
  let allFoods = $state<Food[]>([]);

  // Combobox state
  let comboboxOpen = $state(false);
  let searchQuery = $state("");
  let comboboxTriggerRef = $state<HTMLButtonElement>(null!);

  // Unit combobox state
  let unitOpen = $state(false);
  let unitTriggerRef = $state<HTMLButtonElement>(null!);

  // GI combobox state
  let giOpen = $state(false);
  let giTriggerRef = $state<HTMLButtonElement>(null!);

  // Selected/editing food state
  let selectedFood = $state<Food | null>(null);
  let originalFood = $state<Food | null>(null);
  let isCreatingNew = $state(false);

  // Editable food fields
  let foodName = $state("");
  let foodCategory = $state("");
  let foodSubcategory = $state("");
  let foodPortion = $state<number>(100);
  let foodUnit = $state("g");
  let foodCarbs = $state<number>(0);
  let foodFat = $state<number>(0);
  let foodProtein = $state<number>(0);
  let foodEnergy = $state<number>(0);
  let foodGi = $state<number>(2);

  // Treatment request fields
  let inputMode = $state<TreatmentFoodInputMode>(
    TreatmentFoodInputMode.Portions
  );
  let portions = $state(1);
  let treatmentCarbs = $state<number | undefined>(undefined);
  let timeOffsetMinutes = $state<number | undefined>(0);
  let note = $state("");

  // Loading states
  let isLoading = $state(false);
  let isSubmitting = $state(false);
  let isSaving = $state(false);

  // Constants
  const foodUnits = ["g", "ml", "pcs", "oz"];
  const giOptions = [
    { value: 1, label: "Low" },
    { value: 2, label: "Medium" },
    { value: 3, label: "High" },
  ];

  // Derived: check if any field has been edited
  const hasEdits = $derived.by(() => {
    if (!originalFood) return false;
    return (
      foodName !== (originalFood.name ?? "") ||
      foodCategory !== (originalFood.category ?? "") ||
      foodSubcategory !== (originalFood.subcategory ?? "") ||
      foodPortion !== (originalFood.portion ?? 100) ||
      foodUnit !== (originalFood.unit ?? "g") ||
      foodCarbs !== (originalFood.carbs ?? 0) ||
      foodFat !== (originalFood.fat ?? 0) ||
      foodProtein !== (originalFood.protein ?? 0) ||
      foodEnergy !== (originalFood.energy ?? 0) ||
      foodGi !== (originalFood.gi ?? 2)
    );
  });

  // Derived: filtered foods based on search
  const filteredFoods = $derived.by(() => {
    if (!searchQuery.trim()) return [];
    const query = searchQuery.trim().toLowerCase();
    return allFoods.filter((food) => {
      const name = food.name?.toLowerCase() ?? "";
      const category = food.category?.toLowerCase() ?? "";
      const subcategory = food.subcategory?.toLowerCase() ?? "";
      return (
        name.includes(query) ||
        category.includes(query) ||
        subcategory.includes(query)
      );
    });
  });

  // Derived: check if search matches any existing food name exactly
  const hasExactMatch = $derived(
    allFoods.some(
      (f) => f.name?.toLowerCase() === searchQuery.trim().toLowerCase()
    )
  );

  // Derived: categories from all foods
  const categories = $derived.by(() => {
    const catMap: Record<string, Record<string, boolean>> = {};
    for (const food of allFoods) {
      if (food.category) {
        if (!catMap[food.category]) {
          catMap[food.category] = {};
        }
        if (food.subcategory) {
          catMap[food.category][food.subcategory] = true;
        }
      }
    }
    return catMap;
  });

  // Derived: display labels
  const selectedUnitLabel = $derived(foodUnit || "Select unit...");
  const selectedGiLabel = $derived(
    giOptions.find((opt) => opt.value === foodGi)?.label || "Select GI..."
  );

  $effect(() => {
    if (!open) {
      resetForm();
      return;
    }
    void loadFoods();
  });

  async function loadFoods() {
    isLoading = true;
    try {
      // Use Promise.allSettled to handle auth-required endpoints gracefully
      // favorites and recents require authentication, but allFoods doesn't
      const [favoriteResult, recentResult, allResult] =
        await Promise.allSettled([
          getFavoriteFoods(),
          getRecentFoods(),
          getAllFoods(),
        ]);

      // Extract successful results, defaulting to empty arrays on failure
      favorites =
        favoriteResult.status === "fulfilled" ? favoriteResult.value : [];
      recents = recentResult.status === "fulfilled" ? recentResult.value : [];
      allFoods = allResult.status === "fulfilled" ? allResult.value : [];

      // Log any failures for debugging (but don't fail the whole operation)
      if (favoriteResult.status === "rejected") {
        console.debug(
          "Could not load favorites (user may not be authenticated)"
        );
      }
      if (recentResult.status === "rejected") {
        console.debug(
          "Could not load recent foods (user may not be authenticated)"
        );
      }
      if (allResult.status === "rejected") {
        console.error("Failed to load food list:", allResult.reason);
      }
    } catch (err) {
      console.error("Failed to load foods:", err);
    } finally {
      isLoading = false;
    }
  }

  function resetForm() {
    selectedFood = null;
    originalFood = null;
    isCreatingNew = false;
    searchQuery = "";
    comboboxOpen = false;

    // Reset food fields
    foodName = "";
    foodCategory = "";
    foodSubcategory = "";
    foodPortion = 100;
    foodUnit = "g";
    foodCarbs = 0;
    foodFat = 0;
    foodProtein = 0;
    foodEnergy = 0;
    foodGi = 2;

    // Reset treatment fields
    inputMode = TreatmentFoodInputMode.Portions;
    portions = 1;
    treatmentCarbs = undefined;
    timeOffsetMinutes = 0;
    note = "";
    isSubmitting = false;
    isSaving = false;
  }

  function selectFood(food: Food) {
    selectedFood = food;
    originalFood = { ...food };

    // Populate editable fields
    foodName = food.name ?? "";
    foodCategory = food.category ?? "";
    foodSubcategory = food.subcategory ?? "";
    foodPortion = food.portion ?? 100;
    foodUnit = food.unit ?? "g";
    foodCarbs = food.carbs ?? 0;
    foodFat = food.fat ?? 0;
    foodProtein = food.protein ?? 0;
    foodEnergy = food.energy ?? 0;
    foodGi = food.gi ?? 2;

    // Reset treatment fields
    portions = 1;
    treatmentCarbs = foodCarbs;
    timeOffsetMinutes = 0;
    note = "";
    inputMode = TreatmentFoodInputMode.Portions;
    isCreatingNew = false;

    closeComboboxAndFocus();
  }

  function startCreateNew() {
    selectedFood = null;
    originalFood = null;
    isCreatingNew = true;

    // Use search query as initial name
    foodName = searchQuery.trim();
    foodCategory = "";
    foodSubcategory = "";
    foodPortion = 100;
    foodUnit = "g";
    foodCarbs = 0;
    foodFat = 0;
    foodProtein = 0;
    foodEnergy = 0;
    foodGi = 2;

    // Reset treatment fields
    portions = 1;
    treatmentCarbs = 0;
    timeOffsetMinutes = 0;
    note = "";
    inputMode = TreatmentFoodInputMode.Portions;

    closeComboboxAndFocus();
  }

  function closeComboboxAndFocus() {
    comboboxOpen = false;
    tick().then(() => comboboxTriggerRef?.focus());
  }

  function closeUnitAndFocus() {
    unitOpen = false;
    tick().then(() => unitTriggerRef?.focus());
  }

  function closeGiAndFocus() {
    giOpen = false;
    tick().then(() => giTriggerRef?.focus());
  }

  function selectUnit(unit: string) {
    foodUnit = unit;
    closeUnitAndFocus();
  }

  function selectGi(gi: number) {
    foodGi = gi;
    closeGiAndFocus();
  }

  async function toggleFavorite(food: Food) {
    if (!food._id) return;
    try {
      const isFavorite = favorites.some((fav) => fav._id === food._id);
      if (isFavorite) {
        await removeFavoriteFood(food._id);
        favorites = favorites.filter((fav) => fav._id !== food._id);
      } else {
        await addFavoriteFood(food._id);
        favorites = [...favorites, food].sort((a, b) =>
          (a.name ?? "").localeCompare(b.name ?? "")
        );
        recents = recents.filter((recent) => recent._id !== food._id);
      }
    } catch (err) {
      console.error("Failed to update favorite:", err);
    }
  }

  function buildFoodRecord(): Omit<Food, "_id"> & { _id?: string } {
    return {
      _id: selectedFood?._id,
      type: "food",
      name: foodName,
      category: foodCategory,
      subcategory: foodSubcategory,
      portion: foodPortion,
      unit: foodUnit,
      carbs: foodCarbs,
      fat: foodFat,
      protein: foodProtein,
      energy: foodEnergy,
      gi: foodGi,
    };
  }

  async function handleAddFood() {
    if (!selectedFood?._id) return;

    const request: TreatmentFoodRequest = {
      foodId: selectedFood._id,
      timeOffsetMinutes,
      note: note.trim() || undefined,
      inputMode,
    };

    if (inputMode === TreatmentFoodInputMode.Portions) {
      request.portions = portions;
    } else {
      request.carbs = treatmentCarbs;
    }

    isSubmitting = true;
    onSubmit(request);
    isSubmitting = false;
  }

  async function handleUpdate() {
    if (!selectedFood?._id) return;

    isSaving = true;
    try {
      const foodRecord = buildFoodRecord();
      await updateExistingFood(foodRecord as any);

      // Update local state
      const idx = allFoods.findIndex((f) => f._id === selectedFood._id);
      if (idx !== -1) {
        allFoods[idx] = { ...allFoods[idx], ...foodRecord };
      }

      // Update originalFood to reflect saved state
      originalFood = { ...selectedFood, ...foodRecord };

      toast.success("Food updated successfully");

      // Now add the food to treatment
      await handleAddFood();
    } catch (err) {
      console.error("Failed to update food:", err);
      toast.error("Failed to update food");
    } finally {
      isSaving = false;
    }
  }

  async function handleSaveAsNew() {
    if (!foodName.trim()) {
      toast.error("Please enter a food name");
      return;
    }

    isSaving = true;
    try {
      const foodRecord = buildFoodRecord();
      delete foodRecord._id;

      const result = await createNewFood(foodRecord as any);

      if (result.success && result.record) {
        // Add to allFoods
        const newFood = result.record as Food;
        allFoods = [...allFoods, newFood];

        // Select the new food
        selectedFood = newFood;
        originalFood = { ...newFood };

        toast.success("Food created successfully");

        // Now add the food to treatment
        const request: TreatmentFoodRequest = {
          foodId: newFood._id!,
          timeOffsetMinutes,
          note: note.trim() || undefined,
          inputMode,
        };

        if (inputMode === TreatmentFoodInputMode.Portions) {
          request.portions = portions;
        } else {
          request.carbs = treatmentCarbs;
        }

        onSubmit(request);
      } else {
        throw new Error("Failed to create food");
      }
    } catch (err) {
      console.error("Failed to create food:", err);
      toast.error("Failed to create food");
    } finally {
      isSaving = false;
    }
  }

  // Derived: show form when food selected or creating new
  const showForm = $derived(selectedFood !== null || isCreatingNew);

  // Derived: can submit
  const canSubmit = $derived(
    (selectedFood !== null || (isCreatingNew && foodName.trim())) &&
      !isSubmitting &&
      !isSaving
  );
</script>

<Dialog.Root bind:open {onOpenChange}>
  <Dialog.Content class="max-w-2xl max-h-[90vh] overflow-y-auto">
    <Dialog.Header>
      <Dialog.Title>Add Food</Dialog.Title>
      <Dialog.Description>
        Search for an existing food or create a new one.
      </Dialog.Description>
    </Dialog.Header>

    <div class="space-y-4">
      <!-- Food search combobox -->
      <div class="space-y-2">
        <Label>Food</Label>
        <Popover.Root bind:open={comboboxOpen}>
          <Popover.Trigger class="w-full" bind:ref={comboboxTriggerRef}>
            {#snippet child({ props })}
              <Button
                variant="outline"
                role="combobox"
                aria-expanded={comboboxOpen}
                class="w-full justify-between font-normal"
                {...props}
              >
                {#if selectedFood}
                  <span>{selectedFood.name}</span>
                {:else if isCreatingNew}
                  <span class="text-primary">Creating: {foodName}</span>
                {:else}
                  <span class="text-muted-foreground">
                    Search or create food...
                  </span>
                {/if}
                <ChevronsUpDown class="ml-2 h-4 w-4 shrink-0 opacity-50" />
              </Button>
            {/snippet}
          </Popover.Trigger>
          <Popover.Content class="w-[var(--bits-popover-anchor-width)] p-0">
            <Command.Root shouldFilter={false}>
              <Command.Input
                placeholder="Search foods..."
                bind:value={searchQuery}
              />
              <Command.List class="max-h-[300px]">
                {#if isLoading}
                  <Command.Empty>Loading foods...</Command.Empty>
                {:else if !searchQuery.trim()}
                  <!-- Show favorites and recents when no search -->
                  {#if favorites.length > 0}
                    <Command.Group>
                      <div
                        class="px-2 py-1.5 text-xs font-medium text-muted-foreground flex items-center gap-1"
                      >
                        <Star class="h-3 w-3 text-yellow-500" />
                        Favorites
                      </div>
                      {#each favorites as food (food._id)}
                        <Command.Item
                          value={food._id}
                          onSelect={() => selectFood(food)}
                          class="cursor-pointer"
                        >
                          <Check
                            class={cn(
                              "mr-2 h-4 w-4",
                              selectedFood?._id === food._id
                                ? "opacity-100"
                                : "opacity-0"
                            )}
                          />
                          <div class="flex-1">
                            <span>{food.name}</span>
                            <span class="ml-2 text-xs text-muted-foreground">
                              {food.carbs}g carbs
                            </span>
                          </div>
                        </Command.Item>
                      {/each}
                    </Command.Group>
                  {/if}

                  {#if recents.length > 0}
                    <Command.Group>
                      <div
                        class="px-2 py-1.5 text-xs font-medium text-muted-foreground flex items-center gap-1"
                      >
                        <Clock class="h-3 w-3 text-sky-500" />
                        Recent
                      </div>
                      {#each recents as food (food._id)}
                        <Command.Item
                          value={food._id}
                          onSelect={() => selectFood(food)}
                          class="cursor-pointer"
                        >
                          <Check
                            class={cn(
                              "mr-2 h-4 w-4",
                              selectedFood?._id === food._id
                                ? "opacity-100"
                                : "opacity-0"
                            )}
                          />
                          <div class="flex-1">
                            <span>{food.name}</span>
                            <span class="ml-2 text-xs text-muted-foreground">
                              {food.carbs}g carbs
                            </span>
                          </div>
                        </Command.Item>
                      {/each}
                    </Command.Group>
                  {/if}

                  {#if favorites.length === 0 && recents.length === 0}
                    <Command.Empty>
                      Type to search foods or create a new one.
                    </Command.Empty>
                  {/if}
                {:else}
                  <!-- Show filtered results and create option -->
                  {#if filteredFoods.length > 0}
                    <Command.Group>
                      {#each filteredFoods as food (food._id)}
                        <Command.Item
                          value={food._id}
                          onSelect={() => selectFood(food)}
                          class="cursor-pointer"
                        >
                          <Check
                            class={cn(
                              "mr-2 h-4 w-4",
                              selectedFood?._id === food._id
                                ? "opacity-100"
                                : "opacity-0"
                            )}
                          />
                          <div class="flex-1">
                            <span>{food.name}</span>
                            <span class="ml-2 text-xs text-muted-foreground">
                              {food.carbs}g carbs
                            </span>
                          </div>
                          {#if food.category}
                            <Badge variant="outline" class="text-xs">
                              {food.category}
                            </Badge>
                          {/if}
                        </Command.Item>
                      {/each}
                    </Command.Group>
                  {/if}

                  <!-- Create new option when no exact match -->
                  {#if !hasExactMatch && searchQuery.trim()}
                    <Command.Group>
                      <Command.Item
                        value="__create_new__"
                        onSelect={startCreateNew}
                        class="cursor-pointer text-primary"
                      >
                        <Plus class="mr-2 h-4 w-4" />
                        Create "{searchQuery.trim()}"
                      </Command.Item>
                    </Command.Group>
                  {/if}

                  {#if filteredFoods.length === 0 && hasExactMatch}
                    <Command.Empty>No additional matches found.</Command.Empty>
                  {/if}
                {/if}
              </Command.List>
            </Command.Root>
          </Popover.Content>
        </Popover.Root>
      </div>

      <!-- Nutritional fields form -->
      {#if showForm}
        <div class="border-t pt-4 space-y-4">
          <!-- Name row -->
          <div class="grid gap-4 md:grid-cols-3">
            <div class="space-y-2">
              <Label for="food-name">Name</Label>
              <Input id="food-name" bind:value={foodName} />
            </div>
            <div class="space-y-2 col-span-2">
              <Label>Category & Subcategory</Label>
              <CategorySubcategoryCombobox
                bind:category={foodCategory}
                bind:subcategory={foodSubcategory}
                {categories}
                onCategoryChange={(cat) => (foodCategory = cat)}
                onSubcategoryChange={(sub) => (foodSubcategory = sub)}
                onCategoryCreate={(cat) => {
                  // Category will be created when food is saved
                }}
                onSubcategoryCreate={(cat, sub) => {
                  // Subcategory will be created when food is saved
                }}
              />
            </div>
          </div>

          <!-- Portion and unit row -->
          <div class="grid gap-4 md:grid-cols-4">
            <div class="space-y-2">
              <Label for="food-portion">Portion</Label>
              <Input id="food-portion" type="number" bind:value={foodPortion} />
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
                <Popover.Content
                  class="w-[var(--bits-popover-anchor-width)] p-0"
                >
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
                                foodUnit !== unit && "text-transparent"
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
              <Input id="food-carbs" type="number" bind:value={foodCarbs} />
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
                <Popover.Content
                  class="w-[var(--bits-popover-anchor-width)] p-0"
                >
                  <Command.Root>
                    <Command.Input placeholder="Search GI..." />
                    <Command.List>
                      <Command.Empty>No GI found.</Command.Empty>
                      <Command.Group>
                        {#each giOptions as option}
                          <Command.Item
                            value={option.label}
                            onSelect={() => selectGi(option.value)}
                          >
                            <Check
                              class={cn(
                                "mr-2 size-4",
                                foodGi !== option.value && "text-transparent"
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

          <!-- Additional nutrients row -->
          <div class="grid gap-4 md:grid-cols-3">
            <div class="space-y-2">
              <Label for="food-fat">Fat (g)</Label>
              <Input id="food-fat" type="number" bind:value={foodFat} />
            </div>
            <div class="space-y-2">
              <Label for="food-protein">Protein (g)</Label>
              <Input id="food-protein" type="number" bind:value={foodProtein} />
            </div>
            <div class="space-y-2">
              <Label for="food-energy">Energy (kJ)</Label>
              <Input id="food-energy" type="number" bind:value={foodEnergy} />
            </div>
          </div>

          <!-- Favorite toggle for selected foods -->
          {#if selectedFood}
            <div class="flex items-center gap-2">
              <Button
                type="button"
                variant="ghost"
                size="sm"
                onclick={() => selectedFood && toggleFavorite(selectedFood)}
              >
                <Star
                  class="h-4 w-4 mr-1 {favorites.some(
                    (fav) => fav._id === selectedFood?._id
                  )
                    ? 'text-yellow-500 fill-yellow-500'
                    : 'text-muted-foreground'}"
                />
                {favorites.some((fav) => fav._id === selectedFood?._id)
                  ? "Remove from favorites"
                  : "Add to favorites"}
              </Button>
            </div>
          {/if}
        </div>

        <!-- Treatment request fields -->
        <div class="border-t pt-4 space-y-4">
          <div class="text-sm font-medium">Portion for this treatment</div>

          <div class="flex gap-2">
            <Button
              type="button"
              size="sm"
              variant={inputMode === TreatmentFoodInputMode.Portions
                ? "default"
                : "outline"}
              onclick={() => (inputMode = TreatmentFoodInputMode.Portions)}
            >
              By portions
            </Button>
            <Button
              type="button"
              size="sm"
              variant={inputMode === TreatmentFoodInputMode.Carbs
                ? "default"
                : "outline"}
              onclick={() => (inputMode = TreatmentFoodInputMode.Carbs)}
            >
              By carbs
            </Button>
          </div>

          <div class="grid gap-4 md:grid-cols-2">
            <div class="space-y-2">
              <Label for="portions">Portions</Label>
              <Input
                id="portions"
                type="number"
                step="0.1"
                min="0"
                bind:value={portions}
                disabled={inputMode !== TreatmentFoodInputMode.Portions}
              />
            </div>
            <div class="space-y-2">
              <Label for="treatment-carbs">Carbs (g)</Label>
              <Input
                id="treatment-carbs"
                type="number"
                step="0.1"
                min="0"
                bind:value={treatmentCarbs}
                disabled={inputMode !== TreatmentFoodInputMode.Carbs}
              />
            </div>
          </div>

          <div class="grid gap-4 md:grid-cols-2">
            <div class="space-y-2">
              <Label for="offset">Time offset (min)</Label>
              <Input
                id="offset"
                type="number"
                step="1"
                bind:value={timeOffsetMinutes}
              />
            </div>
            <div class="space-y-2">
              <Label for="note">Note</Label>
              <Input id="note" bind:value={note} />
            </div>
          </div>
        </div>
      {/if}
    </div>

    <Dialog.Footer class="gap-2 flex-wrap">
      <Button
        type="button"
        variant="outline"
        onclick={() => onOpenChange(false)}
      >
        Cancel
      </Button>

      {#if isCreatingNew}
        <!-- Creating new food -->
        <Button
          type="button"
          onclick={handleSaveAsNew}
          disabled={!canSubmit || isSaving}
        >
          {isSaving ? "Saving..." : "Save & Add"}
        </Button>
      {:else if selectedFood && hasEdits}
        <!-- Existing food with edits -->
        <Button
          type="button"
          variant="outline"
          onclick={handleSaveAsNew}
          disabled={!canSubmit || isSaving}
        >
          {isSaving ? "Saving..." : "Save as New"}
        </Button>
        <Button
          type="button"
          onclick={handleUpdate}
          disabled={!canSubmit || isSaving}
        >
          {isSaving ? "Updating..." : "Update & Add"}
        </Button>
      {:else if selectedFood}
        <!-- Existing food without edits -->
        <Button
          type="button"
          onclick={handleAddFood}
          disabled={!canSubmit || isSubmitting}
        >
          {isSubmitting ? "Adding..." : "Add Food"}
        </Button>
      {:else}
        <!-- No selection yet -->
        <Button type="button" disabled>Add Food</Button>
      {/if}
    </Dialog.Footer>
  </Dialog.Content>
</Dialog.Root>
