using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

/// <summary>
/// Service for handling authorization operations including JWT generation and permission management
/// </summary>
public class AuthorizationService : IAuthorizationService, IDisposable
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthorizationService> _logger;
    private readonly PermissionTrie _permissionTrie;
    private readonly Dictionary<string, Permission> _seenPermissions = new();
    private readonly object _permissionsLock = new();

    // Memory protection for _seenPermissions
    private const int MAX_PERMISSIONS_CACHE_SIZE = 5000;
    private bool _disposed;

    public AuthorizationService(
        IPostgreSqlService postgreSqlService,
        IConfiguration configuration,
        ILogger<AuthorizationService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _configuration = configuration;
        _logger = logger;
        _permissionTrie = new PermissionTrie();

        // Initialize with common permissions
        InitializeCommonPermissions();
    }

    /// <summary>
    /// Generate JWT token from access token
    /// </summary>
    /// <param name="accessToken">Access token to exchange</param>
    /// <returns>Authorization response with JWT token</returns>
    public Task<AuthorizationResponse?> GenerateJwtFromAccessTokenAsync(string accessToken)
    {
        try
        {
            _logger.LogDebug("Generating JWT for access token");

            // TODO: Implement Subject management in PostgreSQL service
            // Find subject by access token
            throw new NotImplementedException(
                "Subject management with access tokens is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject/Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating JWT from access token");
            return Task.FromResult<AuthorizationResponse?>(null);
        }
    }

    /// <summary>
    /// Get all permissions that have been seen by the system
    /// </summary>
    /// <returns>List of permissions with usage statistics</returns>
    public Task<PermissionsResponse> GetAllPermissionsAsync()
    {
        try
        {
            _logger.LogDebug("Getting all seen permissions");

            var permissions = new List<Permission>();

            lock (_permissionsLock)
            {
                permissions = _seenPermissions.Values.ToList();
            }

            // TODO: Implement Role management in PostgreSQL service
            var roles = new List<Role>();

            var now = DateTime.UtcNow;

            foreach (var role in roles)
            {
                foreach (var permission in role.Permissions)
                {
                    if (!_seenPermissions.ContainsKey(permission))
                    {
                        permissions.Add(
                            new Permission
                            {
                                Name = permission,
                                Count = 0,
                                FirstSeen = now,
                                LastSeen = now,
                            }
                        );
                    }
                }
            }

            return Task.FromResult(
                new PermissionsResponse { Permissions = permissions.OrderBy(p => p.Name).ToList() }
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all permissions");
            return Task.FromResult(new PermissionsResponse());
        }
    }

    /// <summary>
    /// Get permission hierarchy structure as a trie
    /// </summary>
    /// <returns>Permission trie structure</returns>
    public Task<PermissionTrieResponse> GetPermissionTrieAsync()
    {
        try
        {
            _logger.LogDebug("Building permission trie structure");

            // TODO: Implement Role management in PostgreSQL service
            var roles = new List<Role>();

            var allPermissions = new HashSet<string>();
            foreach (var role in roles)
            {
                foreach (var permission in role.Permissions)
                {
                    allPermissions.Add(permission);
                }
            }

            // Add seen permissions
            lock (_permissionsLock)
            {
                foreach (var permission in _seenPermissions.Keys)
                {
                    allPermissions.Add(permission);
                }
            }

            // Build new trie with all permissions
            var trie = new PermissionTrie();
            trie.Add(allPermissions);

            // Convert to our response format
            var response = new PermissionTrieResponse
            {
                Root = BuildTrieNode(trie),
                Count = trie.Count,
            };

            _logger.LogDebug("Built permission trie with {Count} permissions", response.Count);
            return Task.FromResult(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error building permission trie");
            return Task.FromResult(new PermissionTrieResponse());
        }
    }

    /// <summary>
    /// Check if a permission is allowed for a subject
    /// </summary>
    /// <param name="subjectId">Subject identifier</param>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if permission is granted</returns>
    public Task<bool> CheckPermissionAsync(string subjectId, string permission)
    {
        try
        {
            _logger.LogDebug(
                "Checking permission {Permission} for subject {SubjectId}",
                permission,
                subjectId
            );

            // TODO: Implement Subject lookup in PostgreSQL service
            // TODO: Implement Subject and Role lookup in PostgreSQL service
            throw new NotImplementedException(
                "Subject and Role lookup is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject/Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error checking permission {Permission} for subject {SubjectId}",
                permission,
                subjectId
            );
            return Task.FromResult(false);
        }
    }

    /// <summary>
    /// Record a permission usage for statistics
    /// </summary>
    /// <param name="permission">Permission that was used</param>
    public Task RecordPermissionUsageAsync(string permission)
    {
        try
        {
            lock (_permissionsLock)
            {
                var now = DateTime.UtcNow;

                if (_seenPermissions.ContainsKey(permission))
                {
                    _seenPermissions[permission].Count++;
                    _seenPermissions[permission].LastSeen = now;
                }
                else
                {
                    _seenPermissions[permission] = new Permission
                    {
                        Name = permission,
                        Count = 1,
                        FirstSeen = now,
                        LastSeen = now,
                    };
                }

                // Periodically clean up old permissions to prevent memory leaks
                if (_seenPermissions.Count % 100 == 0)
                {
                    CleanupOldPermissions();
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording permission usage for {Permission}", permission);
        }

        return Task.CompletedTask;
    }

    // Subject management methods
    /// <summary>
    /// Get all subjects
    /// </summary>
    /// <returns>List of all subjects</returns>
    public Task<List<Subject>> GetAllSubjectsAsync()
    {
        try
        {
            _logger.LogDebug("Getting all subjects");

            // TODO: Implement GetAllSubjects in PostgreSQL service
            throw new NotImplementedException(
                "Subject management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all subjects");
            throw;
        }
    }

    /// <summary>
    /// Get a subject by ID
    /// </summary>
    /// <param name="id">Subject ID</param>
    /// <returns>Subject or null if not found</returns>
    public Task<Subject?> GetSubjectByIdAsync(string id)
    {
        try
        {
            _logger.LogDebug("Getting subject by ID: {Id}", id);

            // TODO: Implement GetSubjectById in PostgreSQL service
            throw new NotImplementedException(
                "Subject management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting subject by ID: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Create a new subject
    /// </summary>
    /// <param name="subject">Subject to create</param>
    /// <returns>Created subject</returns>
    public Task<Subject> CreateSubjectAsync(Subject subject)
    {
        try
        {
            _logger.LogDebug("Creating new subject: {Name}", subject.Name);

            // Set timestamps
            var now = DateTime.UtcNow;
            subject.Created = now;
            subject.Modified = now;

            // Generate access token if not provided
            if (string.IsNullOrEmpty(subject.AccessToken))
            {
                subject.AccessToken = GenerateAccessToken();
            }

            // Set ID if not provided
            if (string.IsNullOrEmpty(subject.Id))
            {
                subject.Id = Guid.CreateVersion7().ToString("N");
            }

            // TODO: Implement CreateSubject in PostgreSQL service
            throw new NotImplementedException(
                "Subject management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating subject: {Name}", subject.Name);
            throw;
        }
    }

    /// <summary>
    /// Update an existing subject
    /// </summary>
    /// <param name="subject">Subject to update</param>
    /// <returns>Updated subject or null if not found</returns>
    public Task<Subject?> UpdateSubjectAsync(Subject subject)
    {
        try
        {
            _logger.LogDebug("Updating subject: {Id}", subject.Id);

            // Set modified timestamp
            subject.Modified = DateTime.UtcNow;

            // TODO: Implement UpdateSubject in PostgreSQL service
            throw new NotImplementedException(
                "Subject management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating subject: {Id}", subject.Id);
            throw;
        }
    }

    /// <summary>
    /// Delete a subject by ID
    /// </summary>
    /// <param name="id">Subject ID</param>
    /// <returns>True if deleted, false if not found</returns>
    public Task<bool> DeleteSubjectAsync(string id)
    {
        try
        {
            _logger.LogDebug("Deleting subject: {Id}", id);

            // TODO: Implement DeleteSubject in PostgreSQL service
            throw new NotImplementedException(
                "Subject management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Subject operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting subject: {Id}", id);
            throw;
        }
    }

    // Role management methods
    /// <summary>
    /// Get all roles
    /// </summary>
    /// <returns>List of all roles</returns>
    public Task<List<Role>> GetAllRolesAsync()
    {
        try
        {
            _logger.LogDebug("Getting all roles");

            // TODO: Implement GetAllRoles in PostgreSQL service
            throw new NotImplementedException(
                "Role management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting all roles");
            throw;
        }
    }

    /// <summary>
    /// Get a role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>Role or null if not found</returns>
    public Task<Role?> GetRoleByIdAsync(string id)
    {
        try
        {
            _logger.LogDebug("Getting role by ID: {Id}", id);

            // TODO: Implement GetRoleById in PostgreSQL service
            throw new NotImplementedException(
                "Role management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting role by ID: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Create a new role
    /// </summary>
    /// <param name="role">Role to create</param>
    /// <returns>Created role</returns>
    public Task<Role> CreateRoleAsync(Role role)
    {
        try
        {
            _logger.LogDebug("Creating new role: {Name}", role.Name);

            // Set timestamps
            var now = DateTime.UtcNow;
            role.Created = now;
            role.Modified = now;

            // Set ID if not provided
            if (string.IsNullOrEmpty(role.Id))
            {
                role.Id = Guid.CreateVersion7().ToString("N");
            }

            // TODO: Implement CreateRole in PostgreSQL service
            throw new NotImplementedException(
                "Role management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating role: {Name}", role.Name);
            throw;
        }
    }

    /// <summary>
    /// Update an existing role
    /// </summary>
    /// <param name="role">Role to update</param>
    /// <returns>Updated role or null if not found</returns>
    public Task<Role?> UpdateRoleAsync(Role role)
    {
        try
        {
            _logger.LogDebug("Updating role: {Id}", role.Id);

            // Set modified timestamp
            role.Modified = DateTime.UtcNow;

            // TODO: Implement UpdateRole in PostgreSQL service
            throw new NotImplementedException(
                "Role management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating role: {Id}", role.Id);
            throw;
        }
    }

    /// <summary>
    /// Delete a role by ID
    /// </summary>
    /// <param name="id">Role ID</param>
    /// <returns>True if deleted, false if not found</returns>
    public Task<bool> DeleteRoleAsync(string id)
    {
        try
        {
            _logger.LogDebug("Deleting role: {Id}", id);

            // TODO: Implement DeleteRole in PostgreSQL service
            throw new NotImplementedException(
                "Role management is not yet implemented in PostgreSQL adapter. "
                    + "Requires extending IPostgreSqlService with Role operations."
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting role: {Id}", id);
            throw;
        }
    }

    /// <summary>
    /// Generate a secure access token
    /// </summary>
    /// <returns>Access token string</returns>
    private static string GenerateAccessToken()
    {
        return Guid.CreateVersion7().ToString("N") + "-" + Guid.CreateVersion7().ToString("N");
    }

    /// <summary>
    /// Initialize common Nightscout permissions
    /// </summary>
    private void InitializeCommonPermissions()
    {
        var commonPermissions = new[]
        {
            "*",
            "api:*",
            "api:*:read",
            "api:*:create",
            "api:*:update",
            "api:*:delete",
            "api:*:admin",
            "api:entries:*",
            "api:entries:read",
            "api:entries:create",
            "api:entries:update",
            "api:entries:delete",
            "api:treatments:*",
            "api:treatments:read",
            "api:treatments:create",
            "api:treatments:update",
            "api:treatments:delete",
            "api:devicestatus:*",
            "api:devicestatus:read",
            "api:devicestatus:create",
            "api:devicestatus:update",
            "api:devicestatus:delete",
            "api:profile:*",
            "api:profile:read",
            "api:profile:create",
            "api:profile:update",
            "api:profile:delete",
            "api:food:*",
            "api:food:read",
            "api:food:create",
            "api:food:update",
            "api:food:delete",
            "api:activity:*",
            "api:activity:read",
            "api:activity:create",
            "api:activity:update",
            "api:activity:delete",
            "readable",
            "denied",
            "admin",
        };

        _permissionTrie.Add(commonPermissions);

        var now = DateTime.UtcNow;
        lock (_permissionsLock)
        {
            foreach (var permission in commonPermissions)
            {
                _seenPermissions[permission] = new Permission
                {
                    Name = permission,
                    Count = 0,
                    FirstSeen = now,
                    LastSeen = now,
                };
            }
        }
    }

    /// <summary>
    /// Build a trie node for API response (recursive helper)
    /// </summary>
    /// <param name="trie">The permission trie</param>
    /// <returns>Root trie node</returns>
    private PermissionTrieNode BuildTrieNode(PermissionTrie trie)
    {
        // NOTE: The ShiroTrie library doesn't expose internal structure directly,
        // so we'll create a simplified representation based on the permissions
        var root = new PermissionTrieNode { Name = "root" };

        // We'll need to reconstruct the tree structure from the permissions
        // This is a simplified version - in a real implementation, we'd need
        // access to the internal trie structure or build our own
        var allPermissions = new List<string>();

        lock (_permissionsLock)
        {
            allPermissions.AddRange(_seenPermissions.Keys);
        }

        foreach (var permission in allPermissions)
        {
            var parts = permission.Split(':');
            var currentNode = root;

            for (int i = 0; i < parts.Length; i++)
            {
                var part = parts[i];

                if (!currentNode.Children.ContainsKey(part))
                {
                    currentNode.Children[part] = new PermissionTrieNode
                    {
                        Name = part,
                        IsLeaf = i == parts.Length - 1,
                    };
                }

                currentNode = currentNode.Children[part];

                // Update leaf status - a node is a leaf if it's at the end of this path
                // or if it represents a complete permission
                if (i == parts.Length - 1)
                {
                    currentNode.IsLeaf = true;
                }
            }
        }

        return root;
    }

    /// <summary>
    /// Clean up old permissions from the cache to prevent unbounded memory growth
    /// </summary>
    private void CleanupOldPermissions()
    {
        try
        {
            if (_seenPermissions.Count <= MAX_PERMISSIONS_CACHE_SIZE)
                return;

            var now = DateTime.UtcNow;
            var cutoffTime = now.AddDays(-30); // Remove permissions not seen in 30 days

            var permissionsToRemove = _seenPermissions
                .Where(kvp => kvp.Value.LastSeen < cutoffTime)
                .Select(kvp => kvp.Key)
                .ToList();

            foreach (var permission in permissionsToRemove)
            {
                _seenPermissions.Remove(permission);
            }

            // If still too many, remove the least recently used
            if (_seenPermissions.Count > MAX_PERMISSIONS_CACHE_SIZE)
            {
                var lruPermissions = _seenPermissions
                    .OrderBy(kvp => kvp.Value.LastSeen)
                    .Take(_seenPermissions.Count - (MAX_PERMISSIONS_CACHE_SIZE * 3 / 4))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var permission in lruPermissions)
                {
                    _seenPermissions.Remove(permission);
                }
            }

            _logger.LogDebug(
                "Cleaned up permissions cache, current size: {Count}",
                _seenPermissions.Count
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error cleaning up permissions cache");
        }
    }

    /// <summary>
    /// Dispose resources
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            lock (_permissionsLock)
            {
                _seenPermissions.Clear();
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error disposing AuthorizationService");
        }

        _disposed = true;
    }
}
