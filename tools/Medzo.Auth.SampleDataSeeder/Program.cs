using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Authentication;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

LoadDotEnv();

var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
    ?? throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not configured.");
var databaseProvider = Environment.GetEnvironmentVariable("Database__Provider") ?? "SqlServer";
var demoPassword = Environment.GetEnvironmentVariable("SampleData__Password") ?? "Password1!";

var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    optionsBuilder.UseSqlite(connectionString);
else
    optionsBuilder.UseSqlServer(connectionString);

await using var database = new AuthDbContext(optionsBuilder.Options);
if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
    await database.Database.EnsureCreatedAsync();
else
    await database.Database.MigrateAsync();

var roles = await database.Roles.ToDictionaryAsync(role => role.Name, StringComparer.OrdinalIgnoreCase);
var hasher = new PasswordHasher();

await UpsertUserAsync("demo.admin", "A9001", "demo.admin@medzo.lk", "Demo", "Admin", "Admin");
await UpsertUserAsync("demo.pharmacist", "P1001", "demo.pharmacist@medzo.lk", "Demo", "Pharmacist", "Pharmacist");
await UpsertUserAsync("demo.inventory", "I1001", "demo.inventory@medzo.lk", "Demo", "Inventory", "InventoryManager");

await UpsertInvitationAsync("P2001", "Pharmacist");
await UpsertInvitationAsync("I2001", "InventoryManager");

await UpsertReviewAsync("Nimali Perera", "Customer", 5, "Quick service and clear guidance from the pharmacy team.");
await UpsertReviewAsync("Kasun Silva", "Customer", 4, "The medicine availability check made the visit much easier.");
await UpsertReviewAsync("Ayesha Fernando", "Customer", 5, "Friendly staff and reliable stock information.");

await database.SaveChangesAsync();
Console.WriteLine("Auth sample data was seeded successfully.");
Console.WriteLine("Demo login password: Password1!");

async Task UpsertUserAsync(string username, string staffId, string email, string firstName, string lastName, string roleName)
{
    var user = await database.Users.Include(item => item.Roles)
        .SingleOrDefaultAsync(item => item.Username == username || item.StaffId == staffId || item.Email == email);
    if (user is null)
    {
        user = new User
        {
            Id = Guid.NewGuid(),
            Username = username,
            StaffId = staffId,
            Email = email,
            CreatedAt = DateTime.UtcNow,
        };
        await database.Users.AddAsync(user);
    }

    user.FirstName = firstName;
    user.LastName = lastName;
    user.IsActive = true;
    user.UpdatedAt = DateTime.UtcNow;
    user.PasswordHash = hasher.HashPassword(demoPassword);
    user.Roles.Clear();
    user.Roles.Add(roles[roleName]);
}

async Task UpsertInvitationAsync(string staffId, string role)
{
    var invitation = await database.StaffInvitations.SingleOrDefaultAsync(item => item.StaffId == staffId);
    if (invitation is null)
    {
        await database.StaffInvitations.AddAsync(new StaffInvitation
        {
            Id = Guid.NewGuid(),
            StaffId = staffId,
            Role = role,
            IsClaimed = false,
            CreatedAt = DateTime.UtcNow,
        });
        return;
    }

    if (!invitation.IsClaimed)
        invitation.Role = role;
}

async Task UpsertReviewAsync(string name, string customerType, int rating, string comment)
{
    var exists = await database.Reviews.AnyAsync(item => item.Name == name && item.Comment == comment);
    if (exists) return;
    await database.Reviews.AddAsync(new Review
    {
        Id = Guid.NewGuid(),
        Name = name,
        CustomerType = customerType,
        Rating = rating,
        Comment = comment,
        CreatedAt = DateTime.UtcNow,
    });
}

static void LoadDotEnv()
{
    var directory = new DirectoryInfo(Directory.GetCurrentDirectory());
    while (directory is not null)
    {
        var path = Path.Combine(directory.FullName, ".env");
        if (File.Exists(path))
        {
            foreach (var rawLine in File.ReadLines(path))
            {
                var line = rawLine.Trim();
                if (line.Length == 0 || line.StartsWith('#')) continue;
                var separator = line.IndexOf('=');
                if (separator <= 0) continue;
                var key = line[..separator].Trim();
                var value = line[(separator + 1)..].Trim().Trim('"', '\'');
                if (Environment.GetEnvironmentVariable(key) is null)
                    Environment.SetEnvironmentVariable(key, value);
            }
            return;
        }
        directory = directory.Parent;
    }
}
