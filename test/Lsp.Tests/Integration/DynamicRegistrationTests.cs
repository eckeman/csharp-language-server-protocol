﻿using System;
using System.Linq;
using System.Reactive.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using NSubstitute;
using OmniSharp.Extensions.JsonRpc.Testing;
using OmniSharp.Extensions.LanguageProtocol.Testing;
using OmniSharp.Extensions.LanguageServer.Client;
using OmniSharp.Extensions.LanguageServer.Protocol;
using OmniSharp.Extensions.LanguageServer.Protocol.Client.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Document;
using OmniSharp.Extensions.LanguageServer.Protocol.Document.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Models;
using OmniSharp.Extensions.LanguageServer.Protocol.Models.Proposals;
using OmniSharp.Extensions.LanguageServer.Protocol.Server.Capabilities;
using OmniSharp.Extensions.LanguageServer.Protocol.Shared;
using OmniSharp.Extensions.LanguageServer.Server;
using Xunit;
using Xunit.Abstractions;

namespace Lsp.Tests.Integration
{
    public class DynamicRegistrationTests : LanguageProtocolTestBase
    {
        public DynamicRegistrationTests(ITestOutputHelper outputHelper)  : base(new JsonRpcTestOptions().ConfigureForXUnit(outputHelper))
        {
        }

        [Fact]
        public async Task Should_Register_Dynamically_After_Initialization()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            client.ServerSettings.Capabilities.CompletionProvider.Should().BeNull();

            await Events.SettleNext();

            client.RegistrationManager.Registrations.Items.Should().Contain(x =>
                x.Method == TextDocumentNames.Completion && SelectorMatches(x, z=> z.HasLanguage && z.Language == "csharp")
            );
        }

        [Fact]
        public async Task Should_Register_Dynamically_While_Server_Is_Running()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            client.ServerSettings.Capabilities.CompletionProvider.Should().BeNull();

            await Events.SettleNext();

            server.OnCompletion(
                (@params, token) => Task.FromResult(new CompletionList()),
                registrationOptions: new CompletionRegistrationOptions() {
                    DocumentSelector = DocumentSelector.ForLanguage("vb")
                });

            await Settle().Take(2);

