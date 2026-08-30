using gesFactu.Infrastructure.Integrations.VeriFactu;
using Microsoft.Extensions.Configuration;

namespace gesFactu.AeatE2ETests;

internal sealed class AeatE2ETestSettings
{
    public required VeriFactuOptions VeriFactu { get; init; }
    public required string RecipientNif { get; init; }
    public required string RecipientName { get; init; }

    public static AeatE2ETestSettings Load()
    {
        var configuration = new ConfigurationBuilder()
            // Comparte el mismo UserSecretsId que gesFactu.Api, por lo que
            // reutiliza la configuración TEST/certificado ya existente.
            .AddUserSecrets<AeatE2ETestSettings>(optional: true)
            .AddEnvironmentVariables()
            .Build();

        var options = new VeriFactuOptions();
        configuration
            .GetSection(VeriFactuOptions.SectionName)
            .Bind(options);

        var recipientNif =
            Environment.GetEnvironmentVariable(
                "GESFACTU_AEAT_E2E_RECIPIENT_NIF")
            ?? configuration["AeatE2E:RecipientNif"]
            ?? string.Empty;

        var recipientName =
            Environment.GetEnvironmentVariable(
                "GESFACTU_AEAT_E2E_RECIPIENT_NAME")
            ?? configuration["AeatE2E:RecipientName"]
            ?? string.Empty;

        return new AeatE2ETestSettings
        {
            VeriFactu = options,
            RecipientNif = recipientNif.Trim().ToUpperInvariant(),
            RecipientName = recipientName.Trim()
        };
    }

    public void ValidateSafety()
    {
        if (VeriFactu.Environment != VeriFactuEntorno.Test)
        {
            throw new InvalidOperationException(
                "BLOQUEO E2E: las pruebas automáticas solo pueden ejecutarse con VeriFactu:Environment=Test.");
        }

        if (VeriFactu.AllowProduction)
        {
            throw new InvalidOperationException(
                "BLOQUEO E2E: VeriFactu:AllowProduction debe ser false durante las pruebas automáticas.");
        }

        if (!string.Equals(
                VeriFactu.GetEndpoint(),
                VeriFactuOptions.EndpointTest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "BLOQUEO E2E: el endpoint resuelto no es el endpoint oficial de AEAT TEST.");
        }

        if (string.IsNullOrWhiteSpace(VeriFactu.Taxpayer.Nif) ||
            string.IsNullOrWhiteSpace(VeriFactu.Taxpayer.Name))
        {
            throw new InvalidOperationException(
                "Falta VeriFactu:Taxpayer:Nif/Name en User Secrets.");
        }

        if (string.IsNullOrWhiteSpace(VeriFactu.Certificate.Thumbprint))
        {
            throw new InvalidOperationException(
                "AEAT E2E requiere VeriFactu:Certificate:Thumbprint en User Secrets y el certificado instalado en CurrentUser/My.");
        }

        if (string.IsNullOrWhiteSpace(RecipientNif) ||
            RecipientNif.Length != 9 ||
            string.IsNullOrWhiteSpace(RecipientName))
        {
            throw new InvalidOperationException(
                "Configure AeatE2E:RecipientNif (9 caracteres) y AeatE2E:RecipientName en User Secrets.");
        }

        if (string.Equals(
                VeriFactu.Taxpayer.Nif.Trim(),
                RecipientNif,
                StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "El destinatario E2E debe ser distinto del obligado tributario.");
        }
    }
}
