﻿//  Copyright 2018 Google Inc. All Rights Reserved.
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

// NOTE: This file is a modified version of SymbolResolver.cs from OleViewDotNet
// https://github.com/tyranid/oleviewdotnet. It's been relicensed from GPLv3 by
// the original author James Forshaw to be used under the Apache License for this
// project.

using System;
using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.Debugger.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct IMAGEHLP_DEFERRED_SYMBOL_LOADW
{
    public int SizeOfStruct;
    public long BaseOfImage;
    public int CheckSum;
    public int TimeDateStamp;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 261)]
    public string FileName;
    [MarshalAs(UnmanagedType.U1)]
    public bool Reparse;
    public IntPtr hFile;
    public int Flags;
}
