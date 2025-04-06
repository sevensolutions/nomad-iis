﻿//  Copyright 2021 Google LLC. All Rights Reserved.
//
//  Licensed under the Apache License, Version 2.0 (the "License");
//  you may not use this file except in compliance with the License.
//  You may obtain a copy of the License at
//
//  http://www.apache.org/licenses/LICENSE-2.0
//
//  Unless required by applicable law or agreed to in writing, software
//  distributed under the License is distributed on an "AS IS" BASIS,
//  WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
//  See the License for the specific language governing permissions and
//  limitations under the License.

using NtCoreLib.Utilities.Reflection;
using System;

namespace NtCoreLib.Net.Sockets.Security;

/// <summary>
/// Socket security IPsec flags.
/// </summary>
[Flags]
public enum SocketSecurityIpsecFlags
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    None = 0,
    [SDKName("SOCKET_SETTINGS_IPSEC_SKIP_FILTER_INSTANTIATION")]
    SkipFilterInstantiation = 0x1,
    [SDKName("SOCKET_SETTINGS_IPSEC_OPTIONAL_PEER_NAME_VERIFICATION")]
    OptionalPeerNameValidation = 0x2,
    [SDKName("SOCKET_SETTINGS_IPSEC_ALLOW_FIRST_INBOUND_PKT_UNENCRYPTED")]
    AllowFirstInboundPktUnencrypted = 0x4,
    [SDKName("SOCKET_SETTINGS_IPSEC_PEER_NAME_IS_RAW_FORMAT")]
    PeerNameIsRawFormat = 0x8,
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
