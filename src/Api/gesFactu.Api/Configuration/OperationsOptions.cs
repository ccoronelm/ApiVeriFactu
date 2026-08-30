namespace gesFactu.Api.Configuration;

public sealed class OperationsOptions
{
    public const string SectionName = "Operations";

    /// <summary>
    /// Secreto operativo para endpoints de recuperación. Debe suministrarse
    /// mediante variable de entorno/User Secrets/secret manager, nunca en Git.
    /// </summary>
    public string? AdminApiKey { get; set; }
}
