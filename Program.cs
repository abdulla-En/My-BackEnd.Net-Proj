using System.Security.Cryptography;
using System.Text;

var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();

var users = new List<User>();

string HashPasswordSha256(string password)
{
    using var sha256 = SHA256.Create();
    var bytes = Encoding.UTF8.GetBytes(password);
    var hash = sha256.ComputeHash(bytes);
    return Convert.ToBase64String(hash);
}

app.MapGet("/", () => "Hello iam the root .");
app.MapGet("/users", () => users);

app.MapGet("/users/{id}", (Guid id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    return user is not null ? Results.Ok(user) : Results.NotFound();
});

app.MapPost("/users", (CreateUserRequest input) =>
{
    // what is the Mapper Mean ?
    var user = new User
    {
        Name = input.Name,
        Email = input.Email,
        PasswordHash = HashPasswordSha256(input.PasswordHash),
        Id = Guid.NewGuid()
    };
    users.Add(user);
    return Results.Created($"/users/{user.Id}", user);
});

app.MapPut("/users/{id}", (Guid id, CreateUserRequest updatedUser) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    user.Name = updatedUser.Name;
    user.Email = updatedUser.Email;
    user.PasswordHash = HashPasswordSha256(updatedUser.PasswordHash);
    return Results.Ok(user);
});

app.MapDelete("/users/{id}", (Guid id) =>
{
    var user = users.FirstOrDefault(u => u.Id == id);
    if (user is null) return Results.NotFound();

    users.Remove(user);
    return Results.NoContent();
});

app.Run();

public class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}


//what is a DTO Mean ?
public class CreateUserRequest
{
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
}