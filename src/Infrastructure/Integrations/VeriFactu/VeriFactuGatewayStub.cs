using Microsoft.Extensions.Logging;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Implementación stub/mock de IVeriFactuGateway para desarrollo y testing.
/// 
/// En producción, esta clase será reemplazada por una que hable SOAP real con AEAT.
/// 
/// Ref: /VERIFACTU - Estructura oficial de solicitudes/respuestas
/// </summary>
public sealed class VeriFactuGatewayStub : IVeriFactuGateway
{
    private readonly ILogger<VeriFactuGatewayStub> _logger;
    private static int _submissionIdCounter = 1000;

    public VeriFactuGatewayStub(ILogger<VeriFactuGatewayStub> logger)
    {
        _logger = logger;
    }

    public async Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "STUB: Enviando registro de facturación a AEAT para NIF {TaxpayerNif}. " +
            "En producción esto haría llamada SOAP real.",
            request.TaxpayerNif);

        // Simular latencia de red
        await Task.Delay(100, cancellationToken);

        // Generar ID de envío simulado
        var submissionId = $"STUB-{DateOnly.FromDateTime(DateTime.UtcNow):yyyyMMdd}-{Interlocked.Increment(ref _submissionIdCounter)}";

        var result = new VeriFactuSubmissionResult
        {
            SubmissionId = submissionId,
            IsAccepted = true,
            StatusCode = "1000",  // Código éxito AEAT simulado
            StatusDescription = "Registro enviado correctamente (STUB)",
            AdditionalDetails = "Mock implementation - not sent to real AEAT",
            ServerTimestamp = DateTime.UtcNow
        };

        _logger.LogInformation(
            "STUB: Registro enviado exitosamente. SubmissionId: {SubmissionId}",
            submissionId);

        return result;
    }

    public async Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        _logger.LogInformation(
            "STUB: Consultando estado de envío {SubmissionId} para NIF {TaxpayerNif}",
            request.SubmissionId,
            request.TaxpayerNif);

        // Simular latencia
        await Task.Delay(100, cancellationToken);

        var result = new VeriFactuQueryResult
        {
            SubmissionId = request.SubmissionId,
            Status = "Aceptado",
            StatusDescription = "El registro ha sido aceptado por AEAT (STUB)",
            AdditionalDetails = "Mock implementation"
        };

        return result;
    }
}
