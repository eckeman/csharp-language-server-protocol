using MediatR;
using OmniSharp.Extensions.JsonRpc;
using OmniSharp.Extensions.LanguageServer.Protocol.Serialization;

namespace OmniSharp.Extensions.LanguageServer.Protocol.Models
{
    [Method(TextDocumentNames.DidSave, Direction.ClientToServer)]
    public class DidSaveTextDocumentParams : ITextDocumentIdentifierParams, IRequest
    {
        /// <summary>
        /// The document that was saved.
        /// </summary>
        /// <remarks>
        /// TODO: Change to RequiredVersionedTextDocumentIdentifier (or in the future will be VersionedTextDocumentIdentifier)
        /// </remarks>
        public TextDocumentIdentifier TextDocument { get; set; } = null!;

        /// <summary>
        /// Optional the content when saved. Depends on the includeText value
        /// when the save notification was requested.
        /// </summary>
        [Optional]
        public string? Text { get; set; }
    }
}
