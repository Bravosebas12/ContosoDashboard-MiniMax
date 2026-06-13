using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Estado del escaneo antivirus.
/// </summary>
public enum ScanStatus
{
    /// <summary>Archivo limpio — sin amenazas detectadas.</summary>
    Clean = 0,
    /// <summary>Archivo infectado — amenaza detectada (ver <see cref="ScanResult.ThreatName"/>).</summary>
    Infected = 1,
    /// <summary>Servicio de AV no estaba disponible (degraded mode).</summary>
    NotScanned = 2,
    /// <summary>Error al escanear (timeout, conexión, formato no soportado).</summary>
    Error = 3
}

/// <summary>
/// Resultado de un escaneo antivirus.
/// </summary>
/// <param name="Status">Estado del escaneo.</param>
/// <param name="ThreatName">Nombre de la amenaza si <paramref name="Status"/> es <see cref="ScanStatus.Infected"/>, sino null.</param>
/// <param name="Duration">Tiempo total del escaneo (incluyendo I/O).</param>
/// <param name="ScannerVersion">Versión del motor de AV (ej. "ClamAV 1.2.3").</param>
public record ScanResult(
    ScanStatus Status,
    string? ThreatName,
    TimeSpan Duration,
    string? ScannerVersion);

/// <summary>
/// Abstracción del escaneo antivirus. Implementación training: <c>ClamAvScanner</c>
/// usando nClam. Esta release NO incluye alternativas cloud (ver FR-038).
/// </summary>
public interface IAntivirusScanner
{
    /// <summary>
    /// Escanea un stream de bytes. El stream NO se consume (se lee una copia temporal);
    /// el caller puede seguir usando el stream tras la llamada.
    /// </summary>
    /// <param name="fileStream">Contenido del archivo a escanear.</param>
    /// <param name="fileName">Nombre del archivo (solo para logging y heurísticas del AV; no se persiste).</param>
    /// <param name="ct">Token de cancelación. Si se cancela, devuelve <see cref="ScanStatus.Error"/>.</param>
    /// <returns>Resultado del escaneo. El caller es responsable de actuar según
    /// el estado (rechazar si <see cref="ScanStatus.Infected"/>, permitir si
    /// <see cref="ScanStatus.Clean"/>, etc.).</returns>
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
