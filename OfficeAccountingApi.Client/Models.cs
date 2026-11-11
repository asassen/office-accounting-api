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

public class NewInvoiceLine
{
    public Guid ItemGuid { get; set; }
    public decimal Quantity { get; set; } = 1;
    /// <summary>Defaults to the item's price when null.</summary>
    public decimal? UnitPrice { get; set; }
    public string? Description { get; set; }
}

public class NewInvoice
{
    public Guid CustomerGuid { get; set; }
    /// <summary>Must fall in an open financial year; defaults to today.</summary>
    public DateTime? Date { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Reference { get; set; }
    public List<NewInvoiceLine> Lines { get; set; } = new();
}

public class InvoiceLine
{
    public string? ItemName { get; set; }
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal SubTotal { get; set; }
}

public class Invoice
{
    public Guid Guid { get; set; }
    public string? Number { get; set; }
    public string? Reference { get; set; }
    public Guid? CustomerGuid { get; set; }
    public string? CustomerName { get; set; }
    public DateTime Date { get; set; }
    public DateTime DueDate { get; set; }
    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal Total { get; set; }
    public decimal Balance { get; set; }
    public bool Voided { get; set; }
    public List<InvoiceLine> Lines { get; set; } = new();
}

/// <summary>One row of the invoice list.</summary>
public class InvoiceSummary
{
    public Guid Guid { get; set; }
    public string? DocumentDisplayNumber { get; set; }
    public string? CustomerName { get; set; }
    public DateTime FinancialDateTime { get; set; }
    public DateTime? DueDate { get; set; }
    public decimal TotalPrice { get; set; }
    public decimal Balance { get; set; }
    public string? Symbol { get; set; }
    public bool IsVoided { get; set; }
    public int IntegerPart { get; set; }
}
