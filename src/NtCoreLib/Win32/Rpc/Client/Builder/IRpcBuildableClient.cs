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

using NtCoreLib.Ndr.Dce;
using NtCoreLib.Ndr.Rpc;
using System;
using System.Collections.Generic;

namespace NtCoreLib.Win32.Rpc.Client.Builder;

/// <summary>
/// Interface to represent a "buildable" RPC client.
/// </summary>
public interface IRpcBuildableClient
{
    /// <summary>
    /// The RPC server interface UUID.
    /// </summary>
    Guid InterfaceId { get; }
    /// <summary>
    /// The RPC server interface version.
    /// </summary>
    RpcVersion InterfaceVersion { get; }
    /// <summary>
    /// The list of RPC procedures.
    /// </summary>
    IEnumerable<NdrProcedureDefinition> Procedures { get; }
    /// <summary>
    /// List of parsed complext types.
    /// </summary>
    IEnumerable<NdrComplexTypeReference> ComplexTypes { get; }
    /// <summary>
    /// Path to the PE file this server came from (if known)
    /// </summary>
    string FilePath { get; }
    /// <summary>
    /// Gets whether the client has DCE syntax information.
    /// </summary>
    bool HasDceSyntaxInfo { get; }
}