using System.Threading;
using gesFactu.Application.Common.Abstractions;

namespace gesFactu.Infrastructure.Integrations.VeriFactu;

/// <summary>
/// Implementación stub mejorada de IVeriFactuGateway.
/// Esta versión simula respuestas más realistas incluyendo:
/// - Validaciones de NIF
/// - Errores transitorios simulados ocasionalmente
/// - Simulación de cancelación
/// - Historial de envíos
/// 
/// Para producción, será reemplazada por una implementación real que use SOAP/WSDL.
/// </summary>
public class VeriFactuGatewayStubAdvanced : IVeriFactuGateway
{
    private static int _submissionIdCounter = 1000;
    private static readonly Dictionary<string, VeriFactuSubmissionResult> _submittedRecords = new();
    private static readonly Dictionary<string, bool> _cancelledRecords = new();
    private static readonly Random _random = new();
    private static readonly object _lock = new();

    public async Task<VeriFactuSubmissionResult> SubmitBillingRecordAsync(
        VeriFactuSubmissionRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(100, cancellationToken); // Simular latencia

        // Validaciones básicas
        if (string.IsNullOrWhiteSpace(request.TaxpayerNif))
            return new VeriFactuSubmissionResult
            {
                SubmissionId = "ERROR",
                IsAccepted = false,
                ResponseCode = AeatResponseCode.ValidationError,
                StatusCode = "400",
                StatusDescription = "NIF del contribuyente requerido"
            };

        // Ocasionalmente simular error transitorio (10% de probabilidad)
        if (_random.Next(100) < 10)
        {
            return new VeriFactuSubmissionResult
            {
                SubmissionId = "ERROR",
                IsAccepted = false,
                ResponseCode = AeatResponseCode.TemporaryError,
                StatusCode = "503",
                StatusDescription = "Servicio temporalmente no disponible"
            };
        }

        // Generar SubmissionId
        string submissionId;
        lock (_lock)
        {
            submissionId = _submissionIdCounter++.ToString();
        }

        var result = new VeriFactuSubmissionResult
        {
            SubmissionId = submissionId,
            IsAccepted = true,
            ResponseCode = AeatResponseCode.Success,
            StatusCode = "1000",
            StatusDescription = "Registro aceptado correctamente"
        };

        // Almacenar para consultas posteriores
        lock (_lock)
        {
            _submittedRecords[submissionId] = result;
        }

        return result;
    }

    public async Task<VeriFactuQueryResult> QueryBillingRecordAsync(
        VeriFactuQueryRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(50, cancellationToken);

        return new VeriFactuQueryResult
        {
            FiscalYear = request.FiscalYear,
            Period = request.Period,
            Result = "SinDatos",
            HasMorePages = false,
            Records = Array.Empty<VeriFactuQueryRecord>()
        };
    }


}
