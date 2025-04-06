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
/// Network interface type.
/// </summary>
/// <remarks>See https://www.iana.org/assignments/ianaiftype-mib</remarks>
public enum NetworkInterfaceType : uint
{
    [SDKName("IF_TYPE_OTHER")]
    Other = 1,
    [SDKName("IF_TYPE_TUNNEL")]
    Tunnel = 131
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member