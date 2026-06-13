using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Bunit;
using ContosoDashboard.Services;
using ContosoDashboard.Services.Documents;
using FluentAssertions;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using Xunit;

namespace ContosoDashboard.Tests.Components;

public class DocumentUploadComponentTests : TestContext
{
    [Fact]
    public void Component_ShouldRenderFormAndSubmitButtonDisabledInitially()
    {
        Services.AddSingleton(Substitute.For<IDocumentService>());
        Services.AddSingleton(Substitute.For<IProjectService>());
        Services.AddSingleton(Substitute.For<IDashboardService>());
        Services.AddSingleton<AuthenticationStateProvider>(new FakeAuthStateProvider(4));

        var cut = RenderComponent<ContosoDashboard.Shared.DocumentUploadComponent>(parameters =>
            parameters.Add(p => p.OnUploaded, _ => { }));

        cut.Markup.Should().Contain("Upload document");
        cut.Find("button[type='submit']").HasAttribute("disabled").Should().BeTrue();
    }

    private sealed class FakeAuthStateProvider : AuthenticationStateProvider
    {
        private readonly int _userId;

        public FakeAuthStateProvider(int userId)
        {
            _userId = userId;
        }

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, _userId.ToString()),
                new Claim(ClaimTypes.Name, "test-user")
            ], "test");
            var principal = new ClaimsPrincipal(identity);
            return Task.FromResult(new AuthenticationState(principal));
        }
    }
}
