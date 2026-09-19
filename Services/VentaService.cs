using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Models;

namespace RawSuplementos.Api.Services
{
    public class VentaService
    {
        private readonly ApplicationDbContext _context;

        public VentaService(ApplicationDbContext context) => _context = context;

        public async Task<(bool Ok, string? Error, object? Data)> CrearAsync(CrearVentaDto dto, int negocioId, int usuarioId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var cliente = await _context.Clientes.FirstOrDefaultAsync(c => c.Id == dto.ClienteId && c.NegocioId == negocioId && c.Activo);
                if (cliente == null) return (false, "El cliente no existe, está inactivo o no pertenece al negocio.", null);

                var solicitados = dto.Productos.GroupBy(p => p.ProductoId)
                    .Select(g => new { ProductoId = g.Key, Cantidad = g.Sum(x => x.Cantidad) }).ToList();
                var ids = solicitados.Select(x => x.ProductoId).ToList();
                var productos = await _context.Productos.Where(p => ids.Contains(p.Id) && p.NegocioId == negocioId && p.Activo).ToListAsync();
                if (productos.Count != ids.Count) return (false, "Uno o más productos no existen, están inactivos o no pertenecen al negocio.", null);

                decimal subtotal = 0;
                foreach (var item in solicitados)
                {
                    var producto = productos.First(p => p.Id == item.ProductoId);
                    if (producto.Stock < item.Cantidad) return (false, $"Stock insuficiente para {producto.Nombre}. Disponible: {producto.Stock}.", null);
                    subtotal += producto.PrecioVenta * item.Cantidad;
                }

                if (dto.Descuento > subtotal) return (false, "El descuento no puede ser mayor al subtotal.", null);
                var total = subtotal - dto.Descuento;
                if (dto.PagoInicial > total) return (false, "El pago inicial no puede ser mayor al total.", null);

                DateTime? fechaVencimiento = null;
                if (dto.PagoInicial < total)
                {
                    if (!dto.FechaVencimiento.HasValue) return (false, "Debe indicar una fecha de vencimiento cuando queda saldo pendiente.", null);
                    var fecha = dto.FechaVencimiento.Value.Date;
                    if (fecha < FechaHelper.HoyCostaRica()) return (false, "La fecha de vencimiento no puede ser anterior a hoy.", null);
                    fechaVencimiento = FechaHelper.CostaRicaAUtc(fecha);
                }

                var venta = new Venta
                {
                    NegocioId = negocioId, ClienteId = cliente.Id, UsuarioId = usuarioId, Fecha = FechaHelper.AhoraUtc(),
                    FechaVencimiento = fechaVencimiento, Subtotal = subtotal, Descuento = dto.Descuento, Total = total,
                    Estado = dto.PagoInicial == 0 ? "Pendiente" : dto.PagoInicial < total ? "Parcial" : "Pagada",
                    Notas = string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas.Trim()
                };
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                foreach (var item in solicitados)
                {
                    var producto = productos.First(p => p.Id == item.ProductoId);
                    _context.VentaDetalles.Add(new VentaDetalle { VentaId = venta.Id, ProductoId = producto.Id, Cantidad = item.Cantidad, PrecioUnitario = producto.PrecioVenta, CostoUnitario = producto.PrecioCompra, Subtotal = producto.PrecioVenta * item.Cantidad });
                    var anterior = producto.Stock;
                    producto.Stock -= item.Cantidad;
                    _context.MovimientosInventario.Add(new MovimientoInventario { ProductoId = producto.Id, UsuarioId = usuarioId, VentaId = venta.Id, Tipo = "Venta", Cantidad = -item.Cantidad, StockAnterior = anterior, StockNuevo = producto.Stock, Fecha = FechaHelper.AhoraUtc(), Motivo = $"Venta #{venta.Id}" });
                }

                _context.MovimientosCuenta.Add(new MovimientoCuenta { ClienteId = cliente.Id, VentaId = venta.Id, UsuarioId = usuarioId, Tipo = "Venta", Monto = total, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Venta #{venta.Id}" });
                if (dto.PagoInicial > 0)
                {
                    _context.Pagos.Add(new Pago { VentaId = venta.Id, UsuarioId = usuarioId, Monto = dto.PagoInicial, Fecha = FechaHelper.AhoraUtc(), MetodoPago = string.IsNullOrWhiteSpace(dto.MetodoPago) ? "Efectivo" : dto.MetodoPago.Trim(), Referencia = string.IsNullOrWhiteSpace(dto.ReferenciaPago) ? null : dto.ReferenciaPago.Trim() });
                    _context.MovimientosCuenta.Add(new MovimientoCuenta { ClienteId = cliente.Id, VentaId = venta.Id, UsuarioId = usuarioId, Tipo = "Pago", Monto = -dto.PagoInicial, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Pago inicial venta #{venta.Id}" });
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, null, new { mensaje = "Venta registrada correctamente.", venta = new { venta.Id, Cliente = cliente.Nombre, venta.Subtotal, venta.Descuento, venta.Total, Pagado = dto.PagoInicial, Pendiente = total - dto.PagoInicial, venta.Estado, venta.Fecha } });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool Ok, bool NotFound, string? Error, object? Data)> AnularAsync(int ventaId, AnularVentaDto dto, int negocioId, int usuarioId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas.Include(v => v.Cliente).Include(v => v.Detalles).ThenInclude(d => d.Producto).Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId && v.NegocioId == negocioId);
                if (venta == null) return (false, true, "La venta no existe.", null);
                if (venta.Estado == "Anulada") return (false, false, "La venta ya está anulada.", null);

                foreach (var detalle in venta.Detalles)
                {
                    if (detalle.Producto.NegocioId != negocioId) return (false, false, "La venta contiene un producto que no pertenece al negocio.", null);
                    var anterior = detalle.Producto.Stock;
                    detalle.Producto.Stock += detalle.Cantidad;
                    _context.MovimientosInventario.Add(new MovimientoInventario
                    {
                        ProductoId = detalle.Producto.Id, UsuarioId = usuarioId, VentaId = venta.Id,
                        Tipo = "Devolucion", Cantidad = detalle.Cantidad, StockAnterior = anterior, StockNuevo = detalle.Producto.Stock,
                        Fecha = FechaHelper.AhoraUtc(), Motivo = string.IsNullOrWhiteSpace(dto.Motivo) ? $"Devolución por anulación de venta #{venta.Id}" : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                    });
                }

                var pagado = venta.Pagos.Sum(p => p.Monto);
                _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId,
                    Tipo = "AnulacionVenta", Monto = -venta.Total, Fecha = FechaHelper.AhoraUtc(),
                    Descripcion = string.IsNullOrWhiteSpace(dto.Motivo) ? $"Anulación venta #{venta.Id}" : $"Anulación venta #{venta.Id}: {dto.Motivo.Trim()}"
                });
                if (pagado > 0) _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId,
                    Tipo = "ReversionPago", Monto = pagado, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Reversión de pagos por anulación de venta #{venta.Id}"
                });

