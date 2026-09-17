using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using System.Security.Claims;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsuariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsuariosController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var negocioId) ? negocioId : null;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerUsuarios()
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var usuarios = await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.NegocioId == negocioId.Value)
                .OrderBy(u => u.Nombre)
                .Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Email,
                    u.Rol,
                    u.Activo,
                    u.FechaCreacion
                })
                .ToListAsync();

            return Ok(usuarios);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == id && u.NegocioId == negocioId.Value)
                .Select(u => new
                {
                    u.Id,
                    u.Nombre,
                    u.Email,
                    u.Rol,
                    u.Activo,
                    u.FechaCreacion
                })
                .FirstOrDefaultAsync();

            if (usuario == null) return NotFound("Usuario no encontrado.");
            return Ok(usuario);
        }

        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> CambiarEstadoUsuario(int id, [FromQuery] bool activo)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var usuarioIdActualTexto = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var usuarioIdActual = int.TryParse(usuarioIdActualTexto, out var actualId) ? actualId : 0;

            if (id == usuarioIdActual && !activo)
                return BadRequest("No puede desactivar su propio usuario.");

            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id && u.NegocioId == negocioId.Value);

            if (usuario == null) return NotFound("Usuario no encontrado.");

            usuario.Activo = activo;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = activo ? "Usuario activado correctamente." : "Usuario desactivado correctamente.",
                usuario = new
                {
                    usuario.Id,
                    usuario.Nombre,
                    usuario.Email,
                    usuario.Rol,
                    usuario.Activo
                }
            });
        }
    }
}