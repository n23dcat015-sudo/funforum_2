using System.ComponentModel.DataAnnotations;

namespace Funforum.Api.Dtos.Auth;

// Phase D — ID token (JWT) do Google Identity Services trả về ở frontend.
public class GoogleLoginRequest
{
    [Required]
    public string IdToken { get; set; } = string.Empty;
}
