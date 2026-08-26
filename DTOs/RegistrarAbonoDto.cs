namespace RawSuplementos.Api.DTOs
{
    public class RegistrarAbonoDto
    {
        public decimal Monto { get; set; }

        public string MetodoPago { get; set; } = "Efectivo";

        public string? Referencia { get; set; }

        public string? Notas { get; set; }
    }
}