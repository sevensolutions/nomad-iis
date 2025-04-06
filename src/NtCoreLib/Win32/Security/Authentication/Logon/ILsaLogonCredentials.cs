﻿//  Copyright 2022 Google LLC. All Rights Reserved.
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

using NtCoreLib.Utilities.Collections;
using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.Security.Authentication.Logon;

/// <summary>
/// Interface for logon credentials to use with LsaLogonUser.
/// </summary>
/// <remarks>Use <see cref="AuthenticationCredentials"/> for SSPI calls.</remarks>
public interface ILsaLogonCredentials
{
    /// <summary>
    /// Specify the expected authentication package name.
    /// </summary>
    /// <remarks>This is advisory only, you could pass the same credentials to a different authentication package.</remarks>
    string AuthenticationPackage { get; }

    /// <summary>
    /// Convert the credentials into a safe buffer.
    /// </summary>
    /// <param name="list">Store for any additional allocations.</param>
    /// <returns>The safe buffer containing the credentials.</returns>
    SafeBuffer ToBuffer(DisposableList list);
}
