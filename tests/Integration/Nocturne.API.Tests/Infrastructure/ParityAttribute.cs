using System;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Marks tests that verify 1:1 parity with legacy Nightscout functionality
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false)]
public class ParityAttribute : Attribute { }
