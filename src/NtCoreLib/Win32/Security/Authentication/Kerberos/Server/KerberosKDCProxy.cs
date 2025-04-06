﻿//  Copyright 2022 Google LLC. All Rights Reserved.
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

using NtCoreLib.Win32.Security.Authentication.Kerberos.Client;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Server;

/// <summary>
/// Class to represent a KDC proxy.
/// </summary>
public class KerberosKDCProxy : KerberosKDCServer
{
    private readonly IKerberosKDCClientTransport _client_transport;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="listener">The server listener.</param>
    /// <param name="client_transport">The kerberos client transport.</param>
    public KerberosKDCProxy(IKerberosKDCServerListener listener, IKerberosKDCClientTransport client_transport) 
        : base(listener)
    {
        _client_transport = client_transport;
    }

    /// <summary>
    /// Handle a request.
    /// </summary>
    /// <param name="request">The request to handle.</param>
    /// <returns>The reply.</returns>
    protected override byte[] HandleRequest(byte[] request)
    {
        return _client_transport.SendReceive(request);
    }
}
