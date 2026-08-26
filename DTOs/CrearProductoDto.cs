using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class CrearProductoDto
    {
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

        public int StockMinimo { get; set; }

        public string? ImageUrl { get; set; }

        public int CategoriaId { get; set; }
    }
}