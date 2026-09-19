using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.Models
{
    public class Negocio
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
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

        [MaxLength(180)]
        public string? TituloLanding { get; set; }

        [MaxLength(500)]
        public string? DescripcionLanding { get; set; }

        public bool Activo { get; set; } = true;

        public bool Bloqueado { get; set; } = false;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public ICollection<Usuario> Usuarios { get; set; }
            = new List<Usuario>();

        public ICollection<Categoria> Categorias { get; set; }
            = new List<Categoria>();
    }
}