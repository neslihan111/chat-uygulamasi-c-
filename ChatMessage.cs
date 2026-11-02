using System.Globalization;

namespace ChatApp;

/// Bir sohbet mesajı. Ağ üzerinde tek satır olarak "zaman|gönderen|metin" biçiminde taşınır.
public class ChatMessage
{
    /// Sunucunun gönderdiği sistem mesajlarının gönderen adı (katılma / ayrılma bilgisi).
    public const string SystemSender = "Sistem";

    private const char Separator = '|';

    public ChatMessage(string sender, string text)
        : this(sender, text, DateTime.Now)
    {
    }

    public ChatMessage(string sender, string text, DateTime timestamp)
    {
        Sender = sender;
        Text = text;
        Timestamp = timestamp;
    }

    public string Sender { get; }

    public string Text { get; }

    public DateTime Timestamp { get; }

    public bool IsSystem => Sender == SystemSender;

    /// Satır sonu karakterlerini boşluğa çevirir; protokol satırının bozulmasını önler.
    public static string RemoveLineBreaks(string value)
    {
        return value.Replace('\r', ' ').Replace('\n', ' ');
    }

    /// Kullanıcı adı alanından ayırıcı karakteri ve satır sonlarını temizler.
    public static string Sanitize(string value)
    {
        return RemoveLineBreaks(value).Replace(Separator, '/');
    }

    /// Mesajı protokol satırına dönüştürür (zaman|gönderen|metin).
    public string Serialize()
    {
        string time = Timestamp.ToString("o", CultureInfo.InvariantCulture);
        return $"{time}{Separator}{Sanitize(Sender)}{Separator}{RemoveLineBreaks(Text)}";
    }

    /// Protokol satırını çözümler. Biçim bozuksa false döner.
    /// Metin kendi içinde '|' karakteri içerebilir; yalnızca ilk iki ayırıcı kullanılır.
    public static bool TryParse(string line, out ChatMessage? message)
    {
        message = null;

        string[] parts = line.Split(Separator, 3);
        if (parts.Length != 3)
        {
            return false;
        }

        if (!DateTime.TryParse(parts[0], CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTime timestamp))
        {
            return false;
        }

        message = new ChatMessage(parts[1], parts[2], timestamp);
        return true;
    }
}
