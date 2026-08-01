namespace PrinterDaemon.Utils;

public static class FileHelper
{
    public static bool WaitUntilReady(
        string archivo,
        int timeoutMs = 10000)
    {
        DateTime inicio = DateTime.Now;

        long ultimoTam = -1;

        while ((DateTime.Now - inicio).TotalMilliseconds < timeoutMs)
        {
            try
            {
                FileInfo fi = new FileInfo(archivo);

                if (!fi.Exists)
                    return false;

                long tam = fi.Length;

                using FileStream fs = File.Open(
                    archivo,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.None);

                if (tam == ultimoTam)
                    return true;

                ultimoTam = tam;
            }
            catch
            {
                // todavía está siendo escrito
            }

            Thread.Sleep(500);
        }

        return false;
    }
}