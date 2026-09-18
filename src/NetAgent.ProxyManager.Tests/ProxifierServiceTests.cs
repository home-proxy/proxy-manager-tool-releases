using FluentAssertions;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

public sealed class ProxifierServiceTests
{
    [Fact]
    public void BuildArguments_ShouldQuoteProfilePathAndUseSilentLoad()
    {
        var arguments = ProxifierService.BuildArguments(@"C:\Users\Test User\AppData\Roaming\active.ppx");

        arguments.Should().Be("\"C:\\Users\\Test User\\AppData\\Roaming\\active.ppx\" silent-load");
    }
}
