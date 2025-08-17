using MongoDB.Driver;
using Microsoft.AspNetCore.Cors;
using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddOpenApi();

// ✅ Configure CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ✅ Configure MongoDB connection
builder.Services.AddSingleton<IMongoClient>(serviceProvider =>
{
    // Replace with your MongoDB connection string
    var connectionString = "mongodb+srv://user:15ljgTqomgVY28Ia@cluster0.lpwvps5.mongodb.net/?retryWrites=true&w=majority&appName=Cluster0";
    return new MongoClient(connectionString);
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// ✅ Enable CORS
app.UseCors();

// ✅ Route to get all messages
app.MapGet("/messages", async (IMongoClient mongoClient, string? userId) =>
{
    var database = mongoClient.GetDatabase("Breakthrough");
    var collection = database.GetCollection<User>("users"); // Changed to User collection

    if (string.IsNullOrEmpty(userId))
    {
        return Results.BadRequest(new { Message = "UserId query parameter is required" });
    }

    // Find the user by ID
    var user = await collection.Find(u => u.Id == userId).FirstOrDefaultAsync();
    
    if (user == null)
    {
        return Results.NotFound(new { Message = "User not found" });
    }

    // Return the user's messages sorted by order
    var sortedMessages = user.UserMessages.OrderBy(m => m.Order).ToList();
    return Results.Ok(sortedMessages);
});

// ✅ Route to register a new user
app.MapPost("/auth/register", async (IMongoClient mongoClient, UserRegistrationRequest request) =>
{
    // Validate request
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        var errors = validationResults.Select(v => v.ErrorMessage).ToArray();
        return Results.BadRequest(new { Message = "Validation failed", Errors = errors });
    }

    var database = mongoClient.GetDatabase("Breakthrough");
    var collection = database.GetCollection<User>("users");

    // Check if user already exists (case-insensitive email check)
    var normalizedEmail = request.Email.ToLowerInvariant().Trim();
    var emailFilter = Builders<User>.Filter.Regex(u => u.Email, new BsonRegularExpression($"^{Regex.Escape(normalizedEmail)}$", "i"));
    var existingUser = await collection.Find(emailFilter).FirstOrDefaultAsync();
    
    if (existingUser != null)
    {
        return Results.BadRequest(new { 
            Message = "User with this email already exists",
            ErrorCode = "EMAIL_ALREADY_EXISTS",
            Field = "email"
        });
    }

    // Hash the password
    var hashedPassword = HashPassword(request.Password);

    // Create new user with normalized email
    var newUser = new User
    {
        Id = ObjectId.GenerateNewId().ToString(),
        Name = request.Name.Trim(),
        Email = normalizedEmail, // Use the normalized email
        Gender = request.Gender,
        Timezone = request.Timezone,
        PasswordHash = hashedPassword,
        UserMessages = new List<UserMessage>(), // Initialize with an empty list
        CreatedAt = DateTime.UtcNow
    };

    await collection.InsertOneAsync(newUser);

    // Return user without password
    var userResponse = new UserResponse
    {
        Id = newUser.Id,
        Name = newUser.Name,
        Email = newUser.Email,
        Gender = newUser.Gender,
        Timezone = newUser.Timezone,
        UserMessages = newUser.UserMessages,
        CreatedAt = newUser.CreatedAt
    };

    return Results.Ok(new { Message = "User registered successfully", User = userResponse });
});

// ✅ Route to login a user
app.MapPost("/auth/login", async (IMongoClient mongoClient, UserLoginRequest request) =>
{
    // Validate request
    var validationResults = new List<ValidationResult>();
    var validationContext = new ValidationContext(request);
    if (!Validator.TryValidateObject(request, validationContext, validationResults, true))
    {
        var errors = validationResults.Select(v => v.ErrorMessage).ToArray();
        return Results.BadRequest(new { Message = "Validation failed", Errors = errors });
    }

    var database = mongoClient.GetDatabase("Breakthrough");
    var collection = database.GetCollection<User>("users");

    // Find user by email (case-insensitive)
    var normalizedEmail = request.Email.ToLowerInvariant().Trim();
    var emailFilter = Builders<User>.Filter.Regex(u => u.Email, new BsonRegularExpression($"^{Regex.Escape(normalizedEmail)}$", "i"));
    var user = await collection.Find(emailFilter).FirstOrDefaultAsync();
    
    if (user == null)
    {
        return Results.BadRequest(new { Message = "Invalid email or password" });
    }

    // Verify password
    if (!VerifyPassword(request.Password, user.PasswordHash))
    {
        return Results.BadRequest(new { Message = "Invalid email or password" });
    }

    // Return user without password
    var userResponse = new UserResponse
    {
        Id = user.Id,
        Name = user.Name,
        Email = user.Email,
        Gender = user.Gender,
        Timezone = user.Timezone,
        UserMessages = user.UserMessages,
        CreatedAt = user.CreatedAt
    };

    return Results.Ok(new { Message = "Login successful", User = userResponse });
});

await app.RunAsync();

// ✅ Helper methods for password hashing
static string HashPassword(string password)
{
    using var sha256 = SHA256.Create();
    var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password + "BreakThroughSalt"));
    return Convert.ToBase64String(hashedBytes);
}

static bool VerifyPassword(string password, string hashedPassword)
{
    var hashedInput = HashPassword(password);
    return hashedInput == hashedPassword;
}

// ✅ Models
public class Message
{
    [BsonId] // Marks this property as the MongoDB _id
    [BsonRepresentation(BsonType.ObjectId)] // Converts ObjectId to string automatically
    public string Id { get; set; } = string.Empty;

    [BsonElement("content")] // Maps to the "content" field in your documents
    public string Content { get; set; } = string.Empty;

    [BsonElement("order")] // Maps to the "order" field in your documents
    public int Order { get; set; }
}

public class UserMessage
{
    [BsonId] // Marks this property as the MongoDB _id
    [BsonRepresentation(BsonType.ObjectId)] // Converts ObjectId to string automatically
    public string Id { get; set; } = string.Empty;

    [BsonElement("content")] // Maps to the "content" field in your documents
    public string Content { get; set; } = string.Empty;

    [BsonElement("order")] // Maps to the "order" field in your documents
    public int Order { get; set; }

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class User
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("email")]
    public string Email { get; set; } = string.Empty;

    [BsonElement("gender")]
    public string Gender { get; set; } = string.Empty;

    [BsonElement("timezone")]
    public string Timezone { get; set; } = string.Empty;

    [BsonElement("passwordHash")]
    public string PasswordHash { get; set; } = string.Empty;

    [BsonElement("userMessages")]
    public List<UserMessage> UserMessages { get; set; } = new List<UserMessage>();

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }
}

public class UserRegistrationRequest
{
    [Required]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Gender { get; set; } = string.Empty;

    [Required]
    public string Timezone { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string Password { get; set; } = string.Empty;
}

public class UserLoginRequest
{
    [Required]
    [EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Required]
    public string Password { get; set; } = string.Empty;
}

public class UserResponse
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Gender { get; set; } = string.Empty;
    public string Timezone { get; set; } = string.Empty;
    public List<UserMessage> UserMessages { get; set; } = new List<UserMessage>();
    public DateTime CreatedAt { get; set; }
}