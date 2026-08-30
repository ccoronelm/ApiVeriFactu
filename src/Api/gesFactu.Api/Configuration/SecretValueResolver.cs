namespace gesFactu.Api.Configuration;

internal static class SecretValueResolver
{
    public static string Resolve(string? directValue, string? filePath)
    {
        if (!string.IsNullOrWhiteSpace(directValue))
            return directValue.Trim();

        if (string.IsNullOrWhiteSpace(filePath))
            return string.Empty;

        var path = filePath.Trim();
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"No existe el fichero de secreto configurado: {path}");

        return File.ReadAllText(path).Trim();
    }
}
