﻿//  Copyright 2018 Google Inc. All Rights Reserved.
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

// NOTE: This file is a modified version of SymbolResolver.cs from OleViewDotNet
// https://github.com/tyranid/oleviewdotnet. It's been relicensed from GPLv3 by
// the original author James Forshaw to be used under the Apache License for this
// project.

using NtCoreLib.Image;
using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Native.SafeHandles;
using NtCoreLib.Utilities.Memory;
using NtCoreLib.Win32.Debugger.Interop;
using NtCoreLib.Win32.Loader;
using NtCoreLib.Win32.SafeHandles;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Text;

namespace NtCoreLib.Win32.Debugger.Symbols;

internal sealed partial class DbgHelpSymbolResolver : ISymbolResolver, ISymbolTypeResolver, IDisposable
{
    public static string DEFAULT_SYMSRV = "https://msdl.microsoft.com/download/symbols";

    #region Private Members
    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymInitializeW(
        SafeKernelObjectHandle hProcess,
        [MarshalAs(UnmanagedType.LPWStr)] string UserSearchPath,
        [MarshalAs(UnmanagedType.Bool)] bool fInvadeProcess
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymCleanup(
        SafeKernelObjectHandle hProcess
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymFromNameW(
          SafeKernelObjectHandle hProcess,
          string Name,
          SafeBuffer Symbol
        );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool EnumModules(
        string ModuleName,
        long BaseOfDll,
        IntPtr UserContext);

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymEnumerateModulesW64(
          SafeKernelObjectHandle hProcess,
          EnumModules EnumModulesCallback,
          IntPtr UserContext
        );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymFromAddrW(
        SafeKernelObjectHandle hProcess,
        long Address,
        out long Displacement,
        SafeBuffer Symbol
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetModuleInfoW64(
          SafeKernelObjectHandle hProcess,
          long dwAddr,
          ref IMAGEHLP_MODULE64 ModuleInfo
        );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Ansi, SetLastError = true)]
    delegate long SymLoadModule64(
          SafeKernelObjectHandle hProcess,
          IntPtr hFile,
          string ImageName,
          string ModuleName,
          long BaseOfDll,
          int SizeOfDll
        );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymRefreshModuleList(
        SafeKernelObjectHandle hProcess
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool PsymEnumeratesymbolsCallback(
      IntPtr pSymInfo,
      int SymbolSize,
      IntPtr UserContext
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymEnumTypesW(
      SafeKernelObjectHandle hProcess,
      long BaseOfDll,
      PsymEnumeratesymbolsCallback EnumSymbolsCallback,
      IntPtr UserContext
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymEnumSymbolsW(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        string Mask,
        PsymEnumeratesymbolsCallback EnumSymbolsCallback,
        IntPtr UserContext
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate int SymSetOptions(
        SymOptions SymOptions
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeFromNameW(
      SafeKernelObjectHandle hProcess,
      long BaseOfDll,
      string Name,
      SafeBuffer Symbol
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymEnumTypesByNameW(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        string mask,
        PsymEnumeratesymbolsCallback EnumSymbolsCallback,
        IntPtr UserContext
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeInfo(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int TypeId,
        IMAGEHLP_SYMBOL_TYPE_INFO GetType,
        SafeBuffer pInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeInfoDword(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int TypeId,
        IMAGEHLP_SYMBOL_TYPE_INFO GetType,
        out int pInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeInfoLong(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int TypeId,
        IMAGEHLP_SYMBOL_TYPE_INFO GetType,
        out long pInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeInfoPtr(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int TypeId,
        IMAGEHLP_SYMBOL_TYPE_INFO GetType,
        out SafeLocalAllocBuffer pInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymGetTypeInfoVar(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int TypeId,
        IMAGEHLP_SYMBOL_TYPE_INFO GetType,
        [MarshalAs(UnmanagedType.Struct)] out object pInfo
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymFromIndexW(
        SafeKernelObjectHandle hProcess,
        long BaseOfDll,
        int Index,
        SafeBuffer Symbol
    );

    [UnmanagedFunctionPointer(CallingConvention.StdCall, CharSet = CharSet.Unicode, SetLastError = true)]
    delegate bool SymSetContext(
        SafeKernelObjectHandle hProcess,
        ref IMAGEHLP_STACK_FRAME StackFrame,
        IntPtr Context
    );

    private readonly SafeLoadLibraryHandle _dbghelp_lib;
    private readonly SymInitializeW _sym_init;
    private readonly SymCleanup _sym_cleanup;
    private readonly SymFromNameW _sym_from_name;
    private readonly SymSetOptions _sym_set_options;
    private readonly SymEnumerateModulesW64 _sym_enum_modules;
    private readonly SymFromAddrW _sym_from_addr;
    private readonly SymGetModuleInfoW64 _sym_get_module_info;
    private readonly SymLoadModule64 _sym_load_module;
    private readonly SymRefreshModuleList _sym_refresh_module_list;
    private readonly SymEnumTypesW _sym_enum_types;
    private readonly SymGetTypeFromNameW _sym_get_type_from_name;
    private readonly SymEnumTypesByNameW _sym_enum_types_by_name;
    private readonly SymGetTypeInfo _sym_get_type_info;
    private readonly SymGetTypeInfoDword _sym_get_type_info_dword;
    private readonly SymGetTypeInfoPtr _sym_get_type_info_ptr;
    private readonly SymGetTypeInfoVar _sym_get_type_info_var;
    private readonly SymGetTypeInfoLong _sym_get_type_info_long;
    private readonly SymFromIndexW _sym_from_index;
    private readonly SymSetContext _sym_set_context;
    private readonly SymEnumSymbolsW _sym_enum_symbols;
    private IEnumerable<SymbolLoadedModule> _loaded_modules;
    private readonly TextWriter _trace_writer;
    private readonly bool _trace_symbol_loading;
    private readonly bool _enable_symsrv_fallback;
    private readonly string _symbol_cache_path;
    private readonly Uri _symbol_server_path;
    private readonly DbgHelpDebugCallbackHandler _callback_handler;

    private void GetFunc<T>(ref T f) where T : Delegate
    {
        f = _dbghelp_lib.GetFunctionPointer<T>();
    }

    private void GetFunc<T>(ref T f, string name) where T : Delegate
    {
        f = _dbghelp_lib.GetFunctionPointer<T>(name);
    }

    private static SafeStructureInOutBuffer<SYMBOL_INFO> AllocateSymInfo()
    {
        return new SafeStructureInOutBuffer<SYMBOL_INFO>(new SYMBOL_INFO(SYMBOL_INFO.MAX_SYM_NAME), SYMBOL_INFO.MAX_SYM_NAME * 2, true);
    }

    private static string GetNameFromSymbolInfo(SafeStructureInOutBuffer<SYMBOL_INFO> buffer)
    {
        return buffer.Data.ReadNulTerminatedUnicodeString();
    }

    private static SafeStructureInOutBuffer<SYMBOL_INFO> MapSymbolInfo(IntPtr symbol_info)
    {
        int base_size = Marshal.SizeOf(typeof(SYMBOL_INFO));
        SafeStructureInOutBuffer<SYMBOL_INFO> ret = new(symbol_info, base_size, false);
        var result = ret.Result;
        int total_size = result.MaxNameLen * 2 + result.SizeOfStruct - 2;
        return new SafeStructureInOutBuffer<SYMBOL_INFO>(symbol_info, total_size, false);
    }

    private SymTagEnum? GetSymbolTag(long module_base, int type_index)
    {
        var tag = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_SYMTAG, module_base, type_index);
        if (tag.HasValue)
            return (SymTagEnum)tag.Value;
        return null;
    }

    private bool CheckTypeTag(long module_base, int type_index, SymTagEnum tag)
    {
        var tag_check = GetSymbolTag(module_base, type_index);
        if (!tag_check.HasValue)
        {
            return false;
        }
        return tag_check.Value == tag;
    }

    private int? GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO ti, long module_base, int type_index)
    {
        if (!_sym_get_type_info_dword(Handle, module_base,
            type_index, ti, out int result))
        {
            return null;
        }
        return result;
    }

    private long? GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO ti, long module_base, int type_index)
    {
        if (!_sym_get_type_info_long(Handle, module_base,
            type_index, ti, out long result))
        {
            return null;
        }
        return result;
    }

    private int GetChildCount(long module_base, int type_index)
    {
        if (!_sym_get_type_info_dword(Handle, module_base,
            type_index, IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_CHILDRENCOUNT, out int count))
        {
            return 0;
        }
        return count;
    }

    private int[] GetChildIds(long module_base, int type_index)
    {
        int count = GetChildCount(module_base, type_index);
        if (count == 0)
            return new int[0];

        TI_FINDCHILDREN_PARAMS children = new() { Count = count };
        using var buffer = new SafeStructureInOutBuffer<TI_FINDCHILDREN_PARAMS>(children, count * 4, true);
        if (!_sym_get_type_info(Handle, module_base,
           type_index, IMAGEHLP_SYMBOL_TYPE_INFO.TI_FINDCHILDREN, buffer))
        {
            return new int[0];
        }
        int[] ret = new int[count];
        buffer.Data.ReadArray(0, ret, 0, count);
        return ret;
    }

    private string GetSymbolName(long module_base, int type_index)
    {
        if (!_sym_get_type_info_ptr(Handle, module_base, type_index, IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_SYMNAME, out SafeLocalAllocBuffer ptr))
        {
            return string.Empty;
        }
        using (ptr)
        {
            return Marshal.PtrToStringUni(ptr.DangerousGetHandle());
        }
    }

    private long GetSymbolLength(long module_base, int type_index)
    {
        return GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, type_index) ?? 0;
    }

    private EnumTypeInformation CreateEnumType(TypeInformationCache type_cache, long module_base, int type_index, SymbolLoadedModule module, string name)
    {
        int[] child_ids = GetChildIds(module_base, type_index);
        long length = GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, type_index) ?? 0;
        List<EnumTypeInformationValue> values = new();
        foreach (var id in child_ids)
        {
            if (CheckTypeTag(module_base, id, SymTagEnum.SymTagData))
            {
                long enum_value = 0;
                if (_sym_get_type_info_var(Handle, module_base, id,
                    IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_VALUE, out object value))
                {
                    enum_value = Convert.ToInt64(value);
                }

                values.Add(new EnumTypeInformationValue(GetSymbolName(module_base, id), enum_value));
            }
        }
        return new EnumTypeInformation(length, type_index,
            module, name, values.AsReadOnly());
    }

    private UserDefinedTypeInformation CreateUserDefinedType(TypeInformationCache type_cache, long module_base, int type_index, SymbolLoadedModule module, string name)
    {
        int[] child_ids = GetChildIds(module_base, type_index);
        List<UserDefinedTypeMember> members = new();
        long length = GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, type_index) ?? 0;
        foreach (var id in child_ids)
        {
            if (!CheckTypeTag(module_base, id, SymTagEnum.SymTagData))
            {
                continue;
            }
            string member_name = GetSymbolName(module_base, id);
            int offset = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_OFFSET, module_base, id) ?? 0;
            int? member_type = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPE, module_base, id);

            var bit_position = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_BITPOSITION, module_base, id);
            var bit_length = GetSymbolLength(module_base, id);

            TypeInformation member_type_value = new(SymTagEnum.SymTagNull, 0, 0, module, member_name);
            if (member_type.HasValue)
            {
                var tag = GetSymbolTag(module_base, member_type.Value);
                member_type_value = CreateType(type_cache, tag ?? SymTagEnum.SymTagNull, module_base, member_type.Value,
                    GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, member_type.Value) ?? 0
                    , module, GetSymbolName(module_base, member_type.Value));
            }

            if (bit_position.HasValue)
            {
                members.Add(new UserDefinedTypeBitFieldMember(member_type_value, member_name, offset, bit_position.Value, bit_length));
            }
            else
            {
                members.Add(new UserDefinedTypeMember(member_type_value, member_name, offset));
            }
        }

        UdtKind kind = (UdtKind)(GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_UDTKIND, module_base, type_index) ?? 0);
        return new UserDefinedTypeInformation(length, type_index, module, name, kind == UdtKind.UdtUnion, members.AsReadOnly());
    }

    private BaseTypeInformation CreateBaseType(TypeInformationCache type_cache, long module_base, int index, SymbolLoadedModule module)
    {
        var base_type = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_BASETYPE, module_base, index) ?? 0;
        var length = GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, index) ?? 0;
        return new BaseTypeInformation(length, index, module, (BasicType)base_type);
    }

