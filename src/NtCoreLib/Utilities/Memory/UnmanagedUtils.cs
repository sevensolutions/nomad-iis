﻿//  Copyright 2021 Google LLC. All Rights Reserved.
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
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

namespace NtCoreLib.Utilities.Memory;

internal static class UnmanagedUtils
{
    internal static T ReadStruct<T>(this SafeBuffer buffer)
    {
        return ReadStruct<T>(buffer.DangerousGetHandle());
    }

    internal static T ReadStruct<T>(this IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return default;
        return Marshal.PtrToStructure<T>(ptr);
    }

    internal static Guid? ReadGuid(this IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return null;
        return ReadStruct<Guid>(ptr);
    }

    internal static T[] ReadArray<T>(this IntPtr ptr, int count)
    {
        if (ptr == IntPtr.Zero)
            return null;
        T[] ret = new T[count];
        int element_size = Marshal.SizeOf<T>();
        for (int i = 0; i < count; ++i)
        {
            ret[i] = ReadStruct<T>(ptr + (i * element_size));
        }
        return ret;
    }

    internal static string[] ReadStringArray(this IntPtr ptr, int count)
    {
        if (ptr == IntPtr.Zero)
            return null;
        return ReadArray<IntPtr>(ptr, count).Select(p => Marshal.PtrToStringUni(p)).ToArray();
    }

    internal static string ReadAnsiStringZ(this Stream stm)
    {
        StringBuilder builder = new();
        int ch = stm.ReadByte();
        while (ch > 0)
        {
            builder.Append((char)ch);
            ch = stm.ReadByte();
        }
        return builder.ToString();
    }

    internal static string ReadUnicodeStringZ(this Stream stm)
    {
        StringBuilder builder = new();
        BinaryReader reader = new(stm, Encoding.Unicode);
        char ch = reader.ReadChar();
        while (ch != 0)
        {
            builder.Append(ch);
            ch = reader.ReadChar();
        }
        return builder.ToString();
    }
}
