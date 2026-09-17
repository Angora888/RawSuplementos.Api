using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using RawSuplementos.Api.Data;
using RawSuplementos.Api.DTOs;
using RawSuplementos.Api.Helpers;
using RawSuplementos.Api.Models;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace RawSuplementos.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("registrar")]
        public async Task<IActionResult> Registrar(RegistrarUsuarioDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Nombre)) return BadRequest("El nombre es obligatorio.");
            if (string.IsNullOrWhiteSpace(dto.Email)) return BadRequest("El correo es obligatorio.");
            if (string.IsNullOrWhiteSpace(dto.Password)) return BadRequest("La contraseña es obligatoria.");

            var negocioId = ObtenerNegocioId();
            if (negocioId == null) return Unauthorized("El token no contiene un negocio válido.");

            var negocioActivo = await _context.Negocios
                .AsNoTracking()
                .AnyAsync(n => n.Id == negocioId.Value && n.Activo);

            if (!negocioActivo) return Unauthorized("El negocio está desactivado o no existe.");

            var email = dto.Email.Trim().ToLowerInvariant();
            var existeUsuario = await _context.Usuarios.AnyAsync(u => u.Email == email);
            if (existeUsuario) return BadRequest("Ya existe un usuario con ese correo.");

            var rol = string.IsNullOrWhiteSpace(dto.Rol) ? "Vendedor" : dto.Rol.Trim();
            if (rol != "Admin" && rol != "Vendedor") return BadRequest("El rol debe ser Admin o Vendedor.");

            var usuario = new Usuario
            {
                NegocioId = negocioId.Value,
                Nombre = dto.Nombre.Trim(),
                Email = email,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(dto.Password),
                Rol = rol,
                Activo = true,
                FechaCreacion = FechaHelper.AhoraUtc()
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                mensaje = "Usuario creado correctamente.",
                usuario = new
                {
                    usuario.Id,
                    usuario.NegocioId,
                    usuario.Nombre,
                    usuario.Email,
                    usuario.Rol,
                    usuario.Activo,
                    usuario.FechaCreacion
                }
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Debe ingresar correo y contraseña.");

            var email = dto.Email.Trim().ToLowerInvariant();

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Negocio)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (usuario == null) return Unauthorized("Correo o contraseña incorrectos.");
            if (!usuario.Activo) return Unauthorized("El usuario está desactivado.");
            if (usuario.Negocio == null || !usuario.Negocio.Activo)
                return Unauthorized("El negocio está desactivado.");

            var passwordValido = BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash);
            if (!passwordValido) return Unauthorized("Correo o contraseña incorrectos.");

            var token = GenerarToken(usuario);

            return Ok(new
            {
                token,
                usuario = new
                {
                    usuario.Id,
                    usuario.NegocioId,
                    usuario.Nombre,
                    usuario.Email,
                    usuario.Rol,
                    negocio = new
                    {
                        usuario.Negocio.Id,
                        usuario.Negocio.Nombre,
                        usuario.Negocio.Slug,
                        usuario.Negocio.LogoUrl,
                        usuario.Negocio.WhatsApp
                    }
                }
            });
        }

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var negocioId) ? negocioId : null;
        }

        private string GenerarToken(Usuario usuario)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Jwt:Key no está configurado.");
            if (string.IsNullOrWhiteSpace(jwtIssuer)) throw new InvalidOperationException("Jwt:Issuer no está configurado.");
            if (string.IsNullOrWhiteSpace(jwtAudience)) throw new InvalidOperationException("Jwt:Audience no está configurado.");

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol),
                new Claim("negocioId", usuario.NegocioId.ToString()),
                new Claim("negocioSlug", usuario.Negocio.Slug)
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: jwtIssuer,
                audience: jwtAudience,
                claims: claims,
                expires: DateTime.UtcNow.AddHours(12),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}