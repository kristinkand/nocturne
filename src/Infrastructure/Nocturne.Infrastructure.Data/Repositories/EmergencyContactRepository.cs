using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// Repository for emergency contact management
/// </summary>
public class EmergencyContactRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the EmergencyContactRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public EmergencyContactRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Gets all emergency contacts for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <returns>List of emergency contacts</returns>
    public async Task<List<EmergencyContactEntity>> GetByUserIdAsync(string userId)
    {
        return await _context
            .EmergencyContacts.Where(c => c.UserId == userId && c.IsActive)
            .OrderBy(c => c.Priority)
            .ToListAsync();
    }

    /// <summary>
    /// Gets an emergency contact by ID
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>Emergency contact or null</returns>
    public async Task<EmergencyContactEntity?> GetByIdAsync(Guid id)
    {
        return await _context.EmergencyContacts.FirstOrDefaultAsync(c => c.Id == id);
    }

    /// <summary>
    /// Creates a new emergency contact
    /// </summary>
    /// <param name="contact">Contact to create</param>
    /// <returns>Created contact</returns>
    public async Task<EmergencyContactEntity> CreateAsync(EmergencyContactEntity contact)
    {
        _context.EmergencyContacts.Add(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    /// <summary>
    /// Updates an existing emergency contact
    /// </summary>
    /// <param name="contact">Contact to update</param>
    /// <returns>Updated contact</returns>
    public async Task<EmergencyContactEntity> UpdateAsync(EmergencyContactEntity contact)
    {
        contact.UpdatedAt = DateTime.UtcNow;
        _context.EmergencyContacts.Update(contact);
        await _context.SaveChangesAsync();
        return contact;
    }

    /// <summary>
    /// Deletes an emergency contact
    /// </summary>
    /// <param name="id">Contact ID</param>
    /// <returns>True if deleted</returns>
    public async Task<bool> DeleteAsync(Guid id)
    {
        var contact = await GetByIdAsync(id);
        if (contact == null)
        {
            return false;
        }

        _context.EmergencyContacts.Remove(contact);
        await _context.SaveChangesAsync();
        return true;
    }

    /// <summary>
    /// Gets emergency contacts for a specific alert type and escalation level
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="alertType">Alert type</param>
    /// <param name="escalationLevel">Escalation level</param>
    /// <returns>List of contacts to notify</returns>
    public async Task<List<EmergencyContactEntity>> GetContactsForEscalationAsync(
        string userId,
        string alertType,
        int escalationLevel
    )
    {
        var contacts = await GetByUserIdAsync(userId);

        // Filter contacts based on alert type and escalation level
        var filteredContacts = contacts
            .Where(contact =>
            {
                // For escalation level 1-2, only notify family/caregiver for urgent alerts
                if (escalationLevel <= 2)
                {
                    return (alertType.Contains("URGENT") || alertType.Contains("LOW"))
                        && (
                            contact.ContactType == EmergencyContactType.Family
                            || contact.ContactType == EmergencyContactType.Caregiver
                        );
                }

                // For escalation level 3+, notify all active contacts
                return true;
            })
            .ToList();

        return filteredContacts;
    }
}
