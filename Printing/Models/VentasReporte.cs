namespace PrinterDaemon.Printing.Models;

public sealed class VentasReporte
{
    public string Empresa { get; set; } = "";
    public DateTime FechaEmision { get; set; }
    public string CodigoCliente { get; set; } = "";
    public string Cliente { get; set; } = "";
    public DateTime? FechaDesde { get; set; }
    public DateTime? FechaHasta { get; set; }
    public List<VentaMovimiento> Movimientos { get; set; } = [];
    public decimal TotalContado { get; set; }
    public decimal TotalCuentaCorriente { get; set; }
    public decimal SaldoCuentaCorrienteFecha { get; set; }
    public decimal TotalGeneral { get; set; }
    public decimal SaldoActualCuentaCorriente { get; set; }
}

public sealed class VentaMovimiento
{
    public DateTime? Fecha { get; set; }
    public string Factura { get; set; } = "";
    public decimal Importe { get; set; }
    public string Observacion { get; set; } = "";
}
