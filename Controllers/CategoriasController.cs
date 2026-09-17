using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Models;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class CategoriasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public CategoriasController(ApplicationDbContext context) => _context = context;

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCategorias()
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized();

            var categorias = await _context.Categorias.AsNoTracking()
                .Where(c => c.NegocioId == negocioId.Value)
                .OrderBy(c => c.Nombre).ToListAsync();
            return Ok(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> CrearCategoria(CrearCategoriaDto dto)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized();
            var nombre = dto.Nombre.Trim();
            var existe = await _context.Categorias.AnyAsync(c => c.NegocioId == negocioId.Value && c.Nombre.ToLower() == nombre.ToLower());
            if (existe) return BadRequest("Ya existe una categoría con ese nombre.");

            var categoria = new Categoria { NegocioId = negocioId.Value, Nombre = nombre, Activa = true };
            _context.Categorias.Add(categoria);
            await _context.SaveChangesAsync();
            return Ok(categoria);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarCategoria(int id, CrearCategoriaDto dto)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized();
            var categoria = await _context.Categorias.FirstOrDefaultAsync(c => c.Id == id && c.NegocioId == negocioId.Value);
            if (categoria == null) return NotFound("Categoría no encontrada.");

            var nombre = dto.Nombre.Trim();
            var duplicada = await _context.Categorias.AnyAsync(c => c.NegocioId == negocioId.Value && c.Id != id && c.Nombre.ToLower() == nombre.ToLower());
            if (duplicada) return BadRequest("Ya existe una categoría con ese nombre.");

            categoria.Nombre = nombre;
            await _context.SaveChangesAsync();
            return Ok(categoria);
        }
    }
}