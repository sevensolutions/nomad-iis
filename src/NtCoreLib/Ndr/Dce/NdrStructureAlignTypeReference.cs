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

namespace NtCoreLib.Ndr.Dce;

#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
[Serializable]
public sealed class NdrStructureAlignTypeReference : NdrBaseTypeReference
{
    internal NdrStructureAlignTypeReference(NdrFormatCharacter format) : base(format)
    {
    }

    internal int Align(int offset)
    {
        return Format switch
        {
            NdrFormatCharacter.FC_ALIGNM2 => (offset + 1) & ~1,
            NdrFormatCharacter.FC_ALIGNM4 => (offset + 3) & ~3,
            NdrFormatCharacter.FC_ALIGNM8 => (offset + 7) & ~7,
            _ => throw new InvalidOperationException("Format must be an alignment."),
        };
    }

    public override int GetSize()
    {
        return 0;
    }
}
