namespace Funforum.Api.Dtos.Votes;

public class VoteRequest
{
    public short Value { get; set; } // -1, 0 (bỏ vote), hoặc 1
}