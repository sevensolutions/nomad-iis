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

#nullable enable

using Microsoft.Win32.SafeHandles;
using System;

namespace NtCoreLib.Win32.TerminalServices.Interop;

internal class SafeTerminalServerHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    public SafeTerminalServerHandle() : base(true)
    {
    }

    public SafeTerminalServerHandle(IntPtr handle, bool owns_handle)
    : base(owns_handle)
    {
        SetHandle(handle);
    }

    protected override bool ReleaseHandle()
    {
        NativeMethods.WTSCloseServer(handle);
        return true;
    }

    public static SafeTerminalServerHandle CurrentServer => new(IntPtr.Zero, false);
}
