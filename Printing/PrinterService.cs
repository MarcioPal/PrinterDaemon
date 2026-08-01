namespace PrinterDaemon.Printing;

public class PrinterService
{
    private readonly PdfPrinter _pdf;
    private readonly BmpPrinter _bmp;

    public PrinterService(
        PdfPrinter pdf,
        BmpPrinter bmp)
    {
        _pdf = pdf;
        _bmp = bmp;
    }

    public void Print(string archivo)
    {
        string ext = Path.GetExtension(archivo).ToLowerInvariant();

        switch (ext)
        {
            case ".pdf":
                _pdf.Print(archivo);
                break;

            case ".bmp":
                _bmp.Print(archivo);
                break;

            default:
                throw new Exception($"Formato no soportado: {ext}");
        }
    }
}