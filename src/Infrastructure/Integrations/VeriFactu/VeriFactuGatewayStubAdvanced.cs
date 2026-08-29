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
        await Task.Delay(50, cancellationToken); // Simular latencia

        lock (_lock)
        {
            if (_submittedRecords.TryGetValue(request.SubmissionId, out _))
            {
                var isCancelled = _cancelledRecords.ContainsKey(request.SubmissionId);
                return new VeriFactuQueryResult
                {
                    SubmissionId = request.SubmissionId,
                    Status = isCancelled ? "Cancelado" : "Aceptado",
                    StatusDescription = isCancelled ? "Registro cancelado" : "Registro aceptado correctamente"
                };
            }
        }

        return new VeriFactuQueryResult
        {
            SubmissionId = request.SubmissionId,
            Status = "NoEncontrado",
            StatusDescription = "El registro no fue encontrado en AEAT"
        };
    }

    public async Task<VeriFactuCancellationResult> CancelBillingRecordAsync(
        VeriFactuCancellationRequest request,
        CancellationToken cancellationToken)
    {
        await Task.Delay(150, cancellationToken); // Simular latencia

        lock (_lock)
        {
            if (!_submittedRecords.TryGetValue(request.SubmissionId, out _))
            {
                return new VeriFactuCancellationResult
                {
                    IsAccepted = false,
                    StatusCode = "404",
                    StatusDescription = "El registro no fue encontrado para cancelar"
                };
            }

            if (_cancelledRecords.ContainsKey(request.SubmissionId))
            {
                return new VeriFactuCancellationResult
                {
                    IsAccepted = false,
                    StatusCode = "409",
                    StatusDescription = "El registro ya fue cancelado previamente"
                };
            }

            // Simular cancelación exitosa
            _cancelledRecords[request.SubmissionId] = true;

            var cancellationId = Guid.NewGuid().ToString("N").Substring(0, 20);

            return new VeriFactuCancellationResult
            {
                IsAccepted = true,
                StatusCode = "1000",
                StatusDescription = "Cancelación aceptada correctamente",
                CancellationId = cancellationId
            };
        }
    }

    /// <summary>
    /// Reset para pruebas (limpiar estado).
    /// </summary>
    public static void Reset()
    {
        lock (_lock)
        {
            _submissionIdCounter = 1000;
            _submittedRecords.Clear();
            _cancelledRecords.Clear();
        }
    }
}
