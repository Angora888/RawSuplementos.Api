using System.Security.Claims;

namespace RawSuplementos.Api.Helpers
{
    public static class ClaimsPrincipalExtensions
    {
        public static int? ObtenerNegocioId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        public static int? ObtenerUsuarioId(this ClaimsPrincipal user)
        {
            var claim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(claim, out var id) ? id : null;
        }
    }
}
