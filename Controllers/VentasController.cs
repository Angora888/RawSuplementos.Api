using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Models;
using RawSuplementos.Api.Services;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UsuarioTenantService _usuarioTenantService;
        private readonly VentaService _ventaService;

        public VentasController(ApplicationDbContext context, UsuarioTenantService usuarioTenantService, VentaService ventaService)
        {
            _context = context;
            _usuarioTenantService = usuarioTenantService;
            _ventaService = ventaService;
        }

        private static string? ValidarNuevaVenta(CrearVentaDto dto)
        {
            if (dto.Productos == null || !dto.Productos.Any()) return "Debe agregar al menos un producto.";
            if (dto.Productos.Any(p => p.Cantidad <= 0)) return "Las cantidades de productos deben ser mayores a cero.";
            if (dto.Descuento < 0) return "El descuento no puede ser negativo.";
            if (dto.PagoInicial < 0) return "El pago inicial no puede ser negativo.";
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> CrearVenta(CrearVentaDto dto)
        {
            var negocioId = User.ObtenerNegocioId();
            var usuarioId = User.ObtenerUsuarioId();
            if (negocioId == null || usuarioId == null) return Unauthorized("No se pudo identificar el negocio o usuario.");
            if (!await _usuarioTenantService.EsUsuarioActivoDelNegocioAsync(usuarioId.Value, negocioId.Value)) return Unauthorized("El usuario no pertenece al negocio o está desactivado.");

            var errorValidacion = ValidarNuevaVenta(dto);
            if (errorValidacion != null) return BadRequest(errorValidacion);

            try
            {
                var resultado = await _ventaService.CrearAsync(dto, negocioId.Value, usuarioId.Value);
                return resultado.Ok ? Ok(resultado.Data) : BadRequest(resultado.Error);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al registrar la venta.");
            }
        }

        [HttpPost("{ventaId:int}/anular")]
        public async Task<IActionResult> AnularVenta(int ventaId, AnularVentaDto dto)
        {
            var negocioId = User.ObtenerNegocioId();
            var usuarioId = User.ObtenerUsuarioId();
            if (negocioId == null || usuarioId == null) return Unauthorized("No se pudo identificar el negocio o usuario.");
            if (!await _usuarioTenantService.EsUsuarioActivoDelNegocioAsync(usuarioId.Value, negocioId.Value)) return Unauthorized("El usuario no pertenece al negocio o está desactivado.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas.Include(v => v.Cliente).Include(v => v.Detalles).ThenInclude(d => d.Producto).Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId && v.NegocioId == negocioId.Value);
                if (venta == null) return NotFound("La venta no existe.");
                if (venta.Estado == "Anulada") return BadRequest("La venta ya está anulada.");

                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.Producto.NegocioId != negocioId.Value) return BadRequest("La venta contiene un producto que no pertenece al negocio.");
                    var anterior = detalle.Producto.Stock;
                    detalle.Producto.Stock += detalle.Cantidad;
                    _context.MovimientosInventario.Add(new MovimientoInventario
                    {
                        ProductoId = detalle.Producto.Id, UsuarioId = usuarioId.Value, VentaId = venta.Id,
                        Tipo = "Devolucion", Cantidad = detalle.Cantidad, StockAnterior = anterior, StockNuevo = detalle.Producto.Stock,
                        Fecha = FechaHelper.AhoraUtc(), Motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? $"Devolución por anulación de venta #{venta.Id}" : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                    });
                }

                var pagado = venta.Pagos.Sum(p => p.Monto);
                _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId.Value,
                    Tipo = "AnulacionVenta", Monto = -venta.Total, Fecha = FechaHelper.AhoraUtc(),
                    Descripcion = string.IsNullOrWhiteSpace(dto.Motivo) ? $"Anulación venta #{venta.Id}" : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                });
                if (pagado > 0) _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId.Value,
                    Tipo = "ReversionPago", Monto = pagado, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Reversión de pagos por anulación de venta #{venta.Id}"
                });

                venta.Estado = "Anulada";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { mensaje = "Venta anulada correctamente.", venta = new { venta.Id, Cliente = venta.Cliente.Nombre, venta.Total, TotalPagado = pagado, venta.Estado } });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Ocurrió un error al anular la venta.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerVentas([FromQuery] int? clienteId, [FromQuery] string? estado, [FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var query = _context.Ventas.AsNoTracking().Where(v => v.NegocioId == negocioId.Value).AsQueryable();
            if (clienteId.HasValue) query = query.Where(v => v.ClienteId == clienteId.Value);
            if (!string.IsNullOrWhiteSpace(estado)) query = query.Where(v => v.Estado == estado.Trim());
            if (fechaDesde.HasValue) { var desde = FechaHelper.CostaRicaAUtc(fechaDesde.Value.Date); query = query.Where(v => v.Fecha >= desde); }
            if (fechaHasta.HasValue) { var hasta = FechaHelper.CostaRicaAUtc(fechaHasta.Value.Date.AddDays(1)); query = query.Where(v => v.Fecha < hasta); }

            var ventas = await query.OrderByDescending(v => v.Fecha).Select(v => new
            {
                v.Id, Cliente = new { v.Cliente.Id, v.Cliente.Nombre, v.Cliente.Telefono }, Usuario = v.Usuario.Nombre,
                v.Fecha, v.FechaVencimiento, v.Subtotal, v.Descuento, v.Total,
                Pagado = v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0,
                Pendiente = v.Total - (v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0), v.Estado, v.Notas,
                CantidadProductos = v.Detalles.Sum(d => d.Cantidad)
            }).ToListAsync();
            return Ok(new { cantidad = ventas.Count, totalVentas = ventas.Where(v => v.Estado != "Anulada").Sum(v => v.Total), ventas });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerVenta(int id)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var venta = await _context.Ventas.AsNoTracking().Where(v => v.Id == id && v.NegocioId == negocioId.Value).Select(v => new
            {
                v.Id, Cliente = new { v.Cliente.Id, v.Cliente.Nombre, v.Cliente.Telefono, v.Cliente.Direccion },
                Usuario = new { v.Usuario.Id, v.Usuario.Nombre }, v.Fecha, v.FechaVencimiento, v.Subtotal, v.Descuento, v.Total, v.Estado, v.Notas,
                Detalles = v.Detalles.Select(d => new { d.Id, d.ProductoId, Producto = d.Producto.Nombre, Marca = d.Producto.Marca, d.Cantidad, d.PrecioUnitario, d.CostoUnitario, d.Subtotal, Ganancia = (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad }).ToList(),
                Pagos = v.Pagos.OrderByDescending(p => p.Fecha).Select(p => new { p.Id, p.Monto, p.Fecha, p.MetodoPago, p.Referencia, p.Notas, RegistradoPor = p.Usuario.Nombre }).ToList(),
                TotalPagado = v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0,
                Pendiente = v.Total - (v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0),
                Ganancia = v.Detalles.Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad)
            }).FirstOrDefaultAsync();
            return venta == null ? NotFound("Venta no encontrada.") : Ok(venta);
        }

        [HttpPost("{ventaId:int}/abonos")]
        public async Task<IActionResult> RegistrarAbono(int ventaId, RegistrarAbonoDto dto)
        {
            var negocioId = User.ObtenerNegocioId();
            var usuarioId = User.ObtenerUsuarioId();
            if (negocioId == null || usuarioId == null) return Unauthorized("No se pudo identificar el negocio o usuario.");
            if (!await _usuarioTenantService.EsUsuarioActivoDelNegocioAsync(usuarioId.Value, negocioId.Value)) return Unauthorized("El usuario no pertenece al negocio o está desactivado.");
            if (dto.Monto <= 0) return BadRequest("El monto del abono debe ser mayor a cero.");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas.Include(v => v.Cliente).Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId && v.NegocioId == negocioId.Value);
                if (venta == null) return NotFound("La venta no existe.");
                if (venta.Estado == "Anulada") return BadRequest("No se pueden registrar abonos en una venta anulada.");
                if (venta.Estado == "Pagada") return BadRequest("Esta venta ya está pagada completamente.");

                var totalPagado = venta.Pagos.Sum(p => p.Monto);
                var pendiente = venta.Total - totalPagado;
                if (pendiente <= 0) return BadRequest("Esta venta no tiene saldo pendiente.");
                if (dto.Monto > pendiente) return BadRequest($"El abono no puede ser mayor al saldo pendiente de ₡{pendiente:N2}.");

                var pago = new Pago
                {
                    VentaId = venta.Id, UsuarioId = usuarioId.Value, Monto = dto.Monto, Fecha = FechaHelper.AhoraUtc(),
                    MetodoPago = string.IsNullOrWhiteSpace(dto.MetodoPago) ? "Efectivo" : dto.MetodoPago.Trim(),
                    Referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim(),
                    Notas = string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas.Trim()
                };
                _context.Pagos.Add(pago);
                _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId.Value,
                    Tipo = "Abono", Monto = -dto.Monto, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Abono venta #{venta.Id}"
                });

                var nuevoTotal = totalPagado + dto.Monto;
                var nuevoPendiente = venta.Total - nuevoTotal;
                venta.Estado = nuevoPendiente == 0 ? "Pagada" : "Parcial";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return Ok(new { mensaje = "Abono registrado correctamente.", venta = new { venta.Id, Cliente = venta.Cliente.Nombre, venta.Total, Abonado = nuevoTotal, Pendiente = nuevoPendiente, venta.Estado }, pago = new { pago.Id, pago.Monto, pago.MetodoPago, pago.Referencia, pago.Fecha } });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                return StatusCode(500, "Ocurrió un error al registrar el abono.");
            }
        }
    }
}