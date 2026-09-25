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
            .ReturnsAsync(new Role { Id = "003", Name = "InventoryManager" });
        _roles.Setup(repository => repository.GetByNameAsync("Pharmacist"))
            .ReturnsAsync(new Role { Id = "002", Name = "Pharmacist" });
        _passwordHasher.Setup(hasher => hasher.HashPassword(It.IsAny<string>()))
            .Returns("secure-hash");
        _users.Setup(repository => repository.AddAsync(It.IsAny<User>()))
            .ReturnsAsync((User user) =>
            {
                user.UserNumber = 1;
                return user;
            });
    }

    [Fact]
    public async Task CreateAsync_WithValidRequest_SavesUserWithRoleAndHashedPassword()
    {
        var result = await _service.CreateAsync(ValidRequest());

        result.Username.Should().Be("new.staff");
        result.Email.Should().Be("staff@example.com");
        result.Roles.Should().ContainSingle().Which.Should().Be("InventoryManager");
        result.IsActive.Should().BeTrue();
        result.UserNumber.Should().Be(1);
        result.UserCode.Should().Be("001");
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

    [Fact]
    public async Task GetAllAsync_IncludesDeactivatedAccountsForAdminManagement()
    {
        _users.Setup(repository => repository.GetAllAsync()).ReturnsAsync(new[]
        {
            new User
            {
                Id = Guid.NewGuid(), Username = "active.staff", StaffId = "P1001",
                Email = "active@example.com", IsActive = true
            },
            new User
            {
                Id = Guid.NewGuid(), Username = "inactive.staff", StaffId = "I1002",
                Email = "inactive@example.com", IsActive = false
            }
        });

        var result = (await _service.GetAllAsync()).ToArray();

        result.Should().HaveCount(2);
        result.Single(user => user.StaffId == "I1002").IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ApproveStaffIdAsync_WithDeactivatedExistingAccount_IsRejected()
    {
        _users.Setup(repository => repository.GetByStaffIdAsync("P9001")).ReturnsAsync(new User
        {
            Id = Guid.NewGuid(), StaffId = "P9001", IsActive = false
        });

        var action = () => _service.ApproveStaffIdAsync(new StaffInvitationRequest
        {
            StaffId = "P9001",
            Role = "Pharmacist"
        });

        await action.Should().ThrowAsync<UserConflictException>()
            .WithMessage("*already exists*");
        _staffInvitations.Verify(
            repository => repository.AddAsync(It.IsAny<StaffInvitation>()), Times.Never);
    }

    [Fact]
    public async Task SetActiveAsync_WhenDeactivating_DisablesAccountAndReservesStaffId()
    {
        var id = Guid.NewGuid();
        var user = new User
        {
            Id = id,
            StaffId = "P7001",
            IsActive = true,
            Roles = new List<Role> { new() { Id = "002", Name = "Pharmacist" } }
        };
        _users.Setup(repository => repository.GetByIdAsync(id)).ReturnsAsync(user);

        var result = await _service.SetActiveAsync(id, false);

        result.IsActive.Should().BeFalse();
        _users.Verify(repository =>
            repository.DeactivateAndReserveStaffIdAsync(user, "Pharmacist"), Times.Once);
        _users.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
    }

    [Fact]
    public async Task UpdateManagedAsync_ChangesDetailsStaffIdAndRole()
    {
        var id = Guid.NewGuid();
        var existing = new User
        {
            Id = id,
            Username = "inventory.staff",
            StaffId = "I1001",
            Email = "inventory@example.com",
            FirstName = "Inventory",
            LastName = "Staff",
            Roles = new List<Role> { new() { Id = "003", Name = "InventoryManager" } }
        };
        _users.Setup(repository => repository.GetByIdAsync(id)).ReturnsAsync(existing);
        _users.Setup(repository => repository.GetByUsernameAsync("pharmacy.staff")).ReturnsAsync(existing);
        _users.Setup(repository => repository.GetByEmailAsync("pharmacy@example.com")).ReturnsAsync(existing);
        _users.Setup(repository => repository.GetByStaffIdAsync("P1001")).ReturnsAsync((User?)null);
        _users.Setup(repository => repository.UpdateAsync(existing)).Returns(Task.CompletedTask);

        var result = await _service.UpdateManagedAsync(id, new UpdateManagedUserRequest
        {
            Username = "pharmacy.staff",
            StaffId = "p1001",
            Email = "PHARMACY@example.com",
            FirstName = "Pharmacy",
            LastName = "Staff",
            Role = "Pharmacist"
        });

        result.StaffId.Should().Be("P1001");
        result.Email.Should().Be("pharmacy@example.com");
        result.Roles.Should().ContainSingle().Which.Should().Be("Pharmacist");
        _users.Verify(repository => repository.UpdateAsync(existing), Times.Once);
    }

    [Fact]
    public async Task UpdateManagedAsync_AdminAccount_IsRejected()
    {
        var id = Guid.NewGuid();
        _users.Setup(repository => repository.GetByIdAsync(id)).ReturnsAsync(new User
        {
            Id = id,
            Roles = new List<Role> { new() { Id = "001", Name = "Admin" } }
        });

        var action = () => _service.UpdateManagedAsync(id, new UpdateManagedUserRequest());

        await action.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Admin accounts*");
        _users.Verify(repository => repository.UpdateAsync(It.IsAny<User>()), Times.Never);
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
