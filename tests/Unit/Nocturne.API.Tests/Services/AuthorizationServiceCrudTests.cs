using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.API.Services.Auth;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Tests for authorization service CRUD operations
/// </summary>
public class AuthorizationServiceCrudTests
{
    private readonly Mock<IPostgreSqlService> _mockPostgreSqlService;
    private readonly Mock<IConfiguration> _mockConfiguration;
    private readonly Mock<ILogger<AuthorizationService>> _mockLogger;
    private readonly Mock<ISubjectService> _mockSubjectService;
    private readonly Mock<IRoleService> _mockRoleService;
    private readonly Mock<IJwtService> _mockJwtService;
    private readonly AuthorizationService _authorizationService;

    public AuthorizationServiceCrudTests()
    {
        _mockPostgreSqlService = new Mock<IPostgreSqlService>();
        _mockConfiguration = new Mock<IConfiguration>();
        _mockLogger = new Mock<ILogger<AuthorizationService>>();
        _mockSubjectService = new Mock<ISubjectService>();
        _mockRoleService = new Mock<IRoleService>();
        _mockJwtService = new Mock<IJwtService>();

        // Setup configuration
        _mockConfiguration
            .Setup(c => c["JwtSettings:SecretKey"])
            .Returns("TestSecretKeyForNightscout");

        _authorizationService = new AuthorizationService(
            _mockPostgreSqlService.Object,
            _mockConfiguration.Object,
            _mockLogger.Object,
            _mockSubjectService.Object,
            _mockRoleService.Object,
            _mockJwtService.Object
        );
    }

    #region Subject CRUD Tests

    [Fact]
    public async Task GetAllSubjectsAsync_ThrowsNotImplementedException()
    {
        // Arrange
        // No setup needed since the method currently throws NotImplementedException

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.GetAllSubjectsAsync()
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task GetSubjectByIdAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var subjectId = "test-subject-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.GetSubjectByIdAsync(subjectId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task GetSubjectByIdAsync_WithNonExistentId_ThrowsNotImplementedException()
    {
        // Arrange
        var nonExistentId = "nonexistent-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.GetSubjectByIdAsync(nonExistentId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task CreateSubjectAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var subjectToCreate = new Subject
        {
            Name = "New Subject",
            Roles = new List<string> { "user" },
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.CreateSubjectAsync(subjectToCreate)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task UpdateSubjectAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var subjectToUpdate = new Subject
        {
            Id = "123",
            Name = "Updated Subject",
            AccessToken = "token123",
            Roles = new List<string> { "admin" },
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.UpdateSubjectAsync(subjectToUpdate)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task UpdateSubjectAsync_WithNonExistentSubject_ThrowsNotImplementedException()
    {
        // Arrange
        var subjectToUpdate = new Subject { Id = "nonexistent", Name = "Test Subject" };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.UpdateSubjectAsync(subjectToUpdate)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task DeleteSubjectAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var subjectId = "test-subject-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.DeleteSubjectAsync(subjectId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    [Fact]
    public async Task DeleteSubjectAsync_WithNonExistentId_ThrowsNotImplementedException()
    {
        // Arrange
        var nonExistentId = "nonexistent-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.DeleteSubjectAsync(nonExistentId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Subject", exception.Message);
    }

    #endregion

    #region Role CRUD Tests

    [Fact]
    public async Task GetAllRolesAsync_ThrowsNotImplementedException()
    {
        // Arrange
        // No setup needed since the method currently throws NotImplementedException

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.GetAllRolesAsync()
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Fact]
    public async Task GetRoleByIdAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var roleId = "test-role-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.GetRoleByIdAsync(roleId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Fact]
    public async Task CreateRoleAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var roleToCreate = new Role
        {
            Name = "editor",
            Permissions = new List<string> { "api:*:read", "api:treatments:update" },
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.CreateRoleAsync(roleToCreate)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Fact]
    public async Task UpdateRoleAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var roleToUpdate = new Role
        {
            Id = "456",
            Name = "updated-moderator",
            Permissions = new List<string> { "api:*:read" },
        };

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.UpdateRoleAsync(roleToUpdate)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Fact]
    public async Task DeleteRoleAsync_ThrowsNotImplementedException()
    {
        // Arrange
        var roleId = "test-role-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.DeleteRoleAsync(roleId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    [Fact]
    public async Task DeleteRoleAsync_WithNonExistentId_ThrowsNotImplementedException()
    {
        // Arrange
        var nonExistentId = "nonexistent-id";

        // Act & Assert
        var exception = await Assert.ThrowsAsync<NotImplementedException>(() =>
            _authorizationService.DeleteRoleAsync(nonExistentId)
        );

        // Verify the exception message indicates PostgreSQL implementation is missing
        Assert.Contains("PostgreSQL adapter", exception.Message);
        Assert.Contains("Role", exception.Message);
    }

    #endregion
}
