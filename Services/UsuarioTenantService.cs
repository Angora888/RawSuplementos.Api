using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Data;

namespace RawSuplementos.Api.Services
{
    public class UsuarioTenantService
    {
        private readonly ApplicationDbContext _context;

        public UsuarioTenantService(ApplicationDbContext context) => _context = context;

        public Task<bool> EsUsuarioActivoDelNegocioAsync(int usuarioId, int negocioId) =>
            _context.Usuarios.AsNoTracking().AnyAsync(u =>
                u.Id == usuarioId && u.NegocioId == negocioId && u.Activo);
    }
}
