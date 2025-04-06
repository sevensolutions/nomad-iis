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

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Builder;

/// <summary>
/// Builder class for kerberos authorization data.
/// </summary>
public abstract class KerberosAuthorizationDataBuilder
{
    /// <summary>
    /// Type of authentication data.
    /// </summary>
    public KerberosAuthorizationDataType DataType { get; }

    /// <summary>
    /// Create the authorization data.
    /// </summary>
    /// <returns>The authorization data.</returns>
    public abstract KerberosAuthorizationData Create();

    private protected KerberosAuthorizationDataBuilder(KerberosAuthorizationDataType data_type)
    {
        DataType = data_type;
    }
}
