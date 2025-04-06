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

using System.IO;

namespace NtCoreLib.Net.Smb2;

internal sealed class Smb2ReadResponsePacket : Smb2ResponsePacket
{
    public byte[] Data { get; private set; }

    public override void Read(BinaryReader reader)
    {
        if (reader.ReadUInt16() != 17)
            throw new InvalidDataException("Invalid response size for READ packet.");
        int offset = reader.ReadByte();
        // Reserved
        reader.ReadByte();
        int length = reader.ReadInt32();
        // Ignore remaining reserved fields.
        reader.BaseStream.Position = offset;
        Data = reader.ReadAllBytes(length);
    }
}
