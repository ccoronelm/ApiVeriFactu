namespace gesFactu.Domain.ValueObjects;

/// <summary>
/// Resultado de una operación de validación en Value Objects.
/// Los Value Objects pueden retornar errores sin depender de Application.
/// </summary>
public abstract record ValueObjectResult
{
    public sealed record Success : ValueObjectResult;
    public sealed record ValidationError(string PropertyName, string Message) : ValueObjectResult;
}

public abstract record ValueObjectResult<T> : ValueObjectResult
{
    public sealed record SuccessWithValue(T Value) : ValueObjectResult<T>;
    public new sealed record ValidationError(string PropertyName, string Message) : ValueObjectResult<T>;
}

/// <summary>
/// NIF/CIF del contribuyente.
/// Value Object que encapsula validaciones básicas de formato.
/// </summary>
public record TaxpayerNif
{
    public string Value { get; }

    private TaxpayerNif(string value)
    {
        Value = value;
    }

    public static ValueObjectResult<TaxpayerNif> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ValueObjectResult<TaxpayerNif>.ValidationError(nameof(TaxpayerNif), "El NIF/CIF no puede estar vacío");

        var trimmed = value.Trim().ToUpperInvariant();

        // El XSD oficial AEAT define NIFType con longitud exacta de 9 caracteres.
        if (trimmed.Length != 9)
            return new ValueObjectResult<TaxpayerNif>.ValidationError(nameof(TaxpayerNif), "El NIF debe tener exactamente 9 caracteres");

        return new ValueObjectResult<TaxpayerNif>.SuccessWithValue(new TaxpayerNif(trimmed));
    }
}

/// <summary>
/// Número de serie de factura (p.ej., "12345678-G66").
/// </summary>
public record InvoiceSeries
{
    public string Value { get; }

    private InvoiceSeries(string value)
    {
        Value = value;
    }

    public static ValueObjectResult<InvoiceSeries> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ValueObjectResult<InvoiceSeries>.ValidationError(nameof(InvoiceSeries), "La serie de factura no puede estar vacía");

        var trimmed = value.Trim();

        if (trimmed.Length > 60)
            return new ValueObjectResult<InvoiceSeries>.ValidationError(nameof(InvoiceSeries), "La serie de factura no puede superar 60 caracteres");

        return new ValueObjectResult<InvoiceSeries>.SuccessWithValue(new InvoiceSeries(trimmed));
    }
}

/// <summary>
/// Número de factura dentro de la serie.
/// </summary>
public record InvoiceNumber
{
    public string Value { get; }

    private InvoiceNumber(string value)
    {
        Value = value;
    }

    public static ValueObjectResult<InvoiceNumber> Create(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return new ValueObjectResult<InvoiceNumber>.ValidationError(nameof(InvoiceNumber), "El número de factura no puede estar vacío");

        var trimmed = value.Trim();

        if (trimmed.Length > 60)
            return new ValueObjectResult<InvoiceNumber>.ValidationError(nameof(InvoiceNumber), "El número de factura no puede superar 60 caracteres");

        return new ValueObjectResult<InvoiceNumber>.SuccessWithValue(new InvoiceNumber(trimmed));
    }
}

/// <summary>
/// Identificador único de una factura dentro de un contribuyente.
/// Compuesto por NIF, Serie y Número.
/// </summary>
public record InvoiceIdentifier
{
    public TaxpayerNif IssuerNif { get; }
    public InvoiceSeries Series { get; }
    public InvoiceNumber Number { get; }
    public DateOnly IssueDate { get; }

    private InvoiceIdentifier(TaxpayerNif issuerNif, InvoiceSeries series, InvoiceNumber number, DateOnly issueDate)
    {
        IssuerNif = issuerNif;
        Series = series;
        Number = number;
        IssueDate = issueDate;
    }

    public static ValueObjectResult<InvoiceIdentifier> Create(
        TaxpayerNif issuerNif,
        InvoiceSeries series,
        InvoiceNumber number,
        DateOnly issueDate)
    {
        if (issuerNif == null)
            return new ValueObjectResult<InvoiceIdentifier>.ValidationError(nameof(issuerNif), "El NIF del emisor es requerido");
        if (series == null)
            return new ValueObjectResult<InvoiceIdentifier>.ValidationError(nameof(series), "La serie de factura es requerida");
        if (number == null)
            return new ValueObjectResult<InvoiceIdentifier>.ValidationError(nameof(number), "El número de factura es requerido");

        return new ValueObjectResult<InvoiceIdentifier>.SuccessWithValue(
            new InvoiceIdentifier(issuerNif, series, number, issueDate));
    }
}

/// <summary>
/// Importe monetario en EUR.
/// Siempre usa decimal para precisión fiscal.
/// </summary>
public record Money
{
    public decimal Amount { get; }

    private Money(decimal amount)
    {
        Amount = amount;
    }

    public static ValueObjectResult<Money> Create(decimal amount)
    {
        if (amount < 0)
            return new ValueObjectResult<Money>.ValidationError(nameof(Money), "El importe no puede ser negativo");

        return CreateSigned(amount);
    }

    /// <summary>
    /// Crea un importe con signo. VERI*FACTU admite importes negativos,
    /// entre otros casos, en facturas rectificativas.
    /// </summary>
    public static ValueObjectResult<Money> CreateSigned(decimal amount)
    {
        if (decimal.Round(amount, 2) != amount)
            return new ValueObjectResult<Money>.ValidationError(
                nameof(Money),
                "El importe no puede tener más de 2 decimales");

        return new ValueObjectResult<Money>.SuccessWithValue(new Money(amount));
    }
}

/// <summary>
/// Porcentaje de IVA (4, 10, 21, etc.).
/// </summary>
public record TaxRate
{
    public decimal Percentage { get; }

    private TaxRate(decimal percentage)
    {
        Percentage = percentage;
    }

    public static ValueObjectResult<TaxRate> Create(decimal percentage)
    {
        if (percentage < 0 || percentage > 100)
            return new ValueObjectResult<TaxRate>.ValidationError(nameof(TaxRate), "El porcentaje debe estar entre 0 y 100");

        // Máximo 2 decimales
        if (decimal.Round(percentage, 2) != percentage)
            return new ValueObjectResult<TaxRate>.ValidationError(nameof(TaxRate), "El porcentaje no puede tener más de 2 decimales");

        return new ValueObjectResult<TaxRate>.SuccessWithValue(new TaxRate(percentage));
    }
}

