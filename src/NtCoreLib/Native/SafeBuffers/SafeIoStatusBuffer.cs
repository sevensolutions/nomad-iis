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

using System;

namespace NtCoreLib.Native.SafeBuffers;

/// <summary>
/// Non-generic buffer to hold an IO_STATUS_BLOCK.
/// </summary>
public sealed class SafeIoStatusBuffer : SafeStructureInOutBuffer<IoStatusBlock>
{
    private SafeIoStatusBuffer(int dummy_length) : base(IntPtr.Zero, dummy_length, false)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    public SafeIoStatusBuffer()
    {
    }

    /// <summary>
    /// Get a buffer which represents NULL.
    /// </summary>
    new public static SafeIoStatusBuffer Null { get { return new SafeIoStatusBuffer(0); } }
}
