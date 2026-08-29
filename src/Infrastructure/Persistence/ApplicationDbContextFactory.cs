using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para ApplicationDbContext.
/// Permite a EF Core Tools crear migraciones sin necesidad de una aplicación ejecutable.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        // Cadena de conexión por defecto para desarrollo/migraciones
        // En producción, esto se configurará desde appsettings.json
        const string connectionString = 
            "Server=(localdb)\\mssqllocaldb;Database=gesFactuDb;Integrated Security=true;";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
