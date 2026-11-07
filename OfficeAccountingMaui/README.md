# Office Accounting MAUI client

Uses `OfficeAccounting.Client` (in `..\OfficeAccountingApi.Client`) to talk to the OfficeAccountingApi service.

1. Run the service (console or Windows service) on the accounting PC.
2. Run the app: `dotnet build -f net10.0-windows10.0.19041.0`, then launch, or run from Visual Studio.
3. Enter the service URL (e.g. `http://localhost:5080/`) and the `ApiKey` from the service's `App.config`, then Connect.
4. Pick a company; the customer list reloads and every request sends `X-Company`.

The URL and company are saved in Preferences; the API key in SecureStorage.

Android: plain `http://` to a LAN address needs `android:usesCleartextTraffic="true"` in the manifest.
Prefer an HTTPS tunnel (Cloudflare Tunnel) or Tailscale rather than opening router ports.
