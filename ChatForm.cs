using System.Net.Sockets;

namespace ChatApp;

/// Sohbet ekranı. Sunucu modunda bağlı kullanıcı listesini, istemci modunda yalnızca mesajları gösterir.
public class ChatForm : Form
{
    private readonly ConnectionSettings _settings;
    private ChatServer? _server;
    private ChatClient? _client;

    private readonly RichTextBox _messagesBox;
    private readonly GroupBox _usersGroup;
    private readonly ListBox _usersList;
    private readonly TextBox _inputBox;
    private readonly Button _sendButton;

    public ChatForm(ConnectionSettings settings)
    {
        _settings = settings;

        Text = "Chat Uygulaması";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(700, 450);
        MinimumSize = new Size(500, 350);

        _messagesBox = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BackColor = Color.White,
            Font = new Font("Segoe UI", 10F)
        };

        _usersList = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false
        };

        _usersGroup = new GroupBox
        {
            Text = "Bağlı kullanıcılar",
            Dock = DockStyle.Right,
            Width = 180,
            Visible = settings.Mode == ConnectionMode.Server
        };
        _usersGroup.Controls.Add(_usersList);

        _inputBox = new TextBox { Dock = DockStyle.Fill };
        _sendButton = new Button { Text = "Gönder", Dock = DockStyle.Right, Width = 90 };

        var inputPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 48,
            Padding = new Padding(8)
        };

        // Dock sırası önemlidir: dolduran kontrol önce eklenir, kenara yapışanlar sonra.
        inputPanel.Controls.Add(_inputBox);
        inputPanel.Controls.Add(_sendButton);

        _sendButton.Click += OnSendClicked;
        _inputBox.KeyDown += OnInputKeyDown;

        Controls.Add(_messagesBox);
        Controls.Add(_usersGroup);
        Controls.Add(inputPanel);
    }

    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        StartNetwork();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        base.OnFormClosing(e);
        StopNetwork();
    }

    private void StartNetwork()
    {
        try
        {
            if (_settings.Mode == ConnectionMode.Server)
            {
                _server = new ChatServer(_settings.Port, _settings.Username);
                _server.MessageReceived += OnNetworkMessage;
                _server.UsersChanged += OnUsersChanged;
                _server.StatusChanged += OnStatusChanged;
                _server.Start();

                Text = $"Chat Uygulaması - Sunucu ({_settings.Username}, port {_settings.Port})";
            }
            else
            {
                _client = new ChatClient(_settings.IpAddress, _settings.Port, _settings.Username);
                _client.MessageReceived += OnNetworkMessage;
                _client.Disconnected += OnClientDisconnected;
                _client.Connect();

                Text = $"Chat Uygulaması - İstemci ({_settings.Username})";
                AppendLine($"{_settings.IpAddress}:{_settings.Port} adresine bağlandı.", Color.DarkGreen);
            }
        }
        catch (Exception ex) when (ex is SocketException or IOException)
        {
            MessageBox.Show(this, $"Bağlantı kurulamadı:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Close();
        }
    }

    private void StopNetwork()
    {
        _server?.Stop();
        _client?.Disconnect();
    }

    private void OnNetworkMessage(ChatMessage message)
    {
        RunOnUi(() => AppendMessage(message));
    }

    private void OnUsersChanged(IReadOnlyList<string> users)
    {
        RunOnUi(() =>
        {
            _usersList.Items.Clear();
            _usersList.Items.AddRange(users.ToArray());
            _usersGroup.Text = $"Bağlı kullanıcılar ({users.Count})";
        });
    }

    private void OnStatusChanged(string text)
    {
        RunOnUi(() => AppendLine(text, Color.DarkBlue));
    }

    private void OnClientDisconnected(string reason)
    {
        RunOnUi(() =>
        {
            AppendLine(reason, Color.Firebrick);
            _inputBox.Enabled = false;
            _sendButton.Enabled = false;
        });
    }

    /// Arka plan thread'inden gelen işlemi arayüz thread'ine taşır.
    private void RunOnUi(Action action)
    {
        if (IsDisposed || !IsHandleCreated)
        {
            return;
        }

        if (InvokeRequired)
        {
            BeginInvoke(action);
        }
        else
        {
            action();
        }
    }

    private void OnSendClicked(object? sender, EventArgs e)
    {
        SendCurrentText();
    }

    private void OnInputKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.KeyCode == Keys.Enter)
        {
            e.SuppressKeyPress = true;
            SendCurrentText();
        }
    }

    private void SendCurrentText()
    {
        string text = _inputBox.Text.Trim();
        if (text.Length == 0)
        {
            return;
        }

        if (_server != null)
        {
            var message = new ChatMessage(_settings.Username, text);
            _server.SendToAll(message);
            AppendMessage(message);
            _inputBox.Clear();
        }
        else if (_client != null && _client.SendText(text))
        {
            AppendMessage(new ChatMessage(_settings.Username, text));
            _inputBox.Clear();
        }
    }

    private void AppendMessage(ChatMessage message)
    {
        Color color = message.IsSystem ? Color.Gray : Color.Black;
        AppendLine($"[{message.Timestamp:HH:mm:ss}] {message.Sender}: {message.Text}", color);
    }

    private void AppendLine(string text, Color color)
    {
        _messagesBox.SelectionStart = _messagesBox.TextLength;
        _messagesBox.SelectionLength = 0;
        _messagesBox.SelectionColor = color;
        _messagesBox.SelectedText = text + Environment.NewLine;
        _messagesBox.ScrollToCaret();
    }
}
