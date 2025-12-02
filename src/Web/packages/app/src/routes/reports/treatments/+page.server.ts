import type { Actions, PageServerLoad } from "./$types";
import type { Treatment } from "$lib/api";
import { fail } from "@sveltejs/kit";
import { calculateTreatmentStats } from "$lib/constants/treatment-categories";

export const load: PageServerLoad = async ({ parent, url }) => {
  // Get shared data from layout (treatments, dateRange, etc.)
  const parentData = await parent();

  // Calculate treatment statistics
  const stats = calculateTreatmentStats(parentData.treatments as Treatment[]);

  // Get any filter state from URL params
  const categoryParam = url.searchParams.get("category");
  const searchParam = url.searchParams.get("search");
  const eventTypesParam = url.searchParams.get("eventTypes");

  return {
    treatments: parentData.treatments,
    dateRange: parentData.dateRange,
    stats,
    filters: {
      category: categoryParam || "all",
      search: searchParam || "",
      eventTypes: eventTypesParam ? eventTypesParam.split(",") : [],
    },
  };
};

export const actions: Actions = {
  updateTreatment: async ({ request, locals }) => {
    try {
      const formData = await request.formData();
      const treatmentId = formData.get("treatmentId") as string;
      const treatmentData = formData.get("treatmentData") as string;

      if (!treatmentId || !treatmentData) {
        return fail(400, {
          error: "Treatment ID and data are required",
        });
      }

      const parsedTreatment = JSON.parse(treatmentData);
      const updatedTreatment =
        await locals.apiClient.treatments.updateTreatment2(
          treatmentId,
          parsedTreatment
        );

      return {
        message: "Treatment updated successfully",
        updatedTreatment,
      };
    } catch (error) {
      console.error("Error updating treatment:", error);
      return fail(500, {
        error: "Failed to update treatment",
      });
    }
  },

  deleteTreatment: async ({ request, locals }) => {
    try {
      const formData = await request.formData();
      const treatmentId = formData.get("treatmentId") as string;

      if (!treatmentId) {
        return fail(400, {
          error: "Treatment ID is required",
        });
      }

      // Delete treatment via API client
      await locals.apiClient.treatments.deleteTreatment2(treatmentId);
      return {
        message: "Treatment deleted successfully",
        deletedTreatmentId: treatmentId,
      };
    } catch (error) {
      console.error("Error deleting treatment:", error);
      return fail(500, {
        error: "Failed to delete treatment",
      });
    }
  },

  bulkDeleteTreatments: async ({ request, locals }) => {
    try {
      const formData = await request.formData();
      const treatmentIds = formData.getAll("treatmentIds") as string[];

      if (!treatmentIds.length) {
        return fail(400, {
          error: "Treatment IDs are required",
        });
      }

      // Delete treatments via API client - perform sequential deletes to avoid overwhelming the server
      const deletedIds: string[] = [];
      const failedIds: string[] = [];

      for (const treatmentId of treatmentIds) {
        try {
          await locals.apiClient.treatments.deleteTreatment2(treatmentId);
          deletedIds.push(treatmentId);
        } catch (error) {
          console.error(`Error deleting treatment ${treatmentId}:`, error);
          failedIds.push(treatmentId);
        }
      }

      if (failedIds.length > 0) {
        return fail(500, {
          error: `Failed to delete ${failedIds.length} of ${treatmentIds.length} treatments`,
          deletedTreatmentIds: deletedIds,
        });
      }

      return {
        message: `Successfully deleted ${deletedIds.length} treatment${deletedIds.length !== 1 ? "s" : ""}`,
        deletedTreatmentIds: deletedIds,
      };
    } catch (error) {
      console.error("Error bulk deleting treatments:", error);
      return fail(500, {
        error: "Failed to delete treatments",
      });
    }
  },
};
