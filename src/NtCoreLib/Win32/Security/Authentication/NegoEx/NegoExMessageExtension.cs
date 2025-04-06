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

using System;

namespace NtCoreLib.Win32.Security.Authentication.NegoEx;

/// <summary>
/// Extension value for a NEGOEX message.
/// </summary>
public sealed class NegoExMessageExtension
{
    /// <summary>
    /// Whether the extension is critical.
    /// </summary>
    public bool Critical => Type < 0;

    /// <summary>
    /// The extension type.
    /// </summary>
    public int Type { get; }

    /// <summary>
    /// The extension value.
    /// </summary>
    public byte[] Value { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="type">The extension type.</param>
    /// <param name="value">The extension value.</param>
    public NegoExMessageExtension(int type, byte[] value)
    {
        Type = type;
        Value = (byte[])value?.Clone() ?? throw new ArgumentNullException(nameof(value));
    }
}
