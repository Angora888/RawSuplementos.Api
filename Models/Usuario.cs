using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.Models
{
    public class Usuario
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(150)]
        public string Email { get; set; } = string.Empty;

        [Required]
        public string PasswordHash { get; set; } = string.Empty;

        [Required]
        [MaxLength(30)]
        public string Rol { get; set; } = "Vendedor";

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public ICollection<Venta> Ventas { get; set; }
            = new List<Venta>();

        public ICollection<Pago> PagosRegistrados { get; set; }
            = new List<Pago>();

        public ICollection<MovimientoCuenta> MovimientosCuenta { get; set; }
            = new List<MovimientoCuenta>();

        public ICollection<MovimientoInventario> MovimientosInventario { get; set; }
            = new List<MovimientoInventario>();
    }
}