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
//
//  Note this is relicensed from OleViewDotNet by the author.

using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.SideBySide.Interop;

[StructLayout(LayoutKind.Sequential)]
struct ACTIVATION_CONTEXT_DATA_COM_SERVER_REDIRECTION_SHIM
{
    public int Size;
    public int Flags;
    ACTIVATION_CONTEXT_DATA_COM_SERVER_REDIRECTION_SHIM_TYPE Type;
    public int ModuleLength; // in bytes
    public int ModuleOffset; // offset from section base
    public int TypeLength; // in bytes
    public int TypeOffset; // offset from ACTIVATION_CONTEXT_DATA_COM_SERVER_REDIRECTION_SHIM
    public int ShimVersionLength; // in bytes
    public int ShimVersionOffset; // offset from ACTIVATION_CONTEXT_DATA_COM_SERVER_REDIRECTION_SHIM
    public int DataLength; // in bytes
    public int DataOffset; // offset from ACTIVATION_CONTEXT_DATA_COM_SERVER_REDIRECTION_SHIM
}
