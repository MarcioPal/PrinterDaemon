using Microsoft.Extensions.Options;
using PrinterDaemon.Printing;
using PrinterDaemon.Utils;
using System.Collections.Concurrent;

namespace PrinterDaemon;

public class Worker(
    ILogger<Worker> logger,
    IOptions<PrinterSettings> options,
    PrinterService printerService)
    : BackgroundService
{
    private readonly PrinterSettings _config = options.Value;
    private readonly ConcurrentQueue<string> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private FileSystemWatcher? _watcher;
    private readonly HashSet<string> _pendientes = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Servicio de impresión iniciado.");

        Directory.CreateDirectory(_config.InputFolder);
        Directory.CreateDirectory(_config.ProcessedFolder);
        Directory.CreateDirectory(_config.ErrorFolder);

        // Encola archivos que ya existían al iniciar
        EncolarPendientes();

        // Comienza a vigilar la carpeta
        IniciarWatcher();

        while (!stoppingToken.IsCancellationRequested)
        {
            await _signal.WaitAsync(stoppingToken);

            while (_queue.TryDequeue(out var archivo))
            {
                ProcesarArchivo(archivo);
            }
        }
    }

    private void EncolarPendientes()
    {
        var archivos = Directory
            .GetFiles(_config.InputFolder, _config.FileFilter)
            .OrderBy(f => f);

        foreach (var archivo in archivos)
        {
            if (_pendientes.Add(archivo))
            {
                _queue.Enqueue(archivo);
                _signal.Release();
            }
        }
    }

    private void IniciarWatcher()
    {
        _watcher = new FileSystemWatcher(
            _config.InputFolder,
            _config.FileFilter);

        _watcher.NotifyFilter =
            NotifyFilters.FileName |
            NotifyFilters.CreationTime |
            NotifyFilters.Size;

        _watcher.Created += WatcherCreated;
        _watcher.Renamed += WatcherRenamed;

        _watcher.EnableRaisingEvents = true;
    }

    private void WatcherRenamed(object? sender, RenamedEventArgs e)
    {
        WatcherCreated(sender!, new FileSystemEventArgs(
            WatcherChangeTypes.Created,
            Path.GetDirectoryName(e.FullPath)!,
            e.Name!));
    }
   

    private void WatcherCreated(object sender, FileSystemEventArgs e)
    {
        lock (_lock)
        {
            if (_pendientes.Contains(e.FullPath))
                return;

            _pendientes.Add(e.FullPath);
        }

        _queue.Enqueue(e.FullPath);
        _signal.Release();
    }





    private void ProcesarArchivo(string archivo)
    {
        try
        {
            logger.LogInformation("Procesando {archivo}", archivo);

            if (!FileHelper.WaitUntilReady(archivo))
            {
                logger.LogWarning("El archivo aún no está listo: {archivo}", archivo);
                return;
            }

            bool ok = false;

            for (int intento = 1; intento <= _config.RetryCount; intento++)
            {
                try
                {
                    if (!File.Exists(archivo))
                    {
                        logger.LogWarning("El archivo desapareció: {archivo}", archivo);
                        return;
                    }

                    // Imprime el archivo (PDF o BMP)
                    printerService.Print(archivo);

                    // Da tiempo al spooler de Windows a recibir el trabajo
                    Thread.Sleep(_config.SpoolDelay);

                    ok = true;
                    break;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex,
                        "Error imprimiendo {archivo}. Intento {intento} de {total}",
                        archivo,
                        intento,
                        _config.RetryCount);

                    Thread.Sleep(_config.RetryDelay);
                }
            }

            if (ok)
                MoverProcesado(archivo);
            else
                MoverError(archivo);
        }
        finally
        {
            lock (_lock)
            {
                _pendientes.Remove(archivo);
            }
        }
    }



    private void MoverProcesado(string archivo)
    {
        string destino = Path.Combine(
            _config.ProcessedFolder,
            Path.GetFileName(archivo));

        if (File.Exists(destino))
            File.Delete(destino);

        File.Move(archivo, destino);

        logger.LogInformation("Movido a Procesados.");
    }

    private void MoverError(string archivo)
    {
        string destino = Path.Combine(
            _config.ErrorFolder,
            Path.GetFileName(archivo));

        if (File.Exists(destino))
            File.Delete(destino);

        File.Move(archivo, destino);

        logger.LogError("Movido a Error.");
    }

}