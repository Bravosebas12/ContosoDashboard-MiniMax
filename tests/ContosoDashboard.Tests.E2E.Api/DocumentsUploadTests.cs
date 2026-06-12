using Xunit;

namespace ContosoDashboard.Tests.E2E.Api;

public class DocumentsUploadTests
{
    [Fact(Skip = "E2E API requiere host de prueba autenticado y dataset estable; habilitar en pipeline dedicado.")]
    public void Upload_HappyPath_ShouldReturnSuccess()
    {
    }
}
