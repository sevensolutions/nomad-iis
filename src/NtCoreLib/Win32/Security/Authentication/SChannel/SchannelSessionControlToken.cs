﻿//  Copyright 2021 Google Inc. All Rights Reserved.
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

using NtCoreLib.Utilities.Data;

namespace NtCoreLib.Win32.Security.Authentication.Schannel;

/// <summary>
/// Class to represent an Schannel shutdown control token.
/// </summary>
public sealed class SchannelSessionControlToken : SchannelControlToken
{
    private const int SCHANNEL_SESSION = 3;
    private readonly SchannelSessionFlags _flags;

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="flags">The session flags.</param>
    public SchannelSessionControlToken(SchannelSessionFlags flags)
    {
        _flags = flags;
    }

    private protected override void WriteBuffer(DataWriter writer)
    {
        writer.Write(SCHANNEL_SESSION);
        writer.Write((int)_flags);
    }
}
