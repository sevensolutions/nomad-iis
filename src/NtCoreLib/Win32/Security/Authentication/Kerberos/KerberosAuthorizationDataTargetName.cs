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

using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos;

/// <summary>
/// Class to represent the AD-AUTH-DATA-TARGET-NAME authorization data.
/// </summary>
public sealed class KerberosAuthorizationDataTargetName : KerberosAuthorizationData
{
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="target_name">The target name.</param>
    public KerberosAuthorizationDataTargetName(string target_name)
        : base(KerberosAuthorizationDataType.AD_AUTH_DATA_TARGET_NAME)
    {
        TargetName = target_name ?? throw new System.ArgumentNullException(nameof(target_name));
    }

    /// <summary>
    /// The target name.
    /// </summary>
    public string TargetName { get; }

    private protected override void FormatData(StringBuilder builder)
    {
        builder.AppendLine($"Target Name     : {TargetName}");
    }

    private protected override byte[] GetData()
    {
        return Encoding.Unicode.GetBytes(TargetName);
    }

    internal static bool Parse(byte[] data, out KerberosAuthorizationDataTargetName entry)
    {
        if ((data.Length % 2) != 0)
        {
            entry = null;
            return false;
        }

        entry = new KerberosAuthorizationDataTargetName(Encoding.Unicode.GetString(data));
        return true;
    }
}
