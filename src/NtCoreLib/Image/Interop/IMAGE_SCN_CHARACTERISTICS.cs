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

using System;

namespace NtCoreLib.Image.Interop;

[Flags]
internal enum IMAGE_SCN_CHARACTERISTICS : uint
{
    IMAGE_SCN_TYPE_REG = 0x00000000,
    IMAGE_SCN_TYPE_DSECT = 0x00000001,
    IMAGE_SCN_TYPE_NOLOAD = 0x00000002,
    IMAGE_SCN_TYPE_GROUP = 0x00000004,
    IMAGE_SCN_TYPE_NO_PAD = 0x00000008,
    IMAGE_SCN_TYPE_COPY = 0x00000010,
    IMAGE_SCN_CNT_CODE = 0x00000020,
    IMAGE_SCN_CNT_INITIALIZED_DATA = 0x00000040,
    IMAGE_SCN_CNT_UNINITIALIZED_DATA = 0x00000080,
    IMAGE_SCN_LNK_OTHER = 0x00000100,
    IMAGE_SCN_LNK_INFO = 0x00000200,
    IMAGE_SCN_TYPE_OVER = 0x00000400,
    IMAGE_SCN_LNK_REMOVE = 0x00000800,
    IMAGE_SCN_LNK_COMDAT = 0x00001000,
    IMAGE_SCN_UNUSED_000020000 = 0x00002000,
    IMAGE_SCN_MEM_PROTECTED = 0x00004000,
    IMAGE_SCN_NO_DEFER_SPEC_EXC = 0x00004000,
    IMAGE_SCN_GPREL = 0x00008000,
    IMAGE_SCN_MEM_FARDATA = 0x00008000,
    IMAGE_SCN_MEM_SYSHEAP = 0x00010000,
    IMAGE_SCN_MEM_PURGEABLE = 0x00020000,
    IMAGE_SCN_MEM_16BIT = 0x00020000,
    IMAGE_SCN_MEM_LOCKED = 0x00040000,
    IMAGE_SCN_MEM_PRELOAD = 0x00080000,
    IMAGE_SCN_ALIGN_1BYTES = 0x00100000,
    IMAGE_SCN_ALIGN_2BYTES = 0x00200000,
    IMAGE_SCN_ALIGN_4BYTES = 0x00300000,
    IMAGE_SCN_ALIGN_8BYTES = 0x00400000,
    IMAGE_SCN_ALIGN_16BYTES = 0x00500000,
    IMAGE_SCN_ALIGN_32BYTES = 0x00600000,
    IMAGE_SCN_ALIGN_64BYTES = 0x00700000,
    IMAGE_SCN_ALIGN_128BYTES = 0x00800000,
    IMAGE_SCN_ALIGN_256BYTES = 0x00900000,
    IMAGE_SCN_ALIGN_512BYTES = 0x00A00000,
    IMAGE_SCN_ALIGN_1024BYTES = 0x00B00000,
    IMAGE_SCN_ALIGN_2048BYTES = 0x00C00000,
    IMAGE_SCN_ALIGN_4096BYTES = 0x00D00000,
    IMAGE_SCN_ALIGN_8192BYTES = 0x00E00000,
    IMAGE_SCN_LNK_NRELOC_OVFL = 0x01000000,
    IMAGE_SCN_MEM_DISCARDABLE = 0x02000000,
    IMAGE_SCN_MEM_NOT_CACHED = 0x04000000,
    IMAGE_SCN_MEM_NOT_PAGED = 0x08000000,
    IMAGE_SCN_MEM_SHARED = 0x10000000,
    IMAGE_SCN_MEM_EXECUTE = 0x20000000,
    IMAGE_SCN_MEM_READ = 0x40000000,
    IMAGE_SCN_MEM_WRITE = 0x80000000,
}
