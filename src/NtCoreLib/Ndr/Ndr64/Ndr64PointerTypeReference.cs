﻿//  Copyright 2023 Google LLC. All Rights Reserved.
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

namespace NtCoreLib.Ndr.Ndr64;

/// <summary>
/// Class to represent a NDR64 pointer type.
/// </summary>
[Serializable]
public sealed class Ndr64PointerTypeReference : Ndr64BaseTypeReference
{
    /// <summary>
    /// The pointee type.
    /// </summary>
    public Ndr64BaseTypeReference Type { get; private set; }

    /// <summary>
    /// Flags.
    /// </summary>
    public Ndr64PointerFlags Flags { get; }

    internal Ndr64PointerTypeReference(Ndr64FormatCharacter format, Ndr64ParseContext context, IntPtr ptr) 
        : base(format)
    {
        var pointer = context.ReadStruct<NDR64_POINTER_FORMAT>(ptr);
        Flags = pointer.Flags;
        Type = Read(context, pointer.Pointee);
    }

    internal Ndr64PointerTypeReference(Ndr64BaseTypeReference type) 
        : base(Ndr64FormatCharacter.FC64_POINTER)
    {
        Type = type;
    }

    private protected override void OnFixupLateBoundTypes()
    {
        Type = GetIndirectType(Type);
    }
}