namespace TodoApi.Dtos;

public record TodoGetDto(
    int Id,
    string Title,
    bool IsCompleted
);


   