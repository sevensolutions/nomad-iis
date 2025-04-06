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

namespace NtCoreLib.Kernel.Alpc;

/// <summary>
/// Base class to represent a message attribute.
/// </summary>
public abstract class AlpcMessageAttribute
{
    /// <summary>
    /// The flag for this attribute.
    /// </summary>
    public AlpcMessageAttributeFlags AttributeFlag { get; }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="attribute_flag">The single attribute flag which this represents.</param>
    protected AlpcMessageAttribute(AlpcMessageAttributeFlags attribute_flag)
    {
        AttributeFlag = attribute_flag;
    }

    internal abstract void ToSafeBuffer(SafeAlpcMessageAttributesBuffer buffer);

    internal abstract void FromSafeBuffer(SafeAlpcMessageAttributesBuffer buffer, NtAlpc port, AlpcMessage message);
}
