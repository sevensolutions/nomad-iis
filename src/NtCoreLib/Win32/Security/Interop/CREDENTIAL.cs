﻿//  Copyright 2021 Google Inc. All Rights Reserved.
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

using NtCoreLib.Win32.Security.Credential;
using System;
using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.Security.Interop;

[StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
internal struct CREDENTIAL
{
    public int Flags;
    public CredentialType Type;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string TargetName;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string Comment;
    public FileTime LastWritten;
    public int CredentialBlobSize;
    public IntPtr CredentialBlob;
    public CredentialPersistence Persist;
    public int AttributeCount;
    public IntPtr Attributes; // PCREDENTIAL_ATTRIBUTEW
    [MarshalAs(UnmanagedType.LPWStr)]
    public string TargetAlias;
    [MarshalAs(UnmanagedType.LPWStr)]
    public string UserName;
}
