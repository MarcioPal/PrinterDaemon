using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace PrinterDaemon.Printing;

public class PdfPrinter
{
    private readonly PrinterSettings _config;

    public PdfPrinter(IOptions<PrinterSettings> options)
    {
        _config = options.Value;
    }

    public void Print(string archivo)
    {
        if (!File.Exists(_config.SumatraPath))
            throw new Exception(
                $"No se encontró SumatraPDF en:\n{_config.SumatraPath}");

        ProcessStartInfo psi = new()
        {
            FileName = _config.SumatraPath,
            Arguments =
                $"-print-to \"{_config.PrinterName}\" -silent \"{archivo}\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        using Process? p = Process.Start(psi);

        if (p == null)
            throw new Exception("No se pudo iniciar SumatraPDF.");

        p.WaitForExit();

        if (p.ExitCode != 0)
            throw new Exception(
                $"SumatraPDF terminó con código {p.ExitCode}");
    }
}