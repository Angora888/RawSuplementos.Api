using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class CrearNegocioDto
    {
        [Required, MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required, MaxLength(120)]
        public string Slug { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? WhatsApp { get; set; }

        [MaxLength(20)]
        public string? Telefono { get; set; }

        [MaxLength(250)]
        public string? Direccion { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(20)]
        public string? ColorPrimario { get; set; }

        [MaxLength(20)]
        public string? ColorSecundario { get; set; }

        [Required, MaxLength(150)]
        public string AdminNombre { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(200)]
        public string AdminEmail { get; set; } = string.Empty;

        [Required, MinLength(8)]
        public string AdminPassword { get; set; } = string.Empty;
    }
}
