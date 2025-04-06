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

using NtCoreLib.Utilities.Reflection;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NtCoreLib.Net.Firewall;

/// <summary>
/// IPsec authentication type.
/// </summary>
[SDKName("IPSEC_AUTH_TYPE")]
public enum IPsecAuthType
{
    [SDKName("IPSEC_AUTH_MD5")]
    MD5 = 0,
    [SDKName("IPSEC_AUTH_SHA_1")]
    SHA1 = (MD5 + 1),
    [SDKName("IPSEC_AUTH_SHA_256")]
    SHA256 = (SHA1 + 1),
    [SDKName("IPSEC_AUTH_AES_128")]
    AES128 = (SHA256 + 1),
    [SDKName("IPSEC_AUTH_AES_192")]
    AES192 = (AES128 + 1),
    [SDKName("IPSEC_AUTH_AES_256")]
    AES256 = (AES192 + 1)
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member