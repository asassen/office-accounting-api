namespace OfficeAccountingMaui;

public partial class SettingsPage : ContentPage
{
    public SettingsPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        UrlEntry.Text = Preferences.Get(AppSettings.ServiceUrlKey, "");
        KeyEntry.Text = await SecureStorage.GetAsync(AppSettings.ApiKeyKey) ?? "";
    }

    async void OnSaveClicked(object? sender, EventArgs e)
    {
        var url = UrlEntry.Text?.Trim() ?? "";
        var apiKey = KeyEntry.Text?.Trim() ?? "";
        if (!Uri.TryCreate(url, UriKind.Absolute, out _) || string.IsNullOrWhiteSpace(apiKey))
        {
            StatusLabel.Text = "Enter a valid URL and API key.";
            return;
        }

        Preferences.Set(AppSettings.ServiceUrlKey, url);
        await SecureStorage.SetAsync(AppSettings.ApiKeyKey, apiKey);
        StatusLabel.Text = "Saved.";
    }
}
