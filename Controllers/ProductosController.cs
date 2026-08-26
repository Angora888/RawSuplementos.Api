using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ProductosController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: api/productos
        [HttpGet]
        public async Task<IActionResult> ObtenerProductos()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Marca,
                    p.Presentacion,
                    p.Sabor,
                    p.PrecioCompra,
                    p.PrecioVenta,
                    p.Stock,
                    p.StockMinimo,
                    p.ImageUrl,
                    p.Activo,

                    p.CategoriaId,
                    Categoria = p.Categoria.Nombre,

                    StockBajo = p.Stock <= p.StockMinimo
                })
                .ToListAsync();

            return Ok(productos);
        }

        // ==========================================
        // GET: api/productos/catalogo
        // PÚBLICO
        // ==========================================

        [AllowAnonymous]
        [HttpGet("catalogo")]
        public async Task<IActionResult> ObtenerCatalogoPublico()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p =>
                    p.Activo &&
                    p.Stock > 0
                )
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Marca,
                    p.Presentacion,
                    p.Sabor,
                    p.PrecioVenta,
                    p.ImageUrl,

                    Categoria = p.Categoria.Nombre,

                    Disponible = p.Stock > 0,

                    Stock = p.Stock
                })
                .ToListAsync();

            return Ok(productos);
        }

        // GET: api/productos/5
        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            var producto = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p => p.Id == id)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Marca,
                    p.Presentacion,
                    p.Sabor,
                    p.PrecioCompra,
                    p.PrecioVenta,
                    p.Stock,
                    p.StockMinimo,
                    p.ImageUrl,
                    p.Activo,

                    p.CategoriaId,
                    Categoria = p.Categoria.Nombre,

                    StockBajo = p.Stock <= p.StockMinimo
                })
                .FirstOrDefaultAsync();

            if (producto == null)
            {
                return NotFound("Producto no encontrado.");
            }

            return Ok(producto);
        }

        // GET: api/productos/buscar?texto=creatina
        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarProducto(
            [FromQuery] string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return BadRequest(
                    "Debe ingresar un texto para buscar."
                );
            }

            texto = texto.Trim().ToLower();

            var productos = await _context.Productos
                .AsNoTracking()
                .Include(p => p.Categoria)
                .Where(p =>
                    p.Nombre.ToLower().Contains(texto) ||
                    (p.Marca != null &&
                     p.Marca.ToLower().Contains(texto))
                )
                .OrderBy(p => p.Nombre)
                .Take(30)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Marca,
                    p.Presentacion,
                    p.Sabor,
                    p.PrecioVenta,
                    p.Stock,
                    Categoria = p.Categoria.Nombre
                })
                .ToListAsync();

            return Ok(productos);
        }

        // POST: api/productos
        [HttpPost]
        public async Task<IActionResult> CrearProducto(
            CrearProductoDto dto)
        {
            if (dto.PrecioCompra < 0 ||
                dto.PrecioVenta < 0)
            {
                return BadRequest(
                    "Los precios no pueden ser negativos."
                );
            }

            if (dto.StockMinimo < 0)
            {
                return BadRequest(
                    "El stock mínimo no puede ser negativo."
                );
            }

            var categoriaExiste = await _context.Categorias
                .AnyAsync(c =>
                    c.Id == dto.CategoriaId &&
                    c.Activa
                );

            if (!categoriaExiste)
            {
                return BadRequest(
                    "La categoría indicada no existe o está inactiva."
                );
            }

            var producto = new Producto
            {
                Nombre = dto.Nombre.Trim(),

                Marca = string.IsNullOrWhiteSpace(dto.Marca)
                    ? null
                    : dto.Marca.Trim(),

                Presentacion =
                    string.IsNullOrWhiteSpace(dto.Presentacion)
                        ? null
                        : dto.Presentacion.Trim(),

                Sabor = string.IsNullOrWhiteSpace(dto.Sabor)
                    ? null
                    : dto.Sabor.Trim(),

                PrecioCompra = dto.PrecioCompra,
                PrecioVenta = dto.PrecioVenta,

                Stock = 0,
                StockMinimo = dto.StockMinimo,

                ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl)
                    ? null
                    : dto.ImageUrl.Trim(),

                CategoriaId = dto.CategoriaId,
                Activo = true
            };

            _context.Productos.Add(producto);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerProducto),
                new { id = producto.Id },
                producto
            );
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarProducto(
            int id,
            EditarProductoDto dto)
        {
            var producto = await _context.Productos
                .FirstOrDefaultAsync(p => p.Id == id);

            if (producto == null)
            {
                return NotFound(
                    "Producto no encontrado."
                );
            }

            if (dto.PrecioCompra < 0 ||
                dto.PrecioVenta < 0 ||
                dto.StockMinimo < 0)
            {
                return BadRequest(
                    "Los precios y el stock mínimo no pueden ser negativos."
                );
            }

            var categoriaExiste = await _context.Categorias
                .AnyAsync(c =>
                    c.Id == dto.CategoriaId &&
                    c.Activa
                );

            if (!categoriaExiste)
            {
                return BadRequest(
                    "La categoría indicada no existe o está inactiva."
                );
            }

            producto.Nombre = dto.Nombre.Trim();

            producto.Marca =
                string.IsNullOrWhiteSpace(dto.Marca)
                    ? null
                    : dto.Marca.Trim();

            producto.Presentacion =
                string.IsNullOrWhiteSpace(dto.Presentacion)
                    ? null
                    : dto.Presentacion.Trim();

            producto.Sabor =
                string.IsNullOrWhiteSpace(dto.Sabor)
                    ? null
                    : dto.Sabor.Trim();

            producto.PrecioCompra = dto.PrecioCompra;
            producto.PrecioVenta = dto.PrecioVenta;

            producto.StockMinimo = dto.StockMinimo;

            producto.ImageUrl =
                string.IsNullOrWhiteSpace(dto.ImageUrl)
                    ? null
                    : dto.ImageUrl.Trim();

            producto.CategoriaId =
                dto.CategoriaId;

            producto.Activo =
                dto.Activo;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje =
                    "Producto actualizado correctamente.",

                producto = new
                {
                    producto.Id,
                    producto.Nombre,
                    producto.Marca,
                    producto.Presentacion,
                    producto.Sabor,
                    producto.PrecioCompra,
                    producto.PrecioVenta,

                    // Solo mostramos el stock.
                    // No lo modificamos aquí.
                    producto.Stock,

                    producto.StockMinimo,
                    producto.CategoriaId,
                    producto.Activo
                }
            });
        }

        // GET: api/productos/stock-bajo
        [HttpGet("stock-bajo")]
        public async Task<IActionResult> ObtenerStockBajo()
        {
            var productos = await _context.Productos
                .AsNoTracking()
                .Where(p =>
                    p.Activo &&
                    p.Stock <= p.StockMinimo
                )
                .OrderBy(p => p.Stock)
                .Select(p => new
                {
                    p.Id,
                    p.Nombre,
                    p.Marca,
                    p.Stock,
                    p.StockMinimo
                })
                .ToListAsync();

            return Ok(productos);
        }
    }
}