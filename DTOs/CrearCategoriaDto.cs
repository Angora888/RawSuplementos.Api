using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class CrearCategoriaDto
    {
        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;
    }
}