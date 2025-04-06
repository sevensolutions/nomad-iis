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

using System.Text;

namespace NtCoreLib.Utilities.ASN1.Parser;

/// <summary>
/// Class to represent a primitive ASN.1 object.
/// </summary>
public class ASN1UniversalPrimitive : ASN1Universal
{
    private protected override string FormatValue()
    {
        return Tag switch
        {
            ASN1UniversalTag.ObjectIdentifier => DERUtils.ReadObjID(_data),
            ASN1UniversalTag.GeneralString => Encoding.ASCII.GetString(_data),
            ASN1UniversalTag.IA5String => Encoding.ASCII.GetString(_data),
            ASN1UniversalTag.UTF8String => Encoding.UTF8.GetString(_data),
            ASN1UniversalTag.GeneralizedTime => Encoding.ASCII.GetString(_data),
            _ => NtObjectUtils.ToHexString(_data),
        };
    }

    internal ASN1UniversalPrimitive(DERValue value) : base(value)
    {
    }
}
