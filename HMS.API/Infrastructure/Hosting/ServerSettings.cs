namespace HMS.API.Infrastructure.Hosting;

public static class ServerSettings
{
    // Will be set during Kestrel configuration when HTTPS listen succeeds
    public static bool HttpsEnabled { get; set; } = false;
}
