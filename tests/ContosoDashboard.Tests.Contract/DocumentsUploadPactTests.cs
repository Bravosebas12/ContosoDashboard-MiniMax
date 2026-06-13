using Xunit;

namespace ContosoDashboard.Tests.Contract;

public class DocumentsUploadPactTests
{
    [Fact(Skip = "Pact Broker no configurado en entorno local; habilitar en CI de contratos.")]
    public void Upload_Contract_ShouldBeVerifiedAgainstProvider()
    {
    }
}
