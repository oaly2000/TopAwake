namespace TopAwake;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        try
        {
            using var mutex = new Mutex(true, "TopAwake.SingleInstance", out var createdNew);
            if (!createdNew) return;

            ApplicationConfiguration.Initialize();
            Application.Run(new TrayAppContext());
        }
        catch (Exception ex)
        {
            try
            {
                var dir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TopAwake");
                Directory.CreateDirectory(dir);
                File.WriteAllText(Path.Combine(dir, "error.log"), ex.ToString());
            }
            catch
            {
                // 忽略日志写入失败
            }
        }
    }
}
