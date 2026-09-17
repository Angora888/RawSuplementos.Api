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

            var negocioActivo = await _context.Negocios.AsNoTracking().AnyAsync(n => n.Id == negocioId.Value && n.Activo);
            if (!negocioActivo) return Unauthorized("El negocio está desactivado o no existe.");

            var email = dto.Email.Trim().ToLowerInvariant();
            if (await _context.Usuarios.AnyAsync(u => u.Email == email))
                return BadRequest("Ya existe un usuario con ese correo.");

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
                usuario = new { usuario.Id, usuario.NegocioId, usuario.Nombre, usuario.Email, usuario.Rol, usuario.Activo, usuario.FechaCreacion }
            });
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email) || string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest("Debe ingresar correo y contraseña.");

            var email = dto.Email.Trim().ToLowerInvariant();

            if (EsSuperAdmin(email, dto.Password))
            {
                var tokenSuperAdmin = GenerarTokenSuperAdmin(email);
                return Ok(new
                {
                    token = tokenSuperAdmin,
                    usuario = new
                    {
                        Id = 0,
                        NegocioId = (int?)null,
                        Nombre = "Super Administrador",
                        Email = email,
                        Rol = "SuperAdmin",
                        negocio = (object?)null
                    }
                });
            }

            var usuario = await _context.Usuarios
                .AsNoTracking()
                .Include(u => u.Negocio)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (usuario == null) return Unauthorized("Correo o contraseña incorrectos.");
            if (!usuario.Activo) return Unauthorized("El usuario está desactivado.");
            if (usuario.Negocio == null || !usuario.Negocio.Activo) return Unauthorized("El negocio está desactivado.");
            if (!BCrypt.Net.BCrypt.Verify(dto.Password, usuario.PasswordHash)) return Unauthorized("Correo o contraseña incorrectos.");

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

        private bool EsSuperAdmin(string email, string password)
        {
            var configuredEmail = _configuration["SuperAdmin:Email"]?.Trim().ToLowerInvariant();
            var passwordHash = _configuration["SuperAdmin:PasswordHash"];

            if (string.IsNullOrWhiteSpace(configuredEmail) || string.IsNullOrWhiteSpace(passwordHash)) return false;
            if (!string.Equals(email, configuredEmail, StringComparison.OrdinalIgnoreCase)) return false;

            try
            {
                return BCrypt.Net.BCrypt.Verify(password, passwordHash);
            }
            catch
            {
                return false;
            }
        }

        private int? ObtenerNegocioId()
        {
            var claim = User.FindFirst("negocioId")?.Value;
            return int.TryParse(claim, out var negocioId) ? negocioId : null;
        }

        private string GenerarToken(Usuario usuario)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
                new Claim(ClaimTypes.Name, usuario.Nombre),
                new Claim(ClaimTypes.Email, usuario.Email),
                new Claim(ClaimTypes.Role, usuario.Rol),
                new Claim("negocioId", usuario.NegocioId.ToString()),
                new Claim("negocioSlug", usuario.Negocio.Slug)
            };

            return CrearToken(claims);
        }

        private string GenerarTokenSuperAdmin(string email)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, "superadmin"),
                new Claim(ClaimTypes.Name, "Super Administrador"),
                new Claim(ClaimTypes.Email, email),
                new Claim(ClaimTypes.Role, "SuperAdmin")
            };

            return CrearToken(claims);
        }

        private string CrearToken(IEnumerable<Claim> claims)
        {
            var jwtKey = _configuration["Jwt:Key"];
            var jwtIssuer = _configuration["Jwt:Issuer"];
            var jwtAudience = _configuration["Jwt:Audience"];

            if (string.IsNullOrWhiteSpace(jwtKey)) throw new InvalidOperationException("Jwt:Key no está configurado.");
            if (string.IsNullOrWhiteSpace(jwtIssuer)) throw new InvalidOperationException("Jwt:Issuer no está configurado.");
            if (string.IsNullOrWhiteSpace(jwtAudience)) throw new InvalidOperationException("Jwt:Audience no está configurado.");

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey));
            var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(jwtIssuer, jwtAudience, claims, expires: DateTime.UtcNow.AddHours(12), signingCredentials: credentials);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
