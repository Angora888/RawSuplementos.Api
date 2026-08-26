using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace RawSuplementos.Api.Data
{
    public class ApplicationDbContextFactory
        : IDesignTimeDbContextFactory<ApplicationDbContext>
    {
        public ApplicationDbContext CreateDbContext(
            string[] args)
        {
            var configuration =
                new ConfigurationBuilder()
                    .SetBasePath(
                        Directory.GetCurrentDirectory()
                    )
                    .AddJsonFile(
                        "appsettings.json",
                        optional: true
                    )
                    .AddEnvironmentVariables()
                    .Build();

            var connectionString =
                configuration.GetConnectionString(
                    "DefaultConnection"
                );

            if (string.IsNullOrWhiteSpace(
                connectionString))
            {
                throw new InvalidOperationException(
                    "No se encontró la connection string DefaultConnection."
                );
            }

            var optionsBuilder =
                new DbContextOptionsBuilder<ApplicationDbContext>();

            optionsBuilder.UseNpgsql(
                connectionString
            );

            return new ApplicationDbContext(
                optionsBuilder.Options
            );
        }
    }
}