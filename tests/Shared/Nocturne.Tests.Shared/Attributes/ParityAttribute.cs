using System;

namespace Nocturne.Tests.Shared.Attributes;

/// <summary>
/// Marks a test as implementing functionality parity with the legacy Nightscout JavaScript codebase.
/// These tests ensure that the C# rewrite maintains 1:1 functionality with the original implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class ParityAttribute : Attribute
{
    /// <summary>
    /// Gets the name of the original JavaScript test file that this test corresponds to.
    /// </summary>
    public string? OriginalTestFile { get; init; }

    /// <summary>
    /// Gets additional notes about the parity implementation.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParityAttribute"/> class.
    /// </summary>
    public ParityAttribute() { }

    /// <summary>
    /// Initializes a new instance of the <see cref="ParityAttribute"/> class with the original test file name.
    /// </summary>
    /// <param name="originalTestFile">The name of the original JavaScript test file.</param>
    public ParityAttribute(string originalTestFile)
    {
        OriginalTestFile = originalTestFile;
    }
}
