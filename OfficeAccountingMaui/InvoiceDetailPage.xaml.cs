using OfficeAccounting.Client;

namespace OfficeAccountingMaui;

public partial class InvoiceDetailPage : ContentPage
{
	public InvoiceDetailPage(Invoice invoice, string currencySymbol)
	{
		InitializeComponent();
		Title = invoice.Number ?? "Invoice";
		HeaderLabel.Text = $"Invoice {invoice.Number}";
		CustomerLabel.Text = invoice.CustomerName;
		DatesLabel.Text = $"Date {invoice.Date:dd MMM yyyy}   Due {invoice.DueDate:dd MMM yyyy}";
		ReferenceLabel.Text = string.IsNullOrWhiteSpace(invoice.Reference) ? "" : $"Ref: {invoice.Reference}";
		VoidedLabel.IsVisible = invoice.Voided;
		LinesList.ItemsSource = invoice.Lines;
		SubTotalLabel.Text = currencySymbol + invoice.SubTotal.ToString("N2");
		TaxLabel.Text = currencySymbol + invoice.Tax.ToString("N2");
		TotalLabel.Text = currencySymbol + invoice.Total.ToString("N2");
		BalanceLabel.Text = currencySymbol + invoice.Balance.ToString("N2");
	}
}
