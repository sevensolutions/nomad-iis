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

using NtCoreLib.Security.Token;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NtCoreLib.Native.SafeBuffers;

#pragma warning disable 1591
public class SafePrivilegeSetBuffer : SafeStructureInOutBuffer<PrivilegeSet>
{
    private static int CalculateLength(int count)
    {
        return count * Marshal.SizeOf(typeof(LuidAndAttributes));
    }

    private SafePrivilegeSetBuffer(bool owns_handle)
        : base(IntPtr.Zero, 0, owns_handle)
    {
    }

    private SafePrivilegeSetBuffer(PrivilegeSet privilege_set, int count)
        : base(privilege_set, CalculateLength(count), true)
    {
    }

    private SafePrivilegeSetBuffer(IEnumerable<TokenPrivilege> privileges,
        PrivilegeSetControlFlags control, int count) : this(new PrivilegeSet() { Control = control, PrivilegeCount = count },
            count)
    {
        if (count <= 0)
        {
            throw new ArgumentException("Privilege count must be greater than 0", nameof(count));
        }
        var luids = privileges.Select(p => new LuidAndAttributes() { Luid = p.Luid, Attributes = p.Attributes }).ToArray();
        Data.WriteArray(0, luids, 0, luids.Length);
    }

    public SafePrivilegeSetBuffer(int total_size)
        : base(total_size, false)
    {
    }

    internal SafePrivilegeSetBuffer(IntPtr ptr, int count)
        : base(ptr, CalculateLength(count), true, false)
    {
    }

    public SafePrivilegeSetBuffer(IEnumerable<TokenPrivilege> privileges,
        PrivilegeSetControlFlags control) : this(privileges, control, privileges.Count())
    {
    }

    public SafePrivilegeSetBuffer() : this(new PrivilegeSet(), 1)
    {
    }

    public static new SafePrivilegeSetBuffer Null => new(false);

    public IEnumerable<TokenPrivilege> GetPrivileges()
    {
        return GetPrivileges(null);
    }

    internal IEnumerable<TokenPrivilege> GetPrivileges(string system_name)
    {
        var result = Result;
        LuidAndAttributes[] luids = new LuidAndAttributes[result.PrivilegeCount];
        Data.ReadArray(0, luids, 0, luids.Length);
        return luids.Select(l => new TokenPrivilege(system_name, l.Luid, l.Attributes)).ToArray();
    }
}

#pragma warning restore 1591

