using Medzo.Auth.Domain.Entities;
using Medzo.Auth.Infrastructure.Authentication;
using Medzo.Auth.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

const string username = "ADMIN";
const string staffId = "A1234";
const string email = "medzoadmin@gmail.com";

LoadDotEnv();
var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
    throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not configured.");

var password = Environment.GetEnvironmentVariable("Admin__Password") ?? ReadPassword();
if (password.Length < 8)
    throw new InvalidOperationException("The Admin password must contain at least eight characters.");

var databaseProvider = Environment.GetEnvironmentVariable("Database__Provider") ?? "SqlServer";
var optionsBuilder = new DbContextOptionsBuilder<AuthDbContext>();
if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    optionsBuilder.UseSqlite(connectionString);
}
else
{
    optionsBuilder.UseSqlServer(connectionString);
}

var options = optionsBuilder.Options;
await using var database = new AuthDbContext(options);
if (databaseProvider.Equals("Sqlite", StringComparison.OrdinalIgnoreCase))
{
    await database.Database.EnsureCreatedAsync();
}
else
{
    await database.Database.MigrateAsync();
}

var adminRole = await database.Roles.SingleAsync(role => role.Id == "001" && role.Name == "Admin");
var matches = await database.Users
    .Include(user => user.Roles)
    .Include(user => user.RefreshTokens)
    .Where(user => user.Username == username || user.StaffId == staffId || user.Email == email)
    .ToListAsync();

if (matches.Select(user => user.Id).Distinct().Count() > 1)
    throw new InvalidOperationException("The configured Admin username, Staff ID, or email belongs to different accounts.");

var admin = matches.SingleOrDefault();
    var nextUserNumber = (await database.Users.OrderByDescending(user => user.UserNumber).Select(user => user.UserNumber).FirstOrDefaultAsync()) + 1;
if (admin is null)
{
    admin = new User
    {
        Id = Guid.NewGuid(),
        UserNumber = nextUserNumber,
        CreatedAt = DateTime.UtcNow
    };
    await database.Users.AddAsync(admin);
}

admin.Username = username;
admin.StaffId = staffId;
admin.Email = email;
admin.PasswordHash = new PasswordHasher().HashPassword(password);
admin.FirstName = string.Empty;
admin.LastName = string.Empty;
admin.IsActive = true;
admin.UpdatedAt = DateTime.UtcNow;
admin.Roles.Clear();
admin.Roles.Add(adminRole);
database.RefreshTokens.RemoveRange(admin.RefreshTokens);

await database.SaveChangesAsync();
Console.WriteLine($"Admin account {staffId} was provisioned successfully.");

static string ReadPassword()
{
    Console.Write("Admin password: ");
    var characters = new List<char>();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
            break;
        if (key.Key == ConsoleKey.Backspace && characters.Count > 0)
        {
            characters.RemoveAt(characters.Count - 1);
            continue;
        }
        if (!char.IsControl(key.KeyChar))
            characters.Add(key.KeyChar);
    }
    Console.WriteLine();
    return new string(characters.ToArray());
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
