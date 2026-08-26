using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class UsuariosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public UsuariosController(
            ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: api/usuarios
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerUsuarios()
        {
            var usuarios = await _context.Usuarios
                .AsNoTracking()
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

        // ==========================================
        // GET: api/usuarios/5
        // ==========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerUsuario(int id)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Where(u => u.Id == id)
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

            if (usuario == null)
            {
                return NotFound(
                    "Usuario no encontrado."
                );
            }

            return Ok(usuario);
        }

        // ==========================================
        // PUT: api/usuarios/5/estado
        // ==========================================

        [HttpPut("{id:int}/estado")]
        public async Task<IActionResult> CambiarEstadoUsuario(
            int id,
            [FromQuery] bool activo)
        {
            var usuario = await _context.Usuarios
                .FirstOrDefaultAsync(u => u.Id == id);

            if (usuario == null)
            {
                return NotFound(
                    "Usuario no encontrado."
                );
            }

            usuario.Activo = activo;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = activo
                    ? "Usuario activado correctamente."
                    : "Usuario desactivado correctamente.",

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