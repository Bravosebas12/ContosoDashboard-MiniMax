using System.Runtime.CompilerServices;

// Permite que los proyectos de tests accedan a clases/métodos internal del proyecto principal,
// necesario para usar WebApplicationFactory<Program> en ASP.NET Core integration tests.
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.Unit")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.Components")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.Integration")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.E2E.Api")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.Contract")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.E2E.UI")]
[assembly: InternalsVisibleTo("ContosoDashboard.Tests.Performance")]
