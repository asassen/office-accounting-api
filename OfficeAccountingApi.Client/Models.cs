using System.Text.Json;
using System.Text.Json.Serialization;

namespace OfficeAccounting.Client;

public sealed record CompaniesResult(string? Default, IReadOnlyList<string> Companies);

public sealed record HealthResult(string Status, string Company);

/// <summary>A row from GET /api/customers (raw SDK view, so only the useful columns are mapped).</summary>
public sealed class CustomerSummary
{
    public Guid Guid { get; set; }
    public string Name { get; set; } = "";
    public string? DisplayNumber { get; set; }
    public bool Active { get; set; }
    public bool IsSuspended { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? City { get; set; }
    public decimal Balance { get; set; }
    public string? CurrencySymbol { get; set; }
    public string? CurrencyCode { get; set; }
}

/// <summary>Full customer, returned by GET/POST/PUT /api/customers/{guid}.</summary>
public sealed class Customer
{
    public Guid Guid { get; set; }
    public int AccountId { get; set; }
    public string? DisplayNumber { get; set; }
    public string Name { get; set; } = "";
    public bool Active { get; set; }
    public bool Suspended { get; set; }
    public string? Comments { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public decimal Balance { get; set; }
    public decimal Overdue { get; set; }
    public decimal SalesYtd { get; set; }
}

/// <summary>Fields to create or change. Null means "leave unchanged".</summary>
public sealed class CustomerUpdate
{
    public string? Name { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? Comments { get; set; }
    public bool? Active { get; set; }
}

public sealed class OfficeAccountingException : Exception
{
    public System.Net.HttpStatusCode StatusCode { get; }
    public OfficeAccountingException(System.Net.HttpStatusCode status, string message) : base(message) => StatusCode = status;
}

internal static class Json
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNameCaseInsensitive = true,
    };
}
