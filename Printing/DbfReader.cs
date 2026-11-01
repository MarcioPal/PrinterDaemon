using System.Globalization;
using System.Text;

namespace PrinterDaemon.Printing;

internal sealed record DbfRecord(IReadOnlyDictionary<string, string> Fields);

internal static class DbfReader
{
    public static IReadOnlyList<DbfRecord> Read(string path)
    {
        if (!File.Exists(path))
            throw new FileNotFoundException("No se encontró el archivo FE_PROCE.DBF configurado.", path);

        using var stream = File.OpenRead(path);
        using var reader = new BinaryReader(stream, Encoding.ASCII, leaveOpen: false);

        stream.Seek(4, SeekOrigin.Begin);
        int recordCount = reader.ReadInt32();
        short headerLength = reader.ReadInt16();
        short recordLength = reader.ReadInt16();

        stream.Seek(20, SeekOrigin.Current);
        var fields = new List<(string Name, int Offset, int Length)>();
        int offset = 1; // offset relativo al registro, después del flag de borrado

        while (stream.Position < headerLength)
        {
            byte first = reader.ReadByte();
            if (first == 0x0D)
                break;

            var nameBytes = new byte[11];
            nameBytes[0] = first;
            stream.ReadExactly(nameBytes, 1, 10);
            string name = Encoding.ASCII.GetString(nameBytes).TrimEnd('\0', ' ')
                .ToUpperInvariant();

            reader.ReadByte(); // field type
            stream.Seek(4, SeekOrigin.Current); // field address
            int length = reader.ReadByte();
            reader.ReadByte(); // decimals
            stream.Seek(14, SeekOrigin.Current); // reserved field descriptor bytes

            fields.Add((name, offset, length));
            offset += length;
        }

        var result = new List<DbfRecord>(recordCount);
        for (int record = 0; record < recordCount && stream.Position + recordLength <= stream.Length; record++)
        {
            byte deleted = reader.ReadByte();
            byte[] data = reader.ReadBytes(recordLength - 1);
            if (data.Length != recordLength - 1)
                break;

            if (deleted == '*')
                continue;

            var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var field in fields)
            {
                int start = field.Offset - 1;
                if (start < 0 || start + field.Length > data.Length)
                    continue;

                values[field.Name] = Encoding.ASCII.GetString(data, start, field.Length).Trim();
            }

            result.Add(new DbfRecord(values));
        }

        return result;
    }

    public static string Value(this DbfRecord record, string name) =>
        record.Fields.TryGetValue(name, out var value) ? value : "";

    public static decimal DecimalValue(this DbfRecord record, string name)
    {
        string value = record.Value(name).Trim();
        return decimal.TryParse(value, NumberStyles.Any, CultureInfo.InvariantCulture, out var result)
            ? result
            : 0m;
    }

    public static DateTime? DateValue(this DbfRecord record, string name)
    {
        string value = record.Value(name).Trim();
        return DateTime.TryParseExact(value, ["yyyyMMdd", "dd/MM/yyyy", "yyyy-MM-dd"],
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var result) ? result : null;
    }
}
