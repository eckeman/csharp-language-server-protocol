using FluentAssertions;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;
using Xunit;

namespace Lsp.Tests.Capabilities.Server
{
    public class ExecuteCommandOptionsTests
    {
        [Theory]
        [JsonFixture]
        public void SimpleTest(string expected)
        {
            var model = new ExecuteCommandRegistrationOptions.StaticOptions {
                Commands = new[] { "command1", "command2" }
            };
            var result = Fixture.SerializeObject(model);

            result.Should().Be(expected);

            var deresult = new LspSerializer(ClientVersion.Lsp3).DeserializeObject<ExecuteCommandRegistrationOptions.StaticOptions>(expected);
            deresult.Should().BeEquivalentTo(model);
        }
    }
}
