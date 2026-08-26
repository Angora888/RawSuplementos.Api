using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Models
{
    public class Pago
    {
        public int Id { get; set; }

        public int VentaId { get; set; }

        public Venta Venta { get; set; } = null!;

        public decimal Monto { get; set; }

        public int UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        public DateTime Fecha { get; set; } = FechaHelper.AhoraCostaRica();

        public string MetodoPago { get; set; } = "Efectivo";

        public string? Referencia { get; set; }

        public string? Notas { get; set; }
    }
}