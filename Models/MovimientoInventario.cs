namespace RawSuplementos.Api.Models
{
    public class MovimientoInventario
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }
        public Producto Producto { get; set; } = null!;

        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        public int? VentaId { get; set; }
        public Venta? Venta { get; set; }

        public string Tipo { get; set; } = string.Empty;

        public int Cantidad { get; set; }

        public int StockAnterior { get; set; }

        public int StockNuevo { get; set; }

        public DateTime Fecha { get; set; }

        public string? Motivo { get; set; }
    }
}