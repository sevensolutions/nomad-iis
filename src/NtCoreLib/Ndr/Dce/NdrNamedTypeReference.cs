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

// NOTE: This file is a modified version of NdrParser.cs from OleViewDotNet
// https://github.com/tyranid/oleviewdotnet. It's been relicensed from GPLv3 by
// the original author James Forshaw to be used under the Apache License for this
// project.

using System;
using NtCoreLib.Ndr.Formatter;

namespace NtCoreLib.Ndr.Dce;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
[Serializable]
public sealed class NdrNamedTypeReference : NdrBaseTypeReference
{
    public string Name { get; }

    public NdrNamedTypeReference(string name)
        : base(NdrFormatCharacter.FC_USER_MARSHAL)
    {
        Name = name;
    }

    private protected override string FormatType(INdrFormatterContext context)
    {
        return Name;
    }

    public override int GetSize()
    {
        // We don't really know how big this type is, so just return pointer sized.
        return IntPtr.Size;
    }
}

#pragma warning restore 1591

