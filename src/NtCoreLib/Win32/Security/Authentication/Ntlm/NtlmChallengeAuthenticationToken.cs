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

using NtCoreLib.Win32.Security.Authentication.Ntlm.Builder;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Ntlm;

/// <summary>
/// Class to represent an NTLM CHALLENGE token.
/// </summary>
public sealed class NtlmChallengeAuthenticationToken : NtlmAuthenticationToken
{
    #region Public Properties
    /// <summary>
    /// Target name.
    /// </summary>
    public string TargetName { get; }
    /// <summary>
    /// Server challenge.
    /// </summary>
    public byte[] ServerChallenge { get; }
    /// <summary>
    /// Reserved.
    /// </summary>
    public byte[] Reserved { get; }
    /// <summary>
    /// NTLM version.
    /// </summary>
    public Version Version { get; }
    /// <summary>
    /// NTLM Target Information.
    /// </summary>
    public IReadOnlyList<NtlmAvPair> TargetInfo { get; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Convert the authentication token to a builder.
    /// </summary>
    /// <returns>The NTLM authentication token builder.</returns>
    public override NtlmAuthenticationTokenBuilder ToBuilder()
    {
        NtlmChallengeAuthenticationTokenBuilder builder = new()
        {
            Flags = Flags,
            ServerChallenge = ServerChallenge.CloneBytes(),
            Reserved = Reserved.CloneBytes(),
            TargetName = TargetName,
            Version = Version
        };
        builder.TargetInfo.AddRange(TargetInfo);
        return builder;
    }

    /// <summary>
    /// Format the authentication token.
    /// </summary>
    /// <returns>The formatted token.</returns>
    public override string Format()
    {
        StringBuilder builder = new();
        builder.AppendLine("<NTLM CHALLENGE>");
        builder.AppendLine($"Flags     : {Flags}");
        if (!string.IsNullOrEmpty(TargetName))
        {
            builder.AppendLine($"TargetName: {TargetName}");
        }
        builder.AppendLine($"Challenge : {NtObjectUtils.ToHexString(ServerChallenge)}");
        builder.AppendLine($"Reserved  : {NtObjectUtils.ToHexString(Reserved)}");
        if (Version != null)
        {
            builder.AppendLine($"Version   : {Version}");
        }
        if (TargetInfo.Count > 0)
        {
            builder.AppendLine("=> Target Info");
            foreach (var pair in TargetInfo)
            {
                builder.AppendLine(pair.ToString());
            }
        }

        return builder.ToString();
    }
    #endregion

    #region Constructors
    private NtlmChallengeAuthenticationToken(byte[] data, NtlmNegotiateFlags flags, 
        string target_name, byte[] server_challenge, byte[] reserved, Version version,
        IEnumerable<NtlmAvPair> target_info)
        : base(data, NtlmMessageType.Challenge, flags)
    {
        TargetName = target_name;
        ServerChallenge = server_challenge;
        Reserved = reserved;
        Version = version;
        TargetInfo = target_info.ToList().AsReadOnly();
    }

    private static bool TryParseAvPairs(byte[] data, out List<NtlmAvPair> av_pairs)
    {
        return NtlmUtilsInternal.TryParseAvPairs(new BinaryReader(new MemoryStream(data)), out av_pairs);
    }

    #endregion

    #region Internal Methods
    internal static bool TryParse(byte[] data, BinaryReader reader, out NtlmAuthenticationToken token)
    {
        token = null;

        if (!NtlmUtilsInternal.TryParseStringValues(reader, out int target_name_length, out int target_name_position))
            return false;

        NtlmNegotiateFlags flags = (NtlmNegotiateFlags)reader.ReadInt32();

        byte[] server_challenge = reader.ReadBytes(8);
        if (server_challenge.Length < 8)
            return false;
        byte[] reserved = reader.ReadBytes(8);
        if (reserved.Length < 8)
            return false;

        if (!NtlmUtilsInternal.TryParseStringValues(reader, out int target_info_length, out int target_info_position))
            return false;

        if (!NtlmUtilsInternal.TryParse(reader, flags, out Version version))
            return false;

        string target_name = string.Empty;
        if (flags.HasFlagSet(NtlmNegotiateFlags.RequestTarget))
        {
            if (!NtlmUtilsInternal.ParseString(flags, data, target_name_length,
                target_name_position, out target_name))
            {
                return false;
            }
        }

        IEnumerable<NtlmAvPair> pairs = new NtlmAvPair[0];
        if (flags.HasFlagSet(NtlmNegotiateFlags.TargetInfo))
        {
            if (!NtlmUtilsInternal.ParseBytes(data, target_info_length, target_info_position, out byte[] target_info))
            {
                return false;
            }
            if (!TryParseAvPairs(target_info, out List<NtlmAvPair> list))
            {
                return false;
            }
            pairs = list;
        }

        token = new NtlmChallengeAuthenticationToken(data, flags, target_name, server_challenge, reserved,
            version, pairs);
        return true;
    }
    #endregion
}
