using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Models
{
    public class MovimientoCuenta
    {
        public int Id { get; set; }

        public int ClienteId { get; set; }

        public Cliente Cliente { get; set; } = null!;

        public int? VentaId { get; set; }

        public Venta? Venta { get; set; }

        public int UsuarioId { get; set; }

        public Usuario Usuario { get; set; } = null!;

        public string Tipo { get; set; } = string.Empty;

        public decimal Monto { get; set; }

        public DateTime Fecha { get; set; } = FechaHelper.AhoraCostaRica();

        public string? Descripcion { get; set; }
    }
}