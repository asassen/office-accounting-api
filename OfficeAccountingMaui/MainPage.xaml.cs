using OfficeAccounting.Client;

namespace OfficeAccountingMaui;

public partial class MainPage : ContentPage
{
	OfficeAccountingClient? client;
	bool loadingCompanies;
	string? connectedUrl;
	string? connectedApiKey;

	public MainPage()
	{
		InitializeComponent();
	}

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		await RestoreAsync();
	}

	async Task RestoreAsync()
	{
		var url = Preferences.Get(AppSettings.ServiceUrlKey, "").Trim();
		var apiKey = (await SecureStorage.GetAsync(AppSettings.ApiKeyKey) ?? "").Trim();
		if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrWhiteSpace(apiKey))
		{
			StatusLabel.Text = "Set server URL and API key in Settings.";
			return;
		}

		if (client != null &&
			string.Equals(url, connectedUrl, StringComparison.OrdinalIgnoreCase) &&
			string.Equals(apiKey, connectedApiKey, StringComparison.Ordinal))
		{
			return;
		}

		await ConnectAsync(uri, apiKey);
	}

	async Task ConnectAsync(Uri uri, string apiKey)
	{

		client?.Dispose();
		client = new OfficeAccountingClient(uri, apiKey);
		try
		{
			StatusLabel.Text = "Connecting...";
			var result = await client.GetCompaniesAsync();
			connectedUrl = uri.ToString();
			connectedApiKey = apiKey;

			var companies = result.Companies.ToList();
			loadingCompanies = true;
			CompanyPicker.ItemsSource = companies;
			var saved = Preferences.Get(AppSettings.CompanyKey, result.Default ?? "");
			var index = companies.FindIndex(c => c.Equals(saved, StringComparison.OrdinalIgnoreCase));
			CompanyPicker.SelectedIndex = index >= 0 ? index : (companies.Count > 0 ? 0 : -1);
			loadingCompanies = false;

			await SelectCompanyAsync();
		}
		catch (OfficeAccountingException ex)
		{
			loadingCompanies = false;
			StatusLabel.Text = ex.Message;
		}
	}

	async void OnCompanyChanged(object? sender, EventArgs e)
	{
		if (!loadingCompanies) await SelectCompanyAsync();
	}

	async Task SelectCompanyAsync()
	{
		if (client == null || CompanyPicker.SelectedItem is not string company) return;
		client.Company = company;
		Preferences.Set(AppSettings.CompanyKey, company);
		await LoadCustomersAsync();
	}

	async Task LoadCustomersAsync()
	{
		if (client == null) return;
		try
		{
			StatusLabel.Text = $"Loading {client.Company}...";
			CustomerList.ItemsSource = null;
			var customers = await client.GetCustomersAsync();
			CustomerList.ItemsSource = customers.Where(c => c.Active).OrderBy(c => c.Name).ToList();
			StatusLabel.Text = $"{client.Company}: {customers.Count} customers";
		}
		catch (OfficeAccountingException ex)
		{
			StatusLabel.Text = ex.Message;
		}
	}

	async void OnRefreshing(object? sender, EventArgs e)
	{
		await LoadCustomersAsync();
		Refresher.IsRefreshing = false;
	}

	async void OnCustomerSelected(object? sender, SelectionChangedEventArgs e)
	{
		if (client == null || e.CurrentSelection.FirstOrDefault() is not CustomerSummary summary) return;
		CustomerList.SelectedItem = null;
		try
		{
			var c = await client.GetCustomerAsync(summary.Guid);
			await DisplayAlertAsync(c.Name,
				$"No. {c.DisplayNumber}\nEmail: {c.Email}\nPhone: {c.Phone}\nBalance: {c.Balance:N2}\nOverdue: {c.Overdue:N2}\nSales YTD: {c.SalesYtd:N2}",
				"OK");
		}
		catch (OfficeAccountingException ex)
		{
			await DisplayAlertAsync("Error", ex.Message, "OK");
		}
	}

	async void OnNewCustomerClicked(object? sender, EventArgs e)
	{
		if (client == null || client.Company == null) return;
		var name = await DisplayPromptAsync("New customer", $"Name (in {client.Company}):");
		if (string.IsNullOrWhiteSpace(name)) return;
		var email = await DisplayPromptAsync("New customer", "Email (optional):", keyboard: Keyboard.Email);

		try
		{
			await client.CreateCustomerAsync(new CustomerUpdate
			{
				Name = name.Trim(),
				Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim(),
			});
			await LoadCustomersAsync();
		}
		catch (OfficeAccountingException ex)
		{
			await DisplayAlertAsync("Error", ex.Message, "OK");
		}
	}
}
