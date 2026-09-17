using RawSuplementos.Api.Helpers;
using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.Models
{
    public class Cliente
    {
        public int Id { get; set; }

        public int NegocioId { get; set; }
        public Negocio Negocio { get; set; } = null!;

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

        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = FechaHelper.AhoraCostaRica();

        public ICollection<Venta> Ventas { get; set; } = new List<Venta>();
        public ICollection<MovimientoCuenta> MovimientosCuenta { get; set; } = new List<MovimientoCuenta>();
    }
}