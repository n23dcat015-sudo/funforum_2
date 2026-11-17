namespace Funforum.Api.Dtos.Threads;

public class UpdateThreadRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
}