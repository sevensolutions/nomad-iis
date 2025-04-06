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

using System;
using System.IO;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Builder;

/// <summary>
/// Class for a Kerberos authorization data signature.
/// </summary>
public sealed class KerberosAuthorizationDataPACSignatureBuilder : KerberosAuthorizationDataPACEntryBuilder
{
    internal KerberosAuthorizationDataPACSignatureBuilder(KerberosAuthorizationDataPACEntryType pac_type) : base(pac_type)
    {
        switch (pac_type)
        {
            case KerberosAuthorizationDataPACEntryType.TicketChecksum:
            case KerberosAuthorizationDataPACEntryType.ServerChecksum:
            case KerberosAuthorizationDataPACEntryType.KDCChecksum:
            case KerberosAuthorizationDataPACEntryType.FullPacChecksum:
                break;
            default:
                System.Diagnostics.Debug.Assert(false, "The type must be one of the checksum types.");
                break;
        }
    }

    internal KerberosAuthorizationDataPACSignatureBuilder(KerberosAuthorizationDataPACEntryType pac_type, 
        KerberosChecksumType signature_type, byte[] signature, int? rodc_identifier) : this(pac_type)
    {
        SignatureType = signature_type;
        Signature = signature;
        RODCIdentifier = rodc_identifier;
    }

    /// <summary>
    /// Signature type.
    /// </summary>
    public KerberosChecksumType SignatureType { get; set; }
    /// <summary>
    /// Signature.
    /// </summary>
    public byte[] Signature { get; set; }
    /// <summary>
    /// Read-only Domain Controller Identifier.
    /// </summary>
    public int? RODCIdentifier { get; set; }

    /// <summary>
    /// Create the authorization data.
    /// </summary>
    /// <returns>The authorization data object.</returns>
    public override KerberosAuthorizationDataPACEntry Create()
    {
        MemoryStream stream = new();
        BinaryWriter writer = new(stream);

        writer.Write((int)SignatureType);
        writer.Write(Signature);

        if (RODCIdentifier != null)
        {
            writer.Write((int)RODCIdentifier);
        }

        if (!KerberosAuthorizationDataPACSignature.Parse(PACType, stream.ToArray(), out KerberosAuthorizationDataPACEntry entry))
            throw new InvalidDataException("PAC signature entry is invalid.");
        return entry;
    }

    /// <summary>
    /// Update the signature using a key and data.
    /// </summary>
    /// <param name="key">The key to use for the update.</param>
    /// <param name="data">The data to use for the signature.</param>
    public void UpdateSignature(KerberosAuthenticationKey key, byte[] data)
    {
        if (key is null)
        {
            throw new ArgumentNullException(nameof(key));
        }

        if (data is null)
        {
            throw new ArgumentNullException(nameof(data));
        }

        SignatureType = key.ChecksumType;
        Signature = key.ComputeHash(data, KerberosKeyUsage.KerbNonKerbChksumSalt);
    }

    /// <summary>
    /// Create a ticket checksum builder.
    /// </summary>
    /// <returns>The ticket checksum builder.</returns>
    public static KerberosAuthorizationDataPACSignatureBuilder CreateTicketChecksum()
    {
        return new KerberosAuthorizationDataPACSignatureBuilder(KerberosAuthorizationDataPACEntryType.TicketChecksum);
    }

    /// <summary>
    /// Create a server checksum builder.
    /// </summary>
    /// <returns>The server checksum builder.</returns>
    public static KerberosAuthorizationDataPACSignatureBuilder CreateServerChecksum()
    {
        return new KerberosAuthorizationDataPACSignatureBuilder(KerberosAuthorizationDataPACEntryType.ServerChecksum);
    }

    /// <summary>
    /// Create a KDC checksum builder.
    /// </summary>
    /// <returns>The KDC checksum builder.</returns>
    public static KerberosAuthorizationDataPACSignatureBuilder CreateKDCChecksum()
    {
        return new KerberosAuthorizationDataPACSignatureBuilder(KerberosAuthorizationDataPACEntryType.KDCChecksum);
    }
}
