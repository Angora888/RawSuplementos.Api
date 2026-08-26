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

        public CategoriasController(ApplicationDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCategorias()
        {
            var categorias = await _context.Categorias
                .AsNoTracking()
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            return Ok(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> CrearCategoria(
            CrearCategoriaDto dto)
        {
            var nombre = dto.Nombre.Trim();

            var existe = await _context.Categorias
                .AnyAsync(c => c.Nombre.ToLower() == nombre.ToLower());

            if (existe)
            {
                return BadRequest(
                    "Ya existe una categoría con ese nombre."
                );
            }

            var categoria = new Categoria
            {
                Nombre = nombre,
                Activa = true
            };

            _context.Categorias.Add(categoria);

            await _context.SaveChangesAsync();

            return Ok(categoria);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarCategoria(
            int id,
            CrearCategoriaDto dto)
        {
            var categoria = await _context.Categorias
                .FirstOrDefaultAsync(c => c.Id == id);

            if (categoria == null)
            {
                return NotFound("Categoría no encontrada.");
            }

            categoria.Nombre = dto.Nombre.Trim();

            await _context.SaveChangesAsync();

            return Ok(categoria);
        }
    }
}