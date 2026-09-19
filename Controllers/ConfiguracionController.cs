using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class ConfiguracionController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ConfiguracionController(ApplicationDbContext context) => _context = context;

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        [HttpGet("landing")]
        public async Task<IActionResult> ObtenerLanding()
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized();

            var negocio = await _context.Negocios.AsNoTracking()
                .Where(n => n.Id == negocioId.Value)
                .Select(n => new
                {
                    n.Nombre,
                    n.Slug,
                    n.TituloLanding,
                    n.DescripcionLanding,
                    n.LogoUrl,
                    n.WhatsApp,
                    n.ColorPrimario,
                    n.ColorSecundario,
                    n.ColorFondo, n.ColorHeader, n.ColorFooter, n.ColorBoton, n.ColorTexto, n.HeroFondoUrl, n.TextoFooter
                })
                .FirstOrDefaultAsync();

            return negocio == null ? NotFound("Negocio no encontrado.") : Ok(negocio);
        }

        [HttpPut("landing")]
        public async Task<IActionResult> EditarLanding(EditarLandingDto dto)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized();

            var negocio = await _context.Negocios.FirstOrDefaultAsync(n => n.Id == negocioId.Value);
            if (negocio == null) return NotFound("Negocio no encontrado.");

            negocio.TituloLanding = Limpiar(dto.TituloLanding);
            negocio.DescripcionLanding = Limpiar(dto.DescripcionLanding);
            negocio.LogoUrl = Limpiar(dto.LogoUrl);
            negocio.WhatsApp = Limpiar(dto.WhatsApp);
            negocio.ColorPrimario = NormalizarColor(dto.ColorPrimario, "#5267df");
            negocio.ColorSecundario = NormalizarColor(dto.ColorSecundario, "#172033");
            negocio.ColorFondo = NormalizarColor(dto.ColorFondo, "#f7f9fc");
            negocio.ColorHeader = NormalizarColor(dto.ColorHeader, "#ffffff");
            negocio.ColorFooter = NormalizarColor(dto.ColorFooter, "#f7f9fc");
            negocio.ColorBoton = NormalizarColor(dto.ColorBoton, negocio.ColorPrimario ?? "#5267df");
            negocio.ColorTexto = NormalizarColor(dto.ColorTexto, "#172033");
            negocio.HeroFondoUrl = Limpiar(dto.HeroFondoUrl);
            negocio.TextoFooter = Limpiar(dto.TextoFooter);

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Página pública actualizada correctamente.",
                negocio = new
                {
                    negocio.Nombre,
                    negocio.Slug,
                    negocio.TituloLanding,
                    negocio.DescripcionLanding,
                    negocio.LogoUrl,
                    negocio.WhatsApp,
                    negocio.ColorPrimario,
                    negocio.ColorSecundario,
                    negocio.ColorFondo, negocio.ColorHeader, negocio.ColorFooter, negocio.ColorBoton, negocio.ColorTexto, negocio.HeroFondoUrl, negocio.TextoFooter
                }
            });
        }

        private static string? Limpiar(string? valor) =>
            string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

        private static string NormalizarColor(string? valor, string predeterminado)
        {
            if (string.IsNullOrWhiteSpace(valor)) return predeterminado;
            var color = valor.Trim();
            return System.Text.RegularExpressions.Regex.IsMatch(color, "^#[0-9a-fA-F]{6}$") ? color : predeterminado;
        }
    }
}