                venta.Estado = "Anulada";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, false, null, new { mensaje = "Venta anulada correctamente.", venta = new { venta.Id, Cliente = venta.Cliente.Nombre, venta.Total, TotalPagado = pagado, venta.Estado } });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<(bool Ok, bool NotFound, string? Error, object? Data)> RegistrarAbonoAsync(int ventaId, RegistrarAbonoDto dto, int negocioId, int usuarioId)
        {
            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var venta = await _context.Ventas.Include(v => v.Cliente).Include(v => v.Pagos)
                    .FirstOrDefaultAsync(v => v.Id == ventaId && v.NegocioId == negocioId);
                if (venta == null) return (false, true, "La venta no existe.", null);
                if (venta.Estado == "Anulada") return (false, false, "No se pueden registrar abonos en una venta anulada.", null);
                if (venta.Estado == "Pagada") return (false, false, "Esta venta ya está pagada completamente.", null);

                var totalPagado = venta.Pagos.Sum(p => p.Monto);
                var pendiente = venta.Total - totalPagado;
                if (pendiente <= 0) return (false, false, "Esta venta no tiene saldo pendiente.", null);
                if (dto.Monto > pendiente) return (false, false, $"El abono no puede ser mayor al saldo pendiente de ₡{pendiente:N2}.", null);

                var pago = new Pago
                {
                    VentaId = venta.Id, UsuarioId = usuarioId, Monto = dto.Monto, Fecha = FechaHelper.AhoraUtc(),
                    MetodoPago = string.IsNullOrWhiteSpace(dto.MetodoPago) ? "Efectivo" : dto.MetodoPago.Trim(),
                    Referencia = string.IsNullOrWhiteSpace(dto.Referencia) ? null : dto.Referencia.Trim(),
                    Notas = string.IsNullOrWhiteSpace(dto.Notas) ? null : dto.Notas.Trim()
                };
                _context.Pagos.Add(pago);
                _context.MovimientosCuenta.Add(new MovimientoCuenta
                {
                    ClienteId = venta.ClienteId, VentaId = venta.Id, UsuarioId = usuarioId,
                    Tipo = "Abono", Monto = -dto.Monto, Fecha = FechaHelper.AhoraUtc(), Descripcion = $"Abono venta #{venta.Id}"
                });

                var nuevoTotal = totalPagado + dto.Monto;
                var nuevoPendiente = venta.Total - nuevoTotal;
                venta.Estado = nuevoPendiente == 0 ? "Pagada" : "Parcial";
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                return (true, false, null, new { mensaje = "Abono registrado correctamente.", venta = new { venta.Id, Cliente = venta.Cliente.Nombre, venta.Total, Abonado = nuevoTotal, Pendiente = nuevoPendiente, venta.Estado }, pago = new { pago.Id, pago.Monto, pago.MetodoPago, pago.Referencia, pago.Fecha } });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }
    }
}
