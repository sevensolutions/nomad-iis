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
//
//  Note this is relicensed from OleViewDotNet by the author.

using System;

namespace NtCoreLib.Win32.SideBySide.Parser;

internal class GuidSectionEntry<T>
{
    public Guid Key { get; }
    public T Entry { get; }
    public int Offset { get; }
    public ActivationContextDataAssemblyRoster RosterEntry { get; }

    public GuidSectionEntry(Guid key, T entry, int offset, ActivationContextDataAssemblyRoster roster_entry)
    {
        Key = key;
        Entry = entry;
        Offset = offset;
        RosterEntry = roster_entry;
    }
}
