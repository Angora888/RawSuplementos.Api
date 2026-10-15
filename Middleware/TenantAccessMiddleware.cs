using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;

namespace RawSuplementos.Api.Middleware
{
    /// <summary>
    /// Enforces tenant and account status on authenticated API requests.
    /// Super administrators and explicitly anonymous endpoints keep their own policies.
    /// </summary>
    public sealed class TenantAccessMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantAccessMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            var user = context.User;
            var endpoint = context.GetEndpoint();

            if (endpoint?.Metadata.GetMetadata<Microsoft.AspNetCore.Authorization.IAllowAnonymous>() != null
                || user.Identity?.IsAuthenticated != true
                || user.IsInRole("SuperAdmin"))
            {
                await _next(context);
                return;
            }

            var negocioClaim = user.FindFirst("negocioId")?.Value;
            var usuarioClaim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!int.TryParse(negocioClaim, out var negocioId)
                || !int.TryParse(usuarioClaim, out var usuarioId))
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { mensaje = "La sesión no contiene un usuario y negocio válidos." });
                return;
            }

            var estado = await db.Usuarios.AsNoTracking()
                .Where(u => u.Id == usuarioId && u.NegocioId == negocioId)
                .Select(u => new
                {
                    UsuarioActivo = u.Activo,
                    NegocioActivo = u.Negocio.Activo,
                    NegocioBloqueado = u.Negocio.Bloqueado
                })
                .FirstOrDefaultAsync();

            if (estado == null || !estado.UsuarioActivo || !estado.NegocioActivo)
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { mensaje = "El usuario o el negocio están inactivos." });
                return;
            }

            var esConsultaSesion = string.Equals(
                context.Request.Path.Value?.TrimEnd('/'),
                "/api/auth/sesion",
                StringComparison.OrdinalIgnoreCase);

            if (estado.NegocioBloqueado && !esConsultaSesion)
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new { mensaje = "El negocio está bloqueado. Contacta al administrador para recuperar el acceso." });
                return;
            }

            await _next(context);
        }
    }
}
