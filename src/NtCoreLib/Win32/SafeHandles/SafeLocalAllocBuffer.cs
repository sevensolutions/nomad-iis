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

using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Win32.Memory.Interop;
using System;

namespace NtCoreLib.Win32.SafeHandles;

internal sealed class SafeLocalAllocBuffer : SafeBufferGeneric
{
    protected override bool ReleaseHandle()
    {
        return NativeMethods.LocalFree(handle) == IntPtr.Zero;
    }

    public SafeLocalAllocBuffer(IntPtr handle, bool owns_handle)
        : base(handle, 0, owns_handle)
    {
    }

    public SafeLocalAllocBuffer() : base(true)
    {
    }

    public override bool IsInvalid => handle == IntPtr.Zero;
}
