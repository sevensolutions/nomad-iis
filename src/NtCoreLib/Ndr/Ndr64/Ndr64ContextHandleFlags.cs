﻿//  Copyright 2023 Google LLC. All Rights Reserved.
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

namespace NtCoreLib.Ndr.Ndr64;

/// <summary>
/// Flags for a context handle.
/// </summary>
[Flags]
public enum Ndr64ContextHandleFlags : byte
{
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    None = 0,
    CannotBeNull = 0x1,
    Serialize = 0x2,
    NoSerialize = 0x4,
    Strict = 0x8,
    IsReturn = 0x10,
    IsOut = 0x20,
    IsIn = 0x40,
    IsViaPointer = 0x80
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
}