    private PointerTypeInformation CreatePointerType(TypeInformationCache type_cache, long module_base, int index, SymbolLoadedModule module)
    {
        var length = GetSymbolLong(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_LENGTH, module_base, index) ?? 0;
        var type_id = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPEID, module_base, index);
        var is_reference = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_IS_REFERENCE, module_base, index) ?? 0;
        PointerTypeInformation pointer;
        if (type_id.HasValue)
        {
            pointer = new PointerTypeInformation(length, index, module,
                    null, is_reference != 0);
            type_cache.AddEntry(module_base, index, pointer);
            type_cache.AddFixedup(module_base, type_id.Value, pointer);
            pointer.PointerType = CreateType(type_cache, module_base, type_id.Value, module);
        }
        else
        {
            return new PointerTypeInformation(length, index, module,
                    new BaseTypeInformation(0, 0, module, BasicType.Void), is_reference != 0);
        }
        type_cache.AddEntry(module_base, index, pointer);
        return pointer;
    }

    private int? GetArrayElementType(long module_base, int index)
    {
        int? type_index = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPEID, module_base, index);
        if (!type_index.HasValue)
            return null;
        while (CheckTypeTag(module_base, type_index.Value, SymTagEnum.SymTagArrayType))
        {
            type_index = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPEID, module_base, type_index.Value);
            if (!type_index.HasValue)
                return null;
        }
        return type_index.Value;
    }

    private ArrayTypeInformation CreateArrayType(TypeInformationCache type_cache, long module_base, int index, SymbolLoadedModule module)
    {
        int? array_type_index = GetArrayElementType(module_base, index);
        if (!array_type_index.HasValue)
        {
            return new ArrayTypeInformation(index, module, new BaseTypeInformation(1, 0, module, BasicType.Char));
        }

        TypeInformation array_type = CreateType(type_cache, module_base, array_type_index.Value, module);
        return new ArrayTypeInformation(index, module, array_type);
    }

    private TypeInformation CreateType(TypeInformationCache type_cache,
        long module_base, int index, SymbolLoadedModule module)
    {
        var tag = GetSymbolTag(module_base, index) ?? SymTagEnum.SymTagNull;
        string name = GetSymbolName(module_base, index);
        long length = GetSymbolLength(module_base, index);
        return CreateType(type_cache, tag, module_base, index, length, module, name);
    }

    private bool GetSymbolInfo(List<SymbolInformation> symbols, IntPtr symbol_info, Dictionary<long, SymbolLoadedModule> modules)
    {
        using var sym_info = MapSymbolInfo(symbol_info);
        var result = sym_info.Result;
        var symbol = new DataSymbolInformation(result.Tag, result.Size, result.TypeIndex,
                result.Address, GetModuleForAddress(new IntPtr(result.ModBase)), 
                GetNameFromSymbolInfo(sym_info), (DllMachineType)result.Reserved2);
        symbols.Add(symbol);
        return true;
    }

    private bool GetLocalParameterName(List<string> names, IntPtr symbol_info, SymbolLoadedModule module)
    {
        using var sym_info = MapSymbolInfo(symbol_info);
        var result = sym_info.Result;
        if (!result.Flags.HasFlagSet(SYMBOL_INFO_FLAGS.SYMFLAG_PARAMETER))
            return true;
        string name = GetNameFromSymbolInfo(sym_info);
        if (!names.Exists(s => s == name))
        {
            names.Add(name);
        }
        return true;
    }

    private List<string> QueryLocalParameterNames(long address, SymbolLoadedModule module)
    {
        List<string> ret = new();
        if (address == 0)
            return ret;
        IMAGEHLP_STACK_FRAME frame = new()
        {
            InstructionOffset = address
        };
        if (!_sym_set_context(Handle, ref frame, IntPtr.Zero))
        {
            if (Win32Utils.GetLastWin32Error() != Win32Error.SUCCESS)
                return ret;
        }
        _sym_enum_symbols(Handle, 0, null, (s, z, x) => GetLocalParameterName(ret, s, module), IntPtr.Zero);
        return ret;
    }

    private FunctionTypeInformation CreateFunctionType(TypeInformationCache type_cache, long module_base,
        int type_index, SymbolLoadedModule module, string name, long address)
    {
        List<FunctionParameter> parameters = new();
        TypeInformation return_type = null;
        int[] child_ids = GetChildIds(module_base, type_index);
        List<string> arg_names = QueryLocalParameterNames(address, module);
        foreach (int id in child_ids)
        {
            if (GetSymbolTag(module_base, id) == SymTagEnum.SymTagFunctionArgType)
            {
                var type_id = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPEID, module_base, id);
                if (type_id.HasValue)
                {
                    TypeInformation arg = CreateType(type_cache, module_base, type_id.Value, module);
                    string arg_name = $"p{parameters.Count}";
                    if (arg_names.Count > parameters.Count)
                        arg_name = arg_names[parameters.Count];
                    parameters.Add(new FunctionParameter(arg_name, arg));
                }
            }
        }

        int? ret_id = GetSymbolDword(IMAGEHLP_SYMBOL_TYPE_INFO.TI_GET_TYPE, module_base, type_index);
        if (ret_id.HasValue)
        {
            return_type = CreateType(type_cache, module_base, ret_id.Value, module);
        }

        return new FunctionTypeInformation(type_index, module, name, return_type, parameters);
    }

    private TypeInformation CreateType(TypeInformationCache type_cache, SymTagEnum tag,
        long module_base, int index, long size, SymbolLoadedModule module, string name,
        long address = 0)
    {
        if (type_cache.HasEntry(module_base, index))
        {
            return type_cache.GetEntry(module_base, index);
        }

        TypeInformation ret;
        switch (tag)
        {
            case SymTagEnum.SymTagUDT:
                ret = CreateUserDefinedType(type_cache, module_base, index, module, name);
                break;
            case SymTagEnum.SymTagEnum:
                ret = CreateEnumType(type_cache, module_base, index, module, name);
                break;
            case SymTagEnum.SymTagBaseType:
                ret = CreateBaseType(type_cache, module_base, index, module);
                break;
            case SymTagEnum.SymTagPointerType:
                ret = CreatePointerType(type_cache, module_base, index, module);
                break;
            case SymTagEnum.SymTagArrayType:
                ret = CreateArrayType(type_cache, module_base, index, module);
                break;
            case SymTagEnum.SymTagFunction:
                ret = CreateFunctionType(type_cache, module_base, index, module, name, address);
                break;
            default:
                Debug.WriteLine(tag.ToString());
                ret = new TypeInformation(tag, size, index, module, name);
                break;
        }

        type_cache.AddEntry(module_base, index, ret);
        return ret;
    }

    private bool EnumTypes(TypeInformationCache type_cache, Dictionary<long, SymbolLoadedModule> modules,
        List<TypeInformation> symbols, IntPtr symbol_info)
    {
        using var sym_info = MapSymbolInfo(symbol_info);
        SymbolLoadedModule loaded_module;
        var result = sym_info.Result;
        if (modules.ContainsKey(result.ModBase))
        {
            loaded_module = modules[result.ModBase];
        }
        else
        {
            loaded_module = new SymbolLoadedModule(string.Empty, new IntPtr(result.ModBase), 0, string.Empty, true, this);
            modules.Add(result.ModBase, loaded_module);
        }

        symbols.Add(CreateType(type_cache, result.Tag, result.ModBase, result.TypeIndex, result.Size, loaded_module, GetNameFromSymbolInfo(sym_info)));
        return true;
    }

    private int GetImageSize(IntPtr base_address)
    {
        NativeMethods.GetModuleInformation(Handle, base_address,
            out MODULEINFO mod_info, Marshal.SizeOf(typeof(MODULEINFO))).ToNtException(true);
        return mod_info.SizeOfImage;
    }

    private IMAGEHLP_MODULE64 GetModuleInfo(long base_address)
    {
        if (base_address == 0)
            return new IMAGEHLP_MODULE64();
        IMAGEHLP_MODULE64 module = new();
        module.SizeOfStruct = Marshal.SizeOf(module);
        if (_sym_get_module_info(Handle, base_address, ref module))
        {
            return module;
        }
        return new IMAGEHLP_MODULE64();
    }

    private IEnumerable<SymbolLoadedModule> GetLoadedModulesInternal()
    {
        List<SymbolLoadedModule> modules = new();

        if (!_sym_enum_modules(Handle, (s, m, p) =>
        {
            var mod_info = GetModuleInfo(m);
            modules.Add(new SymbolLoadedModule(s, new IntPtr(m), mod_info.ImageSize,
                mod_info.LoadedPdbName, mod_info.SymType == SYM_TYPE.SymExport, this));
            return true;
        }, IntPtr.Zero))
        {
            throw new Win32Exception();
        }
        return modules.AsReadOnly();
    }

    private static string GetSymbolName(string symbol)
    {
        int last_index = symbol.LastIndexOf("::");
        if (last_index >= 0)
        {
            symbol = symbol.Substring(last_index + 2);
        }

        last_index = symbol.LastIndexOf("`");
        if (last_index >= 0)
        {
            symbol = symbol.Substring(last_index + 1);
        }
        return symbol;
    }

    private DataSymbolInformation GetSymbolInfoForAddress(IntPtr address)
    {
        using var sym_info = AllocateSymInfo();
        if (_sym_from_addr(Handle, address.ToInt64(), out long displacement, sym_info))
        {
            var result = sym_info.Result;

            return new DataSymbolInformation(result.Tag, result.Size, result.TypeIndex,
                result.Address, GetModuleForAddress(new IntPtr(result.ModBase)), 
                GetNameFromSymbolInfo(sym_info), (DllMachineType)result.Reserved2);
        }

        return null;
    }

    private DataSymbolInformation GetSymbolInfoForName(string name)
    {
        using var sym_info = AllocateSymInfo();
        if (!_sym_from_name(Handle, name, sym_info))
        {
            return null;
        }
        var result = sym_info.Result;
        return new DataSymbolInformation(result.Tag, result.Size, result.TypeIndex,
            result.Address, GetModuleForAddress(new IntPtr(result.ModBase)), 
            GetNameFromSymbolInfo(sym_info), (DllMachineType)result.Reserved2);
    }

    private class TraceTextWriter : TextWriter
    {
        public override Encoding Encoding => Encoding.UTF8;

        public override void WriteLine(string value)
        {
            Trace.WriteLine(value);
        }
        public override void Write(string value)
        {
            Trace.Write(value);
        }
    }

    private static bool CheckForSymsrv(string dbghelp_path)
    {
        string path = Path.Combine(Path.GetDirectoryName(dbghelp_path), "symsrv.dll");
        return File.Exists(path);
    }

    private DllDebugData GetDllDebugData(string executable_path)
    {
        using var lib = SafeLoadLibraryHandle.LoadLibrary(executable_path, LoadLibraryFlags.AsDataFile, false);
        if (!lib.IsSuccess)
            return new DllDebugData();
        return lib.Result.DebugData;
    }

    private bool CheckOrDownloadSymbolFile(DllDebugData debug_data, out string cache_path)
    {
        cache_path = null;
        if (string.IsNullOrEmpty(debug_data.PdbPath))
            return false;
        cache_path = debug_data.GetSymbolPath(_symbol_cache_path);
        if (File.Exists(cache_path))
            return true;

        try
        {
            var request = WebRequest.CreateHttp(debug_data.GetSymbolPath(_symbol_server_path.AbsoluteUri));
            using var response = request.GetResponse();
            Directory.CreateDirectory(Path.GetDirectoryName(cache_path));
            using var file = File.OpenWrite(cache_path);
            response.GetResponseStream().CopyTo(file);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private bool CheckOrDownloadSymbolFile(string executable_path)
    {
        if (!_enable_symsrv_fallback)
            return false;
        return CheckOrDownloadSymbolFile(GetDllDebugData(executable_path), out string _);
    }

    private bool SymDebugCallback(
        IntPtr hProcess,
        DbgHelpCallbackActionCode ActionCode,
        IntPtr CallbackData
    )
    {
        if (_trace_symbol_loading)
        {
            switch (ActionCode)
            {
                case DbgHelpCallbackActionCode.CBA_EVENT:
                    var evt = CallbackData.ReadStruct<IMAGEHLP_CBA_EVENTW>();
                    _trace_writer.WriteLine(Marshal.PtrToStringUni(evt.desc).TrimEnd());
                    return true;
            }
        }

        if (_enable_symsrv_fallback)
        {
            switch (ActionCode)
            {
                case DbgHelpCallbackActionCode.CBA_DEFERRED_SYMBOL_LOAD_START:
                    {
                        var load_evt = CallbackData.ReadStruct<IMAGEHLP_DEFERRED_SYMBOL_LOADW>();
                        return CheckOrDownloadSymbolFile(load_evt.FileName);
                    }
            }
        }

        return false;
    }

    private Tuple<string, Uri> GetFallbackSymbolPaths(string symbol_path)
    {
        string cache_path = null;
        Uri server_path = new(DEFAULT_SYMSRV);

        string[] parts = symbol_path.Split(';');
        foreach (var str in parts)
        {
            if (str.StartsWith("srv*"))
            {
                string uri = str.Substring(4);
                if (!Uri.TryCreate(uri, UriKind.Absolute, out Uri srv_path))
                    continue;
                if (srv_path.Scheme != "http" && srv_path.Scheme != "https")
                    continue;
                server_path = srv_path;
            }
            else if (Directory.Exists(str))
            {
                cache_path = Path.GetFullPath(str);
            }
        }
        return Tuple.Create(cache_path, server_path);
    }

    #endregion

    #region Public Methods
    public IEnumerable<SymbolLoadedModule> GetLoadedModules()
    {
        return GetLoadedModules(false);
    }

    public IEnumerable<SymbolLoadedModule> GetLoadedModules(bool refresh)
    {
        if (_loaded_modules == null || refresh)
        {
            _loaded_modules = GetLoadedModulesInternal().OrderBy(s => s.BaseAddress.ToInt64());
        }
        return _loaded_modules;
    }

    public SymbolLoadedModule GetModuleForAddress(IntPtr address, bool refresh)
    {
        var mod_info = GetModuleInfo(address.ToInt64());
        if (!string.IsNullOrEmpty(mod_info.ImageName))
            return new SymbolLoadedModule(mod_info, this);

        long check_addr = address.ToInt64();

        foreach (SymbolLoadedModule module in GetLoadedModules(refresh))
        {
            long base_address = module.BaseAddress.ToInt64();
            if (check_addr >= base_address && check_addr < base_address + module.ImageSize)
            {
                return module;
            }
        }

        return null;
    }

    public SymbolLoadedModule GetModuleForAddress(IntPtr address)
    {
        return GetModuleForAddress(address, false);
    }

    public string GetModuleRelativeAddress(IntPtr address)
    {
        return GetModuleRelativeAddress(address, false);
    }

    public string GetModuleRelativeAddress(IntPtr address, bool refresh)
    {
        SymbolLoadedModule module = GetModuleForAddress(address, refresh);
        if (module == null)
        {
            return $"0x{address.ToInt64():X}";
        }

        return $"{module.Name}+0x{address.ToInt64() - module.BaseAddress.ToInt64():X}";
    }

    public IntPtr GetAddressOfSymbol(string name)
    {
        return new IntPtr(GetSymbolInfoForName(name)?.Address ?? 0);
    }

    public string GetSymbolForAddress(IntPtr address)
    {
        return GetSymbolForAddress(address, true);
    }

    public string GetSymbolForAddress(IntPtr address, bool generate_fake_symbol, bool return_name_only)
    {
        using var sym_info = AllocateSymInfo();
        if (_sym_from_addr(Handle, address.ToInt64(), out long displacement, sym_info))
        {
            string name = GetNameFromSymbolInfo(sym_info);
            if (return_name_only)
            {
                return GetSymbolName(name);
            }
            string disp_str = string.Empty;
            if (displacement < 0)
            {
                disp_str = $"-0x{Math.Abs(displacement):X}";
            }
            else if (displacement > 0)
            {
                disp_str = $"+0x{displacement:X}";
            }

            return $"{name}{disp_str}";
        }
        // Perhaps should return module+X?
        if (generate_fake_symbol && !return_name_only)
        {
            return $"0x{address.ToInt64():X}";
        }
        return null;
    }

    public string GetSymbolForAddress(IntPtr address, bool generate_fake_symbol)
    {
        return GetSymbolForAddress(address, generate_fake_symbol, false);
    }

    public void ReloadModuleList()
    {
        _sym_refresh_module_list(Handle).ToNtException(true);
    }

    public void LoadModule(string module_path, IntPtr base_address)
    {
        if (_sym_load_module(Handle, IntPtr.Zero, module_path,
                            Path.GetFileNameWithoutExtension(module_path), base_address.ToInt64(), GetImageSize(base_address)) == 0)
        {
            Win32Utils.GetLastWin32Error().ToNtException();
        }
    }

    public IEnumerable<string> QueryTypeNames(IntPtr base_address)
    {
        return QueryTypes(base_address).Select(t => t.Name);
    }

    public IEnumerable<TypeInformation> QueryTypes(IntPtr base_address)
    {
        TypeInformationCache type_cache = new();
        Dictionary<long, SymbolLoadedModule> modules = GetLoadedModules().ToDictionary(m => m.BaseAddress.ToInt64());
        List<TypeInformation> symbols = new();
        _sym_enum_types(Handle, base_address.ToInt64(), (s, z, c) => EnumTypes(type_cache, modules, symbols, s), IntPtr.Zero);
        type_cache.FixupPointerTypes();
        return symbols;
    }

    public TypeInformation GetTypeByName(IntPtr base_address, string name)
    {
        Dictionary<long, SymbolLoadedModule> modules = GetLoadedModules().ToDictionary(m => m.BaseAddress.ToInt64());
        using var sym_info = AllocateSymInfo();
        if (_sym_get_type_from_name(Handle, base_address.ToInt64(), name, sym_info))
        {
            var result = sym_info.Result;
            SymbolLoadedModule loaded_module;
            if (modules.ContainsKey(base_address.ToInt64()))
            {
                loaded_module = modules[base_address.ToInt64()];
            }
            else
            {
                loaded_module = new SymbolLoadedModule(string.Empty, new IntPtr(result.ModBase), 0, string.Empty, true, this);
            }
            TypeInformationCache type_cache = new();
            var ret = CreateType(type_cache, result.Tag, result.ModBase, result.TypeIndex, result.Size, loaded_module, GetNameFromSymbolInfo(sym_info));
            type_cache.FixupPointerTypes();
            return ret;
        }
        else
        {
            throw new ArgumentException("Invalid type");
        }
    }

    public IEnumerable<TypeInformation> QueryTypesByName(IntPtr base_address, string mask)
    {
        Dictionary<long, SymbolLoadedModule> modules = GetLoadedModules().ToDictionary(m => m.BaseAddress.ToInt64());
        List<TypeInformation> symbols = new();
        TypeInformationCache type_cache = new();
        _sym_enum_types_by_name(Handle, base_address.ToInt64(), mask, (s, z, c) => EnumTypes(type_cache, modules, symbols, s), IntPtr.Zero);
        type_cache.FixupPointerTypes();
        return symbols;
    }

    public TypeInformation GetTypeForSymbolByName(string name)
    {
        var symbol = GetSymbolInfoForName(name);
        if (symbol == null)
        {
            return null;
        }

        TypeInformationCache type_cache = new();
        var ret = CreateType(type_cache, symbol.Tag, symbol.Module.BaseAddress.ToInt64(), symbol.TypeIndex, symbol.Size, symbol.Module, symbol.Name, symbol.Address);
        type_cache.FixupPointerTypes();
        return ret;
    }

    public TypeInformation GetTypeForSymbolByAddress(IntPtr address)
    {
        var symbol = GetSymbolInfoForAddress(address);
        if (symbol == null)
        {
            return null;
        }

        TypeInformationCache type_cache = new();
        var ret = CreateType(type_cache, symbol.Tag, symbol.Module.BaseAddress.ToInt64(), symbol.TypeIndex, symbol.Size, symbol.Module, symbol.Name, symbol.Address);
        type_cache.FixupPointerTypes();
        return ret;
    }

    public IEnumerable<SymbolInformation> EnumerateSymbols(IntPtr base_address, string mask)
    {
        if (string.IsNullOrWhiteSpace(mask))
        {
            throw new ArgumentException($"'{nameof(mask)}' cannot be null or whitespace.", nameof(mask));
        }

        List<SymbolInformation> symbols = new();
        Dictionary<long, SymbolLoadedModule> modules = GetLoadedModules().ToDictionary(m => m.BaseAddress.ToInt64());
        if (!_sym_enum_symbols(Handle, base_address.ToInt64(), 
            mask, (s, z, x) => GetSymbolInfo(symbols, s, modules), IntPtr.Zero))
        {
            Win32Utils.GetLastWin32Error().ToNtException();
        }
        return symbols.AsReadOnly();
    }

    #endregion

    #region Internals Members
    internal NtProcess Process { get; }
    internal SafeKernelObjectHandle Handle => Process.Handle;

    internal DbgHelpSymbolResolver(NtProcess process, string dbghelp_path, string symbol_path, SymbolResolverFlags flags, TextWriter trace_writer) 
        : this(process, dbghelp_path, symbol_path, flags, trace_writer, true)
    {
    }

    internal DbgHelpSymbolResolver(ImageFile image_file, string symbol_path)
        : this(NtProcess.Current, "dbghelp.dll", symbol_path, SymbolResolverFlags.None, null, false)
    {
        var srv = GetFallbackSymbolPaths(symbol_path);
        _symbol_cache_path = srv.Item1;
        _symbol_server_path = srv.Item2;
        if (string.IsNullOrEmpty(_symbol_cache_path))
            throw new ArgumentException("Must specify an existing local path in the symbol path to store downloaded symbols.");
        if (!CheckOrDownloadSymbolFile(image_file.DebugData, out string cache_path))
            throw new InvalidOperationException("Couldn't find or download PDB for image file.");
        if (_sym_load_module(Handle, IntPtr.Zero, cache_path,
            Path.GetFileNameWithoutExtension(image_file.FileName), image_file.OriginalImageBase, image_file.SizeOfImage) == 0)
        {
            Win32Utils.GetLastWin32Error().ToNtException();
        }
    }

    private DbgHelpSymbolResolver(NtProcess process, string dbghelp_path, string symbol_path, SymbolResolverFlags flags, TextWriter trace_writer, bool init_modules)
    {
        Process = process.Duplicate();
        _dbghelp_lib = SafeLoadLibraryHandle.LoadLibrary(dbghelp_path);
        GetFunc(ref _sym_init);
        GetFunc(ref _sym_cleanup);
        GetFunc(ref _sym_from_name);
        GetFunc(ref _sym_set_options);
        GetFunc(ref _sym_enum_modules);
        GetFunc(ref _sym_from_addr);
        GetFunc(ref _sym_get_module_info);
        GetFunc(ref _sym_load_module);
        GetFunc(ref _sym_refresh_module_list);
        GetFunc(ref _sym_enum_types);
        GetFunc(ref _sym_get_type_from_name);
        GetFunc(ref _sym_enum_types_by_name);
        GetFunc(ref _sym_get_type_info);
        GetFunc(ref _sym_get_type_info_dword, "SymGetTypeInfo");
        GetFunc(ref _sym_get_type_info_ptr, "SymGetTypeInfo");
        GetFunc(ref _sym_get_type_info_var, "SymGetTypeInfo");
        GetFunc(ref _sym_get_type_info_long, "SymGetTypeInfo");
        GetFunc(ref _sym_from_index);
        GetFunc(ref _sym_enum_symbols);
        GetFunc(ref _sym_set_context);

        _trace_writer = trace_writer ?? new TraceTextWriter();
        SymOptions options = SymOptions.INCLUDE_32BIT_MODULES | SymOptions.UNDNAME | SymOptions.DEFERRED_LOADS;
        _enable_symsrv_fallback = flags.HasFlagSet(SymbolResolverFlags.SymSrvFallback) && !CheckForSymsrv(_dbghelp_lib.FullPath);
        _trace_symbol_loading = flags.HasFlagSet(SymbolResolverFlags.TraceSymbolLoading);
        if (_trace_symbol_loading)
        {
            options |= SymOptions.DEBUG;
        }
        if (_enable_symsrv_fallback)
        {
            var srv = GetFallbackSymbolPaths(symbol_path);
            _symbol_cache_path = srv.Item1;
            _symbol_server_path = srv.Item2;
            if (string.IsNullOrEmpty(_symbol_cache_path))
                throw new ArgumentException("Must specify an existing local path in the symbol path to store downloaded symbols.");
            options |= SymOptions.DEBUG;
        }
        if (flags.HasFlagSet(SymbolResolverFlags.DisableExportSymbols))
        {
            options |= SymOptions.EXACT_SYMBOLS;
        }
        _sym_set_options(options);

        if (!_sym_init(Handle, symbol_path, init_modules))
        {
            // If SymInitialize failed then we'll have to bootstrap modules manually.
            if (!init_modules || !_sym_init(Handle, symbol_path, false))
            {
                throw new Win32Exception();
            }

            IntPtr[] modules = new IntPtr[1024];
            if (NativeMethods.EnumProcessModulesEx(Handle, modules, modules.Length * IntPtr.Size, out int return_length,
                process.Is64Bit ? EnumProcessModulesFilter.LIST_MODULES_64BIT : EnumProcessModulesFilter.LIST_MODULES_32BIT))
            {
                foreach (IntPtr module in modules.Take(return_length / IntPtr.Size))
                {
                    StringBuilder dllpath = new(260);
                    if (NativeMethods.GetModuleFileNameEx(Handle, module, dllpath, dllpath.Capacity) > 0)
                    {
                        if (_sym_load_module(Handle, IntPtr.Zero, dllpath.ToString(),
                            Path.GetFileNameWithoutExtension(dllpath.ToString()), module.ToInt64(), GetImageSize(module)) == 0)
                        {
                            Debug.WriteLine($"Couldn't load {dllpath}");
                        }
                    }
                }
            }
        }

        if (_trace_symbol_loading || _enable_symsrv_fallback)
        {
            _callback_handler = DbgHelpDebugCallbackHandler.GetInstance(_dbghelp_lib);
            _callback_handler.RegisterHandler(Handle, SymDebugCallback);
        }
    }

    #endregion

    #region IDisposable Support
    private bool disposedValue = false;

    void Dispose(bool disposing)
    {
        if (!disposedValue)
        {
            disposedValue = true;
            _callback_handler?.RemoveHandler(Handle);
            if (!Handle.IsClosed)
            {
                _sym_cleanup?.Invoke(Handle);
            }
            _dbghelp_lib?.Close();
            Process?.Dispose();
        }
    }

    ~DbgHelpSymbolResolver()
    {
        Dispose(false);
    }

    void IDisposable.Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
    #endregion
}
