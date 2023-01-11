﻿using ProtoBuf;

namespace Rubberduck.InternalApi.RPC.LSP.Capabilities
{
    [ProtoContract(Name = "referenceClientCapabilities")]
    public class ReferenceClientCapabilities
    {
        /// <summary>
        /// Whether the client supports dynamic registration.
        /// </summary>
        [ProtoMember(1, Name = "dynamicRegistration")]
        public bool SupportsDynamicRegistration { get; set; }
    }
}