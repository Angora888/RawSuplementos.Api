using System.ComponentModel.DataAnnotations;

namespace RawSuplementos.Api.DTOs
{
    public class EditarLandingDto
    {
        [MaxLength(180)]
        public string? TituloLanding { get; set; }

        [MaxLength(500)]
        public string? DescripcionLanding { get; set; }

        [MaxLength(500)]
        public string? LogoUrl { get; set; }

        [MaxLength(20)]
        public string? WhatsApp { get; set; }

        [MaxLength(20)]
        public string? ColorPrimario { get; set; }

        [MaxLength(20)]
        public string? ColorSecundario { get; set; }

        [MaxLength(20)] public string? ColorFondo { get; set; }
        [MaxLength(20)] public string? ColorHeader { get; set; }
        [MaxLength(20)] public string? ColorFooter { get; set; }
        [MaxLength(20)] public string? ColorBoton { get; set; }
        [MaxLength(20)] public string? ColorTexto { get; set; }
        [MaxLength(500)] public string? HeroFondoUrl { get; set; }
        [MaxLength(250)] public string? TextoFooter { get; set; }
    }
}