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

using System.Collections.Generic;

namespace NtCoreLib.Win32.Rpc.Client.Builder;

/// <summary>
/// Arguments for the RPC client builder.
/// </summary>
public struct RpcClientBuilderArguments
{
    /// <summary>
    /// Builder flags.
    /// </summary>
    public RpcClientBuilderFlags Flags { get; set; }
    /// <summary>
    /// The namespace for the client class.
    /// </summary>
    public string NamespaceName { get; set; }
    /// <summary>
    /// The class name of the client.
    /// </summary>
    public string ClientName { get; set; }
    /// <summary>
    /// The class name of the complex type encoding class.
    /// </summary>
    public string EncoderName { get; set; }
    /// <summary>
    /// The class name of the complex type decoder class.
    /// </summary>
    public string DecoderName { get; set; }
    /// <summary>
    /// The class name of the wrapper type.
    /// </summary>
    public string WrapperTypeName { get; set; }
    /// <summary>
    /// Enable debugging on built code.
    /// </summary>
    public bool EnableDebugging { get; set; }

    /// <summary>
    /// Equals implementation.
    /// </summary>
    /// <param name="obj">The object to compare against.</param>
    /// <returns>True if the object is equal.</returns>
    public override readonly bool Equals(object obj)
    {
        return obj is RpcClientBuilderArguments arguments &&
               Flags == arguments.Flags &&
               NamespaceName == arguments.NamespaceName &&
               ClientName == arguments.ClientName &&
               EncoderName == arguments.EncoderName &&
               DecoderName == arguments.DecoderName &&
               WrapperTypeName == arguments.WrapperTypeName &&
               EnableDebugging == arguments.EnableDebugging;
    }

    /// <summary>
    /// GetHashCode implementation.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override readonly int GetHashCode()
    {
        int hashCode = 88457562;
        hashCode = hashCode * -1521134295 + Flags.GetHashCode();
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(NamespaceName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(ClientName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(EncoderName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(DecoderName);
        hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(WrapperTypeName);
        hashCode = hashCode * -1521134295 + EnableDebugging.GetHashCode();
        return hashCode;
    }
}
