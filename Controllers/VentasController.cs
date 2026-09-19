using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Services;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class VentasController : ControllerBase
    {
        private readonly UsuarioTenantService _usuarioTenantService;
        private readonly VentaService _ventaService;
        private readonly VentaConsultaService _ventaConsultaService;

        public VentasController(UsuarioTenantService usuarioTenantService, VentaService ventaService, VentaConsultaService ventaConsultaService)
        {
            _usuarioTenantService = usuarioTenantService;
            _ventaService = ventaService;
            _ventaConsultaService = ventaConsultaService;
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

            try
            {
                var resultado = await _ventaService.AnularAsync(ventaId, dto, negocioId.Value, usuarioId.Value);
                if (resultado.NotFound) return NotFound(resultado.Error);
                return resultado.Ok ? Ok(resultado.Data) : BadRequest(resultado.Error);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al anular la venta.");
            }
        }

        [HttpGet]
        public async Task<IActionResult> ObtenerVentas([FromQuery] int? clienteId, [FromQuery] string? estado, [FromQuery] DateTime? fechaDesde, [FromQuery] DateTime? fechaHasta)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            return Ok(await _ventaConsultaService.ObtenerVentasAsync(negocioId.Value, clienteId, estado, fechaDesde, fechaHasta));
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> ObtenerVenta(int id)
        {
            var negocioId = User.ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");
            var venta = await _ventaConsultaService.ObtenerVentaAsync(id, negocioId.Value);
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

            try
            {
                var resultado = await _ventaService.RegistrarAbonoAsync(ventaId, dto, negocioId.Value, usuarioId.Value);
                if (resultado.NotFound) return NotFound(resultado.Error);
                return resultado.Ok ? Ok(resultado.Data) : BadRequest(resultado.Error);
            }
            catch (Exception)
            {
                return StatusCode(500, "Ocurrió un error al registrar el abono.");
            }
        }
    }
}