using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace gesFactu.Infrastructure.Persistence;

/// <summary>
/// Factory de diseño para ApplicationDbContext.
/// La credencial real nunca se guarda en Git: para tareas de EF puede
/// proporcionarse mediante GESFACTU_DESIGN_CONNECTION o --connection.
/// </summary>
public class ApplicationDbContextFactory : IDesignTimeDbContextFactory<ApplicationDbContext>
{
    public ApplicationDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("GESFACTU_DESIGN_CONNECTION")
            ?? "Host=localhost;Port=5432;Database=gesFactuDb;Username=gesfactu";

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseNpgsql(connectionString);

        return new ApplicationDbContext(optionsBuilder.Options);
    }
}
