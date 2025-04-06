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

using NtCoreLib.Win32.Rpc.EndpointMapper;

#nullable enable

namespace NtCoreLib.Win32.Rpc.Transport;

/// <summary>
/// Interface to implement an RPC client transport factory.
/// </summary>
public interface IRpcClientTransportFactory
{
    /// <summary>
    /// Connect a new RPC client transport.
    /// </summary>
    /// <param name="binding">The RPC string binding.</param>
    /// <param name="transport_security">Security for the transport.</param>
    /// <param name="config">Optional transport configuration for the connection.</param>
    /// <returns>The connected transport.</returns>
    IRpcClientTransport Connect(RpcStringBinding binding, RpcTransportSecurity transport_security, 
        RpcClientTransportConfiguration? config);
}
