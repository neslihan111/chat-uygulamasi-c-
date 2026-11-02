using System.Net;
using System.Net.Sockets;

namespace ChatApp;

/// TCP sunucusu: birden fazla istemciyi kabul eder ve mesajları diğer istemcilere yayınlar.
/// Olaylar (events) arka plan thread'lerinden tetiklenir; arayüz tarafı Invoke/BeginInvoke ile dinlemelidir.
public class ChatServer
{
    private readonly int _port;
    private readonly string _hostName;
    private readonly object _clientsLock = new();
    private readonly List<ClientConnection> _clients = new();
    private TcpListener? _listener;
    private CancellationTokenSource? _cts;

    public ChatServer(int port, string hostName)
    {
        _port = port;
        _hostName = hostName;
    }

    /// Bir istemciden veya sistemden yeni mesaj geldiğinde tetiklenir.
    public event Action<ChatMessage>? MessageReceived;

    /// Bağlı kullanıcı listesi değiştiğinde tetiklenir (sunucunun kendi adı dahil).
    public event Action<IReadOnlyList<string>>? UsersChanged;

    /// Bilgi ve hata metinleri için tetiklenir.
    public event Action<string>? StatusChanged;

    /// Tüm arayüzlerde (0.0.0.0) belirtilen portu dinlemeye başlar.
    public void Start()
    {
        _listener = new TcpListener(IPAddress.Any, _port);
        _listener.Start();
        _cts = new CancellationTokenSource();

        Task.Run(() => AcceptLoop(_cts.Token));
        StatusChanged?.Invoke($"Sunucu {_port} portunda dinlemeye başladı.");
        RaiseUsersChanged();
    }

    /// Dinlemeyi durdurur ve tüm istemci bağlantılarını kapatır.
    public void Stop()
    {
        _cts?.Cancel();
        _listener?.Stop();

        List<ClientConnection> toClose;
        lock (_clientsLock)
        {
            toClose = new List<ClientConnection>(_clients);
            _clients.Clear();
        }

        foreach (ClientConnection client in toClose)
        {
            client.Dispose();
        }
    }

    /// Sunucu arayüzünden yazılan mesajı tüm istemcilere gönderir.
    public void SendToAll(ChatMessage message)
    {
        Broadcast(message, null);
    }

    private async Task AcceptLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            try
            {
                TcpClient tcpClient = await _listener!.AcceptTcpClientAsync(token);
                var connection = new ClientConnection(tcpClient);
                _ = Task.Run(() => HandleClient(connection));
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ObjectDisposedException)
            {
                break;
            }
            catch (Exception ex) when (!token.IsCancellationRequested)
            {
                StatusChanged?.Invoke($"Bağlantı kabul edilemedi: {ex.Message}");
            }
        }
    }

    /// Bir istemcinin yaşam döngüsü: kullanıcı adı al, mesajları oku, bağlantı kopunca temizle.
    private void HandleClient(ClientConnection client)
    {
        bool joined = false;

        try
        {
            // Protokol gereği ilk satır kullanıcı adıdır.
            string? name = client.ReadLine();
            if (string.IsNullOrWhiteSpace(name))
            {
                return;
            }

            client.Username = ChatMessage.Sanitize(name.Trim());
            AddClient(client);
            joined = true;

            StatusChanged?.Invoke($"{client.Username} bağlandı ({client.RemoteAddress}).");
            Announce($"{client.Username} sohbete katıldı.");

            while (true)
            {
                string? line = client.ReadLine();
                if (line == null)
                {
                    break;
                }

                if (ChatMessage.TryParse(line, out ChatMessage? parsed) && parsed != null && !string.IsNullOrWhiteSpace(parsed.Text))
                {
                    // Gönderen adı her zaman sunucu tarafında, bağlantıdan alınır.
                    var message = new ChatMessage(client.Username, parsed.Text);
                    MessageReceived?.Invoke(message);
                    Broadcast(message, client);
                }
            }
        }
        catch (IOException)
        {
            // Bağlantı koptu; aşağıda temizlenecek.
        }
        catch (ObjectDisposedException)
        {
            // Sunucu durduruldu; aşağıda temizlenecek.
        }
        finally
        {
            if (joined)
            {
                RemoveClient(client);
            }

            client.Dispose();
        }
    }

    private void AddClient(ClientConnection client)
    {
        lock (_clientsLock)
        {
            _clients.Add(client);
        }

        RaiseUsersChanged();
    }

    private void RemoveClient(ClientConnection client)
    {
        bool removed;
        lock (_clientsLock)
        {
            removed = _clients.Remove(client);
        }

        // Stop() listeyi zaten boşalttıysa ayrılma mesajı gönderilmez.
        if (!removed)
        {
            return;
        }

        StatusChanged?.Invoke($"{client.Username} ayrıldı.");
        Announce($"{client.Username} sohbetten ayrıldı.");
        RaiseUsersChanged();
    }

    /// Sistem mesajını hem sunucu arayüzüne hem de tüm istemcilere gönderir.
    private void Announce(string text)
    {
        var message = new ChatMessage(ChatMessage.SystemSender, text);
        MessageReceived?.Invoke(message);
        Broadcast(message, null);
    }

    /// Mesajı bağlı tüm istemcilere yazar; "except" verilen istemciye yazılmaz.
    private void Broadcast(ChatMessage message, ClientConnection? except)
    {
        string line = message.Serialize();

        foreach (ClientConnection client in GetClientsSnapshot())
        {
            if (client == except)
            {
                continue;
            }

            try
            {
                client.SendLine(line);
            }
            catch (IOException)
            {
                // Kopuk istemci; kendi okuma döngüsü temizleyecek.
            }
            catch (ObjectDisposedException)
            {
                // Bağlantı zaten kapatılmış.
            }
        }
    }

    /// Kilit altında kopya alır; yayın sırasında liste değişse bile güvenlidir.
    private List<ClientConnection> GetClientsSnapshot()
    {
        lock (_clientsLock)
        {
            return _clients.ToList();
        }
    }

    private void RaiseUsersChanged()
    {
        var names = new List<string> { _hostName + " (sunucu)" };

        lock (_clientsLock)
        {
            names.AddRange(_clients.Select(c => c.Username));
        }

        UsersChanged?.Invoke(names);
    }
}
