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

using NtCoreLib.Native.SafeHandles;

namespace NtCoreLib;

/// <summary>
/// Class to represent an NT trace GUID.
/// </summary>
[NtType("EtwRegistration")]
public class NtEtwRegistration : NtObjectWithDuplicate<NtEtwRegistration, TraceAccessRights>
{
    #region Constructors
    internal sealed class NtTypeFactoryImpl : NtTypeFactoryImplBase
    {
        public NtTypeFactoryImpl() : base(false)
        {
        }
    }

    internal NtEtwRegistration(SafeKernelObjectHandle handle) : base(handle)
    {
    }
    #endregion
}
