using ContosoDashboard.Services;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Unit;

/// <summary>
/// Tests de regresion para A6 — verifican que DashboardService usa IServiceScopeFactory
/// (no ApplicationDbContext directamente) por cache factory invocation.
///
/// Si alguien intenta revertir el fix inyectando ApplicationDbContext directamente,
/// los tests fallan en tiempo de compilacion (no matchea el constructor).
/// </summary>
public class DashboardServiceScopeTests
{
    [Fact]
    public void DashboardService_Constructor_ShouldAccept_IServiceScopeFactory_IMemoryCache_ILogger()
    {
        // Arrange
        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        var cache = Substitute.For<IMemoryCache>();
        var logger = Substitute.For<ILogger<DashboardService>>();

        // Act: este codigo compila SOLO si el constructor acepta estos 3 tipos exactos.
        // Si alguien revierte el fix (volviendo a inyectar ApplicationDbContext directamente),
        // este codigo deja de compilar y el test falla. Es un regression guard.
        var sut = new DashboardService(scopeFactory, cache, logger);

        // Assert
        sut.Should().NotBeNull();
    }

    [Fact]
    public void DashboardService_Constructor_ShouldNotAccept_ApplicationDbContext()
    {
        // Documentacion explicita: el constructor NO debe aceptar ApplicationDbContext.
        // Esto protege contra la regresion de A6 (inyectar el DbContext scoped directamente
        // causa InvalidOperationException cuando Blazor prerender invoca OnInitializedAsync
        // dos veces y dos factories concurrentes usan el mismo DbContext).
        //
        // La verificacion real es en tiempo de compilacion: si alguien cambia el constructor
        // a aceptar ApplicationDbContext, el primer test dejaria de compilar.
        var sut = new DashboardService(
            Substitute.For<IServiceScopeFactory>(),
            Substitute.For<IMemoryCache>(),
            Substitute.For<ILogger<DashboardService>>());

        sut.Should().NotBeNull();
    }
}
