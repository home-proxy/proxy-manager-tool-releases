using FluentAssertions;
using System.Runtime.Versioning;
using NetAgent.ProxyManager.Core.Services;

namespace NetAgent.ProxyManager.Tests;

[SupportedOSPlatform("windows")]
public sealed class EmulatorProcessScannerTests
{
    [Fact]
    public void ResolveVirtualMachineName_ShouldUseMemuNameTag()
    {
        using var temp = new TempDirectory();
        var configPath = Path.Combine(temp.Path, "MEmu_1.memu");
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0"?>
            <VirtualBox>
              <Machine uuid="{20260520-aaaa-aaaa-aaaa-000000000001}" name="MEmu_1">
                <GuestProperties>
                  <GuestProperty name="name_tag" value="MEmu2" timestamp="0" flags=""/>
                </GuestProperties>
              </Machine>
            </VirtualBox>
            """);

        var result = EmulatorProcessScanner.ResolveVirtualMachineName(
            executablePath: null,
            commandLine: $"\"{configPath}\"",
            configPattern: "*.memu",
            instanceKey: "MEmu_1");

        result.Should().Be("MEmu2");
    }

    [Fact]
    public void ResolveMEmuNameFromConfigFiles_ShouldMatchHeadlessProcessByForwardedPort()
    {
        using var temp = new TempDirectory();
        var firstConfigPath = Path.Combine(temp.Path, "MEmu.memu");
        var secondConfigPath = Path.Combine(temp.Path, "MEmu_1.memu");
        File.WriteAllText(
            firstConfigPath,
            """
            <?xml version="1.0"?>
            <VirtualBox>
              <Machine uuid="{20260520-aaaa-aaaa-aaaa-000000000000}" name="MEmu">
                <GuestProperties>
                  <GuestProperty name="name_tag" value="MEmu-1" timestamp="0" flags=""/>
                </GuestProperties>
                <Network>
                  <Forwarding name="ADB" proto="1" hostip="127.0.0.1" hostport="21503" guestport="5555"/>
                </Network>
              </Machine>
            </VirtualBox>
            """);
        File.WriteAllText(
            secondConfigPath,
            """
            <?xml version="1.0"?>
            <VirtualBox>
              <Machine uuid="{20260520-aaaa-aaaa-aaaa-000000000001}" name="MEmu_1">
                <GuestProperties>
                  <GuestProperty name="name_tag" value="MEmu2" timestamp="0" flags=""/>
                </GuestProperties>
                <Network>
                  <Forwarding name="ADB" proto="1" hostip="127.0.0.1" hostport="21513" guestport="5555"/>
                </Network>
              </Machine>
            </VirtualBox>
            """);

        var result = EmulatorProcessScanner.ResolveMEmuNameFromConfigFiles(
            instanceKey: "MEmu:17384",
            configPaths: [firstConfigPath, secondConfigPath],
            listeningPorts: [21513]);

        result.Should().Be("MEmu2");
    }

    [Fact]
    public void ResolveNoxNameFromMultiPlayerManager_ShouldMapDefaultNoxKeyToDisplayName()
    {
        using var temp = new TempDirectory();
        var configPath = Path.Combine(temp.Path, "multiplayer.xml");
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <Root>
              <Instance rom="7" vmsource="-1" id="Nox_0" name="Nox1"/>
              <Instance rom="7" vmsource="-1" id="Nox_1" name="Nox2"/>
            </Root>
            """);

        var first = EmulatorProcessScanner.ResolveNoxNameFromMultiPlayerManager(
            instanceKey: "nox",
            commandLine: "--comment nox",
            configPaths: [configPath]);
        var second = EmulatorProcessScanner.ResolveNoxNameFromMultiPlayerManager(
            instanceKey: "Nox_1",
            commandLine: "--comment Nox_1",
            configPaths: [configPath]);

        first.Should().Be("Nox1");
        second.Should().Be("Nox2");
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"netagent-tests-{Guid.NewGuid():N}");
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
