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

using NtCoreLib.Net;
using System.IO;
using System.Net.Sockets;
using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Client;

/// <summary>
/// A class to make requests to over TCP.
/// </summary>
public sealed class KerberosKDCClientTransportTCP : IKerberosKDCClientTransport
{
    #region Private Members
    private readonly string _hostname;
    private readonly int _port;
    #endregion

    #region Constructors.
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="hostname">The hostname of the KDC.</param>
    /// <param name="port">The port of the KDC.</param>
    public KerberosKDCClientTransportTCP(string hostname, int port)
    {
        // Trim any UNC prefix.
        hostname = hostname?.TrimStart('\\');

        if (string.IsNullOrWhiteSpace(hostname))
        {
            throw new System.ArgumentException($"'{nameof(hostname)}' cannot be null or whitespace.", nameof(hostname));
        }

        _hostname = hostname;
        _port = port;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Method to send and receive data to the KDC.
    /// </summary>
    /// <param name="request">The output request token.</param>
    /// <returns>Returns the reply.</returns>
    public byte[] SendReceive(byte[] request)
    {
        using var socket = new TcpClient(_hostname, _port);
        using var stm = socket.GetStream();
        BinaryWriter writer = new(stm, Encoding.ASCII, true);
        writer.WriteInt32BE(request.Length);
        writer.Write(request);
        BinaryReader reader = new(stm, Encoding.ASCII, true);
        int return_length = reader.ReadInt32BE();
        return reader.ReadAllBytes(return_length);
    }

    #endregion
}
