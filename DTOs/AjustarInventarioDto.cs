namespace RawSuplementos.Api.DTOs
{
    public class AjustarInventarioDto
    {
        public int Cantidad { get; set; }

        public string Tipo { get; set; } = "Ajuste";

        public string? Motivo { get; set; }
    }
}