namespace TodoApi.Models;

public class Todoitem
{
    public int Id { get; set; }
    public string Title { get; set; }

    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }

}
