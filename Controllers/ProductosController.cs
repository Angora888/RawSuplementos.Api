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
    public class ProductosController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        public ProductosController(ApplicationDbContext context) => _context = context;

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var id) ? id : null;
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerProductos()
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            var productos = await _context.Productos.AsNoTracking().Where(p => p.NegocioId == negocioId.Value)
                .OrderBy(p => p.Nombre).Select(p => new { p.Id, p.Nombre, p.Marca, p.Presentacion, p.Sabor, p.PrecioCompra, p.PrecioVenta, p.Stock, p.StockMinimo, p.ImageUrl, p.Activo, p.CategoriaId, Categoria = p.Categoria.Nombre, StockBajo = p.Stock <= p.StockMinimo }).ToListAsync();
            return Ok(productos);
        }

        [AllowAnonymous]
        [HttpGet("catalogo/{slug}")]
        public async Task<IActionResult> ObtenerCatalogoPublico(string slug)
        {
            slug = slug.Trim().ToLowerInvariant();
            var negocio = await _context.Negocios.AsNoTracking().FirstOrDefaultAsync(n => n.Slug == slug && n.Activo);
            if (negocio == null) return NotFound("Negocio no encontrado.");
            var productos = await _context.Productos.AsNoTracking()
                .Where(p => p.NegocioId == negocio.Id && p.Activo && p.Stock > 0)
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.Id, p.Nombre, p.Marca, p.Presentacion, p.Sabor, p.PrecioVenta, p.ImageUrl, Categoria = p.Categoria.Nombre, Disponible = p.Stock > 0 })
                .ToListAsync();
            return Ok(new { negocio = new { negocio.Nombre, negocio.Slug, negocio.LogoUrl, negocio.WhatsApp, negocio.Telefono, negocio.Direccion, negocio.ColorPrimario, negocio.ColorSecundario, negocio.TituloLanding, negocio.DescripcionLanding }, productos });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerProducto(int id)
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            var producto = await _context.Productos.AsNoTracking().Where(p => p.Id == id && p.NegocioId == negocioId.Value)
                .Select(p => new { p.Id, p.Nombre, p.Marca, p.Presentacion, p.Sabor, p.PrecioCompra, p.PrecioVenta, p.Stock, p.StockMinimo, p.ImageUrl, p.Activo, p.CategoriaId, Categoria = p.Categoria.Nombre, StockBajo = p.Stock <= p.StockMinimo }).FirstOrDefaultAsync();
            return producto == null ? NotFound("Producto no encontrado.") : Ok(producto);
        }

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarProducto([FromQuery] string texto)
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            if (string.IsNullOrWhiteSpace(texto)) return BadRequest("Debe ingresar un texto para buscar.");
            texto = texto.Trim().ToLower();
            var productos = await _context.Productos.AsNoTracking()
                .Where(p => p.NegocioId == negocioId.Value && (p.Nombre.ToLower().Contains(texto) || (p.Marca != null && p.Marca.ToLower().Contains(texto))))
                .OrderBy(p => p.Nombre).Take(30)
                .Select(p => new { p.Id, p.Nombre, p.Marca, p.Presentacion, p.Sabor, p.PrecioVenta, p.Stock, Categoria = p.Categoria.Nombre }).ToListAsync();
            return Ok(productos);
        }

        [HttpPost]
        public async Task<IActionResult> CrearProducto(CrearProductoDto dto)
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            if (dto.PrecioCompra < 0 || dto.PrecioVenta < 0) return BadRequest("Los precios no pueden ser negativos.");
            if (dto.StockMinimo < 0) return BadRequest("El stock mínimo no puede ser negativo.");
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId && c.NegocioId == negocioId.Value && c.Activa);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe o está inactiva.");
            var producto = new Producto { NegocioId = negocioId.Value, Nombre = dto.Nombre.Trim(), Marca = string.IsNullOrWhiteSpace(dto.Marca) ? null : dto.Marca.Trim(), Presentacion = string.IsNullOrWhiteSpace(dto.Presentacion) ? null : dto.Presentacion.Trim(), Sabor = string.IsNullOrWhiteSpace(dto.Sabor) ? null : dto.Sabor.Trim(), PrecioCompra = dto.PrecioCompra, PrecioVenta = dto.PrecioVenta, Stock = 0, StockMinimo = dto.StockMinimo, ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim(), CategoriaId = dto.CategoriaId, Activo = true };
            _context.Productos.Add(producto); await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(ObtenerProducto), new { id = producto.Id }, producto);
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarProducto(int id, EditarProductoDto dto)
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == id && p.NegocioId == negocioId.Value);
            if (producto == null) return NotFound("Producto no encontrado.");
            if (dto.PrecioCompra < 0 || dto.PrecioVenta < 0 || dto.StockMinimo < 0) return BadRequest("Los precios y el stock mínimo no pueden ser negativos.");
            var categoriaExiste = await _context.Categorias.AnyAsync(c => c.Id == dto.CategoriaId && c.NegocioId == negocioId.Value && c.Activa);
            if (!categoriaExiste) return BadRequest("La categoría indicada no existe o está inactiva.");
            producto.Nombre = dto.Nombre.Trim(); producto.Marca = string.IsNullOrWhiteSpace(dto.Marca) ? null : dto.Marca.Trim(); producto.Presentacion = string.IsNullOrWhiteSpace(dto.Presentacion) ? null : dto.Presentacion.Trim(); producto.Sabor = string.IsNullOrWhiteSpace(dto.Sabor) ? null : dto.Sabor.Trim(); producto.PrecioCompra = dto.PrecioCompra; producto.PrecioVenta = dto.PrecioVenta; producto.StockMinimo = dto.StockMinimo; producto.ImageUrl = string.IsNullOrWhiteSpace(dto.ImageUrl) ? null : dto.ImageUrl.Trim(); producto.CategoriaId = dto.CategoriaId; producto.Activo = dto.Activo;
            await _context.SaveChangesAsync();
            return Ok(new { mensaje = "Producto actualizado correctamente.", producto = new { producto.Id, producto.Nombre, producto.Marca, producto.Presentacion, producto.Sabor, producto.PrecioCompra, producto.PrecioVenta, producto.Stock, producto.StockMinimo, producto.CategoriaId, producto.Activo } });
        }

        [HttpGet("stock-bajo")]
        public async Task<IActionResult> ObtenerStockBajo()
        {
            var negocioId = ObtenerNegocioId(); if (negocioId == null) return Unauthorized();
            var productos = await _context.Productos.AsNoTracking().Where(p => p.NegocioId == negocioId.Value && p.Activo && p.Stock <= p.StockMinimo)
                .OrderBy(p => p.Stock).Select(p => new { p.Id, p.Nombre, p.Marca, p.Stock, p.StockMinimo }).ToListAsync();
            return Ok(productos);
        }
    }
}