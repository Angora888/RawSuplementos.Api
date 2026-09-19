using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.Helpers;

namespace RawSuplementos.Api.Services
{
    public class VentaConsultaService
    {
        private readonly ApplicationDbContext _context;
        public VentaConsultaService(ApplicationDbContext context) => _context = context;

        public async Task<object> ObtenerVentasAsync(int negocioId, int? clienteId, string? estado, DateTime? fechaDesde, DateTime? fechaHasta)
        {
            var query = _context.Ventas.AsNoTracking().Where(v => v.NegocioId == negocioId).AsQueryable();
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
            return new { cantidad = ventas.Count, totalVentas = ventas.Where(v => v.Estado != "Anulada").Sum(v => v.Total), ventas };
        }

        public async Task<object?> ObtenerVentaAsync(int id, int negocioId) =>
            await _context.Ventas.AsNoTracking().Where(v => v.Id == id && v.NegocioId == negocioId).Select(v => new
            {
                v.Id, Cliente = new { v.Cliente.Id, v.Cliente.Nombre, v.Cliente.Telefono, v.Cliente.Direccion },
                Usuario = new { v.Usuario.Id, v.Usuario.Nombre }, v.Fecha, v.FechaVencimiento, v.Subtotal, v.Descuento, v.Total, v.Estado, v.Notas,
                Detalles = v.Detalles.Select(d => new { d.Id, d.ProductoId, Producto = d.Producto.Nombre, Marca = d.Producto.Marca, d.Cantidad, d.PrecioUnitario, d.CostoUnitario, d.Subtotal, Ganancia = (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad }).ToList(),
                Pagos = v.Pagos.OrderByDescending(p => p.Fecha).Select(p => new { p.Id, p.Monto, p.Fecha, p.MetodoPago, p.Referencia, p.Notas, RegistradoPor = p.Usuario.Nombre }).ToList(),
                TotalPagado = v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0,
                Pendiente = v.Total - (v.Pagos.Sum(p => (decimal?)p.Monto) ?? 0),
                Ganancia = v.Detalles.Sum(d => (d.PrecioUnitario - d.CostoUnitario) * d.Cantidad)
            }).FirstOrDefaultAsync();
    }
}
