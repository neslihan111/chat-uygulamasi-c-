using System.Net.Sockets;
using System.Text;

namespace ChatApp;

/// Tek bir TCP bağlantısını satır tabanlı okuma ve yazma için sarmalar.
/// Sunucu tarafında her bağlı istemci için bir örnek bulunur.
public class ClientConnection : IDisposable
{
    private readonly TcpClient _tcpClient;
    private readonly StreamReader _reader;
    private readonly StreamWriter _writer;
    private readonly object _writeLock = new();
    private bool _disposed;

    public ClientConnection(TcpClient tcpClient)
    {
        _tcpClient = tcpClient;

        NetworkStream stream = tcpClient.GetStream();
        _reader = new StreamReader(stream, new UTF8Encoding(false));
        _writer = new StreamWriter(stream, new UTF8Encoding(false)) { AutoFlush = true };
    }

    /// Bağlantının kullanıcı adı (ilk satır olarak gönderilir).
    public string Username { get; set; } = string.Empty;

    public string RemoteAddress => _tcpClient.Client.RemoteEndPoint?.ToString() ?? "bilinmiyor";

    /// Bir satır okur. Karşı taraf bağlantıyı kapattığında null döner.
    public string? ReadLine()
    {
        return _reader.ReadLine();
    }

    /// Bir satır yazar. Birden fazla thread aynı anda yazabileceği için kilitlenir.
    public void SendLine(string line)
    {
        lock (_writeLock)
        {
            _writer.WriteLine(line);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _tcpClient.Close();
    }
}
