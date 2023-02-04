﻿using Rubberduck.RPC.Platform.Metadata;
using Rubberduck.RPC.Proxy.LspServer.Server.Commands.Parameters;
using Rubberduck.RPC.Proxy.LspServer.TextDocument.Language.Model;
using System.Text.Json.Serialization;

namespace Rubberduck.RPC.Proxy.LspServer.TextDocument.Language.Commands.Parameters
{
    public class CallHierarchyIncomingCallsParams : WorkDoneProgressParams, IPartialResultParams
    {
        /// <summary>
        /// A token that the server can use to report partial results (e.g. streaming) to the client.
        /// </summary>
        [JsonPropertyName("partialResultToken"), LspCompliant]
        public string PartialResultToken { get; set; }

        [JsonPropertyName("item"), LspCompliant]
        public CallHierarchyItem Item { get; set; }
    }
}