using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Models
{
    public class Venta
    {
        public int Id { get; set; }

        public int ClienteId { get; set; }

        public Cliente Cliente { get; set; } = null!;

        public int UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        public DateTime Fecha { get; set; } = FechaHelper.AhoraCostaRica();

        public DateTime? FechaVencimiento { get; set; }

        public decimal Subtotal { get; set; }

        public decimal Descuento { get; set; }

        public decimal Total { get; set; }

        public string Estado { get; set; } = "Pendiente";

        public string? Notas { get; set; }

        public ICollection<VentaDetalle> Detalles { get; set; }
            = new List<VentaDetalle>();

        public ICollection<Pago> Pagos { get; set; }
            = new List<Pago>();

        public ICollection<MovimientoInventario> MovimientosInventario { get; set; }
    = new List<MovimientoInventario>();
    }
}