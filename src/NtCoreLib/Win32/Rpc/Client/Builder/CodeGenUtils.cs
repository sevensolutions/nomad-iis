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
using NtCoreLib.Win32.Rpc.Client.Builder;
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace NtCoreLib.Win32.Rpc.Client.Builder;

internal static class CodeGenUtils
{
    #region Private Members

    private static readonly Regex _identifier_regex = new(@"[^a-zA-Z0-9_\.]");
    private const string MARSHAL_PARAM_NAME = "__m";
    private const string UNMARSHAL_PARAM_NAME = "__u";

    private static void AddMarshalInterfaceMethod(CodeTypeDeclaration type, MarshalHelperBuilder marshal_helper, bool non_encapsulated_union)
    {
        CodeMemberMethod method = type.AddMethod(nameof(INdrStructure.Marshal), MemberAttributes.Final | MemberAttributes.Private);
        method.PrivateImplementationType = new CodeTypeReference(typeof(INdrStructure));
        method.AddParam(typeof(INdrMarshalBuffer), MARSHAL_PARAM_NAME);
        if (non_encapsulated_union)
        {
            method.AddThrow(typeof(NotImplementedException));
        }
        else
        {
            method.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(null, nameof(INdrStructure.Marshal)),
                marshal_helper.CastMarshal(GetVariable(MARSHAL_PARAM_NAME))));
        }
    }

    private static void AddMarshalUnionInterfaceMethod(CodeTypeDeclaration type, MarshalHelperBuilder marshal_helper, string selector_name, CodeTypeReference selector_type)
    {
        CodeMemberMethod method = type.AddMethod(nameof(INdrNonEncapsulatedUnion.Marshal), MemberAttributes.Final | MemberAttributes.Private);
        method.PrivateImplementationType = new CodeTypeReference(typeof(INdrNonEncapsulatedUnion));
        method.AddParam(typeof(INdrMarshalBuffer), MARSHAL_PARAM_NAME);
        method.AddParam(typeof(long), "l");
        // Assign the hidden selector.
        method.Statements.Add(new CodeAssignStatement(GetVariable(selector_name), new CodeCastExpression(selector_type, GetVariable("l"))));
        method.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(null, nameof(INdrStructure.Marshal)),
            marshal_helper.CastMarshal(GetVariable(MARSHAL_PARAM_NAME))));
    }

    private static void AddAssignmentStatements(this CodeMemberMethod method, CodeExpression target, IEnumerable<Tuple<CodeTypeReference, string, bool>> parameters)
    {
        foreach (var p in parameters)
        {
            method.AddParam(p.Item1, p.Item2);
            method.Statements.Add(new CodeAssignStatement(target.GetFieldReference(p.Item2), GetVariable(p.Item2)));
        }
    }

    private static CodeExpression GetArmCase(this NdrUnionArm arm, NdrSimpleTypeReference ndr_type)
    {
        long ret = arm.CaseValue;
        switch (ndr_type.Format)
        {
            case NdrFormatCharacter.FC_BYTE:
                ret = (byte)arm.CaseValue;
                break;
            case NdrFormatCharacter.FC_USHORT:
                ret = (ushort)arm.CaseValue;
                break;
            case NdrFormatCharacter.FC_ULONG:
                ret = (uint)arm.CaseValue;
                break;
        }
        return GetPrimitive(ret);
    }

    private static string FindCorrelationArgument(int expected_offset, IEnumerable<Tuple<int, string>> offset_to_name)
    {
        foreach (var offset in offset_to_name)
        {
            if (offset.Item1 == expected_offset)
            {
                return offset.Item2;
            }
            else if (offset.Item1 > expected_offset)
            {
                break;
            }
        }
        return null;
    }

    private static void AddUnmarshalInterfaceMethod(CodeTypeDeclaration type, MarshalHelperBuilder marshal_helper)
    {
        CodeMemberMethod method = type.AddMethod(nameof(INdrStructure.Unmarshal), MemberAttributes.Final | MemberAttributes.Private);
        method.PrivateImplementationType = new CodeTypeReference(typeof(INdrStructure));
        method.AddParam(typeof(INdrUnmarshalBuffer), UNMARSHAL_PARAM_NAME);
        method.Statements.Add(new CodeMethodInvokeExpression(new CodeMethodReferenceExpression(null, nameof(INdrStructure.Unmarshal)),
            marshal_helper.CastUnmarshal(GetVariable(UNMARSHAL_PARAM_NAME))));
    }

    // TODO: Operations might need to be handled as int32 rather than long.
    private static CodeExpression BuildCorrelationExpression(NdrExpression expr, int current_offset,
        IEnumerable<Tuple<int, string>> offset_to_name, bool disable_correlation)
    {
        if (expr is NdrConstantExpression const_expr)
        {
            return GetPrimitive(const_expr.Value);
        }

        // Allow constant expressions even if disabled.
        if (disable_correlation)
        {
            return GetPrimitive(-1);
        }

        if (expr is NdrVariableExpression var_expr)
        {
            string var_name = FindCorrelationArgument(current_offset + var_expr.Offset, offset_to_name);
            if (var_name != null)
            {
                return GetVariable(var_name);
            }
        }
        else if (expr is NdrOperatorExpression op_expr)
        {
            if (op_expr.Arguments.Count == 3)
            {
                return OpTernary(BuildCorrelationExpression(op_expr.Arguments[2], current_offset, offset_to_name, false).ToBool(),
                    BuildCorrelationExpression(op_expr.Arguments[0], current_offset, offset_to_name, false),
                    BuildCorrelationExpression(op_expr.Arguments[1], current_offset, offset_to_name, false));
            }
            else if (op_expr.Arguments.Count == 2)
            {
                switch (op_expr.Operator)
                {
                    case NdrExpressionOperator.OP_AND:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpBitwiseAnd), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_OR:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpBitwiseOr), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_PLUS:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpPlus), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_MINUS:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpMinus), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_MOD:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpMod), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_SLASH:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpSlash), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_STAR:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpStar), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_LEFT_SHIFT:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpLeftShift), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_RIGHT_SHIFT:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpRightShift), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_XOR:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpXor), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_LOGICAL_AND:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpLogicalAnd), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_LOGICAL_OR:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpLogicalOr), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_EQUAL:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpEqual), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_NOT_EQUAL:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpNotEqual), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_LESS:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpLess), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_LESS_EQUAL:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpLessEqual), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_GREATER:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpGreater), current_offset, offset_to_name);
                    case NdrExpressionOperator.OP_GREATER_EQUAL:
                        return GetOpMethod(op_expr, nameof(NdrMarshalUtils.OpGreaterEqual), current_offset, offset_to_name);
                }
            }
            else if (op_expr.Arguments.Count == 1)
            {
                CodeExpression left_expr = BuildCorrelationExpression(op_expr.Arguments[0], current_offset, offset_to_name, false);

                switch (op_expr.Operator)
                {
                    case NdrExpressionOperator.OP_UNARY_INDIRECTION:
                        return left_expr.DeRef();
                    case NdrExpressionOperator.OP_UNARY_CAST:
                        return left_expr.Cast(op_expr.Format.GetSimpleTypeDescriptor().CodeType);
                    case NdrExpressionOperator.OP_UNARY_COMPLEMENT:
                        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.OpComplement), left_expr);
                    case NdrExpressionOperator.OP_UNARY_MINUS:
                        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.OpMinus), left_expr);
                    case NdrExpressionOperator.OP_UNARY_PLUS:
                        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.OpPlus), left_expr);
                }
            }
        }

        // Can't seem to generate expression.
        return GetPrimitive(-1);
    }

    private static CodeExpression GetBinaryExpression(NdrOperatorExpression expr, CodeBinaryOperatorType op, int current_offset, IEnumerable<Tuple<int, string>> offset_to_name)
    {
        return new CodeBinaryOperatorExpression(BuildCorrelationExpression(expr.Arguments[0], current_offset, offset_to_name, false),
            op, BuildCorrelationExpression(expr.Arguments[1], current_offset, offset_to_name, false));
    }

    private static RpcTypeDescriptor GetSimpleTypeDescriptor(this NdrFormatCharacter format)
    {
        return new NdrSimpleTypeReference(format).GetSimpleTypeDescriptor(null, false);
    }

    private static CodeExpression GetOpMethod(NdrOperatorExpression op_expr, string name, int current_offset,
        IEnumerable<Tuple<int, string>> offset_to_name)
    {
        return GetStaticMethod(typeof(NdrMarshalUtils), name, BuildCorrelationExpression(op_expr.Arguments[0], current_offset, offset_to_name, false),
                                BuildCorrelationExpression(op_expr.Arguments[1], current_offset, offset_to_name, false));
    }

    #endregion

    public static CodeNamespace AddNamespace(this CodeCompileUnit unit, string ns_name)
    {
        CodeNamespace ns = new(ns_name);
        unit.Namespaces.Add(ns);
        return ns;
    }

    public static CodeTypeDeclaration AddType(this CodeNamespace ns, string name)
    {
        CodeTypeDeclaration type = new(MakeIdentifier(name));
        ns.Types.Add(type);
        return type;
    }

    public static CodeTypeDeclaration AddType(this CodeTypeDeclaration wrapper, string name)
    {
        CodeTypeDeclaration type = new(MakeIdentifier(name));
        wrapper.Members.Add(type);
        return type;
    }

    public static CodeMemberProperty AddProperty(this CodeTypeDeclaration type, string name, CodeTypeReference prop_type, MemberAttributes attributes, params CodeStatement[] get_statements)
    {
        var property = new CodeMemberProperty
        {
            Name = name,
            Type = prop_type,
            Attributes = attributes
        };
        property.GetStatements.AddRange(get_statements);
        type.Members.Add(property);
        return property;
    }

    public static CodeMemberMethod AddMethod(this CodeTypeDeclaration type, string name, MemberAttributes attributes)
    {
        CodeMemberMethod method = new()
        {
            Name = MakeIdentifier(name),
            Attributes = attributes
        };
        type.Members.Add(method);
        return method;
    }

    public static CodeMemberMethod AddMarshalMethod(this CodeTypeDeclaration type, string marshal_name, MarshalHelperBuilder marshal_helper,
        bool non_encapsulated_union, string selector_name, CodeTypeReference selector_type)
    {
        AddMarshalInterfaceMethod(type, marshal_helper, non_encapsulated_union);
        if (non_encapsulated_union)
        {
            AddMarshalUnionInterfaceMethod(type, marshal_helper, selector_name, selector_type);
        }
        CodeMemberMethod method = type.AddMethod(nameof(INdrStructure.Marshal), MemberAttributes.Final | MemberAttributes.Private);
        method.AddParam(marshal_helper.MarshalHelperType, marshal_name);
        return method;
    }

    public static void AddConformantDimensionsMethod(this CodeTypeDeclaration type, int dimensions, MarshalHelperBuilder marshal_helper)
    {
        CodeMemberMethod method = type.AddMethod(nameof(INdrConformantStructure.GetConformantDimensions), MemberAttributes.Final | MemberAttributes.Private);
        method.PrivateImplementationType = new CodeTypeReference(typeof(INdrConformantStructure));
        method.ReturnType = typeof(int).ToRef();
        method.AddReturn(GetPrimitive(dimensions));
    }

    public static void AddAlignmentMethod(this CodeTypeDeclaration type, int alignment, MarshalHelperBuilder marshal_helper)
    {
        CodeMemberMethod method = type.AddMethod(nameof(INdrStructure.GetAlignment), MemberAttributes.Final | MemberAttributes.Private);
        method.PrivateImplementationType = new CodeTypeReference(typeof(INdrStructure));
        method.ReturnType = typeof(int).ToRef();
        method.AddReturn(GetPrimitive(alignment));
    }

    public static CodeMemberMethod AddUnmarshalMethod(this CodeTypeDeclaration type, string unmarshal_name, MarshalHelperBuilder marshal_helper)
    {
        AddUnmarshalInterfaceMethod(type, marshal_helper);
        CodeMemberMethod method = type.AddMethod(nameof(INdrStructure.Unmarshal), MemberAttributes.Final | MemberAttributes.Private);
        method.AddParam(marshal_helper.UnmarshalHelperType, unmarshal_name);
        return method;
    }

    public static void ThrowNotImplemented(this CodeMemberMethod method, string comment)
    {
        method.Statements.Add(new CodeCommentStatement(comment));
        method.AddThrow(typeof(NotImplementedException), comment);
    }

    public static CodeConstructor AddConstructor(this CodeTypeDeclaration type, MemberAttributes attributes)
    {
        var cons = new CodeConstructor
        {
            Attributes = attributes
        };
        type.Members.Add(cons);
        return cons;
    }

    public static CodeParameterDeclarationExpression AddParam(this CodeMemberMethod method, Type type, string name)
    {
        var param = new CodeParameterDeclarationExpression(type, MakeIdentifier(name));
        method.Parameters.Add(param);
        return param;
    }

    public static CodeParameterDeclarationExpression AddParam(this CodeMemberMethod method, CodeTypeReference type, string name)
    {
        var param = new CodeParameterDeclarationExpression(type, MakeIdentifier(name));
        method.Parameters.Add(param);
        return param;
    }

    public static CodeMemberField AddField(this CodeTypeDeclaration type, CodeTypeReference builtin_type, string name, MemberAttributes attributes)
    {
        var field = new CodeMemberField(builtin_type, name)
        {
            Attributes = attributes
        };
        type.Members.Add(field);
        return field;
    }

    public static CodeFieldReferenceExpression GetFieldReference(this CodeExpression target, string name)
    {
        return new CodeFieldReferenceExpression(target, name);
    }

    public static CodeMethodReturnStatement AddReturn(this CodeStatementCollection statements, CodeExpression return_expr)
    {
        CodeMethodReturnStatement ret = new(return_expr);
        statements.Add(ret);
        return ret;
    }

    public static CodeMethodReturnStatement AddReturn(this CodeMemberMethod method, CodeExpression return_expr)
    {
        return AddReturn(method.Statements, return_expr);
    }

    public static void AddDefaultConstructorMethod(this CodeTypeDeclaration type, string name, MemberAttributes attributes, RpcTypeDescriptor complex_type, Dictionary<string, CodeExpression> initialize_expr)
    {
        CodeMemberMethod method = type.AddMethod(name, attributes);
        method.ReturnType = complex_type.CodeType;
        CodeExpression return_value = new CodeObjectCreateExpression(complex_type.CodeType);
        if (initialize_expr.Count > 0)
        {
            method.Statements.Add(new CodeVariableDeclarationStatement(complex_type.CodeType, "ret", return_value));
            return_value = GetVariable("ret");
            method.Statements.AddRange(initialize_expr.Select(p => new CodeAssignStatement(return_value.GetFieldReference(p.Key), p.Value)).ToArray());
        }
        method.AddReturn(return_value);
    }


    public static void AddComment(this CodeCommentStatementCollection comments, string text)
    {
        comments.Add(new CodeCommentStatement(text));
    }

    public static void AddComment(this CodeNamespace ns, string text)
    {
        ns.Comments.AddComment(text);
    }

    public static void AddConstructorMethod(this CodeTypeDeclaration type, string name,
        RpcTypeDescriptor complex_type, IEnumerable<Tuple<CodeTypeReference, string, bool>> parameters)
    {
        if (!parameters.Any())
        {
            return;
        }

        CodeMemberMethod method = type.AddMethod(name, MemberAttributes.Public | MemberAttributes.Final);
        method.ReturnType = complex_type.CodeType;
        method.Statements.Add(new CodeVariableDeclarationStatement(complex_type.CodeType, "ret", new CodeObjectCreateExpression(complex_type.CodeType)));
        CodeExpression return_value = GetVariable("ret");
        method.AddAssignmentStatements(return_value, parameters.Where(t => !t.Item3));
        method.AddReturn(return_value);
    }

    public static void AddConstructorMethod(this CodeTypeDeclaration type, RpcTypeDescriptor complex_type, IEnumerable<Tuple<CodeTypeReference, string, bool>> parameters)
    {
        if (!parameters.Any())
        {
            return;
        }

        CodeMemberMethod method = type.AddConstructor(MemberAttributes.Public | MemberAttributes.Final);
        method.AddAssignmentStatements(new CodeThisReferenceExpression(), parameters);
    }

    public static void AddArrayConstructorMethod(this CodeTypeDeclaration type, string name, RpcTypeDescriptor complex_type)
    {
        CodeMemberMethod method = type.AddMethod(name, MemberAttributes.Public | MemberAttributes.Final);
        method.AddParam(new CodeTypeReference(typeof(int)), "size");
        method.ReturnType = complex_type.GetArrayType();
        method.AddReturn(new CodeArrayCreateExpression(complex_type.CodeType, GetVariable("size")));
    }

    public static CodeExpression GetVariable(string var_name, bool null_check)
    {
        CodeExpression ret;
        if (var_name == null)
        {
            ret = new CodeThisReferenceExpression();
        }
        else
        {
            ret = new CodeVariableReferenceExpression(MakeIdentifier(var_name));
        }

        if (null_check)
        {
            return ret.AddNullCheck(var_name);
        }

        return ret;
    }

    public static CodeExpression GetVariable(string var_name)
    {
        return GetVariable(var_name, false);
    }

    public static void AddMarshalCall(this CodeMemberMethod method, RpcTypeDescriptor descriptor, string marshal_name, string var_name, bool add_write_referent,
        bool null_check, CodeExpression case_selector, string union_selector, string done_label, params RpcMarshalArgument[] additional_args)
    {
        List<CodeExpression> args = new()
        {
            GetVariable(var_name, null_check)
        };

        CodeMethodReferenceExpression marshal_method = descriptor.GetMarshalMethod(GetVariable(marshal_name));
        if (add_write_referent)
        {
            List<CodeTypeReference> marshal_args = new();
            marshal_args.Add(descriptor.CodeType);
            marshal_args.AddRange(additional_args.Select(a => a.CodeType));
            var create_delegate = new CodeDelegateCreateExpression(CreateActionType(marshal_args.ToArray()),
                GetVariable(marshal_name), descriptor.MarshalMethod);
            args.Add(create_delegate);
            marshal_method = new CodeMethodReferenceExpression(GetVariable(marshal_name), nameof(NdrMarshalBuffer.WriteReferent));
        }

        args.AddRange(additional_args.Select(r => r.Expression));
        CodeMethodInvokeExpression invoke = new(marshal_method, args.ToArray());

        if (case_selector != null)
        {
            method.Statements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(GetVariable(union_selector),
                CodeBinaryOperatorType.ValueEquality, case_selector),
                new CodeExpressionStatement(invoke), new CodeGotoStatement(done_label)));
        }
        else
        {
            method.Statements.Add(invoke);
        }
    }

    public static CodeTypeReference CreateActionType(params CodeTypeReference[] args)
    {
        CodeTypeReference delegate_type = args.Length switch
        {
            0 => new CodeTypeReference(typeof(Action)),
            1 => new CodeTypeReference(typeof(Action<>)),
            2 => new CodeTypeReference(typeof(Action<,>)),
            3 => new CodeTypeReference(typeof(Action<,,>)),
            _ => throw new ArgumentException("Too many delegate arguments"),
        };
        delegate_type.TypeArguments.AddRange(args);
        return delegate_type;
    }

    public static CodeTypeReference CreateFuncType(CodeTypeReference ret, params CodeTypeReference[] args)
    {
        CodeTypeReference delegate_type = args.Length switch
        {
            0 => new CodeTypeReference(typeof(Func<>)),
            1 => new CodeTypeReference(typeof(Func<,>)),
            2 => new CodeTypeReference(typeof(Func<,,>)),
            3 => new CodeTypeReference(typeof(Func<,,,>)),
            _ => throw new ArgumentException("Too many delegate arguments"),
        };
        delegate_type.TypeArguments.AddRange(args);
        delegate_type.TypeArguments.Add(ret);
        return delegate_type;
    }

    public static CodeExpression CreateDelegate(CodeTypeReference delegate_type, CodeExpression target, string name)
    {
        return new CodeDelegateCreateExpression(delegate_type, target, name);
    }

    public static void AddDeferredMarshalCall(this CodeMemberMethod method, RpcTypeDescriptor descriptor, string marshal_name, string var_name,
        CodeExpression case_selector, string union_selector, string done_label, params RpcMarshalArgument[] additional_args)
    {
        List<CodeExpression> args = new()
        {
            GetVariable(var_name)
        };

        List<CodeTypeReference> marshal_args = new();
        marshal_args.Add(descriptor.CodeType);
        marshal_args.AddRange(additional_args.Select(a => a.CodeType));

        string method_name;
        method_name = nameof(NdrMarshalBuffer.WriteEmbeddedPointer);
        var create_delegate = new CodeDelegateCreateExpression(CreateActionType(marshal_args.ToArray()),
            GetVariable(marshal_name), descriptor.MarshalMethod);

        args.Add(create_delegate);
        args.AddRange(additional_args.Select(r => r.Expression));
        CodeMethodReferenceExpression write_pointer = new(GetVariable(marshal_name), method_name, marshal_args.ToArray());
        CodeMethodInvokeExpression invoke = new(write_pointer, args.ToArray());
        if (case_selector != null)
        {
            method.Statements.Add(new CodeConditionStatement(new CodeBinaryOperatorExpression(GetVariable(union_selector),
                CodeBinaryOperatorType.ValueEquality, case_selector),
                new CodeExpressionStatement(invoke), new CodeGotoStatement(done_label)));
        }
        else
        {
            method.Statements.Add(invoke);
        }
    }

    public static void AddUnmarshalCall(this CodeStatementCollection statements, RpcTypeDescriptor descriptor, string unmarshal_name,
        string var_name, CodeExpression case_selector, string union_selector, string done_label, params CodeExpression[] additional_args)
    {
        List<CodeExpression> args = new(additional_args);

        if (var_name != null)
        {
            CodeStatement assign = new CodeAssignStatement(GetVariable(var_name), descriptor.GetUnmarshalMethodInvoke(unmarshal_name, args));
            if (case_selector != null)
            {
                assign = new CodeConditionStatement(new CodeBinaryOperatorExpression(GetVariable(union_selector),
                    CodeBinaryOperatorType.ValueEquality, case_selector),
                    assign, new CodeGotoStatement(done_label));
            }
            statements.Add(assign);
        }
        else
        {
            statements.AddReturn(descriptor.GetUnmarshalMethodInvoke(unmarshal_name, args));
        }
    }

    public static void AddUnmarshalCall(this CodeMemberMethod method, RpcTypeDescriptor descriptor, string unmarshal_name,
        string var_name, CodeExpression case_selector, string union_selector, string done_label, params CodeExpression[] additional_args)
    {
        AddUnmarshalCall(method.Statements, descriptor, unmarshal_name, var_name, case_selector, union_selector, done_label, additional_args);
    }

    public static CodePrimitiveExpression GetPrimitive(object obj)
    {
        return new CodePrimitiveExpression(obj);
    }

    public static void AddDeferredEmbeddedUnmarshalCall(this CodeMemberMethod method, RpcTypeDescriptor descriptor, string unmarshal_name, string var_name,
        CodeExpression case_selector, string union_selector, string done_label, params RpcMarshalArgument[] additional_args)
    {
        List<CodeExpression> args = new();
        List<CodeTypeReference> marshal_args = new();
        marshal_args.Add(descriptor.CodeType);
        marshal_args.AddRange(additional_args.Select(a => a.CodeType));

        var create_delegate = new CodeDelegateCreateExpression(CreateFuncType(descriptor.CodeType, marshal_args.Skip(1).ToArray()),
            descriptor.GetUnmarshalTarget(unmarshal_name), descriptor.UnmarshalMethod);
        args.Add(create_delegate);
        args.Add(GetPrimitive(descriptor.Pointer && descriptor.PointerType == RpcPointerType.Full));
        args.AddRange(additional_args.Select(r => r.Expression));
        CodeMethodReferenceExpression read_pointer = new(GetVariable(unmarshal_name),
            nameof(NdrUnmarshalBuffer.ReadEmbeddedPointer), marshal_args.ToArray());
        CodeMethodInvokeExpression invoke = new(read_pointer, args.ToArray());
        CodeStatement assign = new CodeAssignStatement(GetVariable(var_name), invoke);

        if (case_selector != null)
        {
            assign = new CodeConditionStatement(new CodeBinaryOperatorExpression(GetVariable(union_selector),
                CodeBinaryOperatorType.ValueEquality, case_selector),
                assign, new CodeGotoStatement(done_label));
        }

        method.Statements.Add(assign);
    }

    public static void AddPointerUnmarshalCall(this CodeStatementCollection statements, RpcTypeDescriptor descriptor, string unmarshal_name, string var_name)
    {
        List<CodeExpression> args = new();
        List<CodeTypeReference> marshal_args = new();
        marshal_args.Add(descriptor.CodeType);

        var create_delegate = new CodeDelegateCreateExpression(CreateFuncType(descriptor.CodeType, marshal_args.Skip(1).ToArray()),
            descriptor.GetUnmarshalTarget(unmarshal_name), descriptor.UnmarshalMethod);
        args.Add(create_delegate);
        args.Add(GetPrimitive(descriptor.Pointer && descriptor.PointerType == RpcPointerType.Full));
        CodeMethodReferenceExpression read_pointer = new(GetVariable(unmarshal_name),
            descriptor.ValueType ? nameof(NdrUnmarshalBuffer.ReadReferentValue) : nameof(NdrUnmarshalBuffer.ReadReferent), marshal_args.ToArray());
        CodeMethodInvokeExpression invoke = new(read_pointer, args.ToArray());
        if (var_name != null)
        {
            CodeStatement assign = new CodeAssignStatement(GetVariable(var_name), invoke);
            statements.Add(assign);
        }
        else
        {
            statements.AddReturn(invoke);
        }
    }

    public static void AddPointerUnmarshalCall(this CodeMemberMethod method, RpcTypeDescriptor descriptor, string unmarshal_name, string var_name)
    {
        AddPointerUnmarshalCall(method.Statements, descriptor, unmarshal_name, var_name);
    }

    public static void AddUnmarshalReturn(this CodeStatementCollection statements, RpcTypeDescriptor descriptor, string unmarshal_name, params RpcMarshalArgument[] additional_args)
    {
        if (descriptor.BuiltinType == typeof(void))
        {
            return;
        }
        List<CodeExpression> args = new();
        args.AddRange(additional_args.Select(r => r.Expression));
        CodeMethodReturnStatement ret = new(descriptor.GetUnmarshalMethodInvoke(unmarshal_name, args));
        statements.Add(ret);
    }

    public static CodeExpression AddNullCheck(this CodeExpression var_expr, string var_name)
    {
        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.CheckNull), var_expr, GetPrimitive(var_name));
    }

    public static FieldDirection GetDirection(this NdrProcedureParameter p)
    {
        bool is_in = p.Attributes.HasFlag(NdrParamAttributes.IsIn);
        bool is_out = p.Attributes.HasFlag(NdrParamAttributes.IsOut);

        if (is_in && is_out)
        {
            return FieldDirection.Ref;
        }
        else if (is_out)
        {
            return FieldDirection.Out;
        }
        return FieldDirection.In;
    }

    public static void CreateMarshalObject(this CodeMemberMethod method, string name, MarshalHelperBuilder marshal_helper, bool client)
    {
        CodeObjectCreateExpression create_expr;
        if (client)
        {
            create_expr = new CodeObjectCreateExpression(marshal_helper.MarshalHelperType, new CodeMethodInvokeExpression(null, "CreateMarshalBuffer"));
        }
        else
        {
            create_expr = new CodeObjectCreateExpression(marshal_helper.MarshalHelperType);
        }
        method.Statements.Add(new CodeVariableDeclarationStatement(marshal_helper.MarshalHelperType, 
            name, create_expr));
    }

    public static void CreateSendReceive(this CodeTypeDeclaration type, MarshalHelperBuilder marshal_helper)
    {
        var method = type.AddMethod("SendReceive", MemberAttributes.Private | MemberAttributes.Final);
        method.AddParam(typeof(int), "p");
        method.AddParam(marshal_helper.MarshalHelperType, MARSHAL_PARAM_NAME);
        method.ReturnType = marshal_helper.UnmarshalHelperType;

        CodeExpression call_sendrecv = new CodeMethodInvokeExpression(null, "SendReceiveTransport",
            GetVariable("p"),
            GetVariable(MARSHAL_PARAM_NAME));
        call_sendrecv = new CodeObjectCreateExpression(marshal_helper.UnmarshalHelperType, call_sendrecv);
        method.AddReturn(call_sendrecv);
    }

    public static CodeStatementCollection SendReceive(this CodeMemberMethod method, string marshal_name, string unmarshal_name, int proc_num, MarshalHelperBuilder marshal_helper)
    {
        CodeExpression call_sendrecv = new CodeMethodInvokeExpression(null, "SendReceive",
            GetPrimitive(proc_num),
            GetVariable(marshal_name));
        CodeVariableDeclarationStatement unmarshal = new(marshal_helper.UnmarshalHelperType, unmarshal_name, call_sendrecv);
        method.Statements.Add(unmarshal);
        CodeTryCatchFinallyStatement try_catch = new();
        try_catch.FinallyStatements.Add(new CodeMethodInvokeExpression(new CodeVariableReferenceExpression(unmarshal_name),
            nameof(IDisposable.Dispose)));
        method.Statements.Add(try_catch);
        return try_catch.TryStatements;
    }

    public static void AddStartRegion(this CodeTypeDeclaration type, string text)
    {
        type.StartDirectives.Add(new CodeRegionDirective(CodeRegionMode.Start, text));
    }

    public static void AddEndRegion(this CodeTypeDeclaration type)
    {
        type.EndDirectives.Add(new CodeRegionDirective(CodeRegionMode.End, string.Empty));
    }

    public static string MakeIdentifier(string id)
    {
        id = _identifier_regex.Replace(id, "_");
        if (!char.IsLetter(id[0]) && id[0] != '_')
        {
            id = "_" + id;
        }

        return id;
    }

    public static Type GetSystemHandleType(this NdrSystemHandleTypeReference type)
    {
        return type.Resource switch
        {
            NdrSystemHandleResource.File or NdrSystemHandleResource.Pipe or NdrSystemHandleResource.Socket => typeof(NtFile),
            NdrSystemHandleResource.Semaphore => typeof(NtSemaphore),
            NdrSystemHandleResource.RegKey => typeof(NtKey),
            NdrSystemHandleResource.Event => typeof(NtEvent),
            NdrSystemHandleResource.Job => typeof(NtJob),
            NdrSystemHandleResource.Mutex => typeof(NtMutant),
            NdrSystemHandleResource.Process => typeof(NtProcess),
            NdrSystemHandleResource.Section => typeof(NtSection),
            NdrSystemHandleResource.Thread => typeof(NtThread),
            NdrSystemHandleResource.Token => typeof(NtToken),
            _ => typeof(NtObject),
        };
    }

    public static bool ValidateCorrelation(this NdrCorrelationDescriptor correlation)
    {
        if (!correlation.IsConstant && !correlation.IsNormal
            && !correlation.IsTopLevel && !correlation.IsPointer)
        {
            return false;
        }

        switch (correlation.Operator)
        {
            case NdrFormatCharacter.FC_ADD_1:
            case NdrFormatCharacter.FC_DIV_2:
            case NdrFormatCharacter.FC_MULT_2:
            case NdrFormatCharacter.FC_SUB_1:
            case NdrFormatCharacter.FC_ZERO:
            case NdrFormatCharacter.FC_DEREFERENCE:
                break;
            case NdrFormatCharacter.FC_EXPR:
                return correlation.Expression.IsValid;
            default:
                return false;
        }

        return true;
    }

    public static RpcTypeDescriptor GetSimpleTypeDescriptor(this NdrSimpleTypeReference simple_type, MarshalHelperBuilder marshal_helper, bool unsigned_char)
    {
        NdrFormatCharacter format = simple_type.Format;
        if (unsigned_char && format == NdrFormatCharacter.FC_CHAR)
        {
            format = NdrFormatCharacter.FC_BYTE;
        }

        return format switch
        {
            NdrFormatCharacter.FC_BYTE or NdrFormatCharacter.FC_USMALL => new RpcTypeDescriptor(typeof(byte), nameof(NdrUnmarshalBuffer.ReadByte), nameof(NdrMarshalBuffer.WriteByte), simple_type),
            NdrFormatCharacter.FC_SMALL or NdrFormatCharacter.FC_CHAR => new RpcTypeDescriptor(typeof(sbyte), nameof(NdrUnmarshalBuffer.ReadSByte), nameof(NdrMarshalBuffer.WriteSByte), simple_type),
            NdrFormatCharacter.FC_WCHAR => new RpcTypeDescriptor(typeof(char), nameof(NdrUnmarshalBuffer.ReadChar), nameof(NdrMarshalBuffer.WriteChar), simple_type),
            NdrFormatCharacter.FC_SHORT => new RpcTypeDescriptor(typeof(short), nameof(NdrUnmarshalBuffer.ReadInt16), nameof(NdrMarshalBuffer.WriteInt16), simple_type),
            NdrFormatCharacter.FC_ENUM16 => new RpcTypeDescriptor(typeof(NdrEnum16), nameof(NdrUnmarshalBuffer.ReadEnum16), nameof(NdrMarshalBuffer.WriteEnum16), simple_type),
            NdrFormatCharacter.FC_USHORT => new RpcTypeDescriptor(typeof(ushort), nameof(NdrUnmarshalBuffer.ReadUInt16), nameof(NdrMarshalBuffer.WriteUInt16), simple_type),
            NdrFormatCharacter.FC_LONG or NdrFormatCharacter.FC_ENUM32 => new RpcTypeDescriptor(typeof(int), nameof(NdrUnmarshalBuffer.ReadInt32), nameof(NdrMarshalBuffer.WriteInt32), simple_type),
            NdrFormatCharacter.FC_ULONG or NdrFormatCharacter.FC_ERROR_STATUS_T => new RpcTypeDescriptor(typeof(uint), nameof(NdrUnmarshalBuffer.ReadUInt32), nameof(NdrMarshalBuffer.WriteUInt32), simple_type),
            NdrFormatCharacter.FC_FLOAT => new RpcTypeDescriptor(typeof(float), nameof(NdrUnmarshalBuffer.ReadFloat), nameof(NdrMarshalBuffer.WriteFloat), simple_type),
            NdrFormatCharacter.FC_HYPER => new RpcTypeDescriptor(typeof(long), nameof(NdrUnmarshalBuffer.ReadInt64), nameof(NdrMarshalBuffer.WriteInt64), simple_type),
            NdrFormatCharacter.FC_DOUBLE => new RpcTypeDescriptor(typeof(double), nameof(NdrUnmarshalBuffer.ReadDouble), nameof(NdrMarshalBuffer.WriteDouble), simple_type),
            NdrFormatCharacter.FC_INT3264 => new RpcTypeDescriptor(typeof(NdrInt3264), nameof(NdrUnmarshalBuffer.ReadInt3264), nameof(NdrMarshalBuffer.WriteInt3264), simple_type),
            NdrFormatCharacter.FC_UINT3264 => new RpcTypeDescriptor(typeof(NdrUInt3264), nameof(NdrUnmarshalBuffer.ReadUInt3264), nameof(NdrMarshalBuffer.WriteUInt3264), simple_type),
            NdrFormatCharacter.FC_C_WSTRING => new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingString), nameof(NdrMarshalBuffer.WriteTerminatedString), simple_type),
            NdrFormatCharacter.FC_C_CSTRING => new RpcTypeDescriptor(typeof(string), nameof(NdrUnmarshalBuffer.ReadConformantVaryingAnsiString), nameof(NdrMarshalBuffer.WriteTerminatedAnsiString), simple_type),
            NdrFormatCharacter.FC_ZERO => new RpcTypeDescriptor(typeof(NdrEmpty), nameof(NdrUnmarshalBuffer.ReadEmpty), nameof(NdrMarshalBuffer.WriteEmpty), simple_type),
            _ => null,
        };
    }

    public static RpcMarshalArgument CalculateCorrelationArgument(this NdrCorrelationDescriptor correlation,
        int current_offset, IEnumerable<Tuple<int, string>> offset_to_name, bool disable_correlation)
    {
        if (correlation.IsConstant)
        {
            return RpcMarshalArgument.CreateFromPrimitive((long)correlation.Offset);
        }

        if (correlation.IsTopLevel || correlation.IsPointer)
        {
            current_offset = 0;
        }

        if (correlation.Expression.IsValid)
        {
            return new RpcMarshalArgument(BuildCorrelationExpression(correlation.Expression,
                current_offset, offset_to_name, disable_correlation), typeof(long).ToRef());
        }

        if (disable_correlation)
        {
            return RpcMarshalArgument.CreateFromPrimitive(-1L);
        }

        var offset = FindCorrelationArgument(current_offset + correlation.Offset, offset_to_name);
        if (offset != null)
        {
            CodeExpression expr = GetVariable(offset);
            CodeExpression right_expr = null;
            CodeBinaryOperatorType operator_type = CodeBinaryOperatorType.Add;
            switch (correlation.Operator)
            {
                case NdrFormatCharacter.FC_ADD_1:
                    right_expr = GetPrimitive(1);
                    operator_type = CodeBinaryOperatorType.Add;
                    break;
                case NdrFormatCharacter.FC_DIV_2:
                    right_expr = GetPrimitive(2);
                    operator_type = CodeBinaryOperatorType.Divide;
                    break;
                case NdrFormatCharacter.FC_MULT_2:
                    right_expr = GetPrimitive(2);
                    operator_type = CodeBinaryOperatorType.Multiply;
                    break;
                case NdrFormatCharacter.FC_SUB_1:
                    right_expr = GetPrimitive(2);
                    operator_type = CodeBinaryOperatorType.Multiply;
                    break;
                case NdrFormatCharacter.FC_DEREFERENCE:
                    expr = expr.DeRef();
                    break;
            }

            if (right_expr != null)
            {
                expr = new CodeBinaryOperatorExpression(expr, operator_type, right_expr);
            }
            return new RpcMarshalArgument(expr, new CodeTypeReference(typeof(long)));
        }

        // We failed to find the base name, return -1 as a default.
        return RpcMarshalArgument.CreateFromPrimitive(-1L);
    }

    public static CodeTypeReference ToRef(this Type type)
    {
        return new CodeTypeReference(type);
    }

    public static CodeTypeReference ToRef(this Type type, params CodeTypeReference[] generic_types)
    {
        var ret = new CodeTypeReference(type);
        ret.TypeArguments.AddRange(generic_types);
        return ret;
    }

    public static CodeTypeReference ToRefArray(this CodeTypeReference type)
    {
        return new CodeTypeReference(type, type.ArrayRank + 1);
    }

    public static CodeTypeReference ToBaseRef(this CodeTypeReference type)
    {
        return type.ArrayElementType ?? type;
    }

    public static bool IsNonEncapsulatedUnion(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrUnionTypeReference union_type)
        {
            return union_type.NonEncapsulated;
        }
        return false;
    }

    public static string GetSelectorName(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrUnionTypeReference union_type)
        {
            return union_type.SelectorName;
        }
        return string.Empty;
    }

    public static bool IsUnion(this NdrComplexTypeReference complex_type)
    {
        return complex_type is NdrUnionTypeReference;
    }

    public static bool IsStruct(this NdrComplexTypeReference complex_type)
    {
        return complex_type is NdrBaseStructureTypeReference;
    }

    public static bool IsConformantStruct(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrBaseStructureTypeReference struct_type)
        {
            return struct_type.Conformant;
        }
        return false;
    }

    public static int GetConformantDimensions(this NdrComplexTypeReference complex_type)
    {
        if (complex_type.IsConformantStruct())
        {
            return 1;
        }
        return 0;
    }

    public static int GetAlignment(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrBaseStructureTypeReference struct_type)
        {
            return struct_type.Alignment + 1;
        }
        else if (complex_type is NdrUnionTypeReference union_type)
        {
            return union_type.Arms.Alignment + 1;
        }
        return 0;
    }

    public static List<ComplexTypeMember> GetMembers(this NdrComplexTypeReference complex_type, string selector_name)
    {
        List<ComplexTypeMember> members = new();
        if (complex_type is NdrBaseStructureTypeReference struct_type)
        {
            members.AddRange(struct_type.Members.Select(m => new ComplexTypeMember(m.MemberType, m.Offset, m.Name, null, false, false)).ToList());
        }
        else if (complex_type is NdrUnionTypeReference union_type)
        {
            var selector_type = new NdrSimpleTypeReference(union_type.SwitchType);
            int base_offset = selector_type.GetSize();
            members.Add(new ComplexTypeMember(selector_type, 0, selector_name, null, false, union_type.NonEncapsulated));
            if (!union_type.NonEncapsulated)
            {
                base_offset = union_type.SwitchIncrement;
            }

            members.AddRange(union_type.Arms.Arms.Select(a => new ComplexTypeMember(a.ArmType, base_offset, a.Name, a.GetArmCase(selector_type), false, false)));
            if (union_type.Arms.DefaultArm != null)
            {
                members.Add(new ComplexTypeMember(union_type.Arms.DefaultArm, base_offset, "Arm_Default", null, true, false));
            }
        }
        return members;
    }

    public static NdrSimpleTypeReference GetSelectorType(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrUnionTypeReference union_type)
        {
            return new NdrSimpleTypeReference(union_type.SwitchType);
        }
        return null;
    }

    public static NdrCorrelationDescriptor GetUnionCorrelation(this NdrComplexTypeReference complex_type)
    {
        if (complex_type is NdrUnionTypeReference union_type && union_type.NonEncapsulated && union_type.Correlation.ValidateCorrelation())
        {
            return union_type.Correlation;
        }
        return null;
    }

    public static void AddThrow(this CodeMemberMethod method, Type exception_type, params object[] args)
    {
        method.Statements.Add(new CodeThrowExceptionStatement(new CodeObjectCreateExpression(exception_type.ToRef(), args.Select(o => GetPrimitive(o)).ToArray())));
    }

    public static CodeMethodInvokeExpression GetStaticMethod(Type type, string name, params CodeExpression[] ps)
    {
        return new CodeMethodInvokeExpression(new CodeTypeReferenceExpression(type), name, ps);
    }

    public static CodeExpression DeRef(this CodeExpression expr)
    {
        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.DeRef), expr);
    }

    public static void AddBreakpoint(this CodeMemberMethod method)
    {
        method.Statements.Add(GetStaticMethod(typeof(System.Diagnostics.Debugger), nameof(System.Diagnostics.Debugger.Break)));
    }

    public static CodeExpression Cast(this CodeExpression expr, CodeTypeReference type)
    {
        return new CodeCastExpression(type, expr);
    }

    public static CodeExpression OpTernary(CodeExpression condition_expr, CodeExpression true_expr, CodeExpression false_expr)
    {
        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.OpTernary), condition_expr, true_expr, false_expr);
    }

    public static CodeExpression ToBool(this CodeExpression expr)
    {
        return GetStaticMethod(typeof(NdrMarshalUtils), nameof(NdrMarshalUtils.ToBool), expr);
    }
}
