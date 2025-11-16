namespace Nocturne.Tools.Abstractions.Commands;

/// <summary>
/// Base interface for all tool commands.
/// </summary>
public interface IToolCommandBase
{
    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Gets the description of the command.
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Base interface for tool commands that return a result.
/// </summary>
/// <typeparam name="TResult">The type of result returned by the command.</typeparam>
public interface IToolCommand<TResult> : IToolCommandBase
{
    /// <summary>
    /// Executes the command asynchronously.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command result.</returns>
    Task<TResult> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Base interface for tool commands that return an exit code.
/// </summary>
public interface IToolCommand : IToolCommand<int>
{
    // Inherits ExecuteAsync from IToolCommand<int> which returns Task<int>
}

