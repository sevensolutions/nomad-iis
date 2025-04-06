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
using System;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

namespace NtCoreLib.Net.Firewall;

/// <summary>
/// Flags for a firewall layer.
/// </summary>
[Flags]
public enum FirewallLayerFlags
{
    None = 0,
    [SDKName("FWPM_LAYER_FLAG_KERNEL")]
    Kernel = 0x00000001,
    [SDKName("FWPM_LAYER_FLAG_BUILTIN")]
    Builtin = 0x00000002,
    [SDKName("FWPM_LAYER_FLAG_CLASSIFY_MOSTLY")]
    ClassifyMostly = 0x00000004,
    [SDKName("FWPM_LAYER_FLAG_BUFFERED")]
    Buffered = 0x00000008,
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member