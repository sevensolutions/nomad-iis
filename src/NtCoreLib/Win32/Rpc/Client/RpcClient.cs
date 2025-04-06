﻿//  Copyright 2019 Google Inc. All Rights Reserved.
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

using NtCoreLib.Ndr.Marshal;
using NtCoreLib.Ndr.Rpc;
using NtCoreLib.Win32.Rpc.Server;
using System;

namespace NtCoreLib.Win32.Rpc.Client;

/// <summary>
/// Generic RPC client.
/// </summary>
public sealed class RpcClient : RpcClientBase
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="interface_id">The interface ID.</param>
    public RpcClient(RpcSyntaxIdentifier interface_id) : base(interface_id)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="interface_id">The interface ID.</param>
    /// <param name="interface_version">Version of the interface.</param>
    public RpcClient(Guid interface_id, RpcVersion interface_version)
        : base(interface_id, interface_version)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="server">The RPC server to bind to.</param>
    public RpcClient(RpcServer server)
        : this(server.InterfaceId, server.InterfaceVersion)
    {
    }

    /// <summary>
    /// Send and receive an RPC message.
    /// </summary>
    /// <param name="proc_num">The procedure number.</param>
    /// <param name="ndr_buffer">Marshal NDR buffer for the call.</param>
    /// <returns>Unmarshal NDR buffer for the result.</returns>
    public INdrUnmarshalBuffer SendReceive(int proc_num, INdrMarshalBuffer ndr_buffer)
    {
        return SendReceiveTransport(proc_num, ndr_buffer);
    }
}