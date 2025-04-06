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

namespace NtCoreLib.Ndr.Marshal;

/// <summary>
/// Structure to hold an NDR system handle.
/// </summary>
public readonly struct NdrSystemHandle
{
    /// <summary>
    /// The object handle.
    /// </summary>
    public NtObject Handle { get; }

    /// <summary>
    /// The desired access mask.
    /// </summary>
    public uint DesiredAccess { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="handle">The object handle.</param>
    /// <param name="desired_access">The desired access mask.</param>
    public NdrSystemHandle(NtObject handle, uint desired_access)
    {
        Handle = handle;
        DesiredAccess = desired_access;
    }
}