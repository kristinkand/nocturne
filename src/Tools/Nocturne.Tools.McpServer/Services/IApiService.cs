namespace Nocturne.Tools.McpServer.Services;

/// <summary>
/// Service interface for making API calls to the Nocturne API
/// </summary>
public interface IApiService
{
    /// <summary>
    /// Get data from the specified endpoint
    /// </summary>
    Task<string> GetAsync(string endpoint);

    /// <summary>
    /// Get typed data from the specified endpoint
    /// </summary>
    Task<T?> GetAsync<T>(string endpoint);

    /// <summary>
    /// Post data to the specified endpoint
    /// </summary>
    Task<string> PostAsync<T>(string endpoint, T data);

    /// <summary>
    /// Put data to the specified endpoint
    /// </summary>
    Task<string> PutAsync<T>(string endpoint, T data);

    /// <summary>
    /// Delete from the specified endpoint
    /// </summary>
    Task<string> DeleteAsync(string endpoint);
}
