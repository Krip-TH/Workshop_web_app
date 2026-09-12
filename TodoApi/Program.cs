using TodoApi.Dtos;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

var todos = new List<TodoGetDto>
{
    new TodoGetDto(1, "Learn C#", true),
    new TodoGetDto(2, "Learn ASP.NET Core", false),
    new TodoGetDto(3, "Build a web API", false)
};  

app.MapGet("/api/todos", () => Results.Ok(todos));


app.Run();

