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
    public class CuentasPorCobrarController : ControllerBase
    {
        private readonly ApplicationDbContext _context;

        public CuentasPorCobrarController(ApplicationDbContext context)
        {
            _context = context;
        }

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var negocioId) ? negocioId : null;
        }

        private static (DateTime InicioHoyUtc, DateTime InicioMananaUtc, DateTime InicioCuatroDiasUtc) ObtenerLimitesCobro()
        {
            var hoyCostaRica = FechaHelper.HoyCostaRica();
            return (FechaHelper.CostaRicaAUtc(hoyCostaRica), FechaHelper.CostaRicaAUtc(hoyCostaRica.AddDays(1)), FechaHelper.CostaRicaAUtc(hoyCostaRica.AddDays(4)));
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerCuentasPorCobrar()
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var (inicioHoyUtc, inicioMananaUtc, _) = ObtenerLimitesCobro();

            var clientes = await _context.Clientes.AsNoTracking()
                .Where(c => c.NegocioId == negocioId.Value && c.Activo)
                .Select(c => new
                {
                    c.Id, c.Nombre, c.Telefono,
                    Saldo = c.MovimientosCuenta.Sum(m => (decimal?)m.Monto) ?? 0,
                    VentasPendientes = c.Ventas.Count(v => v.Estado == "Pendiente" || v.Estado == "Parcial"),
                    VentasVencidas = c.Ventas.Count(v => (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue && v.FechaVencimiento.Value < inicioHoyUtc),
                    VencenHoy = c.Ventas.Count(v => (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue && v.FechaVencimiento.Value >= inicioHoyUtc && v.FechaVencimiento.Value < inicioMananaUtc),
                    ProximoVencimiento = c.Ventas.Where(v => (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue).OrderBy(v => v.FechaVencimiento).Select(v => v.FechaVencimiento).FirstOrDefault()
                })
                .Where(c => c.Saldo > 0)
                .OrderByDescending(c => c.VentasVencidas).ThenByDescending(c => c.VencenHoy).ThenByDescending(c => c.Saldo)
                .ToListAsync();

            return Ok(new { totalPorCobrar = clientes.Sum(c => c.Saldo), cantidadClientes = clientes.Count, clientes });
        }

        [HttpGet("cliente/{clienteId:int}")]
        public async Task<IActionResult> ObtenerCuentaCliente(int clienteId)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var (inicioHoyUtc, inicioMananaUtc, inicioCuatroDiasUtc) = ObtenerLimitesCobro();

            var cliente = await _context.Clientes.AsNoTracking()
                .Where(c => c.Id == clienteId && c.NegocioId == negocioId.Value)
                .Select(c => new { c.Id, c.Nombre, c.Telefono, c.Direccion, c.Notas, c.Activo, Saldo = c.MovimientosCuenta.Sum(m => (decimal?)m.Monto) ?? 0 })
                .FirstOrDefaultAsync();
            if (cliente == null) return NotFound("Cliente no encontrado.");

            var ventas = await _context.Ventas.AsNoTracking()
                .Where(v => v.NegocioId == negocioId.Value && v.ClienteId == clienteId && (v.Estado == "Pendiente" || v.Estado == "Parcial"))
                .OrderBy(v => v.FechaVencimiento).ThenBy(v => v.Fecha)
                .Select(v => new
                {
                    v.Id, v.Fecha, v.FechaVencimiento, v.Subtotal, v.Descuento, v.Total, v.Estado, v.Notas,
                    Pagado = v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0,
                    Pendiente = v.Total - (v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0),
                    EstadoCobro = !v.FechaVencimiento.HasValue ? "SinFecha" : v.FechaVencimiento.Value < inicioHoyUtc ? "Vencida" : v.FechaVencimiento.Value < inicioMananaUtc ? "VenceHoy" : v.FechaVencimiento.Value < inicioCuatroDiasUtc ? "VencePronto" : "AlDia",
                    DiasAtraso = v.FechaVencimiento.HasValue && v.FechaVencimiento.Value < inicioHoyUtc ? (inicioHoyUtc - v.FechaVencimiento.Value).Days : 0,
                    DiasParaVencer = v.FechaVencimiento.HasValue && v.FechaVencimiento.Value >= inicioMananaUtc ? (v.FechaVencimiento.Value - inicioHoyUtc).Days : 0
                }).ToListAsync();

            return Ok(new { cliente, ventasPendientes = ventas.Count, ventasVencidas = ventas.Count(v => v.EstadoCobro == "Vencida"), ventasVenceHoy = ventas.Count(v => v.EstadoCobro == "VenceHoy"), ventas });
        }

        [HttpGet("venta/{ventaId:int}")]
        public async Task<IActionResult> ObtenerDetalleVentaPendiente(int ventaId)
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var venta = await _context.Ventas.AsNoTracking()
                .Where(v => v.Id == ventaId && v.NegocioId == negocioId.Value)
                .Select(v => new
                {
                    v.Id,
                    Cliente = new { v.Cliente.Id, v.Cliente.Nombre, v.Cliente.Telefono, v.Cliente.Direccion },
                    v.Fecha, v.FechaVencimiento, v.Subtotal, v.Descuento, v.Total, v.Estado, v.Notas,
                    Detalles = v.Detalles.Select(d => new { d.ProductoId, Producto = d.Producto.Nombre, Marca = d.Producto.Marca, d.Cantidad, d.PrecioUnitario, d.Subtotal }).ToList(),
                    Pagos = v.Pagos.OrderByDescending(p => p.Fecha).Select(p => new { p.Id, p.Monto, p.Fecha, p.MetodoPago, p.Referencia, p.Notas, RegistradoPor = p.Usuario.Nombre }).ToList(),
                    TotalPagado = v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0,
                    Pendiente = v.Total - (v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0)
                }).FirstOrDefaultAsync();
            if (venta == null) return NotFound("Venta no encontrada.");

            var (inicioHoyUtc, inicioMananaUtc, inicioCuatroDiasUtc) = ObtenerLimitesCobro();
            string estadoCobro;
            int diasAtraso = 0, diasParaVencer = 0;
            if (!venta.FechaVencimiento.HasValue) estadoCobro = "SinFecha";
            else if (venta.FechaVencimiento.Value < inicioHoyUtc) { estadoCobro = "Vencida"; diasAtraso = (inicioHoyUtc - venta.FechaVencimiento.Value).Days; }
            else if (venta.FechaVencimiento.Value < inicioMananaUtc) estadoCobro = "VenceHoy";
            else if (venta.FechaVencimiento.Value < inicioCuatroDiasUtc) { estadoCobro = "VencePronto"; diasParaVencer = (venta.FechaVencimiento.Value - inicioHoyUtc).Days; }
            else { estadoCobro = "AlDia"; diasParaVencer = (venta.FechaVencimiento.Value - inicioHoyUtc).Days; }

            return Ok(new { venta.Id, venta.Cliente, venta.Fecha, venta.FechaVencimiento, venta.Subtotal, venta.Descuento, venta.Total, venta.TotalPagado, venta.Pendiente, venta.Estado, EstadoCobro = estadoCobro, DiasAtraso = diasAtraso, DiasParaVencer = diasParaVencer, venta.Notas, venta.Detalles, venta.Pagos });
        }

        [HttpGet("resumen")]
        public async Task<IActionResult> ObtenerResumen()
        {
            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var (inicioHoyUtc, inicioMananaUtc, inicioCuatroDiasUtc) = ObtenerLimitesCobro();

            var totalPorCobrar = await _context.MovimientosCuenta.Where(m => m.Cliente.NegocioId == negocioId.Value).SumAsync(m => (decimal?)m.Monto) ?? 0;
            var clientesConDeuda = await _context.Clientes.CountAsync(c => c.NegocioId == negocioId.Value && (c.MovimientosCuenta.Sum(m => (decimal?)m.Monto) ?? 0) > 0);
            var ventasPendientes = await _context.Ventas.CountAsync(v => v.NegocioId == negocioId.Value && (v.Estado == "Pendiente" || v.Estado == "Parcial"));
            var ventasVencidas = await _context.Ventas.CountAsync(v => v.NegocioId == negocioId.Value && (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue && v.FechaVencimiento.Value < inicioHoyUtc);
            var vencenHoy = await _context.Ventas.CountAsync(v => v.NegocioId == negocioId.Value && (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue && v.FechaVencimiento.Value >= inicioHoyUtc && v.FechaVencimiento.Value < inicioMananaUtc);
            var vencenProximos3Dias = await _context.Ventas.CountAsync(v => v.NegocioId == negocioId.Value && (v.Estado == "Pendiente" || v.Estado == "Parcial") && v.FechaVencimiento.HasValue && v.FechaVencimiento.Value >= inicioMananaUtc && v.FechaVencimiento.Value < inicioCuatroDiasUtc);

            return Ok(new { totalPorCobrar, clientesConDeuda, ventasPendientes, ventasVencidas, vencenHoy, vencenProximos3Dias });
        }
    }
}