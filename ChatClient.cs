using System.Net.Sockets;

namespace ChatApp;

/// Sunucuya bağlanan istemci. Gelen mesajlar ayrı bir arka plan thread'inde okunur.
public class ChatClient
{
    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private volatile ClientConnection? _connection;
    private volatile bool _closedByUser;
    private int _disconnectRaised;

    public ChatClient(string host, int port, string username)
    {
        _host = host;
        _port = port;
        _username = username;
    }

    /// Sunucudan yeni bir mesaj geldiğinde tetiklenir (arka plan thread'inden).
    public event Action<ChatMessage>? MessageReceived;

    /// Bağlantı koptuğunda tetiklenir; parametre kopma nedenidir (arka plan thread'inden).
    public event Action<string>? Disconnected;

    /// Sunucuya bağlanır, kullanıcı adını gönderir ve dinleme thread'ini başlatır.
    /// Bağlanılamazsa SocketException fırlatır.
    public void Connect()
    {
        var tcpClient = new TcpClient();
        try
        {
            tcpClient.Connect(_host, _port);
        }
        catch
        {
            tcpClient.Dispose();
            throw;
        }

        var connection = new ClientConnection(tcpClient);
        connection.SendLine(_username);
        _connection = connection;

        var receiveThread = new Thread(ReceiveLoop)
        {
            IsBackground = true,
            Name = "ChatClient-Receive"
        };
        receiveThread.Start();
    }

    /// Metni sunucuya gönderir. Başarılıysa true döner.
    public bool SendText(string text)
    {
        ClientConnection? connection = _connection;
        if (connection == null)
        {
            return false;
        }

        try
        {
            connection.SendLine(new ChatMessage(_username, text).Serialize());
            return true;
        }
        catch (Exception ex) when (ex is IOException or ObjectDisposedException)
        {
            OnDisconnected($"Mesaj gönderilemedi: {ex.Message}");
            return false;
        }
    }

    /// Bağlantıyı kullanıcı isteğiyle kapatır (Disconnected olayı tetiklenmez).
    public void Disconnect()
    {
        _closedByUser = true;
        _connection?.Dispose();
        _connection = null;
    }

    private void ReceiveLoop()
    {
        string reason = "Sunucu bağlantıyı kapattı.";

        try
        {
            ClientConnection connection = _connection!;

            while (true)
            {
                string? line = connection.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (ChatMessage.TryParse(line, out ChatMessage? message) && message != null)
                {
                    MessageReceived?.Invoke(message);
                }
            }
        }
        catch (IOException ex)
        {
            reason = $"Bağlantı hatası: {ex.Message}";
        }
        catch (ObjectDisposedException)
        {
            reason = "Bağlantı kapatıldı.";
        }
        finally
        {
            OnDisconnected(reason);
        }
    }

    /// Kopma olayını yalnızca bir kez ve kullanıcı kapatmadıysa bildirir.
    private void OnDisconnected(string reason)
    {
        if (_closedByUser || Interlocked.Exchange(ref _disconnectRaised, 1) == 1)
        {
            return;
        }

        _connection?.Dispose();
        _connection = null;
        Disconnected?.Invoke(reason);
    }
}
