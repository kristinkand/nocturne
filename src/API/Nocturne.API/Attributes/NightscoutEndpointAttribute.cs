namespace Nocturne.API.Attributes;

/// <summary>
/// Attribute to mark controller methods with their corresponding Nightscout endpoint
/// This provides documentation and traceability for 1:1 API compatibility
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public class NightscoutEndpointAttribute : Attribute
{
    /// <summary>
    /// The Nightscout endpoint this method implements
    /// </summary>
    public string Endpoint { get; }

    /// <summary>
    /// Initializes a new instance of the NightscoutEndpointAttribute
    /// </summary>
    /// <param name="endpoint">The Nightscout endpoint this method implements (e.g., "/api/v1/profile")</param>
    public NightscoutEndpointAttribute(string endpoint)
    {
        Endpoint = endpoint ?? throw new ArgumentNullException(nameof(endpoint));
    }
}
