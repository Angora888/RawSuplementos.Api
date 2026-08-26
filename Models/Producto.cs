using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.Models
{
    public class Producto
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(100)]
        public string? Marca { get; set; }

        [MaxLength(100)]
        public string? Presentacion { get; set; }

        [MaxLength(100)]
        public string? Sabor { get; set; }

        public decimal PrecioCompra { get; set; }

        public decimal PrecioVenta { get; set; }

        public int Stock { get; set; }

        public int StockMinimo { get; set; }

        public string? ImageUrl { get; set; }

        public bool Activo { get; set; } = true;

        public int CategoriaId { get; set; }

        public Categoria Categoria { get; set; } = null!;

        public ICollection<VentaDetalle> VentaDetalles { get; set; }
            = new List<VentaDetalle>();

        public ICollection<MovimientoInventario> MovimientosInventario { get; set; }
    = new List<MovimientoInventario>();
    }
}