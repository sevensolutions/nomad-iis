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

using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Security.Authorization;
using NtCoreLib.Utilities.Collections;
using NtCoreLib.Win32.Memory.Interop;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Security;

namespace NtCoreLib.Win32.Security.Authorization.AclUI;

[ClassInterface(ClassInterfaceType.None), ComVisible(true)]
internal class SecurityInformationImpl : ISecurityInformation, IDisposable
{
    private readonly GenericMapping _mapping;
    private readonly DisposableList<SafeStringBuffer> _names;
    private readonly SafeHGlobalBuffer _access_map; // SI_ACCESS
    private readonly SafeStringBuffer _obj_name;
    private readonly NtObject _handle;
    private readonly byte[] _sd;
    private readonly bool _read_only;

    private SecurityInformationImpl(string obj_name,
        Dictionary<uint, string> names, GenericMapping generic_mapping,
        bool read_only)
    {
        _mapping = generic_mapping;
        _obj_name = new SafeStringBuffer(obj_name);
        _access_map = new SafeHGlobalBuffer(Marshal.SizeOf(typeof(SiAccess)) * names.Count);
        SiAccess[] sis = new SiAccess[names.Count];
        _names = new DisposableList<SafeStringBuffer>();
        int i = 0;
        foreach (KeyValuePair<uint, string> pair in names)
        {
            _names.Add(new SafeStringBuffer(pair.Value));
            SiAccess si = new()
            {
                pguid = IntPtr.Zero,
                dwFlags = SiAccessFlags.SI_ACCESS_SPECIFIC | SiAccessFlags.SI_ACCESS_GENERAL,
                mask = pair.Key,
                pszName = _names[i].DangerousGetHandle()
            };
            sis[i] = si;
            i++;
        }
        _access_map.WriteArray(0, sis, 0, names.Count);
        _read_only = read_only;
    }

    public SecurityInformationImpl(string obj_name, NtObject handle,
        Dictionary<uint, string> names, GenericMapping generic_mapping,
        bool read_only) : this(obj_name, names, generic_mapping, read_only)
    {
        _handle = handle.DuplicateObject();
    }

    public SecurityInformationImpl(string obj_name, SecurityDescriptor sd,
        Dictionary<uint, string> names, GenericMapping generic_mapping)
        : this(obj_name, names, generic_mapping, true)
    {
        _sd = sd.ToByteArray();
    }

    public void GetAccessRights(ref Guid pguidObjectType, SiObjectInfoFlags dwFlags, out IntPtr ppAccess, out uint pcAccesses, out uint piDefaultAccess)
    {
        ppAccess = _access_map.DangerousGetHandle();
        pcAccesses = (uint)_names.Count;
        piDefaultAccess = 0;
    }

    public void GetInheritTypes(out IntPtr ppInheritTypes, out uint pcInheritTypes)
    {
        ppInheritTypes = IntPtr.Zero;
        pcInheritTypes = 0;
    }

    public void GetObjectInformation(IntPtr pObjectInfo)
    {
        SiObjectInfo object_info = new();
        SiObjectInfoFlags flags = SiObjectInfoFlags.SI_ADVANCED | SiObjectInfoFlags.SI_EDIT_ALL | SiObjectInfoFlags.SI_NO_ADDITIONAL_PERMISSION;
        if (_read_only || !_handle.IsAccessMaskGranted(GenericAccessRights.WriteDac))
        {
            flags |= SiObjectInfoFlags.SI_READONLY;
        }

        object_info.dwFlags = flags;
        object_info.pszObjectName = _obj_name.DangerousGetHandle();
        Marshal.StructureToPtr(object_info, pObjectInfo, false);
    }

    public void GetSecurity(SecurityInformation RequestedInformation,
        out IntPtr ppSecurityDescriptor, [MarshalAs(UnmanagedType.Bool)] bool fDefault)
    {
        byte[] raw_sd = _sd ?? _handle.GetSecurityDescriptorBytes(RequestedInformation);
        IntPtr ret = NativeMethods.LocalAlloc(0, new IntPtr(raw_sd.Length));
        Marshal.Copy(raw_sd, 0, ret, raw_sd.Length);
        ppSecurityDescriptor = ret;
    }

    public void MapGeneric(ref Guid pguidObjectType, IntPtr pAceFlags, ref AccessMask pMask)
    {
        pMask = _mapping.MapMask(pMask);
    }

    public void PropertySheetPageCallback(IntPtr hwnd, uint uMsg, int uPage)
    {
        // Do nothing.
    }

    public void SetSecurity(SecurityInformation SecurityInformation, IntPtr pSecurityDescriptor)
    {
        if (_read_only || _handle == null)
        {
            throw new SecurityException("Can't edit a read only security descriptor");
        }

        SecurityDescriptor sd = new(pSecurityDescriptor);

        _handle.SetSecurityDescriptor(sd, SecurityInformation);
    }

    #region IDisposable Support
    private bool disposedValue = false; // To detect redundant calls

    protected virtual void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            _names?.Dispose();
            _access_map?.Close();
            _obj_name?.Close();
            _handle?.Close();
            disposedValue = true;
        }
    }

    ~SecurityInformationImpl()
    {
        Dispose(false);
    }

    // This code added to correctly implement the disposable pattern.
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
