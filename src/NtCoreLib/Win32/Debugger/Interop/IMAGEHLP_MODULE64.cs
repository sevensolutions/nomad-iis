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
internal struct IMAGEHLP_MODULE64
{
    public int SizeOfStruct;
    public long BaseOfImage;
    public int ImageSize;
    public int TimeDateStamp;
    public int CheckSum;
    public int NumSyms;
    public SYM_TYPE SymType;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)]
    public string ModuleName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string ImageName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string LoadedImageName;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
    public string LoadedPdbName;
    public int CVSig;
    [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260 * 3)]
    public string CVData;
    public int PdbSig;
    public Guid PdbSig70;
    public int PdbAge;
    [MarshalAs(UnmanagedType.Bool)]
    public bool PdbUnmatched;
    [MarshalAs(UnmanagedType.Bool)]
    public bool DbgUnmatched;
    [MarshalAs(UnmanagedType.Bool)]
    public bool LineNumbers;
    [MarshalAs(UnmanagedType.Bool)]
    public bool GlobalSymbols;
    [MarshalAs(UnmanagedType.Bool)]
    public bool TypeInfo;
    [MarshalAs(UnmanagedType.Bool)]
    public bool SourceIndexed;
    [MarshalAs(UnmanagedType.Bool)]
    public bool Publics;
    public int MachineType;
    public int Reserved;
}
