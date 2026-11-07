using System.Net.Http.Json;
using System.Text.Json;

namespace OfficeAccounting.Client;

/// <summary>
/// Typed client for the OfficeAccountingApi service. Set <see cref="Company"/> to choose which
/// company database subsequent calls use (sent as the X-Company header).
/// </summary>
public sealed class OfficeAccountingClient : IDisposable
{
    readonly HttpClient http;
    readonly bool ownsHttp;

    public OfficeAccountingClient(Uri baseAddress, string apiKey)
        : this(new HttpClient { BaseAddress = baseAddress, Timeout = TimeSpan.FromSeconds(60) }, apiKey, true) { }

    /// <summary>Use an HttpClient from IHttpClientFactory; its BaseAddress must be set.</summary>
    public OfficeAccountingClient(HttpClient http, string apiKey, bool ownsHttp = false)
    {
        this.http = http;
        this.ownsHttp = ownsHttp;
        http.DefaultRequestHeaders.Remove("X-Api-Key");
        http.DefaultRequestHeaders.Add("X-Api-Key", apiKey);
    }

    /// <summary>Company database to use. Null uses the server's default.</summary>
    public string? Company { get; set; }

    public Task<CompaniesResult> GetCompaniesAsync(CancellationToken ct = default) =>
        Send<CompaniesResult>(HttpMethod.Get, "api/companies", null, ct);

    public Task<HealthResult> GetHealthAsync(CancellationToken ct = default) =>
        Send<HealthResult>(HttpMethod.Get, "api/health", null, ct);

    public Task<List<CustomerSummary>> GetCustomersAsync(CancellationToken ct = default) =>
        Send<List<CustomerSummary>>(HttpMethod.Get, "api/customers", null, ct);

    public Task<Customer> GetCustomerAsync(Guid guid, CancellationToken ct = default) =>
        Send<Customer>(HttpMethod.Get, $"api/customers/{guid}", null, ct);

    public Task<Customer> CreateCustomerAsync(CustomerUpdate customer, CancellationToken ct = default) =>
        Send<Customer>(HttpMethod.Post, "api/customers", customer, ct);

    public Task<Customer> UpdateCustomerAsync(Guid guid, CustomerUpdate changes, CancellationToken ct = default) =>
        Send<Customer>(HttpMethod.Put, $"api/customers/{guid}", changes, ct);

    // Raw SDK views: columns are not fixed, so rows come back as name -> JSON value.
    public Task<List<Dictionary<string, JsonElement>>> GetVendorsAsync(CancellationToken ct = default) =>
        Send<List<Dictionary<string, JsonElement>>>(HttpMethod.Get, "api/vendors", null, ct);

    public Task<List<Dictionary<string, JsonElement>>> GetItemsAsync(CancellationToken ct = default) =>
        Send<List<Dictionary<string, JsonElement>>>(HttpMethod.Get, "api/items", null, ct);

    public Task<List<Dictionary<string, JsonElement>>> GetInvoicesAsync(CancellationToken ct = default) =>
        Send<List<Dictionary<string, JsonElement>>>(HttpMethod.Get, "api/invoices", null, ct);

    public Task<Invoice> GetInvoiceAsync(Guid guid, CancellationToken ct = default) =>
        Send<Invoice>(HttpMethod.Get, $"api/invoices/{guid}", null, ct);

    public Task<Invoice> CreateInvoiceAsync(NewInvoice invoice, CancellationToken ct = default) =>
        Send<Invoice>(HttpMethod.Post, "api/invoices", invoice, ct);

    async Task<T> Send<T>(HttpMethod method, string path, object? body, CancellationToken ct)
    {
        using var req = new HttpRequestMessage(method, path);
        if (!string.IsNullOrWhiteSpace(Company)) req.Headers.Add("X-Company", Company);
        if (body != null) req.Content = JsonContent.Create(body, options: Json.Options);

        HttpResponseMessage res;
        try { res = await http.SendAsync(req, ct).ConfigureAwait(false); }
        catch (HttpRequestException ex)
        {
            throw new OfficeAccountingException(System.Net.HttpStatusCode.ServiceUnavailable, "Cannot reach the accounting service: " + ex.Message);
        }

        using (res)
        {
            if (!res.IsSuccessStatusCode)
            {
                string message = res.ReasonPhrase ?? "Request failed";
                try
                {
                    var err = await res.Content.ReadFromJsonAsync<ErrorBody>(Json.Options, ct).ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(err?.Error)) message = err.Error;
                }
                catch (JsonException) { }
                throw new OfficeAccountingException(res.StatusCode, message);
            }
            return (await res.Content.ReadFromJsonAsync<T>(Json.Options, ct).ConfigureAwait(false))!;
        }
    }

    sealed record ErrorBody(string? Error);

    public void Dispose()
    {
        if (ownsHttp) http.Dispose();
    }
}
