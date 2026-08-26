using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class CrearVentaDto
    {
        [Required]
        public int ClienteId { get; set; }

        public decimal Descuento { get; set; }

        public decimal PagoInicial { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";

        public string? ReferenciaPago { get; set; }

        public DateTime? FechaVencimiento { get; set; }

        public string? Notas { get; set; }

        [Required]
        [MinLength(1)]
        public List<CrearVentaDetalleDto> Productos { get; set; }
            = new List<CrearVentaDetalleDto>();
    }
}