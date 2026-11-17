namespace Funforum.Api.Dtos.Votes;

public class VoteResultDto
{
    public int Score { get; set; }
    public short? MyVote { get; set; } // null nếu hiện không vote
}