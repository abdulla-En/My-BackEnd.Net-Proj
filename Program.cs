using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();
var logger = app.Logger;

var users = new List<User>();

string HashPasswordSha256(string password)
{
    using var sha256 = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

#region -------------------- Middlewares --------------------

// Global exception handler
app.Use(async (context, next) =>
{
    try
    {
        await next.Invoke();
    }
    catch (Exception ex)
    {
        context.Response.StatusCode = 500;
        await context.Response.WriteAsync($"Internal server error: {ex.Message}");
    }
});

// Token authentication for non-GET requests
app.UseWhen(context => context.Request.Method != "GET", appBuilder =>
{
    appBuilder.Use(async (context, next) =>
    {
        const string TokenHeader = "Authorization";
        const string ValidToken = "Bearer my-secret-token";

        var token = context.Request.Headers[TokenHeader].FirstOrDefault();

        if (token != ValidToken)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsync("Unauthorized: Invalid or missing token.");
            return;
        }

        await next();
    });
});

// Request logging
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var path = context.Request.Path;

    await next();

    var statusCode = context.Response.StatusCode;
    logger.LogInformation("HTTP {Method} {Path} responded with {StatusCode}", method, path, statusCode);
});

#endregion

#region -------------------- Endpoints --------------------

app.MapGet("/", () => "Hello, I am the root.");

app.MapGet("/users", () => users);

app.MapGet("/users/{id}", (Guid id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is not null)
    {
        logger.LogInformation("User with ID {Id} retrieved successfully.", user.Id);
        return Results.Ok(user);
    }

    logger.LogError("No user found with ID {Id}.", id);
    return Results.NotFound();
});

app.MapGet("/users/by-email/{email}", (string email) =>
{
    var user = users.FirstOrDefault(u => u.Email == email);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapGet("/users/search", (string name) =>
{
    var matched = users
        .Where(u => u.Name.Contains(name, StringComparison.OrdinalIgnoreCase))
        .ToList();

    return matched.Any() ? Results.Ok(matched) : Results.NotFound();
});

app.MapPost("/signup", (CreateUserRequest input) =>
{
    var validationContext = new ValidationContext(input);
    var validationResults = new List<ValidationResult>();

    if (!Validator.TryValidateObject(input, validationContext, validationResults, true))
    {
        return Results.BadRequest(validationResults);
    }

    if (users.Any(u => u.Email == input.Email))
    {
        return Results.BadRequest("Email already exists.");
    }

    var user = new User
    {
        Id = Guid.NewGuid(),
        Name = input.Name,
        Email = input.Email,
        PasswordHash = HashPasswordSha256(input.PasswordHash),
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow
    };

    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id}", (Guid id, CreateUserRequest updatedUser) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    if (users.Any(u => u.Email == updatedUser.Email && u.Id != id))
    {
        return Results.BadRequest("Email already exists.");
    }

    var newPass = HashPasswordSha256(updatedUser.PasswordHash);
    if (user.PasswordHash == newPass)
    {
        return Results.BadRequest("The new password must be different from the old one.");
    }

    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    user.PasswordHash = newPass;
    user.UpdatedAt = DateTime.UtcNow;

    return Results.Ok(user);
});

app.MapDelete("/users/{id}", (Guid id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    users.Remove(user);
    return Results.NoContent();
});

app.MapPost("/login", (LoginRequest credentials) =>
{
    var user = users.FirstOrDefault(u => u.Email == credentials.Email);
    var hashed = HashPasswordSha256(credentials.Password);

    if (user is not null && user.PasswordHash == hashed)
    {
        logger.LogInformation("User {Name} logged in successfully at {Time}.", user.Name, DateTime.UtcNow);
        return Results.Ok($"Welcome {user.Name}");
    }

    return Results.BadRequest("Email or password is incorrect. Please try again.");
});

#endregion

app.Run();

#region -------------------- Models --------------------

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public class CreateUserRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string PasswordHash { get; set; } = string.Empty;
}

public class LoginRequest
{
    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

#endregion