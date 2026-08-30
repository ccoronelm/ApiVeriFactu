using Xunit;

namespace gesFactu.AeatE2ETests;

/// <summary>
/// Evita llamadas reales a AEAT salvo activación explícita.
/// Los tests E2E nunca se ejecutan por accidente en CI/cloud.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false)]
public sealed class AeatE2EFactAttribute : FactAttribute
{
    public AeatE2EFactAttribute()
    {
        var enabled = Environment.GetEnvironmentVariable(
            "GESFACTU_RUN_AEAT_E2E");

        if (!string.Equals(
                enabled,
                "true",
                StringComparison.OrdinalIgnoreCase))
        {
            Skip =
                "AEAT E2E desactivado. Establezca GESFACTU_RUN_AEAT_E2E=true para ejecutar contra AEAT TEST.";
        }
    }
}
