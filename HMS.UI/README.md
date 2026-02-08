HMS.UI - Razor Pages frontend for HMS.API

This project is a minimal Razor Pages app that consumes the `HMS.API` backend. It focuses on a lightweight, responsive design with theme switching (light/dark). Default theme: light.

Run:
- Set `Api:BaseUrl` in `appsettings.Development.json` or environment variables
- dotnet run

Notes:
- Pages call the API directly via browser fetch or via `ApiClient` for server-side calls.
- Printing of invoices should use CSS print media rules and a dedicated print view optimized for thermal printers (narrow width, minimal margins).
