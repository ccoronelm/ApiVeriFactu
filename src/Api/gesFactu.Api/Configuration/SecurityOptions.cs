namespace gesFactu.Api.Configuration;

public sealed class SecurityOptions
{
    public const string SectionName = "Security";

    /// <summary>Clave servidor-a-servidor. Preferir secret manager.</summary>
    public string? ApiKey { get; set; }

    /// <summary>Ruta opcional a un Docker/Kubernetes secret montado como fichero.</summary>
    public string? ApiKeyFile { get; set; }

    public string ResolveApiKey()
        => SecretValueResolver.Resolve(ApiKey, ApiKeyFile);
}
