using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using nClam;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Configuración para <see cref="ClamAvScanner"/>.
/// </summary>
public class AntivirusOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 3310;
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(30);
    /// <summary>Si true (training), AV no disponible → NotScanned. Si false (producción), AV no disponible → Error.</summary>
    public bool AllowDegradedMode { get; set; } = true;
}

/// <summary>
/// Implementación de <see cref="IAntivirusScanner"/> que usa ClamAV via nClam.
/// Comportamiento:
/// - Training: fail-open (NotScanned si AV no disponible, con log warning)
/// - Producción: fail-closed (Error si AV no disponible, requiere intervención)
/// Ver FR-004, AC-1.3.3, AC-1.3.4.
/// </summary>
public class ClamAvScanner : IAntivirusScanner
{
    private readonly ILogger<ClamAvScanner> _logger;
    private readonly AntivirusOptions _options;

    public ClamAvScanner(ILogger<ClamAvScanner> logger, IOptions<AntivirusOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<ScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var client = new ClamClient(_options.Host, _options.Port);
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(_options.Timeout);

            // nClam requiere un Stream seekable; copiamos a MemoryStream si no lo es
            Stream seekable = fileStream.CanSeek ? fileStream : await CopyToSeekableAsync(fileStream, ct).ConfigureAwait(false);

            var result = await client.SendAndScanFileAsync(seekable, ct).ConfigureAwait(false);
            sw.Stop();

            return result.Result switch
            {
                ClamScanResults.Clean => new ScanResult(ScanStatus.Clean, null, sw.Elapsed, "ClamAV"),
                ClamScanResults.VirusDetected => new ScanResult(ScanStatus.Infected, result.InfectedFiles?.FirstOrDefault()?.VirusName, sw.Elapsed, "ClamAV"),
                ClamScanResults.Error => new ScanResult(ScanStatus.Error, null, sw.Elapsed, "ClamAV"),
                _ => new ScanResult(ScanStatus.Error, null, sw.Elapsed, "ClamAV"),
            };
        }
        catch (Exception ex) when (IsConnectivityError(ex))
        {
            sw.Stop();
            _logger.LogWarning(ex, "ClamAV no disponible en {Host}:{Port} tras {Elapsed}ms", _options.Host, _options.Port, sw.ElapsedMilliseconds);

            // Fail-open en training, fail-closed en producción (controlado por AllowDegradedMode)
            var status = _options.AllowDegradedMode ? ScanStatus.NotScanned : ScanStatus.Error;
            return new ScanResult(status, null, sw.Elapsed, "ClamAV (unavailable)");
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            _logger.LogWarning("ClamAV scan timeout para {FileName} tras {Elapsed}ms", fileName, sw.ElapsedMilliseconds);
            return new ScanResult(ScanStatus.Error, "timeout", sw.Elapsed, "ClamAV");
        }
    }

    private static bool IsConnectivityError(Exception ex)
        => ex is System.Net.Sockets.SocketException
        || ex is System.Net.Http.HttpRequestException
        || ex is System.IO.IOException
        || ex is TimeoutException
        || ex.InnerException is System.Net.Sockets.SocketException
        || ex.GetType().Name.Contains("Clam", StringComparison.OrdinalIgnoreCase); // Catch-all nClam-specific exceptions

    private static async Task<MemoryStream> CopyToSeekableAsync(Stream source, CancellationToken ct)
    {
        var ms = new MemoryStream();
        await source.CopyToAsync(ms, ct).ConfigureAwait(false);
        ms.Position = 0;
        return ms;
    }
}
