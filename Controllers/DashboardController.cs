using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DashboardController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public DashboardController(ApplicationDbContext context)
        {
            _context = context;
        }

        // ==========================================
        // GET: api/dashboard
        // ==========================================

        [HttpGet]
        public async Task<IActionResult> ObtenerDashboard()
        {
            // ======================================
            // FECHAS COSTA RICA -> UTC
            // ======================================

            var hoyCostaRica =
                FechaHelper.HoyCostaRica();

            var inicioHoyUtc =
                FechaHelper.InicioHoyCostaRicaUtc();

            var inicioMananaUtc =
                FechaHelper.InicioMananaCostaRicaUtc();

            var inicioMesCostaRica =
                new DateTime(
                    hoyCostaRica.Year,
                    hoyCostaRica.Month,
                    1
                );

            var inicioMesSiguienteCostaRica =
                inicioMesCostaRica.AddMonths(1);

            var inicioMesUtc =
                FechaHelper.CostaRicaAUtc(
                    inicioMesCostaRica
                );

            var inicioMesSiguienteUtc =
                FechaHelper.CostaRicaAUtc(
                    inicioMesSiguienteCostaRica
                );

            // ======================================
            // VENTAS DE HOY
            // ======================================

            var ventasHoy = await _context.Ventas
                .AsNoTracking()
                .Where(v =>
                    v.Estado != "Anulada" &&
                    v.Fecha >= inicioHoyUtc &&
                    v.Fecha < inicioMananaUtc
                )
                .SumAsync(v =>
                    (decimal?)v.Total
                ) ?? 0;

            var cantidadVentasHoy =
                await _context.Ventas
                    .AsNoTracking()
                    .CountAsync(v =>
                        v.Estado != "Anulada" &&
                        v.Fecha >= inicioHoyUtc &&
                        v.Fecha < inicioMananaUtc
                    );

            // ======================================
            // VENTAS DEL MES
            // ======================================

            var ventasMes = await _context.Ventas
                .AsNoTracking()
                .Where(v =>
                    v.Estado != "Anulada" &&
                    v.Fecha >= inicioMesUtc &&
                    v.Fecha < inicioMesSiguienteUtc
                )
                .SumAsync(v =>
                    (decimal?)v.Total
                ) ?? 0;

            var cantidadVentasMes =
                await _context.Ventas
                    .AsNoTracking()
                    .CountAsync(v =>
                        v.Estado != "Anulada" &&
                        v.Fecha >= inicioMesUtc &&
                        v.Fecha < inicioMesSiguienteUtc
                    );

            // ======================================
            // GANANCIA HOY
            // ======================================
            //
            // Ya usamos CostoUnitario histórico
            // guardado en VentaDetalle.
            // ======================================

            var gananciaHoy =
                await _context.VentaDetalles
                    .AsNoTracking()
                    .Where(d =>
                        d.Venta.Estado != "Anulada" &&
                        d.Venta.Fecha >= inicioHoyUtc &&
                        d.Venta.Fecha < inicioMananaUtc
                    )
                    .SumAsync(d =>
                        (decimal?)
                        (
                            (d.PrecioUnitario -
                             d.CostoUnitario)
                            * d.Cantidad
                        )
                    ) ?? 0;

            // ======================================
            // GANANCIA DEL MES
            // ======================================

            var gananciaMes =
                await _context.VentaDetalles
                    .AsNoTracking()
                    .Where(d =>
                        d.Venta.Estado != "Anulada" &&
                        d.Venta.Fecha >= inicioMesUtc &&
                        d.Venta.Fecha <
                            inicioMesSiguienteUtc
                    )
                    .SumAsync(d =>
                        (decimal?)
                        (
                            (d.PrecioUnitario -
                             d.CostoUnitario)
                            * d.Cantidad
                        )
                    ) ?? 0;

            // ======================================
            // PAGOS / ABONOS RECIBIDOS HOY
            // ======================================

            var pagosHoy =
                await _context.Pagos
                    .AsNoTracking()
                    .Where(p =>
                        p.Fecha >= inicioHoyUtc &&
                        p.Fecha < inicioMananaUtc
                    )
                    .SumAsync(p =>
                        (decimal?)p.Monto
                    ) ?? 0;

            var cantidadPagosHoy =
                await _context.Pagos
                    .AsNoTracking()
                    .CountAsync(p =>
                        p.Fecha >= inicioHoyUtc &&
                        p.Fecha < inicioMananaUtc
                    );

            // ======================================
            // CUENTAS POR COBRAR
            // ======================================

            var totalPorCobrar =
                await _context.MovimientosCuenta
                    .AsNoTracking()
                    .SumAsync(m =>
                        (decimal?)m.Monto
                    ) ?? 0;

            var clientesConDeuda =
                await _context.Clientes
                    .AsNoTracking()
                    .CountAsync(c =>
                        (
                            c.MovimientosCuenta
                                .Sum(m =>
                                    (decimal?)m.Monto
                                ) ?? 0
                        ) > 0
                    );

            var ventasPendientes =
                await _context.Ventas
                    .AsNoTracking()
                    .CountAsync(v =>
                        v.Estado == "Pendiente" ||
                        v.Estado == "Parcial"
                    );

            // ======================================
            // VENCIMIENTOS
            // ======================================
            //
            // FechaVencimiento también está
            // almacenada en UTC.
            //
            // Una fecha vence si es anterior
            // al inicio del día actual de CR.
            // ======================================

            var ventasVencidas =
                await _context.Ventas
                    .AsNoTracking()
                    .CountAsync(v =>
                        (v.Estado == "Pendiente" ||
                         v.Estado == "Parcial") &&

                        v.FechaVencimiento.HasValue &&

                        v.FechaVencimiento.Value <
                            inicioHoyUtc
                    );

            var vencenHoy =
                await _context.Ventas
                    .AsNoTracking()
                    .CountAsync(v =>
                        (v.Estado == "Pendiente" ||
                         v.Estado == "Parcial") &&

                        v.FechaVencimiento.HasValue &&

                        v.FechaVencimiento.Value >=
                            inicioHoyUtc &&

                        v.FechaVencimiento.Value <
                            inicioMananaUtc
                    );

            // ======================================
            // INVENTARIO
            // ======================================

            var productosStockBajo =
                await _context.Productos
                    .AsNoTracking()
                    .CountAsync(p =>
                        p.Activo &&
                        p.Stock <= p.StockMinimo
                    );

            var productosSinStock =
                await _context.Productos
                    .AsNoTracking()
                    .CountAsync(p =>
                        p.Activo &&
                        p.Stock <= 0
                    );

            // ======================================
            // CLIENTES
            // ======================================

            var totalClientes =
                await _context.Clientes
                    .AsNoTracking()
                    .CountAsync(c =>
                        c.Activo
                    );

            // ======================================
            // RESPUESTA
            // ======================================

            return Ok(new
            {
                fecha = hoyCostaRica,

                ventas = new
                {
                    hoy = ventasHoy,
                    cantidadHoy = cantidadVentasHoy,

                    mes = ventasMes,
                    cantidadMes = cantidadVentasMes
                },

                ganancias = new
                {
                    hoy = gananciaHoy,
                    mes = gananciaMes
                },

                pagos = new
                {
                    hoy = pagosHoy,
                    cantidadHoy = cantidadPagosHoy
                },

                cuentasPorCobrar = new
                {
                    total = totalPorCobrar,

                    clientesConDeuda,

                    ventasPendientes,

                    ventasVencidas,

                    vencenHoy
                },

                inventario = new
                {
                    stockBajo =
                        productosStockBajo,

                    sinStock =
                        productosSinStock
                },

                clientes = new
                {
                    total =
                        totalClientes
                }
            });
        }
    }
}