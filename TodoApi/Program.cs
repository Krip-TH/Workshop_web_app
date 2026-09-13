using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Dtos;
using TodoApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();
builder.Services.AddDbContext<TodoDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("TodoDatabase")));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<TodoDbContext>();
    db.Database.EnsureCreated();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

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

todoGroup.MapGet("/", async (TodoDbContext db) =>
{
    var todos = await db.Todos
        .AsNoTracking()
        .OrderBy(todo => todo.Id)
        .Select(todo => new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted))
        .ToListAsync();

    return Results.Ok(todos);
});

todoGroup.MapPost("/", async (TodoPostDto dto, TodoDbContext db) =>
{
    if (string.IsNullOrWhiteSpace(dto.Title))
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(dto.Title)] = ["Title is required."]
        });
    }

    var todo = new TodoItem
    {
        Title = dto.Title.Trim(),
        IsCompleted = false,
        CreatedAt = DateTime.UtcNow
    };

    db.Todos.Add(todo);
    await db.SaveChangesAsync();

    var todoGetDto = new TodoGetDto(todo.Id, todo.Title, todo.IsCompleted);

    return Results.Created($"/api/todos/{todo.Id}", todoGetDto);

});

#endregion

app.Run();
