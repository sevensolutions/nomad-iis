﻿//  Copyright 2020 Google Inc. All Rights Reserved.
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
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Text;

namespace NtCoreLib.Utilities.ASN1;

internal struct DERValue
{
    public DERTagType Type;
    public bool Constructed;
    public int Tag;
    public byte[] Data;
    public DERValue[] Children;
    public long Offset;
    public long DataOffset;

    public bool Check(bool constructed, DERTagType type, int tag)
    {
        return Constructed == constructed && Type == type && Tag == tag;
    }

    public bool CheckApplication(int tag)
    {
        return Check(true, DERTagType.Application, tag);
    }

    public bool CheckPrimitive(UniversalTag tag)
    {
        return Check(false, DERTagType.Universal, (int)tag);
    }

    public bool CheckSequence()
    {
        return Check(true, DERTagType.Universal, (int)UniversalTag.SEQUENCE);
    }

    public bool CheckContext(int context)
    {
        return Check(true, DERTagType.ContextSpecific, context) && HasChildren();
    }

    public bool HasChildren()
    {
        return (Children?.Length ?? 0) != 0;
    }

    public string FormatTag()
    {
        if (Type == DERTagType.Universal)
            return ((UniversalTag)Tag).ToString();
        return Tag.ToString();
    }

    public BitArray ReadBitString()
    {
        return DERUtils.ReadBitString(Data);
    }

    public string ReadObjID()
    {
        return DERUtils.ReadObjID(Data);
    }

    public BigInteger ReadBigInteger()
    {
        return new BigInteger(Data.Reverse().ToArray());
    }

    public int ReadInteger()
    {
        return (int)ReadBigInteger();
    }

    public bool ReadBoolean()
    {
        if (!CheckPrimitive(UniversalTag.BOOLEAN))
            throw new InvalidDataException();
        return Data[0] != 0;
    }

    public long ReadLong()
    {
        return (long)ReadBigInteger();
    }

    private string FormatInteger()
    {
        return ReadBigInteger().ToString("X");
    }

    public string ReadString(UniversalTag tag_type)
    {
        if (!CheckPrimitive(tag_type))
            throw new InvalidDataException();
        return tag_type switch
        {
            UniversalTag.GeneralString => Encoding.ASCII.GetString(Data),
            UniversalTag.IA5String => Encoding.ASCII.GetString(Data),
            UniversalTag.UTF8String => Encoding.UTF8.GetString(Data),
            UniversalTag.GeneralizedTime => Encoding.ASCII.GetString(Data),
            _ => throw new InvalidDataException(),
        };
    }

    public string ReadGeneralString()
    {
        if (!CheckPrimitive(UniversalTag.GeneralString))
            throw new InvalidDataException();
        return Encoding.ASCII.GetString(Data);
    }

    public string ReadGeneralizedTime()
    {
        if (!CheckPrimitive(UniversalTag.GeneralizedTime))
            throw new InvalidDataException();
        return Encoding.ASCII.GetString(Data);
    }

    public int ReadChildInteger()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.INTEGER))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadInteger();
    }

    public int ReadChildEnumerated()
    {
        if (!HasChildren() || (!Children[0].CheckPrimitive(UniversalTag.ENUMERATED) && !Children[0].CheckPrimitive(UniversalTag.INTEGER)))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadInteger();
    }

    public byte[] ReadChildOctetString()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.OCTET_STRING))
        {
            throw new InvalidDataException();
        }
        return Children[0].Data;
    }

    public string ReadChildGeneralString()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.GeneralString))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadGeneralString();
    }

    public string ReadChildObjID()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.OBJECT_IDENTIFIER))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadObjID();
    }

    public string ReadChildGeneralizedTime()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.GeneralizedTime))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadGeneralizedTime();
    }

    public List<T> ReadChildSequence<T>(Func<DERValue, T> func)
    {
        List<T> ret = new();
        if (!HasChildren() || Children[0].CheckSequence())
        {
            foreach (var v in Children[0].Children)
            {
                ret.Add(func(v));
            }
        }
        return ret;
    }

    public List<string> ReadChildStringSequence()
    {
        return ReadChildSequence(v => v.ReadGeneralString());
    }

    public List<int> ReadChildIntegerSequence()
    {
        return ReadChildSequence(v => v.ReadInteger());
    }

    public List<T> ReadChildEnumSequence<T>() where T : Enum
    {
        return ReadChildSequence(v => (T)(object)v.ReadInteger());
    }

    public BitArray ReadChildBitString()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.BIT_STRING))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadBitString();
    }

    public T ReadChildBitFlags<T>() where T : Enum
    {
        var flags = ReadChildBitString();
        uint ret = 0;
        int total_length = Math.Min(32, flags.Length);
        for (int i = 0; i < total_length; ++i)
        {
            if (flags[i])
                ret |= (1U << i);
        }
        return (T)Enum.ToObject(typeof(T), ret);
    }

    public bool ReadChildBoolean()
    {
        if (!HasChildren() || !Children[0].CheckPrimitive(UniversalTag.BOOLEAN))
        {
            throw new InvalidDataException();
        }
        return Children[0].ReadBoolean();
    }

    public string FormatValue()
    {
        if (Type == DERTagType.Universal)
        {
            UniversalTag tag = (UniversalTag)Tag;
            switch (tag)
            {
                case UniversalTag.GeneralString:
                    return ReadGeneralString();
                case UniversalTag.OBJECT_IDENTIFIER:
                    return ReadObjID();
                case UniversalTag.INTEGER:
                case UniversalTag.ENUMERATED:
                    return FormatInteger();
                case UniversalTag.OCTET_STRING:
                    return BitConverter.ToString(Data);
                case UniversalTag.BIT_STRING:
                    return string.Join(",", ReadBitString().Cast<bool>().Select(b => b ? 1 : 0));
                case UniversalTag.UTF8String:
                case UniversalTag.IA5String:
                    return ReadString(tag);
                case UniversalTag.GeneralizedTime:
                    return ReadGeneralizedTime();
                case UniversalTag.BMPString:
                    return Encoding.BigEndianUnicode.GetString(Data);
            }
        }
        
        if (!Constructed)
        {
            return BitConverter.ToString(Data);
        }

        return $"Len: {Data.Length:X}";
    }

    public DERValue? GetChild(int index)
    {
        if (!HasChildren() || index >= Children.Length)
            return null;
        return Children[index];
    }

    public IReadOnlyList<T> ReadSequence<T>(Func<DERValue, T> parse_func)
    {
        if (!CheckSequence())
            throw new InvalidDataException();
        return Children.Select(parse_func).ToList().AsReadOnly();
    }
}
