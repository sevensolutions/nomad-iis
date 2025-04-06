﻿//  Copyright 2019 Google Inc. All Rights Reserved.
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

namespace NtCoreLib.Security.Authorization;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member

/// <summary>
/// Object ACE flags.
/// </summary>
[Flags]
public enum ObjectAceFlags : uint
{
    None = 0,
    [SDKName("ACE_OBJECT_TYPE_PRESENT")]
    ObjectTypePresent = 0x1,
    [SDKName("ACE_INHERITED_OBJECT_TYPE_PRESENT")]
    InheritedObjectTypePresent = 0x2,
}

#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member