using Microsoft.EntityFrameworkCore;
using RawSuplementos.Api.Models;

namespace RawSuplementos.Api.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options) { }

        public DbSet<Negocio> Negocios { get; set; }
        public DbSet<Usuario> Usuarios { get; set; }
        public DbSet<Cliente> Clientes { get; set; }
        public DbSet<Categoria> Categorias { get; set; }
        public DbSet<Producto> Productos { get; set; }
        public DbSet<Venta> Ventas { get; set; }
        public DbSet<VentaDetalle> VentaDetalles { get; set; }
        public DbSet<Pago> Pagos { get; set; }
        public DbSet<MovimientoCuenta> MovimientosCuenta { get; set; }
        public DbSet<MovimientoInventario> MovimientosInventario { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Negocio>().HasIndex(n => n.Slug).IsUnique();

            modelBuilder.Entity<Usuario>().HasIndex(u => u.Email).IsUnique();
            modelBuilder.Entity<Usuario>().Property(u => u.Email).HasMaxLength(150);
            modelBuilder.Entity<Usuario>().Property(u => u.Rol).HasMaxLength(30);
            modelBuilder.Entity<Usuario>().HasOne(u => u.Negocio).WithMany(n => n.Usuarios).HasForeignKey(u => u.NegocioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Cliente>().HasIndex(c => new { c.NegocioId, c.Telefono });
            modelBuilder.Entity<Cliente>().Property(c => c.Nombre).HasMaxLength(150);
            modelBuilder.Entity<Cliente>().Property(c => c.Telefono).HasMaxLength(20);
            modelBuilder.Entity<Cliente>().HasOne(c => c.Negocio).WithMany().HasForeignKey(c => c.NegocioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Categoria>().HasIndex(c => new { c.NegocioId, c.Nombre });
            modelBuilder.Entity<Categoria>().HasOne(c => c.Negocio).WithMany(n => n.Categorias).HasForeignKey(c => c.NegocioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Producto>().Property(p => p.PrecioCompra).HasPrecision(18, 2);
            modelBuilder.Entity<Producto>().Property(p => p.PrecioVenta).HasPrecision(18, 2);
            modelBuilder.Entity<Producto>().HasOne(p => p.Negocio).WithMany().HasForeignKey(p => p.NegocioId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Producto>().HasOne(p => p.Categoria).WithMany(c => c.Productos).HasForeignKey(p => p.CategoriaId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Venta>().Property(v => v.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<Venta>().Property(v => v.Descuento).HasPrecision(18, 2);
            modelBuilder.Entity<Venta>().Property(v => v.Total).HasPrecision(18, 2);
            modelBuilder.Entity<Venta>().HasOne(v => v.Negocio).WithMany().HasForeignKey(v => v.NegocioId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Venta>().HasOne(v => v.Cliente).WithMany(c => c.Ventas).HasForeignKey(v => v.ClienteId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<Venta>().HasOne(v => v.Usuario).WithMany(u => u.Ventas).HasForeignKey(v => v.UsuarioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<VentaDetalle>().Property(d => d.PrecioUnitario).HasPrecision(18, 2);
            modelBuilder.Entity<VentaDetalle>().Property(d => d.Subtotal).HasPrecision(18, 2);
            modelBuilder.Entity<VentaDetalle>().HasOne(d => d.Venta).WithMany(v => v.Detalles).HasForeignKey(d => d.VentaId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<VentaDetalle>().HasOne(d => d.Producto).WithMany(p => p.VentaDetalles).HasForeignKey(d => d.ProductoId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Pago>().Property(p => p.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<Pago>().HasOne(p => p.Venta).WithMany(v => v.Pagos).HasForeignKey(p => p.VentaId).OnDelete(DeleteBehavior.Cascade);
            modelBuilder.Entity<Pago>().HasOne(p => p.Usuario).WithMany(u => u.PagosRegistrados).HasForeignKey(p => p.UsuarioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoCuenta>().Property(m => m.Monto).HasPrecision(18, 2);
            modelBuilder.Entity<MovimientoCuenta>().HasOne(m => m.Cliente).WithMany(c => c.MovimientosCuenta).HasForeignKey(m => m.ClienteId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MovimientoCuenta>().HasOne(m => m.Venta).WithMany().HasForeignKey(m => m.VentaId).OnDelete(DeleteBehavior.SetNull);
            modelBuilder.Entity<MovimientoCuenta>().HasOne(m => m.Usuario).WithMany(u => u.MovimientosCuenta).HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<MovimientoInventario>().HasOne(m => m.Producto).WithMany(p => p.MovimientosInventario).HasForeignKey(m => m.ProductoId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MovimientoInventario>().HasOne(m => m.Usuario).WithMany(u => u.MovimientosInventario).HasForeignKey(m => m.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            modelBuilder.Entity<MovimientoInventario>().HasOne(m => m.Venta).WithMany(v => v.MovimientosInventario).HasForeignKey(m => m.VentaId).OnDelete(DeleteBehavior.SetNull);
        }
    }
}