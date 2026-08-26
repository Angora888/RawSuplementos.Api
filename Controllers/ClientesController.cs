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
    public class ClientesController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public ClientesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: api/clientes
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerClientes()
        {
            var clientes = await _context.Clientes
                .AsNoTracking()
                .OrderBy(c => c.Nombre)
                .Select(c => new
                {
                    c.Id,
                    c.Nombre,
                    c.Telefono,
                    c.Direccion,
                    c.Notas,
                    c.Activo,
                    c.FechaCreacion,

                    Saldo = c.MovimientosCuenta
                        .Sum(m => (decimal?)m.Monto) ?? 0
                })
                .ToListAsync();

            return Ok(clientes);
        }

        // ==========================================
        // GET: api/clientes/5
        // ==========================================

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerCliente(int id)
        {
            var cliente = await _context.Clientes
                .AsNoTracking()
                .Where(c => c.Id == id)
                .Select(c => new
                {
                    c.Id,
                    c.Nombre,
                    c.Telefono,
                    c.Direccion,
                    c.Notas,
                    c.Activo,
                    c.FechaCreacion,

                    Saldo = c.MovimientosCuenta
                        .Sum(m => (decimal?)m.Monto) ?? 0,

                    TotalVentas = c.Ventas
                        .Where(v => v.Estado != "Anulada")
                        .Sum(v => (decimal?)v.Total) ?? 0
                })
                .FirstOrDefaultAsync();

            if (cliente == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            return Ok(cliente);
        }

        // ==========================================
        // GET: api/clientes/buscar?texto=8888
        // ==========================================

        [HttpGet("buscar")]
        public async Task<IActionResult> BuscarCliente(
            [FromQuery] string texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
            {
                return BadRequest(
                    "Debe ingresar un nombre o teléfono."
                );
            }

            texto = texto.Trim().ToLower();

            var clientes = await _context.Clientes
                .AsNoTracking()
                .Where(c =>
                    c.Nombre.ToLower().Contains(texto) ||
                    c.Telefono.Contains(texto)
                )
                .OrderBy(c => c.Nombre)
                .Select(c => new
                {
                    c.Id,
                    c.Nombre,
                    c.Telefono,
                    c.Direccion,
                    c.Activo,

                    Saldo = c.MovimientosCuenta
                        .Sum(m => (decimal?)m.Monto) ?? 0
                })
                .Take(20)
                .ToListAsync();

            return Ok(clientes);
        }

        // ==========================================
        // POST: api/clientes
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CrearCliente(
            CrearClienteDto dto)
        {
            var nombre = dto.Nombre.Trim();
            var telefono = dto.Telefono.Trim();

            var cliente = new Cliente
            {
                Nombre = nombre,
                Telefono = telefono,
                Direccion = string.IsNullOrWhiteSpace(dto.Direccion)
                    ? null
                    : dto.Direccion.Trim(),

                Notas = string.IsNullOrWhiteSpace(dto.Notas)
                    ? null
                    : dto.Notas.Trim(),

                Activo = true,
                FechaCreacion = FechaHelper.AhoraUtc()
            };

            _context.Clientes.Add(cliente);

            await _context.SaveChangesAsync();

            return CreatedAtAction(
                nameof(ObtenerCliente),
                new { id = cliente.Id },
                new
                {
                    cliente.Id,
                    cliente.Nombre,
                    cliente.Telefono,
                    cliente.Direccion,
                    cliente.Notas,
                    cliente.Activo,
                    Saldo = 0m
                }
            );
        }

        // ==========================================
        // PUT: api/clientes/5
        // ==========================================

        [HttpPut("{id:int}")]
        public async Task<IActionResult> EditarCliente(
            int id,
            EditarClienteDto dto)
        {
            var cliente = await _context.Clientes
                .FirstOrDefaultAsync(c => c.Id == id);

            if (cliente == null)
            {
                return NotFound("Cliente no encontrado.");
            }

            cliente.Nombre = dto.Nombre.Trim();
            cliente.Telefono = dto.Telefono.Trim();

            cliente.Direccion =
                string.IsNullOrWhiteSpace(dto.Direccion)
                    ? null
                    : dto.Direccion.Trim();

            cliente.Notas =
                string.IsNullOrWhiteSpace(dto.Notas)
                    ? null
                    : dto.Notas.Trim();

            cliente.Activo = dto.Activo;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Cliente actualizado correctamente.",
                cliente = new
                {
                    cliente.Id,
                    cliente.Nombre,
                    cliente.Telefono,
                    cliente.Direccion,
                    cliente.Notas,
                    cliente.Activo
                }
            });
        }

        // ==========================================
        // GET: api/clientes/5/movimientos
        // ==========================================

        [HttpGet("{id:int}/movimientos")]
        public async Task<IActionResult> ObtenerMovimientos(
            int id)
        {
            var existeCliente =
                await _context.Clientes
                    .AnyAsync(c => c.Id == id);

            if (!existeCliente)
            {
                return NotFound("Cliente no encontrado.");
            }

            var movimientos =
                await _context.MovimientosCuenta
                    .AsNoTracking()
                    .Where(m => m.ClienteId == id)
                    .OrderByDescending(m => m.Fecha)
                    .Select(m => new
                    {
                        m.Id,
                        m.VentaId,
                        m.Tipo,
                        m.Monto,
                        m.Fecha,
                        m.Descripcion,

                        Usuario = m.Usuario.Nombre
                    })
                    .ToListAsync();

            var saldo = movimientos.Sum(m => m.Monto);

            return Ok(new
            {
                clienteId = id,
                saldo,
                movimientos
            });
        }
    }
}