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
    }
}