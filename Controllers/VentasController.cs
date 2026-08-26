using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Models;
using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public VentasController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // POST: api/ventas
        // ==========================================

        [HttpPost]
        public async Task<IActionResult> CrearVenta(
            CrearVentaDto dto)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                // ==============================
                // USUARIO DESDE JWT
                // ==============================

                var usuarioIdClaim =
                    User.FindFirstValue(
                        ClaimTypes.NameIdentifier
                    );

                if (!int.TryParse(
                    usuarioIdClaim,
                    out int usuarioId))
                {
                    return Unauthorized(
                        "No se pudo identificar al usuario."
                    );
                }

                // ==============================
                // VALIDAR CLIENTE
                // ==============================

                var cliente =
                    await _context.Clientes
                        .FirstOrDefaultAsync(
                            c =>
                                c.Id == dto.ClienteId &&
                                c.Activo
                        );

                if (cliente == null)
                {
                    return BadRequest(
                        "El cliente no existe o está inactivo."
                    );
                }

                // ==============================
                // VALIDACIONES GENERALES
                // ==============================

                if (dto.Productos == null ||
                    !dto.Productos.Any())
                {
                    return BadRequest(
                        "Debe agregar al menos un producto."
                    );
                }

                if (dto.Descuento < 0)
                {
                    return BadRequest(
                        "El descuento no puede ser negativo."
                    );
                }

                if (dto.PagoInicial < 0)
                {
                    return BadRequest(
                        "El pago inicial no puede ser negativo."
                    );
                }

                // ==============================
                // AGRUPAR PRODUCTOS REPETIDOS
                // ==============================

                var productosSolicitados =
                    dto.Productos
                        .GroupBy(p => p.ProductoId)
                        .Select(g => new
                        {
                            ProductoId = g.Key,
                            Cantidad = g.Sum(x => x.Cantidad)
                        })
                        .ToList();

                var productoIds =
                    productosSolicitados
                        .Select(p => p.ProductoId)
                        .ToList();

                var productos =
                    await _context.Productos
                        .Where(p =>
                            productoIds.Contains(p.Id) &&
                            p.Activo
                        )
                        .ToListAsync();

                if (productos.Count != productoIds.Count)
                {
                    return BadRequest(
                        "Uno o más productos no existen o están inactivos."
                    );
                }

                // ==============================
                // CALCULAR SUBTOTAL
                // ==============================

                decimal subtotal = 0;

                foreach (var item in productosSolicitados)
                {
                    var producto =
                        productos.First(
                            p => p.Id == item.ProductoId
                        );

                    if (item.Cantidad <= 0)
                    {
                        return BadRequest(
                            $"Cantidad inválida para {producto.Nombre}."
                        );
                    }

                    if (producto.Stock < item.Cantidad)
                    {
                        return BadRequest(
                            $"Stock insuficiente para {producto.Nombre}. " +
                            $"Disponible: {producto.Stock}."
                        );
                    }

                    subtotal +=
                        producto.PrecioVenta *
                        item.Cantidad;
                }

                // ==============================
                // TOTAL
                // ==============================

                if (dto.Descuento > subtotal)
                {
                    return BadRequest(
                        "El descuento no puede ser mayor al subtotal."
                    );
                }

                decimal total =
                    subtotal - dto.Descuento;

                if (dto.PagoInicial > total)
                {
                    return BadRequest(
                        "El pago inicial no puede ser mayor al total."
                    );
                }

                // ==============================
                // FECHA DE VENCIMIENTO
                // ==============================

                DateTime? fechaVencimiento = null;

                // Solo necesitamos vencimiento
                // si queda saldo pendiente
                if (dto.PagoInicial < total)
                {
                    if (!dto.FechaVencimiento.HasValue)
                    {
                        return BadRequest(
                            "Debe indicar una fecha de vencimiento cuando queda saldo pendiente."
                        );
                    }

                    var fechaSeleccionada =
                        dto.FechaVencimiento.Value.Date;

                    var hoyCostaRica =
                        FechaHelper.HoyCostaRica();

                    if (fechaSeleccionada < hoyCostaRica)
                    {
                        return BadRequest(
                            "La fecha de vencimiento no puede ser anterior a hoy."
                        );
                    }

                    // PostgreSQL guarda timestamp with time zone,
                    // por lo tanto convertimos la fecha CR a UTC.
                    fechaVencimiento =
                        FechaHelper.CostaRicaAUtc(
                            fechaSeleccionada
                        );
                }

                // ==============================
                // ESTADO
                // ==============================

                string estado;

                if (dto.PagoInicial == 0)
                {
                    estado = "Pendiente";
                }
                else if (dto.PagoInicial < total)
                {
                    estado = "Parcial";
                }
                else
                {
                    estado = "Pagada";
                }

                // ==============================
                // CREAR VENTA
                // ==============================

                var venta = new Venta
                {
                    ClienteId = cliente.Id,
                    UsuarioId = usuarioId,

                    Fecha = FechaHelper.AhoraUtc(),

                    FechaVencimiento = fechaVencimiento,

                    Subtotal = subtotal,
                    Descuento = dto.Descuento,
                    Total = total,

                    Estado = estado,

                    Notas =
                        string.IsNullOrWhiteSpace(dto.Notas)
                            ? null
                            : dto.Notas.Trim()
                };

                _context.Ventas.Add(venta);

                await _context.SaveChangesAsync();

                // ==============================
                // DETALLES + STOCK
                // ==============================

                foreach (var item in productosSolicitados)
                {
                    var producto =
                        productos.First(
                            p => p.Id == item.ProductoId
                        );

                    var detalle =
                        new VentaDetalle
                        {
                            VentaId = venta.Id,

                            ProductoId = producto.Id,

                            Cantidad = item.Cantidad,

                            PrecioUnitario =
                                producto.PrecioVenta,

                            CostoUnitario =
                                producto.PrecioCompra,

                            Subtotal =
                                producto.PrecioVenta *
                                item.Cantidad
                        };

                    _context.VentaDetalles.Add(detalle);

                    var stockAnterior = producto.Stock;

                    producto.Stock -= item.Cantidad;

                    var movimientoInventario = new MovimientoInventario
                    {
                        ProductoId = producto.Id,
                        UsuarioId = usuarioId,
                        VentaId = venta.Id,

                        Tipo = "Venta",

                        Cantidad = item.Cantidad * -1,

                        StockAnterior = stockAnterior,
                        StockNuevo = producto.Stock,

                        Fecha = FechaHelper.AhoraUtc(),

                        Motivo = $"Venta #{venta.Id}"
                    };

                    _context.MovimientosInventario.Add(movimientoInventario);
                }

                // ==============================
                // MOVIMIENTO DE LA VENTA
                // ==============================

                var movimientoVenta =
                    new MovimientoCuenta
                    {
                        ClienteId = cliente.Id,

                        VentaId = venta.Id,

                        UsuarioId = usuarioId,

                        Tipo = "Venta",

                        Monto = total,

                        Fecha = FechaHelper.AhoraUtc(),

                        Descripcion =
                            $"Venta #{venta.Id}"
                    };

                _context.MovimientosCuenta
                    .Add(movimientoVenta);

                // ==============================
                // PAGO INICIAL
                // ==============================

                if (dto.PagoInicial > 0)
                {
                    var pago = new Pago
                    {
                        VentaId = venta.Id,

                        UsuarioId = usuarioId,

                        Monto = dto.PagoInicial,

                        Fecha = FechaHelper.AhoraUtc(),

                        MetodoPago =
                            string.IsNullOrWhiteSpace(
                                dto.MetodoPago
                            )
                                ? "Efectivo"
                                : dto.MetodoPago.Trim(),

                        Referencia =
                            string.IsNullOrWhiteSpace(
                                dto.ReferenciaPago
                            )
                                ? null
                                : dto.ReferenciaPago.Trim()
                    };

                    _context.Pagos.Add(pago);

                    var movimientoPago =
                        new MovimientoCuenta
                        {
                            ClienteId = cliente.Id,

                            VentaId = venta.Id,

                            UsuarioId = usuarioId,

                            Tipo = "Pago",

                            Monto =
                                dto.PagoInicial * -1,

                            Fecha = FechaHelper.AhoraUtc(),

                            Descripcion =
                                $"Pago inicial venta #{venta.Id}"
                        };

                    _context.MovimientosCuenta
                        .Add(movimientoPago);
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                // ==============================
                // SALDO DE LA VENTA
                // ==============================

                decimal pendiente =
                    total - dto.PagoInicial;

                return Ok(new
                {
                    mensaje =
                        "Venta registrada correctamente.",

                    venta = new
                    {
                        venta.Id,

                        Cliente = cliente.Nombre,

                        venta.Subtotal,

                        venta.Descuento,

                        venta.Total,

                        Pagado = dto.PagoInicial,

                        Pendiente = pendiente,

                        venta.Estado,

                        venta.Fecha
                    }
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();

                Console.WriteLine("ERROR CREANDO VENTA:");
                Console.WriteLine(ex.Message);
                Console.WriteLine(ex.InnerException?.Message);
                Console.WriteLine(ex.StackTrace);

                return StatusCode(
                    500,
                    new
                    {
                        mensaje = "Ocurrió un error al registrar la venta.",
                        error = ex.Message,
                        detalle = ex.InnerException?.Message
                    }
                );
            }
        }

        [HttpPost("{ventaId:int}/anular")]
        public async Task<IActionResult> AnularVenta(
            int ventaId,
            AnularVentaDto dto)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var usuarioIdClaim =
                    User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!int.TryParse(usuarioIdClaim, out int usuarioId))
                {
                    return Unauthorized(
                        "No se pudo identificar al usuario."
                    );
                }

                var venta = await _context.Ventas
                    .Include(v => v.Cliente)
                    .Include(v => v.Detalles)
                        .ThenInclude(d => d.Producto)
                    .Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                {
                    return NotFound(
                        "La venta no existe."
                    );
                }

                if (venta.Estado == "Anulada")
                {
                    return BadRequest(
                        "La venta ya está anulada."
                    );
                }

                // ==========================================
                // DEVOLVER INVENTARIO
                // ==========================================

                foreach (var detalle in venta.Detalles)
                {
                    var producto = detalle.Producto;

                    var stockAnterior = producto.Stock;

                    producto.Stock += detalle.Cantidad;

                    var movimientoInventario =
                        new MovimientoInventario
                        {
                            ProductoId = producto.Id,

                            UsuarioId = usuarioId,

                            VentaId = venta.Id,

                            Tipo = "Devolucion",

                            Cantidad = detalle.Cantidad,

                            StockAnterior = stockAnterior,

                            StockNuevo = producto.Stock,

                            Fecha = FechaHelper.AhoraUtc(),

                            Motivo =
                            string.IsNullOrWhiteSpace(dto.Motivo)
                                ? $"Devolución por anulación de venta #{venta.Id}"
                                : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                        };

                    _context.MovimientosInventario
                        .Add(movimientoInventario);
                }

                // ==========================================
                // CALCULAR IMPACTO DE CUENTA
                // ==========================================

                var totalPagado =
                    venta.Pagos.Sum(p => p.Monto);

                /*
                    Al crear la venta tuvimos:

                    Venta  +54,000
                    Pago   -20,000

                    Saldo = +34,000

                    Para anular necesitamos llevar
                    esa cuenta nuevamente a cero.

                    Entonces revertimos:
                    - Total de venta
                    + Total pagado
                */

                var movimientoAnulacionVenta =
                    new MovimientoCuenta
                    {
                        ClienteId = venta.ClienteId,

                        VentaId = venta.Id,

                        UsuarioId = usuarioId,

                        Tipo = "AnulacionVenta",

                        Monto = venta.Total * -1,

                        Fecha = FechaHelper.AhoraUtc(),
                        Descripcion =
                        string.IsNullOrWhiteSpace(dto.Motivo)
                            ? $"Anulación venta #{venta.Id}"
                            : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                    };

                _context.MovimientosCuenta
                    .Add(movimientoAnulacionVenta);

                // ==========================================
                // REVERTIR PAGOS DE LA CUENTA
                // ==========================================

                if (totalPagado > 0)
                {
                    var movimientoReversionPagos =
                        new MovimientoCuenta
                        {
                            ClienteId = venta.ClienteId,

                            VentaId = venta.Id,

                            UsuarioId = usuarioId,

                            Tipo = "ReversionPago",

                            Monto = totalPagado,

                            Fecha = FechaHelper.AhoraUtc(),

                            Descripcion =
                                $"Reversión de pagos por anulación de venta #{venta.Id}"
                        };

                    _context.MovimientosCuenta
                        .Add(movimientoReversionPagos);
                }

                // ==========================================
                // CAMBIAR ESTADO
                // ==========================================

                venta.Estado = "Anulada";

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje =
                        "Venta anulada correctamente.",

                    venta = new
                    {
                        venta.Id,

                        Cliente =
                            venta.Cliente.Nombre,

                        venta.Total,

                        TotalPagado =
                            totalPagado,

                        venta.Estado
                    }
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    500,
                    "Ocurrió un error al anular la venta."
                );
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerVentas(
    [FromQuery] int? clienteId,
    [FromQuery] string? estado,
    [FromQuery] DateTime? fechaDesde,
    [FromQuery] DateTime? fechaHasta)
        {
            var query = _context.Ventas
                .AsNoTracking()
                .AsQueryable();

            if (fechaDesde.HasValue)
            {
                var desdeCostaRica =
                    fechaDesde.Value.Date;

                var desdeUtc =
                    FechaHelper.CostaRicaAUtc(
                        desdeCostaRica
                    );

                query = query.Where(v =>
                    v.Fecha >= desdeUtc);
            }

            if (fechaHasta.HasValue)
            {
                var hastaCostaRica =
                    fechaHasta.Value
                        .Date
                        .AddDays(1);

                var hastaUtc =
                    FechaHelper.CostaRicaAUtc(
                        hastaCostaRica
                    );

                query = query.Where(v =>
                    v.Fecha < hastaUtc);
            }

            if (fechaDesde.HasValue)
            {
                var desde = fechaDesde.Value.Date;

                query = query.Where(v =>
                    v.Fecha >= desde);
            }

            if (fechaHasta.HasValue)
            {
                var hasta =
                    fechaHasta.Value.Date.AddDays(1);

                query = query.Where(v =>
                    v.Fecha < hasta);
            }

            var ventas = await query
                .OrderByDescending(v => v.Fecha)
                .Select(v => new
                {
                    v.Id,

                    Cliente = new
                    {
                        v.Cliente.Id,
                        v.Cliente.Nombre,
                        v.Cliente.Telefono
                    },

                    Usuario = v.Usuario.Nombre,

                    v.Fecha,
                    v.FechaVencimiento,

                    v.Subtotal,
                    v.Descuento,
                    v.Total,

                    Pagado = v.Pagos
                        .Sum(p =>
                            (decimal?)p.Monto) ?? 0,

                    Pendiente =
                        v.Total -
                        (
                            v.Pagos
                                .Sum(p =>
                                    (decimal?)p.Monto) ?? 0
                        ),

                    v.Estado,
                    v.Notas,

                    CantidadProductos =
                        v.Detalles.Sum(d => d.Cantidad)
                })
                .ToListAsync();

            return Ok(new
            {
                cantidad = ventas.Count,

                totalVentas =
                    ventas
                        .Where(v => v.Estado != "Anulada")
                        .Sum(v => v.Total),

                ventas
            });
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerVenta(int id)
        {
            var venta = await _context.Ventas
                .AsNoTracking()
                .Where(v => v.Id == id)
                .Select(v => new
                {
                    v.Id,

                    Cliente = new
                    {
                        v.Cliente.Id,
                        v.Cliente.Nombre,
                        v.Cliente.Telefono,
                        v.Cliente.Direccion
                    },

                    Usuario = new
                    {
                        v.Usuario.Id,
                        v.Usuario.Nombre
                    },

                    v.Fecha,
                    v.FechaVencimiento,

                    v.Subtotal,
                    v.Descuento,
                    v.Total,
                    v.Estado,
                    v.Notas,

                    Detalles = v.Detalles
                        .Select(d => new
                        {
                            d.Id,
                            d.ProductoId,

                            Producto =
                                d.Producto.Nombre,

                            Marca =
                                d.Producto.Marca,

                            d.Cantidad,

                            d.PrecioUnitario,

                            d.CostoUnitario,

                            d.Subtotal,

                            Ganancia =
                                (
                                    d.PrecioUnitario -
                                    d.CostoUnitario
                                ) * d.Cantidad
                        })
                        .ToList(),

                    Pagos = v.Pagos
                        .OrderByDescending(p => p.Fecha)
                        .Select(p => new
                        {
                            p.Id,
                            p.Monto,
                            p.Fecha,
                            p.MetodoPago,
                            p.Referencia,
                            p.Notas,

                            RegistradoPor =
                                p.Usuario.Nombre
                        })
                        .ToList(),

                    TotalPagado =
                        v.Pagos.Sum(p =>
                            (decimal?)p.Monto) ?? 0,

                    Pendiente =
                        v.Total -
                        (
                            v.Pagos.Sum(p =>
                                (decimal?)p.Monto) ?? 0
                        ),

                    Ganancia =
                        v.Detalles.Sum(d =>
                            (
                                d.PrecioUnitario -
                                d.CostoUnitario
                            ) * d.Cantidad
                        )
                })
                .FirstOrDefaultAsync();

            if (venta == null)
            {
                return NotFound(
                    "Venta no encontrada."
                );
            }

            return Ok(venta);
        }

        [HttpPost("{ventaId:int}/abonos")]
        public async Task<IActionResult> RegistrarAbono(
    int ventaId,
    RegistrarAbonoDto dto)
        {
            await using var transaction =
                await _context.Database.BeginTransactionAsync();

            try
            {
                var usuarioIdClaim =
                    User.FindFirstValue(ClaimTypes.NameIdentifier);

                if (!int.TryParse(usuarioIdClaim, out int usuarioId))
                {
                    return Unauthorized(
                        "No se pudo identificar al usuario."
                    );
                }

                if (dto.Monto <= 0)
                {
                    return BadRequest(
                        "El monto del abono debe ser mayor a cero."
                    );
                }

                var venta = await _context.Ventas
                    .Include(v => v.Cliente)
                    .Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId);

                if (venta == null)
                {
                    return NotFound(
                        "La venta no existe."
                    );
                }

                if (venta.Estado == "Anulada")
                {
                    return BadRequest(
                        "No se pueden registrar abonos en una venta anulada."
                    );
                }

                if (venta.Estado == "Pagada")
                {
                    return BadRequest(
                        "Esta venta ya está pagada completamente."
                    );
                }

                // ==========================
                // CALCULAR SALDO DE LA VENTA
                // ==========================

                var totalPagado = venta.Pagos.Sum(p => p.Monto);

                var pendiente = venta.Total - totalPagado;

                if (pendiente <= 0)
                {
                    return BadRequest(
                        "Esta venta no tiene saldo pendiente."
                    );
                }

                if (dto.Monto > pendiente)
                {
                    return BadRequest(
                        $"El abono no puede ser mayor al saldo pendiente de ₡{pendiente:N2}."
                    );
                }

                // ==========================
                // CREAR PAGO
                // ==========================

                var pago = new Pago
                {
                    VentaId = venta.Id,

                    UsuarioId = usuarioId,

                    Monto = dto.Monto,

                    Fecha = FechaHelper.AhoraUtc(),

                    MetodoPago =
                        string.IsNullOrWhiteSpace(dto.MetodoPago)
                            ? "Efectivo"
                            : dto.MetodoPago.Trim(),

                    Referencia =
                        string.IsNullOrWhiteSpace(dto.Referencia)
                            ? null
                            : dto.Referencia.Trim(),

                    Notas =
                        string.IsNullOrWhiteSpace(dto.Notas)
                            ? null
                            : dto.Notas.Trim()
                };

                _context.Pagos.Add(pago);

                // ==========================
                // MOVIMIENTO DEL CLIENTE
                // ==========================

                var movimiento = new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId,

                    VentaId = venta.Id,

                    UsuarioId = usuarioId,

                    Tipo = "Abono",

                    Monto = dto.Monto * -1,

                    Fecha = FechaHelper.AhoraUtc(),

                    Descripcion =
                        $"Abono venta #{venta.Id}"
                };

                _context.MovimientosCuenta.Add(movimiento);

                // ==========================
                // NUEVO SALDO
                // ==========================

                var nuevoTotalPagado =
                    totalPagado + dto.Monto;

                var nuevoPendiente =
                    venta.Total - nuevoTotalPagado;

                if (nuevoPendiente == 0)
                {
                    venta.Estado = "Pagada";
                }
                else
                {
                    venta.Estado = "Parcial";
                }

                await _context.SaveChangesAsync();

                await transaction.CommitAsync();

                return Ok(new
                {
                    mensaje = "Abono registrado correctamente.",

                    venta = new
                    {
                        venta.Id,

                        Cliente = venta.Cliente.Nombre,

                        venta.Total,

                        Abonado = nuevoTotalPagado,

                        Pendiente = nuevoPendiente,

                        venta.Estado
                    },

                    pago = new
                    {
                        pago.Id,
                        pago.Monto,
                        pago.MetodoPago,
                        pago.Referencia,
                        pago.Fecha
                    }
                });
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();

                return StatusCode(
                    500,
                    "Ocurrió un error al registrar el abono."
                );
            }
        }
    }
}