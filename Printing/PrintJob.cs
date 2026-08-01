namespace PrinterDaemon.Printing;

public class PrintJob
{
    public string FileName { get; }

    public DateTime Created { get; }

    public PrintJob(string file)
    {
        FileName = file;
        Created = File.GetCreationTime(file);
    }

    public override string ToString()
    {
        return Path.GetFileName(FileName);
    }
}