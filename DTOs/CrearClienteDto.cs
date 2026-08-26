using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class CrearClienteDto
    {
        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(20)]
        public string Telefono { get; set; } = string.Empty;

        [MaxLength(250)]
        public string? Direccion { get; set; }

        [MaxLength(500)]
        public string? Notas { get; set; }
    }
}