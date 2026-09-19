using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Models;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class InventarioController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public InventarioController(ApplicationDbContext context)
        {
            _context = context;
        }


        [HttpPost("ajustar/{productoId:int}")]
        public async Task<IActionResult> AjustarInventario(int productoId, AjustarInventarioDto dto)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var usuarioId = User.ObtenerUsuarioId();
            if (usuarioId == null) return Unauthorized("No se pudo identificar al usuario.");

            var usuarioValido = await _context.Usuarios.AsNoTracking()
                .AnyAsync(u => u.Id == usuarioId.Value && u.NegocioId == negocioId.Value && u.Activo);
            if (!usuarioValido) return Unauthorized("El usuario no pertenece al negocio o está desactivado.");

            if (dto.Cantidad == 0) return BadRequest("La cantidad no puede ser cero.");

            var tiposPermitidos = new[] { "Entrada", "Ajuste", "Devolucion", "Perdida", "Correccion" };
            if (!tiposPermitidos.Contains(dto.Tipo)) return BadRequest("Tipo de movimiento inválido.");

            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == productoId && p.NegocioId == negocioId.Value && p.Activo);
            if (producto == null) return NotFound("Producto no encontrado.");

            var stockAnterior = producto.Stock;
            var stockNuevo = stockAnterior + dto.Cantidad;
            if (stockNuevo < 0)
                return BadRequest($"El ajuste dejaría el stock en negativo. Stock actual: {stockAnterior}.");

            producto.Stock = stockNuevo;

            var movimiento = new MovimientoInventario
            {
                ProductoId = producto.Id,
                UsuarioId = usuarioId.Value,
                Tipo = dto.Tipo,
                Cantidad = dto.Cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = stockNuevo,
                Fecha = FechaHelper.AhoraUtc(),
                Motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? null : dto.Motivo.Trim()
            };

            _context.MovimientosInventario.Add(movimiento);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Inventario actualizado correctamente.",
                producto = new { producto.Id, producto.Nombre, stockAnterior, stockNuevo },
                movimiento = new { movimiento.Tipo, movimiento.Cantidad, movimiento.Fecha, movimiento.Motivo }
            });
        }

        [HttpGet("producto/{productoId:int}")]
        public async Task<IActionResult> ObtenerMovimientosProducto(int productoId)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var producto = await _context.Productos.AsNoTracking()
                .FirstOrDefaultAsync(p => p.Id == productoId && p.NegocioId == negocioId.Value);
            if (producto == null) return NotFound("Producto no encontrado.");

            var movimientos = await _context.MovimientosInventario.AsNoTracking()
                .Where(m => m.ProductoId == productoId && m.Producto.NegocioId == negocioId.Value)
                .OrderByDescending(m => m.Fecha)
                .Select(m => new
                {
                    m.Id, m.Tipo, m.Cantidad, m.StockAnterior, m.StockNuevo,
                    m.Fecha, m.Motivo, m.VentaId,
                    Usuario = m.Usuario.Nombre
                })
                .ToListAsync();

            return Ok(new
            {
                producto = new { producto.Id, producto.Nombre, producto.Stock },
                movimientos
            });
        }
    }
}