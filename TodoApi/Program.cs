using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using TodoApi.Data;
using TodoApi.Dtos;
using TodoApi.Models;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("DefaultConnection is not configured.");

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(connectionString));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT key is not configured.");

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

var todoGroup = app.MapGroup("/api/todos").WithTags("Todos");

todoGroup.MapGet("/", async (AppDbContext db) =>
{
    var todos = await db.Todos
        .AsNoTracking()
        .OrderBy(todo => todo.Id)
        .Select(todo => new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted))
        .ToListAsync();

    return todos.Count == 0 ? Results.NotFound() : Results.Ok(todos);
})
.RequireAuthorization();

todoGroup.MapPost("/", async (AppDbContext db, TodoPostDto dto) =>
{
    if (string.IsNullOrWhiteSpace(dto.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(dto.Title)] = ["Title is required."]
        });
    }

    var lastTodo = await db.Todos
        .OrderByDescending(todo => todo.Id)
        .FirstOrDefaultAsync();

    var todo = new TodoItem
    {
        Id = lastTodo is null ? 1 : lastTodo.Id + 1,
        Title = dto.Title.Trim(),
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    var response = new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted);
    return Results.Created($"/api/todos/{todo.Id}", response);
})
.RequireAuthorization();

app.MapPost("/api/auth/login", (
    LoginDto login,
    IConfiguration configuration) =>
{
    if (login.Username != "student" || login.Password != "password")
    {
        return Results.Unauthorized();
    }

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, login.Username)
    };

    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

    var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    var expireDays = int.Parse(configuration["Jwt:ExpireDays"]!);
    var expiration = DateTime.UtcNow.AddDays(expireDays);

    var token = new JwtSecurityToken(
        issuer: configuration["Jwt:Issuer"],
        audience: configuration["Jwt:Audience"],
        claims: claims,
        expires: expiration,
        signingCredentials: credentials);

    var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
    return Results.Ok(new LoginResponseDto(tokenString, expiration));
})
.WithTags("Authentication")
.WithName("Login")
.Produces<LoginResponseDto>(StatusCodes.Status200OK)
.Produces(StatusCodes.Status401Unauthorized);

app.Run();
