using UCR.EB.BioMicroscopeAdmin.Frontend.Presentation.Security;

namespace UCR.EB.BioMicroscopeAdmin.Frontend.Presentation.Tests.Unit;

public class ReturnUrlTests
{
    [Theory]
    [InlineData("/slides/4", "/slides/4")]
    [InlineData("/slides?box=1", "/slides?box=1")]
    [InlineData(null, "/")]
    [InlineData("", "/")]
    [InlineData("https://sitio-externo.test", "/")]
    [InlineData("//sitio-externo.test", "/")]
    [InlineData("/\\sitio-externo.test", "/")]
    [InlineData("javascript:alert(1)", "/")]
    public void Sanitize_OnlyAllowsLocalPaths(string? value, string expected) =>
        Assert.Equal(expected, ReturnUrl.Sanitize(value));
}
