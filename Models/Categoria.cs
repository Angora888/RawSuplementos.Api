using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.Models
{
    public class Categoria
    {
        public int Id { get; set; }

        public int NegocioId { get; set; }
        public Negocio Negocio { get; set; } = null!;

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        public bool Activa { get; set; } = true;

        public ICollection<Producto> Productos { get; set; }
            = new List<Producto>();
    }
}