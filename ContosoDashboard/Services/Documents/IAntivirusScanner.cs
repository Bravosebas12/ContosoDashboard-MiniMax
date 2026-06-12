using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ContosoDashboard.Services.Documents;

/// <summary>
/// Estado del escaneo antivirus. Ver <c>specs/001-documents-management/contracts/IAntivirusScanner.cs</c>.
/// </summary>
public enum ScanStatus
{
    Clean = 0,
    Infected = 1,
    NotScanned = 2,
    Error = 3
}

/// <summary>
/// Resultado de un escaneo antivirus.
/// </summary>
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
    Task<ScanResult> ScanAsync(Stream fileStream, string fileName, CancellationToken ct = default);
}