            client.RegistrationManager.Registrations.Items.Should().Contain(x =>
                x.Method == TextDocumentNames.Completion && SelectorMatches(x, z=> z.HasLanguage && z.Language == "vb")
            );
        }

        [Fact]
        public async Task Should_Unregister_Dynamically_While_Server_Is_Running()
        {
            var (client, server) = await Initialize(ConfigureClient, ConfigureServer);

            client.ServerSettings.Capabilities.CompletionProvider.Should().BeNull();

            await Events.SettleNext();

            var disposable = server.OnCompletion(
                (@params, token) => Task.FromResult(new CompletionList()),
                registrationOptions: new CompletionRegistrationOptions() {
                    DocumentSelector = DocumentSelector.ForLanguage("vb")
                });

            await Events.SettleNext();

            disposable.Dispose();

            await Settle().Take(2);

            client.RegistrationManager.Registrations.Items.Should().NotContain(x =>
                x.Method == TextDocumentNames.Completion && SelectorMatches(x, z=> z.HasLanguage && z.Language == "vb")
            );
        }

        [Fact]
        public async Task Should_Gather_Static_Registrations()
        {
            var (client, server) = await Initialize(ConfigureClient,
                options => {
                    ConfigureServer(options);
                    var semanticRegistrationOptions = new SemanticTokensRegistrationOptions() {
                        Id = Guid.NewGuid().ToString(),
                        Legend = new SemanticTokensLegend(),
                        DocumentProvider = new SemanticTokensDocumentProviderOptions(),
                        DocumentSelector = DocumentSelector.ForLanguage("csharp"),
                        RangeProvider = true
                    };

                    // Our server only statically registers when it detects a server that does not support dynamic capabilities
                    // This forces it to do that.
                    options.OnInitialized(
                        (server, request, response, token) => {
                            response.Capabilities.SemanticTokensProvider = SemanticTokensOptions.Of(semanticRegistrationOptions,
                                Enumerable.Empty<ILspHandlerDescriptor>());
                            response.Capabilities.SemanticTokensProvider.Id = semanticRegistrationOptions.Id;
                            return Task.CompletedTask;
                        });
                });
            client.RegistrationManager.Registrations.Items.Should().Contain(x => x.Method == TextDocumentNames.SemanticTokens);
        }

        [Fact]
        public async Task  Should_Register_Static_When_Dynamic_Is_Disabled()
        {
            var (client, server) = await Initialize(options => {
                ConfigureClient(options);
                options.DisableDynamicRegistration();
            }, ConfigureServer);

            client.ServerSettings.Capabilities.CompletionProvider.Should().BeEquivalentTo(new CompletionOptions() {
                ResolveProvider = false,
                TriggerCharacters = new Container<string>("a", "b"),
                AllCommitCharacters = new Container<string>("1", "2"),
            }, x => x.Excluding(z => z.WorkDoneProgress));
            server.ServerSettings.Capabilities.CompletionProvider.Should().BeEquivalentTo(new CompletionOptions() {
                ResolveProvider = false,
                TriggerCharacters = new Container<string>("a", "b"),
                AllCommitCharacters = new Container<string>("1", "2"),
            }, x => x.Excluding(z => z.WorkDoneProgress));
            server.ClientSettings.Capabilities.TextDocument.Completion.Value.Should().BeEquivalentTo(new CompletionCapability() {
                CompletionItem = new CompletionItemCapability() {
                    DeprecatedSupport = true,
                    DocumentationFormat = new[] {MarkupKind.Markdown},
                    PreselectSupport = true,
                    SnippetSupport = true,
                    TagSupport = new CompletionItemTagSupportCapability() {
                        ValueSet = new[] {
                            CompletionItemTag.Deprecated
                        }
                    },
                    CommitCharactersSupport = true
                },
                ContextSupport = true,
                CompletionItemKind = new CompletionItemKindCapability() {
                    ValueSet = new Container<CompletionItemKind>(Enum.GetValues(typeof(CompletionItemKind))
                        .Cast<CompletionItemKind>())
                }
            }, x => x.ConfigureForSupports().Excluding(z => z.DynamicRegistration));
            client.ClientSettings.Capabilities.TextDocument.Completion.Value.Should().BeEquivalentTo(new CompletionCapability() {
                CompletionItem = new CompletionItemCapability() {
                    DeprecatedSupport = true,
                    DocumentationFormat = new[] {MarkupKind.Markdown},
                    PreselectSupport = true,
                    SnippetSupport = true,
                    TagSupport = new CompletionItemTagSupportCapability() {
                        ValueSet = new[] {
                            CompletionItemTag.Deprecated
                        }
                    },
                    CommitCharactersSupport = true
                },
                ContextSupport = true,
                CompletionItemKind = new CompletionItemKindCapability() {
                    ValueSet = new Container<CompletionItemKind>(Enum.GetValues(typeof(CompletionItemKind))
                        .Cast<CompletionItemKind>())
                }
            }, x => x.ConfigureForSupports().Excluding(z => z.DynamicRegistration));

            client.RegistrationManager.Registrations.Items.Should().NotContain(x => x.Method == TextDocumentNames.SemanticTokens);
        }

        private void ConfigureClient(LanguageClientOptions options)
        {
            options.WithCapability(new CompletionCapability() {
                CompletionItem = new CompletionItemCapability() {
                    DeprecatedSupport = true,
                    DocumentationFormat = new[] {MarkupKind.Markdown},
                    PreselectSupport = true,
                    SnippetSupport = true,
                    TagSupport = new CompletionItemTagSupportCapability() {
                        ValueSet = new[] {
                            CompletionItemTag.Deprecated
                        }
                    },
                    CommitCharactersSupport = true
                },
                ContextSupport = true,
                CompletionItemKind = new CompletionItemKindCapability() {
                    ValueSet = new Container<CompletionItemKind>(Enum.GetValues(typeof(CompletionItemKind))
                        .Cast<CompletionItemKind>())
                }
            });

            options.WithCapability(new SemanticTokensCapability() {
                TokenModifiers = SemanticTokenModifier.Defaults.ToArray(),
                TokenTypes = SemanticTokenType.Defaults.ToArray()
            });
        }

        private void ConfigureServer(LanguageServerOptions options)
        {
            options.OnCompletion(
                (@params, token) => Task.FromResult(new CompletionList()),
                registrationOptions: new CompletionRegistrationOptions() {
                    DocumentSelector = DocumentSelector.ForLanguage("csharp"),
                    ResolveProvider = false,
                    TriggerCharacters = new Container<string>("a", "b"),
                    AllCommitCharacters = new Container<string>("1", "2"),
                });

            options.OnSemanticTokens(
                (builder, @params, ct) => { return Task.CompletedTask; },
                (@params, token) => { return Task.FromResult(new SemanticTokensDocument(new SemanticTokensLegend())); },
                new SemanticTokensRegistrationOptions());
        }

        private bool SelectorMatches(Registration registration, Func<DocumentFilter, bool> documentFilter)
        {
            return SelectorMatches(registration.RegisterOptions, documentFilter);
        }

        private bool SelectorMatches(object options, Func<DocumentFilter, bool> documentFilter)
        {
            if (options is ITextDocumentRegistrationOptions tdro)
                return tdro.DocumentSelector.Any(documentFilter);
            if (options is DocumentSelector selector)
                return selector.Any(documentFilter);
            return false;
        }
    }
}