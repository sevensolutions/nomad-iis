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

using System;
using System.Collections.Generic;
using NtCoreLib.Win32.Debugger.Interop;

namespace NtCoreLib.Win32.Debugger.Symbols;

/// <summary>
/// Represents a loaded module from the symbol resolver.
/// </summary>
public sealed class SymbolLoadedModule
{
    /// <summary>
    /// The name of the module.
    /// </summary>
    public string Name { get; }
    /// <summary>
    /// The base address of the module.
    /// </summary>
    public IntPtr BaseAddress { get; }
    /// <summary>
    /// The image size of the module.
    /// </summary>
    public int ImageSize { get; }
    /// <summary>
    /// Get the path to the loaded PDB file is known.
    /// </summary>
    public string PdbPath { get; }
    /// <summary>
    /// True indicates this module only has export symbols.
    /// </summary>
    public bool ExportSymbols { get; }

    private readonly ISymbolTypeResolver _type_resolver;

    /// <summary>
    /// Query names of types for this module.
    /// </summary>
    /// <returns>The list of type names.</returns>
    public IEnumerable<string> QueryTypeNames()
    {
        return _type_resolver?.QueryTypeNames(BaseAddress) ?? new string[0];
    }

    /// <summary>
    /// Query types in a module.
    /// </summary>
    /// <returns>The list of types.</returns>
    public IEnumerable<TypeInformation> QueryTypes()
    {
        return _type_resolver?.QueryTypes(BaseAddress) ?? new TypeInformation[0];
    }

    /// <summary>
    /// Get a type by name.
    /// </summary>
    /// <param name="name">The name of the type.</param>
    /// <returns></returns>
    public TypeInformation GetTypeByName(string name)
    {
        return _type_resolver?.GetTypeByName(BaseAddress, name) ?? throw new NotImplementedException();
    }

    /// <summary>
    /// Query types by name
    /// </summary>
    /// <param name="mask">A mask string for the type name. e.g. mod!ABC*</param>
    /// <returns>The list of types.</returns>
    public IEnumerable<TypeInformation> QueryTypesByName(string mask)
    {
        return _type_resolver?.QueryTypesByName(BaseAddress, mask) ?? new TypeInformation[0];
    }

    internal SymbolLoadedModule(IMAGEHLP_MODULE64 mod_info, ISymbolTypeResolver type_resolver)
        : this(mod_info.ImageName, new IntPtr(mod_info.BaseOfImage),
        mod_info.ImageSize, mod_info.LoadedPdbName, mod_info.SymType == SYM_TYPE.SymExport, type_resolver)
    {
    }

    internal SymbolLoadedModule(string name, IntPtr base_address, int image_size, string pdb_path, bool export_symbols, ISymbolTypeResolver type_resolver)
    {
        Name = name;
        BaseAddress = base_address;
        ImageSize = image_size;
        _type_resolver = type_resolver;
        PdbPath = pdb_path;
        ExportSymbols = export_symbols;
    }

    /// <summary>
    /// Returns the name of the module.
    /// </summary>
    /// <returns>The name of the module.</returns>
    public override string ToString()
    {
        return Name;
    }
}
