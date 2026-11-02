namespace ChatApp;

/// Uygulamanın çalışma modu: sunucu mu, istemci mi.
public enum ConnectionMode
{
    Server,
    Client
}

/// Başlangıç ekranında girilen bağlantı bilgileri.
public sealed class ConnectionSettings
{
    public ConnectionMode Mode { get; init; }

    public string Username { get; init; } = string.Empty;

    public string IpAddress { get; init; } = "127.0.0.1";

    public int Port { get; init; } = 5000;
}
