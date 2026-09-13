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

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TodoDatabase")));

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
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseAuthentication();
app.UseAuthorization();

var todoGroup = app.MapGroup("/api/todos").WithTags("Todos");

#region In-memory Endpoint

// var todos = new List<TodoGetDto>
// {
//     new TodoGetDto(1, "Learn C#", true),
//     new TodoGetDto(2, "Learn ASP.NET Core", false),
//     new TodoGetDto(3, "Build a web API", false)
// };

// todoGroup.MapGet("/", () => Results.Ok(todos));

// todoGroup.MapGet("/{id}", (int id) =>
// {
//     var todo = todos.FirstOrDefault(t => t.Id == id);

//     return todo is not null ? Results.Ok(todo) : Results.NotFound();

// });

//     todoGroup.MapPost("/", (TodoPostDto dto) =>
//     {
//         var nextId = todos.Count == 0 ? 1 : todos.Max(t => t.Id) + 1;

//         var todo = new TodoGetDto(nextId, dto.Title, false);
//         todos.Add(todo);

//         return Results.Created($"/api/todos/{todo.Id}", todo);
//     });

// todoGroup.MapPut("/{id}", (int id, TodoPutDto dto) =>
// {
//     try
//     {
//         var index = todos.FindIndex(t => t.Id == id);
//         if (index == -1) return Results.NotFound();

//         todos[index] = todos[index] with
//         {
//             Title = dto.Title,
//             IsCompleted = dto.IsCompleted 
//         };

//         return Results.Ok(todos[index]);
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem(ex.Message);
//     }
// });

// todoGroup.MapDelete("/{id}", (int id) =>
// {
//     try
//     {
//         var todo = todos.FirstOrDefault(t => t.Id == id);
//         if (todo is null) return Results.NotFound();

//         todos.Remove(todo);
//         return Results.NoContent();
//     }
//     catch(ArgumentNullException ex)
//     {
//         return Results.Problem($"Parameter is null: {ex.ParamName}");
//     }
//     catch (Exception ex)
//     {
//         return Results.Problem(ex.Message);
//     }

// });

#endregion

#region Database Endpoint

todoGroup.MapGet("/", async (AppDbContext db) =>
{
    var todos = await db.Todos.ToListAsync();

    var todoGetDtos = todos.Select(t =>
        new TodoGetDto(
            t.Id,
            t.Title,
            t.IsCompleted));

    return todoGetDtos.Count() == 0
        ? Results.NotFound()
        : Results.Ok(todoGetDtos);
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
        .OrderByDescending(t => t.Id)
        .FirstOrDefaultAsync();
    var nextId = lastTodo is null ? 1 : lastTodo.Id + 1;

    var todo = new TodoItem
    {
        Id = nextId,
        Title = dto.Title.Trim(),
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    var todoGetDto = new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted);

    return Results.Created($"/api/todos/{todo.Id}", todoGetDto);

})
.RequireAuthorization();

#endregion
#region  Authentication Endpoints

app.MapPost("/api/login", (
    LoginDto login,
    IConfiguration configuration) =>
{
    if (login.Username != "student" || login.Password != "password")
        return Results.Unauthorized();

    var claims = new[]
    {
        new Claim(ClaimTypes.Name, login.Username)
    };

    var key = new SymmetricSecurityKey(
        Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));

    var credentials = new SigningCredentials(
        key,
        SecurityAlgorithms.HmacSha256);

    var expireDays = configuration.GetValue<double>("Jwt:ExpireDays");
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

#endregion

app.Run();
