using System.Text;
using Leafy_Library.Components;
using Leafy_Library.Models;
using Leafy_Library.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// MongoDB settings
builder.Services.Configure<MongoDbSettings>(
    builder.Configuration.GetSection("MongoDb"));

// JWT settings
builder.Services.Configure<JwtSettings>(
    builder.Configuration.GetSection("Jwt"));

// Embedding settings
builder.Services.Configure<EmbeddingSettings>(
    builder.Configuration.GetSection("Embedding"));

// Services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddHttpClient<EmbeddingService>();
builder.Services.AddSingleton<BookService>();
builder.Services.AddSingleton<AuthorService>();
builder.Services.AddSingleton<ReviewService>();
builder.Services.AddSingleton<IssueDetailService>();
builder.Services.AddSingleton<ReservationService>();
builder.Services.AddSingleton<UserService>();
builder.Services.AddSingleton<TokenService>();
builder.Services.AddScoped<JwtAuthenticationStateProvider>();
builder.Services.AddScoped<AuthenticationStateProvider>(
    provider => provider.GetRequiredService<JwtAuthenticationStateProvider>());

// Authentication with JWT Bearer (for API endpoints / middleware)
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("JWT Secret must be configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "LeafyLibrary",
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "LeafyLibraryUsers",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("Admin", policy =>
        policy.RequireRole("Admin"));
});
builder.Services.AddCascadingAuthenticationState();

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();

app.MapControllers();

// Login API endpoint — matches GET /users/login/:username from the Express app.
// If the user exists, logs them in; if not, creates a new user automatically.
app.MapGet("/api/users/login/{username}", async (string username, UserService userService, TokenService tokenService) =>
{
    if (string.IsNullOrWhiteSpace(username))
    {
        return Results.BadRequest(new { message = "Username is required" });
    }

    var user = await userService.GetOrCreateUserAsync(username);
    var token = tokenService.CreateToken(user);

    return Results.Ok(new
    {
        user.Id,
        user.Name,
        user.IsAdmin,
        Token = token
    });
});

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Ensure the Atlas Vector Search index exists and generate embeddings
var dbService = app.Services.GetRequiredService<DatabaseService>();
var embeddingSettings = app.Services.GetRequiredService<IOptions<EmbeddingSettings>>();
await dbService.EnsureSearchIndexAsync(embeddingSettings.Value.Dimensions);

var bookService = app.Services.GetRequiredService<BookService>();
await bookService.GenerateEmbeddingsAsync();

app.Run();
