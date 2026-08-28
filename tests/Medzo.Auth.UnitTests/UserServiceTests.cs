using FluentAssertions;
using Medzo.Auth.Application.DTOs;
using Medzo.Auth.Application.Exceptions;
using Medzo.Auth.Application.Interfaces;
using Medzo.Auth.Application.Services;
using Medzo.Auth.Domain.Entities;
using Moq;
using Xunit;

namespace Medzo.Auth.UnitTests;

public class UserServiceTests
{
    private readonly Mock<IUserRepository> _users = new();
    private readonly Mock<IRoleRepository> _roles = new();
    private readonly Mock<IPasswordHasher> _passwordHasher = new();
    private readonly Mock<IStaffInvitationRepository> _staffInvitations = new();
    private readonly UserService _service;

    public UserServiceTests()
    {
        _service = new UserService(_users.Object, _roles.Object, _passwordHasher.Object, _staffInvitations.Object);
        _users.Setup(repository => repository.GetByNameAsync(It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(Array.Empty<User>());
        _roles.Setup(repository => repository.GetByNameAsync("InventoryManager"))
            .ReturnsAsync(new Role { Id = Guid.NewGuid(), Name = "InventoryManager" });
        _passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<string>()))
            .Returns("secure-hash");
        _users.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) => user);
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_SavesUserWithRoleAndHashedPassword()
    {
        var result = await _service.CreateAsync(ValidRequest());

        result.Username.Should().Be("new.staff");
        result.Email.Should().Be("staff@example.com");
        result.Roles.Should().ContainSingle().Which.Should().Be("InventoryManager");
        result.IsActive.Should().BeTrue();
        _users.Verify(repository => repository.AddAsync(It.Is<User>(user =>
            user.PasswordHash == "secure-hash" &&
            user.StaffId == "I1001" &&
            user.Roles.Single().Name == "InventoryManager")), Times.Once);
    }

    [Fact]
    public async Task CreateAsync_WithExistingEmail_RejectsWithoutSaving()
    {
        _users.Setup(repository => repository.GetByEmailAsync("staff@example.com"))
            .ReturnsAsync(new User());

        var action = () => _service.CreateAsync(ValidRequest());

        await action.Should().ThrowAsync<UserConflictException>()
            .WithMessage("*email address*");
        _users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithMatchingName_RequiresExplicitConfirmation()
    {
        _users.Setup(repository => repository.GetByNameAsync("New", "Staff"))
            .ReturnsAsync(new[]
            {
                new User
                {
                    Id = Guid.NewGuid(), Username = "other.staff", Email = "other@example.com",
                    FirstName = "New", LastName = "Staff"
                }
            });

        var action = () => _service.CreateAsync(ValidRequest());

        var exception = await action.Should().ThrowAsync<PotentialDuplicateUserException>();
        exception.Which.Duplicates.Should().ContainSingle();
        _users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task CreateAsync_WithConfirmedMatchingName_CreatesUser()
    {
        _users.Setup(repository => repository.GetByNameAsync("New", "Staff"))
            .ReturnsAsync(new[] { new User { FirstName = "New", LastName = "Staff" } });
        var request = ValidRequest();
        request.ConfirmPotentialDuplicate = true;

        var result = await _service.CreateAsync(request);

        result.Id.Should().NotBeEmpty();
        _users.Verify(repository => repository.AddAsync(It.IsAny<User>()), Times.Once);
    }

    private static CreateUserRequest ValidRequest() => new()
    {
        Username = "new.staff",
        StaffId = "I1001",
        Email = "STAFF@example.com",
        Password = "Strong1!",
        ConfirmPassword = "Strong1!",
        FirstName = "New",
        LastName = "Staff",
        Role = "InventoryManager"
    };
}
