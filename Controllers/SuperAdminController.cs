using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Models;
using System.Text.RegularExpressions;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "SuperAdmin")]
    public class SuperAdminController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public SuperAdminController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet("negocios")]
        public async Task<IActionResult> ObtenerNegocios()
        {
            var negocios = await _context.Negocios
                .AsNoTracking()
                .OrderBy(n => n.Nombre)
                .Select(n => new
                {
                    n.Id,
                    n.Nombre,
                    n.Slug,
                    n.WhatsApp,
                    n.Telefono,
                    n.Direccion,
                    n.LogoUrl,
                    n.ColorPrimario,
                    n.ColorSecundario,
                    n.Activo,
                    n.Bloqueado,
                    n.FechaCreacion,
                    Usuarios = n.Usuarios.Count(),
                    Productos = _context.Productos.Count(p => p.NegocioId == n.Id),
                    Clientes = _context.Clientes.Count(c => c.NegocioId == n.Id),
                    Ventas = _context.Ventas.Count(v => v.NegocioId == n.Id)
                })
                .ToListAsync();

            return Ok(negocios);
        }

        [HttpPost("negocios")]
        public async Task<IActionResult> CrearNegocio(CrearNegocioDto dto)
        {
            var nombre = dto.Nombre.Trim();
            var slug = NormalizarSlug(dto.Slug);
            var adminEmail = dto.AdminEmail.Trim().ToLowerInvariant();

            if (string.IsNullOrWhiteSpace(nombre)) return BadRequest("El nombre del negocio es obligatorio.");
            if (string.IsNullOrWhiteSpace(slug)) return BadRequest("El slug no es válido.");
            if (string.IsNullOrWhiteSpace(dto.AdminNombre)) return BadRequest("El nombre del administrador es obligatorio.");

            if (await _context.Negocios.AnyAsync(n => n.Slug == slug))
                return BadRequest("Ya existe un negocio con ese slug.");

            if (await _context.Usuarios.AnyAsync(u => u.Email == adminEmail))
                return BadRequest("Ya existe un usuario con ese correo.");

            await using var transaction = await _context.Database.BeginTransactionAsync();

            var negocio = new Negocio
            {
                Nombre = nombre,
                Slug = slug,
                WhatsApp = Limpiar(dto.WhatsApp),
                Telefono = Limpiar(dto.Telefono),
                Direccion = Limpiar(dto.Direccion),
                LogoUrl = Limpiar(dto.LogoUrl),
                ColorPrimario = Limpiar(dto.ColorPrimario),
                ColorSecundario = Limpiar(dto.ColorSecundario),
                Activo = true,
                FechaCreacion = FechaHelper.AhoraUtc()
            };

            _context.Negocios.Add(negocio);
            await _context.SaveChangesAsync();

            var admin = new Usuario
            {
                NegocioId = negocio.Id,
                Nombre = dto.AdminNombre.Trim(),
                Email = adminEmail,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.AdminPassword),
                Rol = "Admin",
                Activo = true,
                FechaCreacion = FechaHelper.AhoraUtc()
            };

            _context.Usuarios.Add(admin);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                mensaje = "Negocio creado y activado correctamente.",
                negocio = new
                {
                    negocio.Id,
                    negocio.Nombre,
                    negocio.Slug,
                    negocio.Activo,
                    negocio.Bloqueado,
                    negocio.WhatsApp,
                    negocio.LogoUrl,
                    catalogo = $"/catalogo/{negocio.Slug}"
                },
                administrador = new
                {
                    admin.Id,
                    admin.Nombre,
                    admin.Email,
                    admin.Rol
                }
            });
        }

        [HttpPut("negocios/{id:int}/estado")]
        public async Task<IActionResult> CambiarEstado(int id, [FromBody] CambiarEstadoNegocioDto dto)
        {
            var negocio = await _context.Negocios.FirstOrDefaultAsync(n => n.Id == id);
            if (negocio == null) return NotFound("Negocio no encontrado.");

            negocio.Activo = dto.Activo;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = negocio.Activo ? "Negocio activado correctamente." : "Negocio desactivado correctamente.",
                negocio.Id,
                negocio.Nombre,
                negocio.Slug,
                negocio.Activo
            });
        }


        [HttpPut("negocios/{id:int}/bloqueo")]
        public async Task<IActionResult> CambiarBloqueo(int id, [FromBody] CambiarBloqueoNegocioDto dto)
        {
            var negocio = await _context.Negocios.FirstOrDefaultAsync(n => n.Id == id);
            if (negocio == null) return NotFound("Negocio no encontrado.");

            negocio.Bloqueado = dto.Bloqueado;
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = negocio.Bloqueado ? "Negocio bloqueado por pago pendiente." : "Bloqueo del negocio removido.",
                negocio.Id,
                negocio.Nombre,
                negocio.Bloqueado
            });
        }

        private static string NormalizarSlug(string valor)
        {
            var slug = (valor ?? string.Empty).Trim().ToLowerInvariant();
            slug = Regex.Replace(slug, @"\s+", "-");
            slug = Regex.Replace(slug, @"[^a-z0-9-]", string.Empty);
            slug = Regex.Replace(slug, @"-+", "-").Trim('-');
            return slug;
        }

        private static string? Limpiar(string? valor)
        {
            return string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
        }
    }

    public class CambiarBloqueoNegocioDto
    {
        public bool Bloqueado { get; set; }
    }

    public class CambiarEstadoNegocioDto
    {
        public bool Activo { get; set; }
    }
}
