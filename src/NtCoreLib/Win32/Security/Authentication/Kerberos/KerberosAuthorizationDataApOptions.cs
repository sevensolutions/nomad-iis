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

using System;
using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos;

/// <summary>
/// Class to represent the AD-AUTH-DATA-AP-OPTIONS authorization data.
/// </summary>
public sealed class KerberosAuthorizationDataApOptions : KerberosAuthorizationData
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="flags">The AP options flags.</param>
    public KerberosAuthorizationDataApOptions(KerberosApOptionsFlags flags) 
        : base(KerberosAuthorizationDataType.AD_AUTH_DATA_AP_OPTIONS)
    {
        Flags = flags;
    }

    /// <summary>
    /// Flags for the AD-AUTH-DATA-AP-OPTIONS authorization data.
    /// </summary>
    public KerberosApOptionsFlags Flags { get; }

    private protected override void FormatData(StringBuilder builder)
    {
        builder.AppendLine($"Flags           : {Flags}");
    }

    private protected override byte[] GetData()
    {
        return BitConverter.GetBytes((uint)Flags);
    }

    internal static bool Parse(byte[] data, out KerberosAuthorizationDataApOptions entry)
    {
        if (data.Length != 4)
        {
            entry = null;
            return false;
        }
        entry = new KerberosAuthorizationDataApOptions((KerberosApOptionsFlags)BitConverter.ToUInt32(data, 0));
        return true;
    }
}
