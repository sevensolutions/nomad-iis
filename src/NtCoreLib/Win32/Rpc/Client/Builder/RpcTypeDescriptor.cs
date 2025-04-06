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
using NtCoreLib.Ndr.Marshal;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;

namespace NtCoreLib.Win32.Rpc.Client.Builder;

internal sealed class RpcTypeDescriptor
{
    public CodeTypeReference CodeType { get; }
    public Type BuiltinType { get; }
    public NdrBaseTypeReference NdrType { get; }
    public bool Pointer => PointerType != RpcPointerType.None;
    public RpcPointerType PointerType { get; }
    public bool ValueType { get; }
    public bool Constructed { get; }
    public bool Union { get; }
    public string UnmarshalMethod { get; }
    public string MarshalMethod { get; }
    public NdrCorrelationDescriptor ConformanceDescriptor { get; }
    public NdrCorrelationDescriptor VarianceDescriptor { get; }
    public int FixedCount { get; set; }
    public bool Unsupported => BuiltinType == typeof(NdrUnsupported);
    public bool UnionAlias { get; }

    public RpcTypeDescriptor(CodeTypeReference code_type, bool value_type, string unmarshal_method,
        MarshalHelperBuilder marshal_helper, string marshal_method, NdrBaseTypeReference ndr_type,
        NdrCorrelationDescriptor conformance, NdrCorrelationDescriptor variance,
        AdditionalArguments additional_marshal_args, AdditionalArguments additional_unmarshal_args)
    {
        CodeType = code_type;
        UnmarshalMethod = unmarshal_method;
        if (additional_marshal_args != null)
        {
            if (marshal_helper == null)
            {
                throw new ArgumentNullException(nameof(marshal_helper));
            }

            MarshalMethod = marshal_helper.AddGenericMarshal(ndr_type, code_type, marshal_method, additional_marshal_args);
        }
        else
        {
            MarshalMethod = marshal_method;
        }

        if (additional_unmarshal_args != null)
        {
            if (marshal_helper == null)
            {
                throw new ArgumentNullException(nameof(marshal_helper));
            }

            UnmarshalMethod = marshal_helper.AddGenericUnmarshal(ndr_type, code_type, unmarshal_method, additional_unmarshal_args ?? new AdditionalArguments());
        }
        else
        {
            UnmarshalMethod = unmarshal_method;
        }

        NdrType = ndr_type;
        ValueType = value_type;
        ConformanceDescriptor = conformance ?? new NdrCorrelationDescriptor();
        VarianceDescriptor = variance ?? new NdrCorrelationDescriptor();
    }

    public RpcTypeDescriptor(Type code_type, string unmarshal_method, MarshalHelperBuilder marshal_helper,
        string marshal_method, NdrBaseTypeReference ndr_type,
        NdrCorrelationDescriptor conformance, NdrCorrelationDescriptor variance, 
        AdditionalArguments additional_marshal_args, AdditionalArguments additional_unmarshal_args, RpcPointerType pointer_type = RpcPointerType.None)
        : this(new CodeTypeReference(code_type), code_type.IsValueType || typeof(NtObject).IsAssignableFrom(code_type),
              unmarshal_method, marshal_helper, marshal_method, ndr_type, conformance, variance, additional_marshal_args, additional_unmarshal_args)
    {
        BuiltinType = code_type;
        PointerType = pointer_type;
    }

    public RpcTypeDescriptor(Type code_type, string unmarshal_method, string marshal_method, NdrBaseTypeReference ndr_type, RpcPointerType pointer_type = RpcPointerType.None)
        : this(code_type, unmarshal_method, null, marshal_method, ndr_type, null, null, null, null, pointer_type)
    {
    }

    public RpcTypeDescriptor(string name, bool value_type, string unmarshal_method, MarshalHelperBuilder marshal_helper,
        string marshal_method, NdrBaseTypeReference ndr_type, NdrCorrelationDescriptor conformance, NdrCorrelationDescriptor variance,
        AdditionalArguments additional_marshal_args, AdditionalArguments additional_unmarshal_args)
        : this(new CodeTypeReference(name), value_type, unmarshal_method, marshal_helper, marshal_method, ndr_type,
              conformance, variance, additional_marshal_args, additional_unmarshal_args)
    {
        Constructed = true;
        Union = ndr_type is NdrUnionTypeReference;
    }

    public RpcTypeDescriptor(RpcTypeDescriptor original_desc, RpcPointerType pointer_type)
        : this(original_desc.CodeType, original_desc.ValueType, original_desc.UnmarshalMethod, null,
        original_desc.MarshalMethod, original_desc.NdrType, original_desc.ConformanceDescriptor, original_desc.VarianceDescriptor,
        null, null)
    {
        PointerType = pointer_type;
        Constructed = original_desc.Constructed;
        FixedCount = original_desc.FixedCount;
        Union = original_desc.Union;
    }

    public RpcTypeDescriptor(RpcTypeDescriptor original_desc, NdrCorrelationDescriptor correlation_desc)
    : this(original_desc.CodeType, original_desc.ValueType, original_desc.UnmarshalMethod, null,
                original_desc.MarshalMethod, original_desc.NdrType, correlation_desc, null, null, null)
    {
        // Creates an alias for a non-encapsulated union.
        Constructed = original_desc.Constructed;
        FixedCount = original_desc.FixedCount;
        Union = original_desc.Union;
        UnionAlias = true;
    }

    public CodeMethodReferenceExpression GetMarshalMethod(CodeExpression target)
    {
        return new CodeMethodReferenceExpression(target, MarshalMethod);
    }

    public CodeExpression GetUnmarshalTarget(string unmarshal_name)
    {
        return CodeGenUtils.GetVariable(unmarshal_name);
    }

    public CodeMethodInvokeExpression GetUnmarshalMethodInvoke(string unmarshal_name, IEnumerable<CodeExpression> additional_args)
    {
        CodeExpression unmarshal_target = GetUnmarshalTarget(unmarshal_name);
        CodeMethodReferenceExpression unmarshal_method;
        unmarshal_method = new CodeMethodReferenceExpression(unmarshal_target, UnmarshalMethod);
        return new CodeMethodInvokeExpression(unmarshal_method, additional_args.ToArray());
    }

    public CodeTypeReference GetStructureType()
    {
        if (Pointer)
        {
            CodeTypeReference ret = new(typeof(NdrEmbeddedPointer<>));
            ret.TypeArguments.Add(CodeType);
            return ret;
        }
        return CodeType;
    }

    public CodeTypeReference GetParameterType()
    {
        if (Pointer && ValueType && PointerType != RpcPointerType.Reference)
        {
            CodeTypeReference ret = new(typeof(Nullable<>));
            ret.TypeArguments.Add(CodeType);
            return ret;
        }
        return CodeType;
    }

    public CodeTypeReference GetArrayType()
    {
        return new CodeTypeReference(CodeType, CodeType.ArrayRank + 1);
    }

    public CodeTypeReference GetReferenceType()
    {
        if (ValueType)
        {
            CodeTypeReference ret = new(typeof(Nullable<>));
            ret.TypeArguments.Add(CodeType);
            return ret;
        }
        return CodeType;
    }
}
