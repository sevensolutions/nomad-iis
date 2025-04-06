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

// NOTE: This file is a modified version of NdrParser.cs from OleViewDotNet
// https://github.com/tyranid/oleviewdotnet. It's been relicensed from GPLv3 by
// the original author James Forshaw to be used under the Apache License for this

using System;

namespace NtCoreLib.Ndr.Dce;
#pragma warning disable 1591
[Flags]
[Serializable]
public enum NdrParamAttributes : ushort
{
    None = 0,
    MustSize = 0x0001,
    MustFree = 0x0002,
    IsPipe = 0x0004,
    IsIn = 0x0008,
    IsOut = 0x0010,
    IsReturn = 0x0020,
    IsBasetype = 0x0040,
    IsByValue = 0x0080,
    IsSimpleRef = 0x0100,
    IsDontCallFreeInst = 0x0200,
    SaveForAsyncFinish = 0x0400,
    Unused0800 = 0x0800,
    Unused1000 = 0x1000,
    ServerAllocSize2000 = 0x2000,  // This is the server alloc size.
    ServerAllocSize4000 = 0x4000,
    ServerAllocSize8000 = 0x8000
}

#pragma warning restore 1591
