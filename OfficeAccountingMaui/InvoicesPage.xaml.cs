using OfficeAccounting.Client;

namespace OfficeAccountingMaui;

public partial class InvoicesPage : ContentPage
{
	OfficeAccountingClient? client;
	string? connectedKey;

	public InvoicesPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await LoadAsync();
	}

	async Task<OfficeAccountingClient?> GetClientAsync()
	{
		var url = Preferences.Get(AppSettings.ServiceUrlKey, "").Trim();
		var apiKey = (await SecureStorage.GetAsync(AppSettings.ApiKeyKey) ?? "").Trim();
		var company = Preferences.Get(AppSettings.CompanyKey, "");
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(apiKey))
		{
			StatusLabel.Text = "Set server URL and API key in Settings.";
			return null;
		}

		var key = $"{uri}|{apiKey}";
		if (client == null || key != connectedKey)
		{
			client?.Dispose();
			client = new OfficeAccountingClient(uri, apiKey);
			connectedKey = key;
		}
		client.Company = string.IsNullOrWhiteSpace(company) ? null : company;
		return client;
	}

	async Task LoadAsync()
	{
		var c = await GetClientAsync();
		if (c == null) return;
		try
		{
			StatusLabel.Text = $"Loading invoices{(c.Company == null ? "" : " for " + c.Company)}...";
			InvoiceList.ItemsSource = null;
			var invoices = await c.GetInvoiceSummariesAsync();
			InvoiceList.ItemsSource = invoices;
			StatusLabel.Text = $"{c.Company}: {invoices.Count} invoices, newest first";
		}
		catch (OfficeAccountingException ex)
		{
			StatusLabel.Text = ex.Message;
		}
	}

	async void OnRefreshing(object? sender, EventArgs e)
	{
		await LoadAsync();
		Refresher.IsRefreshing = false;
	}

	async void OnInvoiceSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (e.CurrentSelection.FirstOrDefault() is not InvoiceSummary summary) return;
		InvoiceList.SelectedItem = null;
		var c = await GetClientAsync();
		if (c == null) return;
		try
		{
			var invoice = await c.GetInvoiceAsync(summary.Guid);
			await Navigation.PushAsync(new InvoiceDetailPage(invoice, summary.Symbol ?? ""));
		}
		catch (OfficeAccountingException ex)
		{
			await DisplayAlertAsync("Error", ex.Message, "OK");
		}
	}
}
