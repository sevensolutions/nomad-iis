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

using NtCoreLib.Utilities.ASN1;
using NtCoreLib.Win32.Security.Authentication.Kerberos;
using NtCoreLib.Win32.Security.Authentication.NegoEx;
using NtCoreLib.Win32.Security.Authentication.Ntlm;
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Negotiate;

/// <summary>
/// SPNEGO Authentication Token.
/// </summary>
public abstract class NegotiateAuthenticationToken : ASN1AuthenticationToken
{
    /// <summary>
    /// The negotiated authentication token.
    /// </summary>
    public AuthenticationToken Token { get; private set; }

    /// <summary>
    /// Optional message integrity code.
    /// </summary>
    public byte[] MessageIntegrityCode { get; }

    /// <summary>
    /// Decrypt the Authentication Token using a keyset.
    /// </summary>
    /// <param name="keyset">The set of keys to decrypt the </param>
    /// <returns>The decrypted token, or the same token if nothing could be decrypted.</returns>
    public override AuthenticationToken Decrypt(IEnumerable<AuthenticationKey> keyset)
    {
        if (Token == null)
            return this;
        var ret = (NegotiateAuthenticationToken)MemberwiseClone();
        ret.Token = Token.Decrypt(keyset);
        return ret;
    }

    /// <summary>
    /// Format the authentication token.
    /// </summary>
    /// <returns>The token as a formatted string.</returns>
    public override string Format()
    {
        StringBuilder builder = new();
        builder.AppendLine($"<SPNEGO {(this is NegotiateInitAuthenticationToken ? "Init" : "Response")}>");
        FormatData(builder);
        if (MessageIntegrityCode?.Length > 0)
        {
            builder.AppendLine($"MIC             : {NtObjectUtils.ToHexString(MessageIntegrityCode)}");
        }
        string token_format = Token?.Format() ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(token_format))
        {
            builder.AppendLine("<SPNEGO Token>");
            builder.AppendLine(token_format.TrimEnd());
            builder.AppendLine("</SPNEGO Token>");
        }
        return builder.ToString();
    }

    private protected abstract void FormatData(StringBuilder builder);

    private static AuthenticationToken ParseToken(byte[] data, int token_count, bool client)
    {
        if (NtlmAuthenticationToken.TryParse(data, token_count, client, out NtlmAuthenticationToken ntlm_token))
        {
            return ntlm_token;
        }

        if (NegoExAuthenticationToken.TryParse(data, token_count, client, out NegoExAuthenticationToken negoex_token))
        {
            return negoex_token;
        }

        if (KerberosAuthenticationToken.TryParse(data, token_count, client, out KerberosAuthenticationToken kerb_token))
        {
            return kerb_token;
        }

        return new AuthenticationToken(data);
    }

    private static IEnumerable<string> ParseMechList(DERValue[] values)
    {
        List<string> mech_list = new();
        if (values.CheckValueSequence())
        {
            foreach (var next in values[0].Children)
            {
                if (!next.CheckPrimitive(UniversalTag.OBJECT_IDENTIFIER))
                {
                    throw new InvalidDataException();
                }
                mech_list.Add(next.ReadObjID());
            }
        }
        return mech_list.AsReadOnly();
    }

    private static NegotiateContextFlags ConvertContextFlags(BitArray flags)
    {
        if (flags.Length > 32)
            throw new InvalidDataException();
        int ret = 0;
        for (int i = 0; i < flags.Length; ++i)
        {
            if (flags[i])
                ret |= (1 << i);
        }
        return (NegotiateContextFlags)ret;
    }

    private static bool ParseNegoHint(DERValue value, out string hint_name, out byte[] hint_address)
    {
        hint_name = null;
        hint_address = null;

        foreach (var next in value.Children)
        {
            if (next.Type != DERTagType.ContextSpecific)
                return false;
            switch (next.Tag)
            {
                case 0:
                    hint_name = next.ReadChildGeneralString();
                    break;
                case 1:
                    hint_address = next.ReadChildOctetString();
                    break;
                default:
                    return false;
            }
        }

        return true;
    }

    private static bool ParseInit(byte[] data, DERValue[] values, int token_count, bool client, out NegotiateAuthenticationToken token)
    {
        token = null;
        if (!values.CheckValueSequence())
        {
            return false;
        }

        IEnumerable<string> mech_list = null;
        NegotiateContextFlags? flags = null;
        AuthenticationToken auth_token = null;
        string hint_name = null;
        byte[] hint_address = null;
        bool init2 = false;
        byte[] mic = null;

        foreach (var next in values[0].Children)
        {
            if (next.Type != DERTagType.ContextSpecific)
                return false;
            switch (next.Tag)
            {
                case 0:
                    mech_list = ParseMechList(next.Children);
                    break;
                case 1:
                    flags = ConvertContextFlags(next.ReadChildBitString());
                    break;
                case 2:
                    auth_token = ParseToken(next.ReadChildOctetString(), token_count, client);
                    break;
                case 3:
                    // If NegTokenInit2 then just ignore neg hints.
                    if (next.HasChildren() && next.Children[0].CheckSequence())
                    {
                        init2 = true;
                        if (!ParseNegoHint(next.Children[0], out hint_name, out hint_address))
                            return false;
                    }
                    else
                    {
                        mic = next.ReadChildOctetString();
                    }
                    break;
                case 4:
                    // Used if NegTokenInit2.
                    mic = next.ReadChildOctetString();
                    break;
                default:
                    return false;
            }
        }

        if (init2)
        {
            token = new NegotiateInit2AuthenticationToken(data, mech_list, flags, auth_token, mic, hint_name, hint_address);
        }
        else
        {
            token = new NegotiateInitAuthenticationToken(data, mech_list, flags, auth_token, mic);
        }
        return true;
    }

    private static bool ParseResp(byte[] data, DERValue[] values, int token_count, bool client, out NegotiateAuthenticationToken token)
    {
        token = null;
        if (!values.CheckValueSequence())
        {
            return false;
        }

        string mech = null;
        NegotiateAuthenticationState? state = null;
        AuthenticationToken auth_token = null;
        byte[] mic = null;

        foreach (var next in values[0].Children)
        {
            if (next.Type != DERTagType.ContextSpecific)
                return false;
            switch (next.Tag)
            {
                case 0:
                    state = (NegotiateAuthenticationState)next.ReadChildEnumerated();
                    break;
                case 1:
                    mech = next.ReadChildObjID();
                    break;
                case 2:
                    auth_token = ParseToken(next.ReadChildOctetString(), token_count, client);
                    break;
                case 3:
                    mic = next.ReadChildOctetString();
                    break;
                default:
                    return false;
            }
        }

        token = new NegotiateResponseAuthenticationToken(data, mech, state, auth_token, mic);
        return true;
    }

    private protected NegotiateAuthenticationToken(byte[] data, AuthenticationToken token, byte[] mic) 
        : base(data)
    {
        Token = token;
        MessageIntegrityCode = mic;
    }

    #region Public Static Methods
    /// <summary>
    /// Parse bytes into a negotiate token.
    /// </summary>
    /// <param name="data">The negotiate token in bytes.</param>
    /// <returns>The Negotiate token.</returns>
    public static NegotiateAuthenticationToken Parse(byte[] data)
    {
        if (!TryParse(data, 0, false, out NegotiateAuthenticationToken token))
        {
            throw new ArgumentException("Invalid authentication token.", nameof(data));
        }
        return token;
    }
    #endregion

    #region Internal Static Methods
    internal static bool TryParse(byte[] data, int token_count, bool client, out NegotiateAuthenticationToken token)
    {
        token = null;
        if (data == null)
            return false;
        try
        {
            byte[] token_data;
            if (GSSAPIUtils.TryParse(data, out token_data, out string oid))
            {
                if (oid != OIDValues.SPNEGO)
                {
                    return false;
                }
            }
            else
            {
                token_data = data;
            }

            DERValue[] values = DERParser.ParseData(token_data, 0);
            if (values.Length != 1 || values[0].Type != DERTagType.ContextSpecific)
            {
                return false;
            }

            if (values[0].CheckContext(0))
            {
                return ParseInit(data, values[0].Children, token_count, client, out token);
            }
            else if (values[0].CheckContext(1))
            {
                return ParseResp(data, values[0].Children, token_count, client, out token);
            }
            else
            {
                return false;
            }
        }
        catch
        {
        }
        return false;
    }
    #endregion
}
