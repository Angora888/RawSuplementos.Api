using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class RegistrarUsuarioDto
    {
        [Required]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MinLength(6)]
        public string Password { get; set; } = string.Empty;

        public string Rol { get; set; } = "Vendedor";
    }
}