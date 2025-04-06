﻿//  Copyright 2016, 2017 Google Inc. All Rights Reserved.
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

using System.Runtime.InteropServices;

namespace NtCoreLib.Image.Interop;

[StructLayout(LayoutKind.Sequential)]
internal struct IMAGE_EXPORT_DIRECTORY
{
    public uint Characteristics;
    public uint TimeDateStamp;
    public ushort MajorVersion;
    public ushort MinorVersion;
    public int Name;
    public int Base;
    public int NumberOfFunctions;
    public int NumberOfNames;
    public int AddressOfFunctions;     // RVA from base of image
    public int AddressOfNames;     // RVA from base of image
    public int AddressOfNameOrdinals;  // RVA from base of image
}
