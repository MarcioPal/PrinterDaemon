using System.Globalization;
using System.Text;
using PrinterDaemon.Printing.Models;

namespace PrinterDaemon.Printing;

public static class LvParser
{
    public static VentasReporte Parse(string archivo, string feProceDbfPath)
    {
        var solicitud = LeerSolicitud(archivo);
        var reporte = new VentasReporte
        {
            Empresa = "Mercadito \"Don Americo\"",
            FechaEmision = DateTime.Today,
            CodigoCliente = solicitud.Cliente,
            Cliente = solicitud.Nombre,
            FechaDesde = solicitud.Desde,
            FechaHasta = solicitud.Hasta
        };

        foreach (var registro in DbfReader.Read(feProceDbfPath))
        {
            int cliente = ParseInt(registro.Value("NRO_CLIENT"));
            int tipo = ParseInt(registro.Value("TIPO"));
            DateTime? fecha = registro.DateValue("FECHA");
            if (!int.TryParse(solicitud.Cliente, out var clienteSolicitado) ||
                cliente != clienteSolicitado ||
                tipo is not (1 or 6) ||
                fecha is null || fecha < solicitud.Desde || fecha > solicitud.Hasta)
                continue;

            decimal importe = registro.DecimalValue("TOTAL");
            string puntoVenta = registro.Value("PTO_VTA");
            string comprobante = registro.Value("NRO_COMP");
            string factura = string.IsNullOrWhiteSpace(puntoVenta) && string.IsNullOrWhiteSpace(comprobante)
                ? registro.Value("ID")
                : $"{RellenarConCeros(puntoVenta, 5)}-{RellenarConCeros(comprobante, 8)}";

            reporte.Movimientos.Add(new VentaMovimiento
            {
                Fecha = fecha,
                Factura = factura,
                Importe = importe,
                Observacion = registro.Value("OBSER")
            });

            // FE_PROCE.DBF no contiene un campo que indique la condición de pago.
            // Los tipos 1 y 6 son los comprobantes incluidos en este listado.
            reporte.TotalContado += importe;
            reporte.TotalGeneral += importe;
        }

        return reporte;
    }

    private static (string Cliente, string Nombre, DateTime Desde, DateTime Hasta) LeerSolicitud(string archivo)
    {
        string? linea = File.ReadLines(archivo, Encoding.UTF8)
            .FirstOrDefault(linea => !string.IsNullOrWhiteSpace(linea));
        if (linea is null)
            throw new FormatException($"El archivo {Path.GetFileName(archivo)} está vacío.");

        var campos = SepararCampos(linea);
        int indiceFecha = campos.Count >= 4 ? 2 : 1;
        if (campos.Count > indiceFecha && !EsFecha(campos[indiceFecha]))
        {
            campos = SepararCampos(File.ReadLines(archivo).Skip(1).FirstOrDefault() ?? "");
            indiceFecha = campos.Count >= 4 ? 2 : 1;
        }

        if (campos.Count < 3 || (campos.Count >= 4 && string.IsNullOrWhiteSpace(campos[1])))
            throw new FormatException($"{Path.GetFileName(archivo)} debe contener cliente, nombre, fecha desde y fecha hasta.");

        if (!TryParseDate(campos[indiceFecha], out var desde) ||
            !TryParseDate(campos[indiceFecha + 1], out var hasta))
            throw new FormatException($"Las fechas de {Path.GetFileName(archivo)} no tienen un formato soportado.");
        if (desde > hasta)
            throw new FormatException("La fecha desde no puede ser posterior a la fecha hasta.");

        string nombre = campos.Count >= 4 ? campos[1].Trim() : campos[0].Trim();
        return (campos[0].Trim(), nombre, desde, hasta);
    }

    private static List<string> SepararCampos(string linea)
    {
        char separador = linea.Contains(';') ? ';' : ',';
        return linea.Split(separador).Select(c => c.Trim().Trim('"')).ToList();
    }

    private static bool EsFecha(string valor) => TryParseDate(valor, out _);

    private static bool TryParseDate(string valor, out DateTime fecha) =>
        DateTime.TryParseExact(valor.Trim().Trim('"'),
            new[] { "dd/MM/yyyy", "yyyyMMdd", "yyyy-MM-dd" },
            CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    private static int ParseInt(string value) =>
        int.TryParse(value.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0;

    private static string RellenarConCeros(string value, int longitud)
    {
        string digitos = new(value.Where(char.IsDigit).ToArray());
        return digitos.PadLeft(longitud, '0');
    }

}
