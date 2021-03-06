using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using NSubstitute;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Shared;
using OmniSharp.Extensions.LanguageServer.Protocol.Workspace;
using OmniSharp.Extensions.LanguageServer.Server.Matchers;
using Xunit;
using Xunit.Abstractions;

namespace Lsp.Tests.Matchers
{
    public class ExecuteCommandHandlerMatcherTests : AutoTestBase
    {
        public ExecuteCommandHandlerMatcherTests(ITestOutputHelper testOutputHelper) : base(testOutputHelper)
        {
        }

        [Fact]
        public void Should_Not_Return_Null()
        {
            // Given
            var handlerDescriptors = Enumerable.Empty<ILspHandlerDescriptor>();
            var handlerMatcher = AutoSubstitute.Resolve<ExecuteCommandMatcher>();

            // When
            var result = handlerMatcher.FindHandler(1, handlerDescriptors);

            // Then
            result.Should().NotBeNull();
        }

        [Fact]
        public void Should_Return_Empty_Descriptor()
        {
            // Given
            var handlerDescriptors = Enumerable.Empty<ILspHandlerDescriptor>();
            var handlerMatcher = AutoSubstitute.Resolve<ExecuteCommandMatcher>();

            // When
            var result = handlerMatcher.FindHandler(1, handlerDescriptors);

            // Then
            result.Should().BeEmpty();
        }

        [Fact]
        public void Should_Return_Handler_Descriptor()
        {
            // Given
            var handlerMatcher = AutoSubstitute.Resolve<ExecuteCommandMatcher>();
            var executeCommandHandler = Substitute.For<IExecuteCommandHandler>().With(new Container<string>("Command"));
            var registrationsOptions = new ExecuteCommandRegistrationOptions {
                Commands = new Container<string>("Command")
            };

            // When
            var result = handlerMatcher.FindHandler(
                new ExecuteCommandParams { Command = "Command" },
                new List<LspHandlerDescriptor> {
                    new LspHandlerDescriptor(
                        "workspace/executeCommand",
                        "Key",
                        executeCommandHandler,
                        executeCommandHandler.GetType(),
                        typeof(ExecuteCommandParams),
                        typeof(ExecuteCommandRegistrationOptions),
                        registrationsOptions,
                        () => true,
                        typeof(ExecuteCommandCapability),
                        null,
                        () => { },
                        Substitute.For<ILspHandlerTypeDescriptor>()
                    )
                }
            );

            // Then
            var lspHandlerDescriptors = result as ILspHandlerDescriptor[] ?? result.ToArray();
            lspHandlerDescriptors.Should().NotBeNullOrEmpty();
            lspHandlerDescriptors.Should().Contain(x => x.Method == "workspace/executeCommand");
        }
    }
}
