﻿namespace OmniSharp.Extensions.LanguageServer.Protocol.Server
{
    public interface ILanguageServerFacade : ILanguageServerProxy
    {
        ITextDocumentLanguageServer TextDocument { get; }
        IClientLanguageServer Client { get; }
        IGeneralLanguageServer General { get; }
        IWindowLanguageServer Window { get; }
        IWorkspaceLanguageServer Workspace { get; }
    }
}