﻿//  Copyright 2016 Google Inc. All Rights Reserved.
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
using System.Runtime.InteropServices;

namespace NtCoreLib;

#pragma warning disable 1591
[StructLayout(LayoutKind.Explicit)]
public class LargeInteger
{
    [FieldOffset(0)]
    public uint LowPart;
    [FieldOffset(4)]
    public int HighPart;
    [FieldOffset(0)]
    public long QuadPart;

    public LargeInteger()
    {
    }

    public LargeInteger(long value)
    {
        QuadPart = value;
    }

    internal DateTime ToDateTime()
    {
        return DateTime.FromFileTime(QuadPart);
    }

    internal LargeIntegerStruct ToStruct()
    {
        return new LargeIntegerStruct() { QuadPart = QuadPart };
    }

    internal NtWaitTimeout ToTimeout()
    {
        return new NtWaitTimeout(QuadPart);
    }

    public override string ToString()
    {
        return QuadPart.ToString();
    }
}

[StructLayout(LayoutKind.Explicit)]
public struct LargeIntegerStruct
{
    [FieldOffset(0)]
    public uint LowPart;
    [FieldOffset(4)]
    public int HighPart;
    [FieldOffset(0)]
    public long QuadPart;

    internal DateTime ToDateTime()
    {
        try
        {
            return DateTime.FromFileTime(QuadPart);
        }
        catch (ArgumentException)
        {
            return DateTime.MinValue;
        }
    }

    internal NtWaitTimeout ToTimeout()
    {
        return new NtWaitTimeout(QuadPart);
    }

    internal ulong ToUInt64()
    {
        return (ulong)QuadPart;
    }

    public override string ToString()
    {
        return QuadPart.ToString();
    }
}
#pragma warning restore 1591

