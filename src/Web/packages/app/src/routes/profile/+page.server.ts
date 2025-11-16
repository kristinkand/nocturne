import { error, fail, type Actions } from '@sveltejs/kit';

export const load = async ({ locals }) => {
  try {
    // Load profile records using API client
    const mongoRecords = await locals.apiClient.profile.getProfiles2();

    return {
      mongoRecords: Array.isArray(mongoRecords) ? mongoRecords : [mongoRecords],
    };
  } catch (err) {
    console.error('Error loading profile data:', err);
    throw error(500, 'Failed to load profile data');
  }
};

export const actions: Actions = {
  save: async ({ request, locals }) => {
    try {
      const formData = await request.formData();
      const profileData = formData.get('profileData');

      if (!profileData) {
        return fail(400, { error: 'Profile data is required' });
      }

      // Parse the JSON string to pass as proper object to API client
      let parsedProfileData;
      try {
        parsedProfileData = JSON.parse(profileData as string);
      } catch {
        return fail(400, { error: 'Invalid profile data format' });
      }

      await locals.apiClient.profile.createProfile2(parsedProfileData);

      return {
        success: true,
        message: 'Profile saved successfully'
      };
    } catch (err) {
      console.error('Error saving profile:', err);
      return fail(500, { error: 'Internal server error' });
    }
  }
};
