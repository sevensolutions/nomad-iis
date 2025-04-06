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

using Microsoft.CSharp;
using NtCoreLib.Ndr.Dce;
using NtCoreLib.Ndr.Formatter;
using NtCoreLib.Ndr.Marshal;
using NtCoreLib.Win32.Rpc.Server;
using System;
using System.CodeDom;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;

namespace NtCoreLib.Win32.Rpc.Client.Builder;

/// <summary>
/// Builder to create an RPC client from an RpcServer class.
/// </summary>
public sealed class RpcClientBuilder
{
    #region Private Members
    private static readonly Dictionary<Tuple<RpcServer, RpcClientBuilderArguments>, Assembly> _compiled_clients
        = new();
    private readonly Dictionary<NdrBaseTypeReference, RpcTypeDescriptor> _type_descriptors;
    private readonly IEnumerable<NdrComplexTypeReference> _complex_types;
    private readonly IRpcBuildableClient _build;
    private readonly RpcClientBuilderArguments _args;
    private readonly HashSet<string> _proc_names;

    private bool HasFlag(RpcClientBuilderFlags flag)
    {
        return _args.Flags.HasFlagSet(flag);
    }

    private RpcTypeDescriptor GetSimpleArrayTypeDescriptor(NdrSimpleArrayTypeReference simple_array, MarshalHelperBuilder marshal_helper)
    {
        RpcTypeDescriptor element_type = GetTypeDescriptor(simple_array.ElementType, marshal_helper);
        CodeExpression arg = CodeGenUtils.GetPrimitive(simple_array.ElementCount);
        if (element_type.BuiltinType == typeof(char))
        {
            var args = new AdditionalArguments(false, arg);
            return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadFixedString), marshal_helper, nameof(NdrMarshalBuffer.WriteFixedString), simple_array, null, null, args, args)
            {
                FixedCount = simple_array.ElementCount
            };
        }
        else if (element_type.BuiltinType == typeof(byte))
        {
            var args = new AdditionalArguments(false, arg);
            return new RpcTypeDescriptor(typeof(byte[]), nameof(NdrUnmarshalBuffer.ReadFixedByteArray), marshal_helper, nameof(NdrMarshalBuffer.WriteFixedByteArray), simple_array, null, null, args, args)
            {
                FixedCount = simple_array.ElementCount
            };
        }
        else if (element_type.BuiltinType != null && element_type.BuiltinType.IsPrimitive)
        {
            var args = new AdditionalArguments(true, arg);
            return new RpcTypeDescriptor(element_type.CodeType.ToRefArray(), false,
                nameof(NdrUnmarshalBuffer.ReadFixedPrimitiveArray), marshal_helper, nameof(NdrMarshalBuffer.WriteFixedPrimitiveArray), simple_array,
                null, null, args, args)
            {
                FixedCount = simple_array.ElementCount
            };
        }
        else if (element_type.Constructed)
        {
            var args = new AdditionalArguments(true, arg);
            return new RpcTypeDescriptor(element_type.CodeType.ToRefArray(), false,
                nameof(NdrUnmarshalBuffer.ReadFixedStructArray), marshal_helper, nameof(NdrMarshalBuffer.WriteFixedStructArray), simple_array,
                null, null, args, args)
            {
                FixedCount = simple_array.ElementCount
            };
        }

        return null;
    }

    private RpcTypeDescriptor GetBogusArrayTypeDescriptor(NdrBogusArrayTypeReference bogus_array_type, MarshalHelperBuilder marshal_helper)
    {
        RpcTypeDescriptor element_type = GetTypeDescriptor(bogus_array_type.ElementType, marshal_helper);
        // We only support a limited set of types for now.
        bool is_string = element_type.NdrType.Format == NdrFormatCharacter.FC_C_WSTRING
            || element_type.NdrType.Format == NdrFormatCharacter.FC_C_CSTRING;
        bool is_basic = element_type.BuiltinType == typeof(NdrEnum16)
            || element_type.BuiltinType == typeof(Guid)
            || element_type.BuiltinType == typeof(NdrInterfacePointer)
            || element_type.NdrType.Format == NdrFormatCharacter.FC_SYSTEM_HANDLE;
        bool is_pointer = element_type.PointerType == RpcPointerType.Unique || element_type.PointerType == RpcPointerType.Full;
        if (!element_type.Constructed && !is_string && !is_basic)
        {
            return null;
        }

        List<CodeTypeReference> marshal_params = new();
        List<CodeExpression> marshal_expr = new();
        List<CodeExpression> unmarshal_expr = new();
        string marshal_name = null;
        string unmarshal_name = null;

        if (is_string || is_basic)
        {
            var func_type = CodeGenUtils.CreateFuncType(element_type.CodeType);
            unmarshal_expr.Add(CodeGenUtils.CreateDelegate(func_type, CodeGenUtils.GetVariable(null), element_type.UnmarshalMethod));
            var action_type = CodeGenUtils.CreateActionType(element_type.CodeType);
            marshal_expr.Add(CodeGenUtils.CreateDelegate(action_type, CodeGenUtils.GetVariable(null), element_type.MarshalMethod));
        }

        if (bogus_array_type.VarianceDescriptor.IsValid && bogus_array_type.ConformanceDescriptor.IsValid)
        {
            if (!bogus_array_type.VarianceDescriptor.ValidateCorrelation()
             || !bogus_array_type.ConformanceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            if (is_string)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantVaryingStringArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantVaryingStringArray);
            }
            else if (is_basic)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantVaryingArrayCallback);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantVaryingArrayCallback);
            }
            else if (is_pointer)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantVaryingStructPointerArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantVaryingStructPointerArray);
            }
            else
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantVaryingStructArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantVaryingStructArray);
            }

            marshal_params.Add(typeof(long).ToRef());
            marshal_params.Add(typeof(long).ToRef());
        }
        else if (bogus_array_type.ConformanceDescriptor.IsValid)
        {
            // Check support for this correlation descriptor.
            if (!bogus_array_type.ConformanceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            if (is_string)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantStringArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantStringArray);
            }
            else if (is_basic)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantArrayCallback);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantArrayCallback);
            }
            else if (is_pointer)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantStructPointerArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantStructPointerArray);
            }
            else
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteConformantStructArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantStructArray);
            }

            marshal_params.Add(typeof(long).ToRef());
        }
        else if (bogus_array_type.VarianceDescriptor.IsValid)
        {
            if (!bogus_array_type.VarianceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            marshal_params.Add(typeof(long).ToRef());

            if (is_string)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteVaryingStringArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadVaryingStringArray);
            }
            else if (is_basic)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteVaryingArrayCallback);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadVaryingArrayCallback);
            }
            else if (is_pointer)
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteVaryingStructPointerArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadVaryingStructPointerArray);
            }
            else
            {
                marshal_name = nameof(NdrMarshalBuffer.WriteVaryingStructArray);
                unmarshal_name = nameof(NdrUnmarshalBuffer.ReadVaryingStructArray);
            }
        }
        else if (bogus_array_type.ElementCount > 0 && element_type.Constructed && !is_pointer)
        {
            // For now we don't support fixed basic/string bogus arrays.
            marshal_expr.Add(CodeGenUtils.GetPrimitive(bogus_array_type.ElementCount));
            unmarshal_expr.Add(CodeGenUtils.GetPrimitive(bogus_array_type.ElementCount));
            marshal_name = nameof(NdrMarshalBuffer.WriteFixedStructArray);
            unmarshal_name = nameof(NdrUnmarshalBuffer.ReadFixedStructArray);
        }
        else
        {
            return null;
        }

        CodeTypeReference real_element_type = element_type.CodeType;
        CodeTypeReference generic_type = null;

        if (is_pointer && !is_string)
        {
            unmarshal_expr.Add(CodeGenUtils.GetPrimitive(element_type.PointerType == RpcPointerType.Full));
            generic_type = real_element_type;
            real_element_type = element_type.GetReferenceType();
        }

        return new RpcTypeDescriptor(new CodeTypeReference(real_element_type, 1), false,
            unmarshal_name, marshal_helper, marshal_name,
            bogus_array_type, bogus_array_type.ConformanceDescriptor, bogus_array_type.VarianceDescriptor,
            new AdditionalArguments(marshal_expr.ToArray(), marshal_params.ToArray(), !is_string) { GenericType = generic_type },
            new AdditionalArguments(!is_string, unmarshal_expr.ToArray()) { GenericType = generic_type })
        {
            FixedCount = bogus_array_type.ElementCount
        };
    }

    private RpcTypeDescriptor GetConformantArrayTypeDescriptor(NdrConformantArrayTypeReference conformant_array_type, MarshalHelperBuilder marshal_helper)
    {
        RpcTypeDescriptor element_type = GetTypeDescriptor(conformant_array_type.ElementType, marshal_helper);
        List<CodeTypeReference> marshal_params = new();
        string marshal_name = null;
        string unmarshal_name = null;

        if (conformant_array_type.VarianceDescriptor.IsValid && conformant_array_type.ConformanceDescriptor.IsValid)
        {
            if (!conformant_array_type.VarianceDescriptor.ValidateCorrelation()
             || !conformant_array_type.ConformanceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            marshal_params.Add(typeof(long).ToRef());
            marshal_params.Add(typeof(long).ToRef());
            marshal_name = nameof(NdrMarshalBuffer.WriteConformantVaryingArray);
            unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantVaryingArray);
        }
        else if (conformant_array_type.ConformanceDescriptor.IsValid)
        {
            // Check support for this correlation descriptor.
            if (!conformant_array_type.ConformanceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            marshal_params.Add(typeof(long).ToRef());
            marshal_name = nameof(NdrMarshalBuffer.WriteConformantArray);
            unmarshal_name = nameof(NdrUnmarshalBuffer.ReadConformantArray);
        }
        else
        {
            // Not sure how we got here, conformant or both descriptors should be valid.
            return null;
        }

        AdditionalArguments marshal_args = new(true, marshal_params.ToArray());
        AdditionalArguments unmarshal_args = new(true);
        return new RpcTypeDescriptor(element_type.CodeType.ToRefArray(), false, unmarshal_name, marshal_helper, marshal_name, conformant_array_type,
            conformant_array_type.ConformanceDescriptor, conformant_array_type.VarianceDescriptor, marshal_args, unmarshal_args);
    }

    private RpcTypeDescriptor GetVaryingArrayTypeDescriptor(NdrVaryingArrayTypeReference varying_array_type, MarshalHelperBuilder marshal_helper)
    {
        RpcTypeDescriptor element_type = GetTypeDescriptor(varying_array_type.ElementType, marshal_helper);
        List<CodeTypeReference> marshal_params = new();
        string marshal_name = null;
        string unmarshal_name = null;

        if (varying_array_type.VarianceDescriptor.IsValid)
        {
            if (!varying_array_type.VarianceDescriptor.ValidateCorrelation())
            {
                return null;
            }

            marshal_params.Add(typeof(long).ToRef());
            marshal_name = nameof(NdrMarshalBuffer.WriteVaryingArray);
            unmarshal_name = nameof(NdrUnmarshalBuffer.ReadVaryingArray);
        }
        else
        {
            // Not sure how we got here variance descriptors should be valid.
            return null;
        }

        AdditionalArguments marshal_args = new(true, marshal_params.ToArray());
        AdditionalArguments unmarshal_args = new(true);
        return new RpcTypeDescriptor(element_type.CodeType.ToRefArray(), false, unmarshal_name, marshal_helper, marshal_name, varying_array_type,
            null, varying_array_type.VarianceDescriptor, marshal_args, unmarshal_args);
    }

    private RpcTypeDescriptor GetArrayTypeDescriptor(NdrBaseArrayTypeReference array_type, MarshalHelperBuilder marshal_helper)
    {
        if (array_type is NdrSimpleArrayTypeReference simple_array_type)
        {
            return GetSimpleArrayTypeDescriptor(simple_array_type, marshal_helper);
        }
        if (array_type is NdrBogusArrayTypeReference bogus_array_type)
        {
            return GetBogusArrayTypeDescriptor(bogus_array_type, marshal_helper);
        }
        if (array_type is NdrConformantArrayTypeReference conformant_array_type)
        {
            return GetConformantArrayTypeDescriptor(conformant_array_type, marshal_helper);
        }
        if (array_type is NdrVaryingArrayTypeReference varying_array_type)
        {
            return GetVaryingArrayTypeDescriptor(varying_array_type, marshal_helper);
        }
        return null;
    }

    private RpcTypeDescriptor GetPointerTypeDescriptor(NdrPointerTypeReference pointer, MarshalHelperBuilder marshal_helper)
    {
        var desc = GetTypeDescriptor(pointer.Type, marshal_helper);
        var pointer_type = pointer.Format switch
        {
            NdrFormatCharacter.FC_UP or NdrFormatCharacter.FC_OP => RpcPointerType.Unique,
            NdrFormatCharacter.FC_RP => RpcPointerType.Reference,
            _ => RpcPointerType.Full,
        };
        if (desc.Pointer && pointer_type == RpcPointerType.Reference)
        {
            return desc;
        }

        return new RpcTypeDescriptor(desc, pointer_type);
    }

    private RpcTypeDescriptor GetKnownTypeDescriptor(NdrKnownTypeReference known_type, MarshalHelperBuilder marshal_helper)
    {
        return known_type.KnownType switch
        {
            NdrKnownTypes.GUID => new RpcTypeDescriptor(typeof(Guid), nameof(NdrUnmarshalBuffer.ReadGuid), nameof(NdrMarshalBuffer.WriteGuid), known_type),
            NdrKnownTypes.BSTR => new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadBasicString),
                                nameof(NdrMarshalBuffer.WriteBasicString), known_type, RpcPointerType.Unique),
            NdrKnownTypes.HSTRING => new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadHString),
                                nameof(NdrMarshalBuffer.WriteHString), known_type, RpcPointerType.Unique),
            NdrKnownTypes.HWND or NdrKnownTypes.HMENU => new RpcTypeDescriptor(typeof(NdrWindowHandle), nameof(NdrUnmarshalBuffer.ReadStruct), marshal_helper,
                        nameof(NdrMarshalBuffer.WriteStruct), known_type, null, null, new AdditionalArguments(true), new AdditionalArguments(true), RpcPointerType.Unique),
        // TODO: Implement remaining custom marshallers?
        _ => null,
        };
    }

    private RpcTypeDescriptor GetStringTypeDescriptor(NdrBaseStringTypeReference string_type, MarshalHelperBuilder marshal_helper)
    {
        if (string_type is NdrConformantStringTypeReference conformant_str)
        {
            if (!conformant_str.ConformanceDescriptor.IsValid)
            {
                if (conformant_str.Format == NdrFormatCharacter.FC_C_CSTRING)
                {
                    return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingAnsiString), nameof(NdrMarshalBuffer.WriteTerminatedAnsiString), string_type);
                }
                return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingString), nameof(NdrMarshalBuffer.WriteTerminatedString), string_type);
            }
            else
            {
                AdditionalArguments marshal_args = new(false, typeof(long).ToRef());
                if (conformant_str.Format == NdrFormatCharacter.FC_C_CSTRING)
                {
                    return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingAnsiString), marshal_helper,
                        nameof(NdrMarshalBuffer.WriteConformantVaryingAnsiString), string_type, conformant_str.ConformanceDescriptor, null, marshal_args, null);
                }
                return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingString), marshal_helper,
                        nameof(NdrMarshalBuffer.WriteConformantVaryingString), string_type, conformant_str.ConformanceDescriptor, null, marshal_args, null);
            }
        }
        else if (string_type is NdrStringTypeReference fixed_str)
        {
            if (fixed_str.Format == NdrFormatCharacter.FC_WSTRING)
            {
                return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadVaryingString), nameof(NdrMarshalBuffer.WriteVaryingString), fixed_str);
            }
            else if (fixed_str.Format == NdrFormatCharacter.FC_CSTRING)
            {
                return new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadVaryingAnsiString), nameof(NdrMarshalBuffer.WriteVaryingAnsiString), fixed_str);
            }
        }

        return null;
    }

    private RpcTypeDescriptor GetSystemHandleTypeDescriptor(NdrSystemHandleTypeReference system_handle_type, MarshalHelperBuilder marshal_helper)
    {
        return new RpcTypeDescriptor(system_handle_type.GetSystemHandleType(),
            nameof(NdrUnmarshalBuffer.ReadSystemHandle), marshal_helper, nameof(NdrMarshalBuffer.WriteSystemHandle), system_handle_type, null, null,
            new AdditionalArguments(true, CodeGenUtils.GetPrimitive(system_handle_type.AccessMask)), new AdditionalArguments(true));
    }

    private RpcTypeDescriptor GetHandleTypeDescriptor(NdrHandleTypeReference handle_type, MarshalHelperBuilder marshal_helper)
    {
        if (handle_type.Format == NdrFormatCharacter.FC_BIND_CONTEXT)
        {
            return new RpcTypeDescriptor(typeof(NdrContextHandle), 
                nameof(NdrUnmarshalBuffer.ReadContextHandle), nameof(NdrMarshalBuffer.WriteContextHandle), handle_type);
        }
        return null;
    }

    private RpcTypeDescriptor GetPipeTypeDescriptor(NdrPipeTypeReference pipe_type, MarshalHelperBuilder marshal_helper)
    {
        bool pipe_array = HasFlag(RpcClientBuilderFlags.MarshalPipesAsArrays);
        var base_type_descriptor = GetTypeDescriptor(pipe_type.BaseType, marshal_helper);
        CodeTypeReference type_ref = pipe_array ? base_type_descriptor.CodeType.ToRefArray() :
            typeof(NdrPipe<>).ToRef(base_type_descriptor.CodeType);
        string unmarshal_name = pipe_array ? nameof(NdrUnmarshalBuffer.ReadPipeArray) : nameof(NdrUnmarshalBuffer.ReadPipe);
        string marshal_name = pipe_array ? nameof(NdrMarshalBuffer.WritePipeArray) : nameof(NdrMarshalBuffer.WritePipe);
        AdditionalArguments args = new(base_type_descriptor.CodeType);
        return new RpcTypeDescriptor(type_ref, false, unmarshal_name, marshal_helper, marshal_name,
            pipe_type, null, null, args, args);
    }

    private RpcTypeDescriptor GetSupplementTypeDescriptor(NdrSupplementTypeReference supplement_type, MarshalHelperBuilder marshal_helper)
    {
        if (supplement_type.SupplementType.Format != NdrFormatCharacter.FC_BIND_CONTEXT || 
            !_args.Flags.HasFlagSet(RpcClientBuilderFlags.GenerateTypeStrictHandles))
        {
            return GetTypeDescriptor(supplement_type.SupplementType, marshal_helper);
        }

        return marshal_helper.GetContextHandleType(supplement_type);
    }

    private RpcTypeDescriptor GetTypeDescriptorInternal(NdrBaseTypeReference type, MarshalHelperBuilder marshal_helper)
    {
        RpcTypeDescriptor ret_desc = type switch
        {
            NdrSimpleTypeReference simple_type => simple_type.GetSimpleTypeDescriptor(marshal_helper, HasFlag(RpcClientBuilderFlags.UnsignedChar)),
            NdrKnownTypeReference known_type => GetKnownTypeDescriptor(known_type, marshal_helper),
            NdrBaseStringTypeReference string_type => GetStringTypeDescriptor(string_type, marshal_helper),
            NdrSystemHandleTypeReference system_handle_type => GetSystemHandleTypeDescriptor(system_handle_type, marshal_helper),
            NdrBaseArrayTypeReference array_type => GetArrayTypeDescriptor(array_type, marshal_helper),
            NdrPointerTypeReference pointer_type => GetPointerTypeDescriptor(pointer_type, marshal_helper),
            NdrSupplementTypeReference supplement_type => GetSupplementTypeDescriptor(supplement_type, marshal_helper),
            NdrHandleTypeReference handle_type => GetHandleTypeDescriptor(handle_type, marshal_helper),
            NdrRangeTypeReference range_type => GetTypeDescriptor(range_type.RangeType, marshal_helper),
            NdrByteCountPointerReferenceType byte_count_pointer_type => GetTypeDescriptor(byte_count_pointer_type.Type, marshal_helper),
            NdrInterfacePointerTypeReference => new RpcTypeDescriptor(typeof(NdrInterfacePointer), nameof(NdrUnmarshalBuffer.ReadInterfacePointer),
                                    nameof(NdrMarshalBuffer.WriteInterfacePointer), type, RpcPointerType.Unique),
            NdrPipeTypeReference pipe_type => GetPipeTypeDescriptor(pipe_type, marshal_helper),
            NdrIgnoreTypeReference => new RpcTypeDescriptor(typeof(IntPtr), nameof(NdrUnmarshalBuffer.ReadIgnorePointer), nameof(NdrMarshalBuffer.WriteIgnorePointer), type),
            _ => null,
        };

        if (ret_desc != null)
        {
            return ret_desc;
        }

        // No known type, return an unsupported type.
        var formatter = new IdlNdrFormatterContext(null, null, NdrFormatterFlags.RemoveComments);
        var type_name_arg = CodeGenUtils.GetPrimitive($"{type.Format} - {formatter.FormatType(type)}");
        AdditionalArguments additional_args = new(false, type_name_arg);
        RpcPointerType p_type = RpcPointerType.None;
        if (type is NdrUserMarshalTypeReference user_marshal && user_marshal.Flags.HasFlagSet(NdrUserMarshalFlags.USER_MARSHAL_UNIQUE))
        {
            p_type = RpcPointerType.Unique;
        }
        return new RpcTypeDescriptor(typeof(NdrUnsupported), nameof(NdrUnmarshalBuffer.ReadUnsupported), marshal_helper,
            nameof(NdrMarshalBuffer.WriteUnsupported), type, null, null, additional_args, additional_args, p_type);
    }

    // Should implement this for each type rather than this.
    private RpcTypeDescriptor GetTypeDescriptor(NdrBaseTypeReference type, MarshalHelperBuilder marshal_helper)
    {
        // Void.
        if (type == null)
        {
            return new RpcTypeDescriptor(typeof(void), "Unsupported", "Unsupported", null);
        }

        if (!_type_descriptors.ContainsKey(type))
        {
            _type_descriptors[type] = GetTypeDescriptorInternal(type, marshal_helper);
        }
        return _type_descriptors[type];
    }

    private bool DisableCalculatedCorrelation(RpcTypeDescriptor desc)
    {
        return HasFlag(RpcClientBuilderFlags.DisableCalculatedCorrelations) && !desc.Union;
    }

    private const string MARSHAL_NAME = "__m";
    private const string UNMARSHAL_NAME = "__u";
    private const string CONSTRUCTOR_STRUCT_NAME = "_Constructors";
    private const string ARRAY_CONSTRUCTOR_STRUCT_NAME = "_Array_Constructors";
    private const string UNMARSHAL_HELPER_NAME = "_Unmarshal_Helper";
    private const string MARSHAL_HELPER_NAME = "_Marshal_Helper";
    private const string UNION_SELECTOR_NAME = "Selector";
    private const string DEFAULT_UNION_ARM_LABEL = "done";

    private int GenerateComplexTypes(Func<string, CodeTypeDeclaration> add_type, MarshalHelperBuilder marshal_helper)
    {
        Dictionary<NdrUnionArms, RpcTypeDescriptor> arms_to_desc = new();
        int type_count = 0;

        // First populate the type cache.
        foreach (var complex_type in _complex_types)
        {
            if (!complex_type.IsStruct() && !complex_type.IsUnion())
            {
                continue;
            }

            bool non_encapsulated_union = complex_type.IsNonEncapsulatedUnion();
            string marshal_method = non_encapsulated_union ? nameof(NdrMarshalBuffer.WriteUnion) : nameof(NdrMarshalBuffer.WriteStruct);
            AdditionalArguments marshal_arguments = non_encapsulated_union ? new AdditionalArguments(true, typeof(long).ToRef()) : new AdditionalArguments(true);

            NdrCorrelationDescriptor union_correlation = complex_type.GetUnionCorrelation();
            if (union_correlation == null && non_encapsulated_union)
            {
                // If the correlation is invalid then we've got a problem.
                marshal_arguments = new AdditionalArguments(true, CodeGenUtils.GetPrimitive(0));
            }

            RpcTypeDescriptor type_desc = null;
            if (non_encapsulated_union)
            {
                NdrUnionTypeReference union_type = (NdrUnionTypeReference)complex_type;
                if (!arms_to_desc.ContainsKey(union_type.Arms))
                {
                    type_desc = new RpcTypeDescriptor(complex_type.Name, true,
                        nameof(NdrUnmarshalBuffer.ReadStruct), marshal_helper, marshal_method, complex_type, union_correlation, null,
                        marshal_arguments, new AdditionalArguments(true));
                    arms_to_desc[union_type.Arms] = type_desc;
                }
                else
                {
                    type_desc = new RpcTypeDescriptor(arms_to_desc[union_type.Arms], union_type.GetUnionCorrelation());
                }
            }
            else
            {
                type_desc = new RpcTypeDescriptor(complex_type.Name, true,
                    nameof(NdrUnmarshalBuffer.ReadStruct), marshal_helper, marshal_method, complex_type, union_correlation, null,
                    marshal_arguments, new AdditionalArguments(true));
            }

            _type_descriptors[complex_type] = type_desc;
            type_count++;
        }

        if (type_count == 0)
        {
            return 0;
        }

        bool create_constructor_properties = HasFlag(RpcClientBuilderFlags.GenerateConstructorProperties);
        CodeTypeDeclaration constructor_type = null;
        CodeTypeDeclaration array_constructor_type = null;

        if (create_constructor_properties)
        {
            constructor_type = add_type(CONSTRUCTOR_STRUCT_NAME);
            constructor_type.AddStartRegion("Constructors");
            constructor_type.IsStruct = true;
            array_constructor_type = add_type(ARRAY_CONSTRUCTOR_STRUCT_NAME);
            array_constructor_type.IsStruct = true;
            array_constructor_type.AddEndRegion();
        }

        CodeTypeDeclaration start_type = null;
        CodeTypeDeclaration end_type = null;

        // Now generate the complex types.
        foreach (var complex_type in _complex_types)
        {
            bool non_encapsulated_union = complex_type.IsNonEncapsulatedUnion();
            if (non_encapsulated_union && 
                _type_descriptors.TryGetValue(complex_type, out RpcTypeDescriptor union_desc) && 
                union_desc.UnionAlias)
            {
                continue;
            }
            bool is_union = complex_type.IsUnion();
            bool is_conformant = complex_type.IsConformantStruct();
            var selector_type = complex_type.GetSelectorType();
            string selector_name = complex_type.GetSelectorName();
            if (string.IsNullOrWhiteSpace(selector_name))
                selector_name = UNION_SELECTOR_NAME;

            var s_type = add_type(complex_type.Name);
            start_type ??= s_type;
            end_type = s_type;
            s_type.IsStruct = true;
            if (non_encapsulated_union)
            {
                s_type.BaseTypes.Add(new CodeTypeReference(typeof(INdrNonEncapsulatedUnion)));
            }
            else if (is_conformant)
            {
                s_type.BaseTypes.Add(new CodeTypeReference(typeof(INdrConformantStructure)));
            }
            else
            {
                s_type.BaseTypes.Add(new CodeTypeReference(typeof(INdrStructure)));
            }

            var marshal_method = s_type.AddMarshalMethod(MARSHAL_NAME, marshal_helper, non_encapsulated_union, selector_name,
                selector_type?.GetSimpleTypeDescriptor(null, HasFlag(RpcClientBuilderFlags.UnsignedChar)).CodeType);
            var unmarshal_method = s_type.AddUnmarshalMethod(UNMARSHAL_NAME, marshal_helper);
            if (is_conformant)
            {
                s_type.AddConformantDimensionsMethod(complex_type.GetConformantDimensions(), marshal_helper);
            }
            s_type.AddAlignmentMethod(complex_type.GetAlignment(), marshal_helper);

            var offset_to_name =
                complex_type.GetMembers(selector_name).Select(m => Tuple.Create(m.Offset, m.Name)).ToList();
            var default_initialize_expr = new Dictionary<string, CodeExpression>();
            var member_parameters = new List<Tuple<CodeTypeReference, string, bool>>();
            bool set_default_arm = false;

            foreach (var member in complex_type.GetMembers(selector_name))
            {
                var f_type = GetTypeDescriptor(member.MemberType, marshal_helper);
                s_type.AddField(f_type.GetStructureType(), member.Name, member.Hidden ? MemberAttributes.Private : MemberAttributes.Public);
                member_parameters.Add(Tuple.Create(f_type.GetParameterType(), member.Name, member.Hidden));

                List<RpcMarshalArgument> extra_marshal_args = new();

                if (f_type.ConformanceDescriptor.IsValid)
                {
                    extra_marshal_args.Add(f_type.ConformanceDescriptor.CalculateCorrelationArgument(member.Offset, offset_to_name,
                        DisableCalculatedCorrelation(f_type)));
                }

                if (f_type.VarianceDescriptor.IsValid)
                {
                    extra_marshal_args.Add(f_type.VarianceDescriptor.CalculateCorrelationArgument(member.Offset,
                        offset_to_name, DisableCalculatedCorrelation(f_type)));
                }

                if (f_type.Pointer)
                {
                    marshal_method.AddDeferredMarshalCall(f_type, MARSHAL_NAME, member.Name, member.Selector,
                        selector_name, DEFAULT_UNION_ARM_LABEL, extra_marshal_args.ToArray());
                    unmarshal_method.AddDeferredEmbeddedUnmarshalCall(f_type, UNMARSHAL_NAME, member.Name, member.Selector,
                        selector_name, DEFAULT_UNION_ARM_LABEL);
                }
                else
                {
                    bool null_check = false;
                    if (!f_type.ValueType)
                    {
                        null_check = true;
                    }

                    marshal_method.AddMarshalCall(f_type, MARSHAL_NAME, member.Name, false, null_check, member.Selector,
                        selector_name, DEFAULT_UNION_ARM_LABEL, extra_marshal_args.ToArray());
                    unmarshal_method.AddUnmarshalCall(f_type, UNMARSHAL_NAME, member.Name, member.Selector,
                        selector_name, DEFAULT_UNION_ARM_LABEL);
                }

                if (!f_type.Pointer || f_type.PointerType == RpcPointerType.Reference)
                {
                    if (f_type.CodeType.ArrayRank > 0)
                    {
                        default_initialize_expr.Add(member.Name, new CodeArrayCreateExpression(f_type.CodeType, CodeGenUtils.GetPrimitive(f_type.FixedCount)));
                    }
                    else if (f_type.BuiltinType == typeof(string) && f_type.FixedCount > 0)
                    {
                        default_initialize_expr.Add(member.Name, new CodeObjectCreateExpression(f_type.CodeType, CodeGenUtils.GetPrimitive('\0'),
                            CodeGenUtils.GetPrimitive(f_type.FixedCount)));
                    }
                }

                if (member.Default)
                {
                    set_default_arm = true;
                }
            }

            if (is_union)
            {
                if (!set_default_arm)
                {
                    marshal_method.AddThrow(typeof(ArgumentException), $"No matching union selector when marshaling {s_type.Name}");
                    unmarshal_method.AddThrow(typeof(ArgumentException), $"No matching union selector when marshaling {s_type.Name}");
                }
                marshal_method.Statements.Add(new CodeLabeledStatement(DEFAULT_UNION_ARM_LABEL, new CodeMethodReturnStatement()));
                unmarshal_method.Statements.Add(new CodeLabeledStatement(DEFAULT_UNION_ARM_LABEL, new CodeMethodReturnStatement()));
            }

            var p_type = _type_descriptors[complex_type];

            if (!create_constructor_properties)
            {
                s_type.AddDefaultConstructorMethod("CreateDefault", MemberAttributes.Public | MemberAttributes.Static, p_type, default_initialize_expr);
                s_type.AddConstructorMethod(p_type, member_parameters);
            }
            else
            {
                constructor_type.AddDefaultConstructorMethod(complex_type.Name, MemberAttributes.Public | MemberAttributes.Final, p_type, default_initialize_expr);
                constructor_type.AddConstructorMethod(complex_type.Name, p_type, member_parameters);
                array_constructor_type.AddArrayConstructorMethod(complex_type.Name, p_type);
            }
        }

        if (type_count > 0)
        {
            start_type.AddStartRegion("Complex Types");
            end_type.AddEndRegion();
        }

        return type_count;
    }

    private void GenerateComplexTypesEncoders(string encoder_name, string decoder_name, bool wrap_complex_type, Func<string, CodeTypeDeclaration> add_type, MarshalHelperBuilder marshal_helper)
    {
        CodeTypeDeclaration encoder_type = add_type(encoder_name);
        encoder_type.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
        encoder_type.AddStartRegion("Complex Type Encoders");
        encoder_type.AddConstructor(MemberAttributes.Private);
        CodeTypeDeclaration decoder_type = add_type(decoder_name);
        decoder_type.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
        decoder_type.AddEndRegion();
        decoder_type.AddConstructor(MemberAttributes.Private);

        // Now generate the complex types.
        foreach (var complex_type in _complex_types)
        {
            var desc = GetTypeDescriptor(complex_type, marshal_helper);
            if (desc.Unsupported || !desc.Constructed || desc.UnionAlias)
            {
                continue;
            }

            if (complex_type.IsConformantStruct() || wrap_complex_type)
            {
                // Conformant structures need to be wrapped in a unique pointer.
                desc = new RpcTypeDescriptor(desc, RpcPointerType.Unique);
            }

            var marshal_method = marshal_helper.MarshalMethods[complex_type];
            var unmarshal_method = marshal_helper.UnmarshalMethods[complex_type];

            var encode_method = encoder_type.AddMethod($"{complex_type.Name}_Encode", MemberAttributes.Public | MemberAttributes.Static);
            List<CodeParameterDeclarationExpression> marshal_params = new();
            marshal_params.Add(new CodeParameterDeclarationExpression(desc.GetParameterType(), "o"));
            marshal_params.AddRange(marshal_method.Parameters.Cast<CodeParameterDeclarationExpression>().Skip(1).ToArray());
            encode_method.Parameters.AddRange(marshal_params.ToArray());
            encode_method.CreateMarshalObject(MARSHAL_NAME, marshal_helper, false);
            encode_method.ReturnType = typeof(NdrPickledType).ToRef();

            RpcMarshalArgument[] additional_args = marshal_params.Skip(1).Select(
                p => new RpcMarshalArgument(CodeGenUtils.GetVariable(p.Name), p.Type)).ToArray();

            encode_method.AddMarshalCall(desc, MARSHAL_NAME, "o", desc.Pointer, false, null, null, null, additional_args);
            encode_method.AddReturn(new CodeMethodInvokeExpression(CodeGenUtils.GetVariable(MARSHAL_NAME), nameof(NdrMarshalBuffer.ToPickledType)));

            var decode_method = decoder_type.AddMethod($"{complex_type.Name}_Decode", MemberAttributes.Public | MemberAttributes.Static);
            decode_method.AddParam(typeof(NdrPickledType).ToRef(), "pickled_type");

            decode_method.Statements.Add(new CodeVariableDeclarationStatement(marshal_helper.UnmarshalHelperType, UNMARSHAL_NAME,
                new CodeObjectCreateExpression(marshal_helper.UnmarshalHelperType, CodeGenUtils.GetVariable("pickled_type"))));
            CodeTryCatchFinallyStatement try_catch = new();
            try_catch.FinallyStatements.Add(new CodeMethodInvokeExpression(new CodeVariableReferenceExpression(UNMARSHAL_NAME),
                nameof(IDisposable.Dispose)));
            decode_method.Statements.Add(try_catch);
            if (desc.Pointer)
            {
                try_catch.TryStatements.AddPointerUnmarshalCall(desc, UNMARSHAL_NAME, null);
            }
            else
            {
                try_catch.TryStatements.AddUnmarshalCall(desc, UNMARSHAL_NAME, null, null, null, null);
            }
            decode_method.ReturnType = desc.GetParameterType();
        }
    }

    private CodeTypeDeclaration GenerateStuctureWrapper(Func<string, CodeTypeDeclaration> add_type, NdrProcedureDefinition proc, CodeTypeDeclaration client,
        CodeMemberMethod private_method)
    {
        if (HasFlag(RpcClientBuilderFlags.HideWrappedMethods))
        {
            private_method.Attributes = MemberAttributes.Private | MemberAttributes.Final;
        }

        var type = add_type($"{private_method.Name}_RetVal");
        type.TypeAttributes = TypeAttributes.Public;
        type.IsStruct = true;

        CodeMemberMethod method = new()
        {
            Name = private_method.Name,
            Attributes = MemberAttributes.Public | MemberAttributes.Final
        };

        client.Members.Add(method);
        method.Name = private_method.Name;
        if (proc.HasAsyncHandle)
        {
            method.Comments.Add(new CodeCommentStatement("async"));
        }

        var retval_type = new CodeTypeReference(type.Name);
        method.ReturnType = retval_type;
        method.Statements.Add(new CodeVariableDeclarationStatement(retval_type, "r", new CodeObjectCreateExpression(retval_type)));
        CodeExpression retval_ref = CodeGenUtils.GetVariable("r");

        List<CodeExpression> call_params = new();
        foreach (var p in private_method.Parameters.Cast<CodeParameterDeclarationExpression>())
        {
            if (p.Direction == FieldDirection.In)
            {
                method.Parameters.Add(new CodeParameterDeclarationExpression(p.Type, p.Name));
                call_params.Add(CodeGenUtils.GetVariable(p.Name));
            }
            else
            {
                type.AddField(p.Type, p.Name, MemberAttributes.Public);
                var field_ref = new CodeFieldReferenceExpression(retval_ref, p.Name);
                if (p.Direction == FieldDirection.Ref)
                {
                    method.Parameters.Add(new CodeParameterDeclarationExpression(p.Type, p.Name));
                    method.Statements.Add(new CodeAssignStatement(field_ref, CodeGenUtils.GetVariable(p.Name)));
                }
                call_params.Add(new CodeDirectionExpression(p.Direction, field_ref));
            }
        }

        CodeExpression invoke_expr = new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(null, private_method.Name),
            call_params.ToArray());
        if (proc.ReturnValue != null)
        {
            type.AddField(private_method.ReturnType, "retval", MemberAttributes.Public);
            method.Statements.Add(new CodeAssignStatement(new CodeFieldReferenceExpression(retval_ref, "retval"), invoke_expr));
        }
        else
        {
            method.Statements.Add(invoke_expr);
        }

        method.AddReturn(retval_ref);
        return type;
    }

    private void GenerateClient(string name, Func<string, CodeTypeDeclaration> add_type, int complex_type_count, MarshalHelperBuilder marshal_helper)
    {
        if (_build == null)
        {
            return;
        }
        CodeTypeDeclaration type = add_type(name);
        CodeTypeDeclaration last_type = type;
        type.AddStartRegion("Client Implementation");
        type.IsClass = true;
        type.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
        type.BaseTypes.Add(typeof(RpcClientBase));

        CodeConstructor constructor = type.AddConstructor(MemberAttributes.Public | MemberAttributes.Final);
        constructor.BaseConstructorArgs.Add(CodeGenUtils.GetPrimitive(_build.InterfaceId.ToString()));
        constructor.BaseConstructorArgs.Add(CodeGenUtils.GetPrimitive(_build.InterfaceVersion.Major));
        constructor.BaseConstructorArgs.Add(CodeGenUtils.GetPrimitive(_build.InterfaceVersion.Minor));

        type.CreateSendReceive(marshal_helper);

        foreach (var proc in _build.Procedures)
        {
            string proc_name = proc.Name;
            if (!_proc_names.Add(proc_name))
            {
                proc_name = $"{proc_name}_{proc.ProcNum}";
                if (!_proc_names.Add(proc_name))
                {
                    throw new ArgumentException($"Duplicate name {proc.Name}");
                }
            }

            var method = type.AddMethod(proc_name, MemberAttributes.Public | MemberAttributes.Final);

            if (HasFlag(RpcClientBuilderFlags.InsertBreakpoints))
            {
                method.AddBreakpoint();
            }

            if (proc.HasAsyncHandle)
            {
                method.Comments.Add(new CodeCommentStatement("async"));
            }

            if (proc.Params.Any(p => p.IsPipe))
            {
                string check_method = proc.HasAsyncHandle ? "CheckAsynchronousPipeSupport" : "CheckSynchronousPipeSupport";
                method.Statements.Add(new CodeMethodInvokeExpression(null, check_method));
            }

            RpcTypeDescriptor return_type = GetTypeDescriptor(proc.ReturnValue?.Type, marshal_helper);
            if (return_type == null)
            {
                method.ThrowNotImplemented("Return type unsupported.");
                continue;
            }

            var offset_to_name =
                proc.Params.Select(p => Tuple.Create(p.Offset, p.Name)).ToList();

            method.ReturnType = return_type.CodeType;
            method.CreateMarshalObject(MARSHAL_NAME, marshal_helper, true);

            int out_parameter_count = 0;
            List<Action> pipe_marshal_cmds = new();
            foreach (var p in proc.Params)
            {
                if (p == proc.Handle)
                {
                    continue;
                }

                if (p.IsOut)
                {
                    out_parameter_count++;
                }

                RpcTypeDescriptor p_type = GetTypeDescriptor(p.Type, marshal_helper);

                List<RpcMarshalArgument> extra_marshal_args = new();

                if (p_type.ConformanceDescriptor.IsValid)
                {
                    extra_marshal_args.Add(p_type.ConformanceDescriptor.CalculateCorrelationArgument(p.Offset, offset_to_name, DisableCalculatedCorrelation(p_type)));
                }

                if (p_type.VarianceDescriptor.IsValid)
                {
                    extra_marshal_args.Add(p_type.VarianceDescriptor.CalculateCorrelationArgument(p.Offset, offset_to_name, DisableCalculatedCorrelation(p_type)));
                }

                var p_obj = method.AddParam(p_type.GetParameterType(), p.Name);
                p_obj.Direction = p.GetDirection();
                if (!p.IsIn)
                {
                    continue;
                }

                bool write_ref = false;
                bool null_check = false;
                if (p_type.Pointer)
                {
                    if (p_type.PointerType == RpcPointerType.Reference)
                    {
                        null_check = !p_type.ValueType;
                    }
                    else
                    {
                        write_ref = true;
                    }
                }
                else if (!p_type.ValueType)
                {
                    null_check = true;
                }

                void marshal_method() => method.AddMarshalCall(p_type, MARSHAL_NAME, p.Name,
                        write_ref, null_check, null, null, null, extra_marshal_args.ToArray());

                if (p.IsPipe)
                {
                    pipe_marshal_cmds.Add(marshal_method);
                }
                else
                {
                    marshal_method();
                }
            }

            pipe_marshal_cmds.ForEach(a => a());

            var try_statement = method.SendReceive(MARSHAL_NAME, UNMARSHAL_NAME, proc.ProcNum, marshal_helper);

            List<Action> non_pipe_unmarshal_cmds = new();
            foreach (var p in proc.Params.Where(x => x.IsOut))
            {
                if (p == proc.Handle)
                {
                    continue;
                }

                void unmarshal_method()
                {
                    RpcTypeDescriptor p_type = GetTypeDescriptor(p.Type, marshal_helper);
                    if (p_type.Pointer && p_type.PointerType != RpcPointerType.Reference)
                    {
                        try_statement.AddPointerUnmarshalCall(p_type, UNMARSHAL_NAME, p.Name);
                    }
                    else
                    {
                        try_statement.AddUnmarshalCall(p_type, UNMARSHAL_NAME, p.Name, null, null, null);
                    }
                }
                if (p.IsPipe)
                {
                    unmarshal_method();
                }
                else
                {
                    non_pipe_unmarshal_cmds.Add(unmarshal_method);
                }
            }

            non_pipe_unmarshal_cmds.ForEach(a => a());

            try_statement.AddUnmarshalReturn(return_type, UNMARSHAL_NAME);

            if (HasFlag(RpcClientBuilderFlags.StructureReturn) && out_parameter_count > 0)
            {
                last_type = GenerateStuctureWrapper(add_type, proc, type, method);
            }
        }

        if (complex_type_count > 0 && HasFlag(RpcClientBuilderFlags.GenerateConstructorProperties))
        {
            var constructor_type = new CodeTypeReference(CodeGenUtils.MakeIdentifier(CONSTRUCTOR_STRUCT_NAME));
            var prop = type.AddProperty("New", constructor_type, MemberAttributes.Public | MemberAttributes.Final,
                new CodeMethodReturnStatement(new CodeObjectCreateExpression(constructor_type)));
            constructor_type = new CodeTypeReference(CodeGenUtils.MakeIdentifier(ARRAY_CONSTRUCTOR_STRUCT_NAME));
            type.AddProperty("NewArray", constructor_type, MemberAttributes.Public | MemberAttributes.Final,
                new CodeMethodReturnStatement(new CodeObjectCreateExpression(constructor_type)));
        }

        last_type.AddEndRegion();
    }

    private static string GenerateSourceCode(CodeDomProvider provider, CodeGeneratorOptions options, CodeCompileUnit unit)
    {
        StringBuilder builder = new();
        TextWriter writer = new StringWriter(builder);
        provider.GenerateCodeFromCompileUnit(unit, writer, options);
        return builder.ToString();
    }

    private void AddServerComment(CodeCompileUnit unit)
    {
        if (_build == null)
        {
            return;
        }
        CodeNamespace ns = unit.AddNamespace(string.Empty);

        ns.AddComment($"Source Executable: {_build.FilePath.ToLower()}");
        ns.AddComment($"Interface ID: {_build.InterfaceId}");
        ns.AddComment($"Interface Version: {_build.InterfaceVersion}");

        if (!_args.Flags.HasFlag(RpcClientBuilderFlags.ExcludeVariableSourceText))
        {
            ns.AddComment($"Client Generated: {DateTime.Now}");
            ns.AddComment($"NtCoreLib Version: {NtObjectUtils.GetVersion()}");
        }
    }

    private CodeCompileUnit Generate()
    {
        CodeCompileUnit unit = new();
        string ns_name = _args.NamespaceName;
        if (string.IsNullOrWhiteSpace(ns_name))
        {
            if (_build != null && !HasFlag(RpcClientBuilderFlags.NoNamespace))
            {
                ns_name = $"rpc_{_build.InterfaceId.ToString().Replace('-', '_')}_{_build.InterfaceVersion.Major}_{_build.InterfaceVersion.Minor}";
            }
            else
            {
                ns_name = string.Empty;
            }
        }
        string name = _args.ClientName;
        if (string.IsNullOrWhiteSpace(name))
        {
            name = "Client";
        }
        AddServerComment(unit);
        CodeNamespace ns = unit.AddNamespace(ns_name);
        Func<string, CodeTypeDeclaration> add_type = name => ns.AddType(name);
        bool gen_wrapper_type = _args.Flags.HasFlagSet(RpcClientBuilderFlags.GenerateWrapperType);

        if (gen_wrapper_type)
        {
            string wrapper_name = _args.WrapperTypeName;
            if (string.IsNullOrWhiteSpace(wrapper_name))
            {
                wrapper_name = "RpcImplementation";
            }

            CodeTypeDeclaration wrapper_type = ns.AddType(wrapper_name);
            wrapper_type.TypeAttributes = TypeAttributes.Public | TypeAttributes.Sealed;
            wrapper_type.AddConstructor(MemberAttributes.Private);
            add_type = name => wrapper_type.AddType(name);
        }

        bool type_decode = HasFlag(RpcClientBuilderFlags.GenerateComplexTypeEncodeMethods);
        MarshalHelperBuilder marshal_helper = new(add_type, gen_wrapper_type, MARSHAL_HELPER_NAME, UNMARSHAL_HELPER_NAME, type_decode);
        int complex_type_count = GenerateComplexTypes(add_type, marshal_helper);
        if (type_decode)
        {
            string encode_name = _args.EncoderName;
            if (string.IsNullOrWhiteSpace(encode_name))
            {
                encode_name = "Encoder";
            }
            string decode_name = _args.DecoderName;
            if (string.IsNullOrWhiteSpace(decode_name))
            {
                decode_name = "Decoder";
            }
            GenerateComplexTypesEncoders(encode_name, decode_name,
                HasFlag(RpcClientBuilderFlags.PointerComplexTypeDecoders), add_type, marshal_helper);
        }

        if (!HasFlag(RpcClientBuilderFlags.ExcludeClient))
        {
            GenerateClient(name, add_type, complex_type_count, marshal_helper);
        }

        return unit;
    }

    private Assembly Compile(CodeCompileUnit unit, CodeDomProvider provider, bool enable_debugging)
    {
        CompilerParameters compile_params = new();
        using TempFileCollection temp_files = new(Path.GetTempPath());
        enable_debugging = enable_debugging || HasFlag(RpcClientBuilderFlags.InsertBreakpoints);
        compile_params.GenerateExecutable = false;
        compile_params.GenerateInMemory = true;
        compile_params.IncludeDebugInformation = enable_debugging;
        compile_params.TempFiles = temp_files;
        temp_files.KeepFiles = enable_debugging;
        compile_params.ReferencedAssemblies.Add(typeof(RpcClientBuilder).Assembly.Location);
        CompilerResults results = provider.CompileAssemblyFromDom(compile_params, unit);
        if (results.Errors.HasErrors)
        {
            throw new RpcClientBuilderException("Internal error compiling RPC source code", results.Errors);
        }
        return results.CompiledAssembly;
    }

    #endregion

    #region Constructors

    private RpcClientBuilder(IEnumerable<NdrComplexTypeReference> complex_types, RpcClientBuilderArguments args)
    {
        _complex_types = complex_types;
        _type_descriptors = new Dictionary<NdrBaseTypeReference, RpcTypeDescriptor>();
        _args = args;
        _proc_names = new HashSet<string>();
    }

    private RpcClientBuilder(IRpcBuildableClient build, RpcClientBuilderArguments args)
        : this(build.ComplexTypes, args: args)
    {
        if (!build.HasDceSyntaxInfo)
            throw new ArgumentException("No DCE NDR syntax available.", nameof(build));
        _build = build;
    }

    #endregion

    #region Static Methods

    /// <summary>
    /// Build a source file for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <param name="options">The code generation options, can be null.</param>
    /// <param name="provider">The code dom provider, such as CSharpDomProvider</param>
    /// <returns>The source code file.</returns>
    public static string BuildSource(RpcServer server, RpcClientBuilderArguments args, CodeDomProvider provider, CodeGeneratorOptions options)
    {
        return GenerateSourceCode(provider, options, new RpcClientBuilder(server, args).Generate());
    }

    /// <summary>
    /// Build a C# source file for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <returns>The C# source code file.</returns>
    public static string BuildSource(RpcServer server, RpcClientBuilderArguments args)
    {
        CodeDomProvider provider = new CSharpCodeProvider();
        CodeGeneratorOptions options = new()
        {
            IndentString = "    ",
            BlankLinesBetweenMembers = false,
            VerbatimOrder = true,
            BracingStyle = "C"
        };
        return BuildSource(server, args, provider, options);
    }

    /// <summary>
    /// Build a C# source file for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <returns>The C# source code file.</returns>
    public static string BuildSource(RpcServer server)
    {
        return BuildSource(server, new RpcClientBuilderArguments());
    }

    /// <summary>
    /// Build a source file for RPC complex types.
    /// </summary>
    /// <param name="complex_types">The RPC complex types to build the encoders from.</param>
    /// <param name="decoder_name">Name of the decoder class. Can be null or empty to use default.</param>
    /// <param name="encoder_name">Name of the encoder class. Can be null or empty to use default.</param>
    /// <param name="namespace_name">Name of the generated namespace. Null or empty specified no namespace.</param>
    /// <param name="options">The code generation options, can be null.</param>
    /// <param name="provider">The code dom provider, such as CSharpDomProvider</param>
    /// <param name="pointer_complex_types">True to wrap complex decoders in a unique pointer.</param>
    /// <returns>The source code file.</returns>
    public static string BuildSource(IEnumerable<NdrComplexTypeReference> complex_types,
        string encoder_name, string decoder_name, string namespace_name,
        bool pointer_complex_types, CodeDomProvider provider, CodeGeneratorOptions options)
    {
        RpcClientBuilderArguments args = new()
        {
            EncoderName = encoder_name,
            DecoderName = decoder_name,
            NamespaceName = namespace_name,
            Flags = RpcClientBuilderFlags.GenerateComplexTypeEncodeMethods
        };
        if (pointer_complex_types)
        {
            args.Flags |= RpcClientBuilderFlags.PointerComplexTypeDecoders;
        }
        return GenerateSourceCode(provider, options, new RpcClientBuilder(complex_types, args).Generate());
    }

    /// <summary>
    /// Build a source file for RPC complex types.
    /// </summary>
    /// <param name="complex_types">The RPC complex types to build the encoders from.</param>
    /// <param name="decoder_name">Name of the decoder class. Can be null or empty to use default.</param>
    /// <param name="encoder_name">Name of the encoder class. Can be null or empty to use default.</param>
    /// <param name="namespace_name">Name of the generated namespace. Null or empty specified no namespace.</param>
    /// <param name="options">The code generation options, can be null.</param>
    /// <param name="provider">The code dom provider, such as CSharpDomProvider</param>
    /// <returns>The source code file.</returns>
    public static string BuildSource(IEnumerable<NdrComplexTypeReference> complex_types,
        string encoder_name, string decoder_name, string namespace_name, CodeDomProvider provider, CodeGeneratorOptions options)
    {
        return BuildSource(complex_types, encoder_name, decoder_name, namespace_name, false, provider, options);
    }

    /// <summary>
    /// Build a source file for RPC complex types.
    /// </summary>
    /// <param name="complex_types">The RPC complex types to build the encoders from.</param>
    /// <param name="decoder_name">Name of the decoder class. Can be null or empty to use default.</param>
    /// <param name="encoder_name">Name of the encoder class. Can be null or empty to use default.</param>
    /// <param name="namespace_name">Name of the generated namespace. Null or empty specified no namespace.</param>
    /// <param name="pointer_complex_types">True to wrap complex decoders in a unique pointer.</param>
    /// <returns>The source code file.</returns>
    public static string BuildSource(IEnumerable<NdrComplexTypeReference> complex_types,
        string encoder_name, string decoder_name, string namespace_name,
        bool pointer_complex_types)
    {
        CodeDomProvider provider = new CSharpCodeProvider();
        CodeGeneratorOptions options = new()
        {
            IndentString = "    ",
            BlankLinesBetweenMembers = false,
            VerbatimOrder = true,
            BracingStyle = "C"
        };
        return BuildSource(complex_types, encoder_name, decoder_name, namespace_name, pointer_complex_types, provider, options);
    }

    /// <summary>
    /// Build a source file for RPC complex types.
    /// </summary>
    /// <param name="complex_types">The RPC complex types to build the encoders from.</param>
    /// <param name="decoder_name">Name of the decoder class. Can be null or empty to use default.</param>
    /// <param name="encoder_name">Name of the encoder class. Can be null or empty to use default.</param>
    /// <param name="namespace_name">Name of the generated namespace. Null or empty specified no namespace.</param>
    /// <returns>The source code file.</returns>
    public static string BuildSource(IEnumerable<NdrComplexTypeReference> complex_types,
        string encoder_name, string decoder_name, string namespace_name)
    {
        return BuildSource(complex_types, encoder_name, decoder_name, namespace_name, false);
    }

    /// <summary>
    /// Build a source file for RPC complex types.
    /// </summary>
    /// <param name="complex_types">The RPC complex types to build the encoders from.</param>
    /// <returns>The C# source code file.</returns>
    public static string BuildSource(IEnumerable<NdrComplexTypeReference> complex_types)
    {
        return BuildSource(complex_types, string.Empty, string.Empty, string.Empty);
    }

    /// <summary>
    /// Compile an in-memory assembly for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <param name="ignore_cache">True to ignore cached assemblies.</param>
    /// <param name="provider">Code DOM provider to compile the assembly.</param>
    /// <returns>The compiled assembly.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static Assembly BuildAssembly(RpcServer server, RpcClientBuilderArguments args, bool ignore_cache, CodeDomProvider provider)
    {
        var builder = new RpcClientBuilder(server, args);
        if (ignore_cache)
        {
            return builder.Compile(builder.Generate(), provider, args.EnableDebugging);
        }

        var key = Tuple.Create(server, args);
        if (!_compiled_clients.ContainsKey(key))
        {
            _compiled_clients[key] = builder.Compile(builder.Generate(), provider, args.EnableDebugging);
        }
        return _compiled_clients[key];
    }

    /// <summary>
    /// Compile an in-memory assembly for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <param name="ignore_cache">True to ignore cached assemblies.</param>
    /// <returns>The compiled assembly.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static Assembly BuildAssembly(RpcServer server, RpcClientBuilderArguments args, bool ignore_cache)
    {
        return BuildAssembly(server, args, ignore_cache, new CSharpCodeProvider());
    }

    /// <summary>
    /// Compile an in-memory assembly for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <returns>The compiled assembly.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static Assembly BuildAssembly(RpcServer server, RpcClientBuilderArguments args)
    {
        return BuildAssembly(server, args, false);
    }

    /// <summary>
    /// Compile an in-memory assembly for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="ignore_cache">True to ignore cached assemblies.</param>
    /// <returns>The compiled assembly.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static Assembly BuildAssembly(RpcServer server, bool ignore_cache)
    {
        return BuildAssembly(server, new RpcClientBuilderArguments(), ignore_cache);
    }

    /// <summary>
    /// Compile an in-memory assembly for the RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <returns>The compiled assembly.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static Assembly BuildAssembly(RpcServer server)
    {
        return BuildAssembly(server, false);
    }

    /// <summary>
    /// Create an instance of an RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="ignore_cache">True to ignore cached assemblies.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <param name="provider">Code DOM provider to compile the assembly.</param>
    /// <returns>The created RPC client.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static RpcClientBase CreateClient(RpcServer server, RpcClientBuilderArguments args, bool ignore_cache, CodeDomProvider provider)
    {
        Type type = BuildAssembly(server, args, ignore_cache, provider ?? new CSharpCodeProvider()).GetTypes().Where(t => typeof(RpcClientBase).IsAssignableFrom(t)).First();
        return (RpcClientBase)Activator.CreateInstance(type);
    }

    /// <summary>
    /// Create an instance of an RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="ignore_cache">True to ignore cached assemblies.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <returns>The created RPC client.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static RpcClientBase CreateClient(RpcServer server, RpcClientBuilderArguments args, bool ignore_cache)
    {
        return CreateClient(server, args, ignore_cache, null);
    }

    /// <summary>
    /// Create an instance of an RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <param name="args">Additional builder arguments.</param>
    /// <returns>The created RPC client.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static RpcClientBase CreateClient(RpcServer server, RpcClientBuilderArguments args)
    {
        return CreateClient(server, args, false);
    }

    /// <summary>
    /// Create an instance of an RPC client.
    /// </summary>
    /// <param name="server">The RPC server to base the client on.</param>
    /// <returns>The created RPC client.</returns>
    /// <remarks>This method will cache the results of the compilation against the RpcServer.</remarks>
    public static RpcClientBase CreateClient(RpcServer server)
    {
        return CreateClient(server, new RpcClientBuilderArguments());
    }

    #endregion
}
