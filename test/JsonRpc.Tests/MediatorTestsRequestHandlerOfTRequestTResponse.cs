using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DryIoc;
using MediatR;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NSubstitute;
using OmniSharp.Extensions.JsonRpc;
using Xunit;
using Xunit.Abstractions;
using Arg = NSubstitute.Arg;
using Request = OmniSharp.Extensions.JsonRpc.Server.Request;

namespace JsonRpc.Tests
{
    public class MediatorTestsRequestHandlerOfTRequestTResponse : AutoTestBase
    {
        [Method("textDocument/codeAction")]
        public interface ICodeActionHandler : IJsonRpcRequestHandler<CodeActionParams, IEnumerable<Command>>
        {
        }

        public class CodeActionParams : IRequest<IEnumerable<Command>>
        {
            public string TextDocument { get; set; } = null!;
            public string Range { get; set; } = null!;
            public string Context { get; set; } = null!;
        }

        public class Command
        {
            public string Title { get; set; } = null!;
            [JsonProperty("command")] public string Name { get; set; } = null!;
        }

        public MediatorTestsRequestHandlerOfTRequestTResponse(ITestOutputHelper testOutputHelper) : base(testOutputHelper) =>
            Container = JsonRpcTestContainer.Create(testOutputHelper);

        [Fact]
        public async Task ExecutesHandler()
        {
            var codeActionHandler = Substitute.For<ICodeActionHandler>();

            var collection = new HandlerCollection(Substitute.For<IResolverContext>(), new HandlerTypeDescriptorProvider(new [] { typeof(HandlerTypeDescriptorProvider).Assembly, typeof(HandlerResolverTests).Assembly })) { codeActionHandler };
            AutoSubstitute.Provide<IHandlersManager>(collection);
            var router = AutoSubstitute.Resolve<RequestRouter>();

            var id = Guid.NewGuid().ToString();
            var @params = new CodeActionParams { TextDocument = "TextDocument", Range = "Range", Context = "Context" };
            var request = new Request(id, "textDocument/codeAction", JObject.Parse(JsonConvert.SerializeObject(@params)));

            await router.RouteRequest(router.GetDescriptors(request), request, CancellationToken.None);

            await codeActionHandler.Received(1).Handle(Arg.Any<CodeActionParams>(), Arg.Any<CancellationToken>());
        }
    }
}
