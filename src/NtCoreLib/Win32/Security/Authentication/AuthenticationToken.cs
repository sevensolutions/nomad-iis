﻿//  Copyright 2020 Google Inc. All Rights Reserved.
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

using NtCoreLib.Utilities.Text;
using NtCoreLib.Win32.Security.Authentication.CredSSP;
using NtCoreLib.Win32.Security.Authentication.Digest;
using NtCoreLib.Win32.Security.Authentication.Kerberos;
using NtCoreLib.Win32.Security.Authentication.Negotiate;
using NtCoreLib.Win32.Security.Authentication.Ntlm;
using NtCoreLib.Win32.Security.Authentication.Schannel;
using System;
using System.Collections.Generic;

namespace NtCoreLib.Win32.Security.Authentication;

/// <summary>
/// Base class to represent an authentication token.
/// </summary>
public class AuthenticationToken
{
    private readonly byte[] _data;

    /// <summary>
    /// Decrypt the Authentication Token using a keyset.
    /// </summary>
    /// <param name="keyset">The set of keys to decrypt the token.</param>
    /// <returns>The decrypted token, or the same token if nothing could be decrypted.</returns>
    public virtual AuthenticationToken Decrypt(IEnumerable<AuthenticationKey> keyset)
    {
        return this;
    }

    /// <summary>
    /// Decrypt the Authentication Token using a key.
    /// </summary>
    /// <param name="key">The keys to decrypt the token.</param>
    /// <returns>The decrypted token, or the same token if nothing could be decrypted.</returns>
    public AuthenticationToken Decrypt(AuthenticationKey key)
    {
        return Decrypt(new[] { key });
    }

    /// <summary>
    /// Convert the authentication token to a byte array.
    /// </summary>
    /// <returns>The byte array.</returns>
    public virtual byte[] ToArray()
    {
        return _data.CloneBytes();
    }

    /// <summary>
    /// Get the length of the token in bytes.
    /// </summary>
    public virtual int Length => _data.Length;

    /// <summary>
    /// Get whether the token is empty.
    /// </summary>
    public virtual bool IsEmpty => _data.Length == 0;

    /// <summary>
    /// Format the authentication token.
    /// </summary>
    /// <returns>The token as a formatted string.</returns>
    public virtual string Format()
    {
        if (_data.Length == 0)
            return string.Empty;
        HexDumpBuilder builder = new(true, true, true, false, 0);
        builder.Append(_data);
        builder.Complete();
        return builder.ToString();
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="data">The authentication token data.</param>
    public AuthenticationToken(byte[] data)
    {
        _data = data.CloneBytes();
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public AuthenticationToken()
    {
        _data = Array.Empty<byte>();
    }

    /// <summary>
    /// Parse a structured authentication token.
    /// </summary>
    /// <param name="context">The authentication context.</param>
    /// <param name="token">The token to parse.</param>
    /// <returns>The parsed authentication token. If can't parse any other format returns
    /// a raw AuthenticationToken.</returns>
    public static AuthenticationToken Parse(IAuthenticationContext context, byte[] token)
    {
        if (context is null)
        {
            throw new ArgumentNullException(nameof(context));
        }

        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return Parse(context.PackageName, 0, context is IClientAuthenticationContext, token);
    }

    /// <summary>
    /// Parse a structured authentication token.
    /// </summary>
    /// <param name="package_name">The package name to parse as.</param>
    /// <param name="client">True if the token is from a client.</param>
    /// <param name="token">The token to parse.</param>
    /// <returns>The parsed authentication token. If can't parse any other format returns
    /// a raw AuthenticationToken.</returns>
    public static AuthenticationToken Parse(string package_name, bool client, byte[] token)
    {
        if (package_name is null)
        {
            throw new ArgumentNullException(nameof(package_name));
        }

        if (token is null)
        {
            throw new ArgumentNullException(nameof(token));
        }

        return Parse(package_name, 0, client, token);
    }

    internal static AuthenticationToken Parse(string package_name, int token_count, bool client, byte[] token)
    {
        if (token.Length == 0)
            return new AuthenticationToken(token);

        if (AuthenticationPackage.CheckNtlm(package_name) 
            && NtlmAuthenticationToken.TryParse(token, token_count, client, out NtlmAuthenticationToken ntlm_token))
        {
            return ntlm_token;
        }

        if (AuthenticationPackage.CheckKerberos(package_name) 
            && KerberosAuthenticationToken.TryParse(token, token_count, client, out KerberosAuthenticationToken kerb_token))
        {
            return kerb_token;
        }

        if (AuthenticationPackage.CheckNegotiate(package_name))
        {
            if (NegotiateAuthenticationToken.TryParse(token, token_count,
            client, out NegotiateAuthenticationToken nego_token))
            {
                return nego_token;
            }
            if (NtlmAuthenticationToken.TryParse(token, token_count, client, 
                out NtlmAuthenticationToken nego_ntlm_token))
            {
                return nego_ntlm_token;
            }
            if (KerberosAuthenticationToken.TryParse(token, token_count, client, 
                out KerberosAuthenticationToken nego_kerb_token))
            {
                return nego_kerb_token;
            }
            return new AuthenticationToken(token);
        }

        if (AuthenticationPackage.CheckDigest(package_name) &&
            DigestAuthenticationToken.TryParse(token, out DigestAuthenticationToken digest_token))
        {
            return digest_token;
        }

        if ((AuthenticationPackage.CheckSChannel(package_name) || AuthenticationPackage.CheckCredSSP(package_name))
            && SchannelAuthenticationToken.TryParse(token, token_count, client, out SchannelAuthenticationToken schannel_token))
        {
            return schannel_token;
        }

        if (AuthenticationPackage.CheckTSSSP(package_name)
            && TSAuthenticationToken.TryParse(token, token_count,
            client, out TSAuthenticationToken credssp_token))
        {
            return credssp_token;
        }

        if (ASN1AuthenticationToken.TryParse(token, token_count, 
            client, out ASN1AuthenticationToken asn1_token))
        {
            return asn1_token;
        }

        return new AuthenticationToken(token);
    }
}
