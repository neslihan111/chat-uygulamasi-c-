using System.Net;

namespace ChatApp;

/// Uygulama açılış ekranı: mod, kullanıcı adı, IP adresi ve port seçimi.
public class StartForm : Form
{
    private readonly RadioButton _serverRadio;
    private readonly RadioButton _clientRadio;
    private readonly TextBox _nameBox;
    private readonly TextBox _ipBox;
    private readonly NumericUpDown _portBox;

    public StartForm()
    {
        Text = "Chat Uygulaması - Başlangıç";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(360, 240);

        _serverRadio = new RadioButton
        {
            Text = "Sunucu",
            Location = new Point(140, 18),
            AutoSize = true,
            Checked = true
        };

        _clientRadio = new RadioButton
        {
            Text = "İstemci",
            Location = new Point(230, 18),
            AutoSize = true
        };

        _nameBox = new TextBox { Location = new Point(140, 55), Width = 200 };

        // IP adresi yalnızca istemci modunda kullanılır.
        _ipBox = new TextBox
        {
            Location = new Point(140, 90),
            Width = 200,
            Text = "127.0.0.1",
            Enabled = false
        };

        _portBox = new NumericUpDown
        {
            Location = new Point(140, 125),
            Width = 100,
            Minimum = 1,
            Maximum = 65535,
            Value = 5000
        };

        var startButton = new Button
        {
            Text = "Başlat",
            Location = new Point(140, 180),
            Size = new Size(95, 30)
        };

        var cancelButton = new Button
        {
            Text = "İptal",
            Location = new Point(245, 180),
            Size = new Size(95, 30),
            DialogResult = DialogResult.Cancel
        };

        Controls.Add(CreateLabel("Mod:", 20, 18));
        Controls.Add(_serverRadio);
        Controls.Add(_clientRadio);
        Controls.Add(CreateLabel("Kullanıcı adı:", 20, 55));
        Controls.Add(_nameBox);
        Controls.Add(CreateLabel("IP adresi:", 20, 90));
        Controls.Add(_ipBox);
        Controls.Add(CreateLabel("Port:", 20, 125));
        Controls.Add(_portBox);
        Controls.Add(startButton);
        Controls.Add(cancelButton);

        _serverRadio.CheckedChanged += (s, e) => _ipBox.Enabled = _clientRadio.Checked;
        startButton.Click += OnStartClicked;

        AcceptButton = startButton;
        CancelButton = cancelButton;
    }

    /// Kullanıcı "Başlat" dediğinde doldurulur.
    public ConnectionSettings? Settings { get; private set; }

    private static Label CreateLabel(string text, int x, int y)
    {
        return new Label
        {
            Text = text,
            Location = new Point(x, y + 3),
            AutoSize = true
        };
    }

    private void OnStartClicked(object? sender, EventArgs e)
    {
        string username = _nameBox.Text.Trim();
        if (username.Length == 0)
        {
            ShowWarning("Lütfen bir kullanıcı adı girin.");
            _nameBox.Focus();
            return;
        }

        if (username.Contains('|'))
        {
            ShowWarning("Kullanıcı adı '|' karakterini içeremez.");
            _nameBox.Focus();
            return;
        }

        string ip = _ipBox.Text.Trim();
        bool isServer = _serverRadio.Checked;

        if (!isServer && !IPAddress.TryParse(ip, out _))
        {
            ShowWarning("Geçerli bir IP adresi girin (örn. 127.0.0.1).");
            _ipBox.Focus();
            return;
        }

        Settings = new ConnectionSettings
        {
            Mode = isServer ? ConnectionMode.Server : ConnectionMode.Client,
            Username = username,
            IpAddress = ip,
            Port = (int)_portBox.Value
        };

        DialogResult = DialogResult.OK;
    }

    private void ShowWarning(string text)
    {
        MessageBox.Show(this, text, "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }
}
