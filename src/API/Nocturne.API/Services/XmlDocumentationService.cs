using System.Reflection;
using System.Xml;
using Nocturne.Core.Contracts;

namespace Nocturne.API.Services;

/// <summary>
/// Service for extracting XML documentation comments from methods
/// </summary>
public interface IXmlDocumentationService
{
    /// <summary>
    /// Get the summary documentation for a method
    /// </summary>
    /// <param name="methodInfo">The method to get documentation for</param>
    /// <returns>The summary text or null if not found</returns>
    string? GetMethodSummary(MethodInfo methodInfo);
}

/// <summary>
/// Implementation of XML documentation service
/// </summary>
public class XmlDocumentationService : IXmlDocumentationService
{
    private readonly Dictionary<string, XmlDocument> _xmlDocuments = new();
    private readonly ILogger<XmlDocumentationService> _logger;

    public XmlDocumentationService(ILogger<XmlDocumentationService> logger)
    {
        _logger = logger;
        LoadXmlDocumentation();
    }

    /// <summary>
    /// Get the summary documentation for a method
    /// </summary>
    /// <param name="methodInfo">The method to get documentation for</param>
    /// <returns>The summary text or null if not found</returns>
    public string? GetMethodSummary(MethodInfo methodInfo)
    {
        if (methodInfo?.DeclaringType == null)
            return null;

        try
        {
            var assemblyName = methodInfo.DeclaringType.Assembly.GetName().Name;
            if (assemblyName == null || !_xmlDocuments.ContainsKey(assemblyName))
                return null;

            var xmlDoc = _xmlDocuments[assemblyName];
            var memberName = GetMemberName(methodInfo);

            var memberNode = xmlDoc.SelectSingleNode($"//member[@name='{memberName}']");
            var summaryNode = memberNode?.SelectSingleNode("summary");

            if (summaryNode?.InnerText != null)
            {
                // Clean up the XML text (remove extra whitespace, newlines)
                return summaryNode
                    .InnerText.Trim()
                    .Replace("\n", " ")
                    .Replace("\r", "")
                    .Replace("  ", " ")
                    .Trim();
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(
                ex,
                "Could not extract XML documentation for method {Method}",
                methodInfo.Name
            );
        }

        return null;
    }

    /// <summary>
    /// Load XML documentation files for all loaded assemblies
    /// </summary>
    private void LoadXmlDocumentation()
    {
        var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();

        foreach (var assembly in loadedAssemblies)
        {
            try
            {
                var assemblyName = assembly.GetName().Name;
                if (string.IsNullOrEmpty(assemblyName))
                    continue;

                // Look for XML documentation file next to the assembly
                var assemblyLocation = assembly.Location;
                if (string.IsNullOrEmpty(assemblyLocation))
                    continue;

                var xmlPath = Path.ChangeExtension(assemblyLocation, ".xml");

                if (File.Exists(xmlPath))
                {
                    var xmlDoc = new XmlDocument();
                    xmlDoc.Load(xmlPath);
                    _xmlDocuments[assemblyName] = xmlDoc;

                    _logger.LogDebug(
                        "Loaded XML documentation for assembly: {AssemblyName}",
                        assemblyName
                    );
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Could not load XML documentation for assembly {Assembly}",
                    assembly.GetName().Name
                );
            }
        }
    }

    /// <summary>
    /// Generate the XML member name for a method
    /// </summary>
    /// <param name="methodInfo">The method info</param>
    /// <returns>The XML member name</returns>
    private static string GetMemberName(MethodInfo methodInfo)
    {
        var declaringType = methodInfo.DeclaringType;
        if (declaringType == null)
            return string.Empty;

        var methodName = methodInfo.Name;
        var typeName = declaringType.FullName?.Replace('+', '.');

        // Handle method parameters
        var parameters = methodInfo.GetParameters();
        if (parameters.Length > 0)
        {
            var parameterTypes = parameters.Select(p => p.ParameterType.FullName).ToArray();
            methodName += $"({string.Join(",", parameterTypes)})";
        }

        return $"M:{typeName}.{methodName}";
    }
}
