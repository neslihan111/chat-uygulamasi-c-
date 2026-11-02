namespace ChatApp;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        Application.SetHighDpiMode(HighDpiMode.SystemAware);
        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);

        using var startForm = new StartForm();
        if (startForm.ShowDialog() != DialogResult.OK)
        {
            return;
        }

        ConnectionSettings? settings = startForm.Settings;
        if (settings == null)
        {
            return;
        }

        Application.Run(new ChatForm(settings));
    }
}
