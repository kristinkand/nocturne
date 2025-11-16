using System.ComponentModel.DataAnnotations;

namespace Nocturne.Core.Models;

/// <summary>
/// Simple permission trie implementation for organizing and checking permissions
/// </summary>
public class PermissionTrie
{
    private readonly HashSet<string> _permissions = new();

    /// <summary>
    /// Number of permissions in the trie
    /// </summary>
    public int Count => _permissions.Count;

    /// <summary>
    /// Add a single permission to the trie
    /// </summary>
    /// <param name="permission">Permission to add</param>
    public void Add(string permission)
    {
        _permissions.Add(permission);
    }

    /// <summary>
    /// Add multiple permissions to the trie
    /// </summary>
    /// <param name="permissions">Permissions to add</param>
    public void Add(IEnumerable<string> permissions)
    {
        foreach (var permission in permissions)
        {
            _permissions.Add(permission);
        }
    }

    /// <summary>
    /// Check if a permission is granted by the trie
    /// This supports wildcard permissions and hierarchical checking
    /// </summary>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if permission is granted</returns>
    public bool Check(string permission)
    {
        // Check for exact match
        if (_permissions.Contains(permission))
        {
            return true;
        }

        // Check for wildcard permissions
        if (_permissions.Contains("*"))
        {
            return true;
        }

        // Check for hierarchical wildcards (e.g., "api:*" matches "api:entries:read")
        var parts = permission.Split(':');
        for (int i = 1; i <= parts.Length; i++)
        {
            var wildcardPermission = string.Join(":", parts.Take(i)) + ":*";
            if (_permissions.Contains(wildcardPermission))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Check if a permission exists in the trie (exact match)
    /// </summary>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if permission exists</returns>
    public bool Contains(string permission)
    {
        return _permissions.Contains(permission);
    }

    /// <summary>
    /// Get all permissions in the trie
    /// </summary>
    /// <returns>Collection of all permissions</returns>
    public IEnumerable<string> GetAll()
    {
        return _permissions.ToList();
    }
}

/// <summary>
/// Authorization request for generating JWT from access token
/// </summary>
public class AuthorizationRequest
{
    /// <summary>
    /// Access token to exchange for JWT
    /// </summary>
    [Required]
    public string AccessToken { get; set; } = string.Empty;
}

/// <summary>
/// Authorization response containing JWT token
/// </summary>
public class AuthorizationResponse
{
    /// <summary>
    /// Generated JWT token
    /// </summary>
    public string Token { get; set; } = string.Empty;

    /// <summary>
    /// Token expiration timestamp
    /// </summary>
    public long Exp { get; set; }

    /// <summary>
    /// Subject identifier
    /// </summary>
    public string Sub { get; set; } = string.Empty;

    /// <summary>
    /// Issued at timestamp
    /// </summary>
    public long Iat { get; set; }
}

/// <summary>
/// Permission information
/// </summary>
public class Permission
{
    /// <summary>
    /// Permission string (e.g., "api:treatments:read")
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Number of times this permission has been seen/used
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// First time this permission was seen
    /// </summary>
    public DateTime FirstSeen { get; set; }

    /// <summary>
    /// Last time this permission was seen
    /// </summary>
    public DateTime LastSeen { get; set; }
}

/// <summary>
/// List of all permissions that have been seen by the system
/// </summary>
public class PermissionsResponse
{
    /// <summary>
    /// List of all permissions
    /// </summary>
    public List<Permission> Permissions { get; set; } = new();
}

/// <summary>
/// Permission hierarchy structure represented as a trie
/// </summary>
public class PermissionTrieResponse
{
    /// <summary>
    /// Root node of the permission trie
    /// </summary>
    public PermissionTrieNode Root { get; set; } = new();

    /// <summary>
    /// Total number of permissions in the trie
    /// </summary>
    public int Count { get; set; }
}

/// <summary>
/// Node in the permission trie structure
/// </summary>
public class PermissionTrieNode
{
    /// <summary>
    /// Name of this node
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Whether this node represents a complete permission (leaf node)
    /// </summary>
    public bool IsLeaf { get; set; }

    /// <summary>
    /// Child nodes
    /// </summary>
    public Dictionary<string, PermissionTrieNode> Children { get; set; } = new();
}

/// <summary>
/// Role definition
/// </summary>
public class Role
{
    /// <summary>
    /// Unique identifier for the role
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// MongoDB internal ID (for compatibility)
    /// </summary>
    public string? _id { get; set; }

    /// <summary>
    /// Name of the role
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of permissions assigned to this role
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// Optional notes about the role
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Whether this is a system-generated role
    /// </summary>
    public bool AutoGenerated { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime Modified { get; set; }
}

/// <summary>
/// Subject (user/device) definition
/// </summary>
public class Subject
{
    /// <summary>
    /// Unique identifier for the subject
    /// </summary>
    public string? Id { get; set; }

    /// <summary>
    /// MongoDB internal ID (for compatibility)
    /// </summary>
    public string? _id { get; set; }

    /// <summary>
    /// Name of the subject
    /// </summary>
    [Required]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// List of roles assigned to this subject
    /// </summary>
    public List<string> Roles { get; set; } = new();

    /// <summary>
    /// Access token for this subject
    /// </summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Optional notes about the subject
    /// </summary>
    public string? Notes { get; set; }

    /// <summary>
    /// Creation timestamp
    /// </summary>
    public DateTime Created { get; set; }

    /// <summary>
    /// Last modified timestamp
    /// </summary>
    public DateTime Modified { get; set; }
}
