﻿//  Copyright 2016, 2017, 2018 Google Inc. All Rights Reserved.
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

using NtCoreLib.Image;
using NtCoreLib.Ndr.Dce;
using NtCoreLib.Ndr.Formatter;
using NtCoreLib.Ndr.Interop;
using NtCoreLib.Ndr.Ndr64;
using NtCoreLib.Ndr.Parser;
using NtCoreLib.Ndr.Rpc;
using NtCoreLib.Security.Authorization;
using NtCoreLib.Utilities.Memory;
using NtCoreLib.Win32.Debugger.Symbols;
using NtCoreLib.Win32.Loader;
using NtCoreLib.Win32.Rpc.Client.Builder;
using NtCoreLib.Win32.Rpc.EndpointMapper;
using NtCoreLib.Win32.Rpc.Interop;
using NtCoreLib.Win32.Service;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters;
using System.Runtime.Serialization.Formatters.Binary;

#nullable enable

namespace NtCoreLib.Win32.Rpc.Server;

/// <summary>
/// A class to represent an RPC server.
/// </summary>
[Serializable]
public sealed class RpcServer : IRpcBuildableClient
{
    #region Public Methods
    /// <summary>
    /// Resolve the current running endpoint for this server.
    /// </summary>
    /// <returns></returns>
    public string ResolveRunningEndpoint()
    {
        return RpcEndpointMapper.QueryAlpcEndpoints(Server.InterfaceId).FirstOrDefault()?.Endpoint ?? string.Empty;
    }

    /// <summary>
    /// Format the RPC server as text.
    /// </summary>
    /// <param name="remove_comments">True to remove comments from the output.</param>
    /// <param name="format">Output text format type.</param>
    /// <returns>The formatted RPC server.</returns>
    public string FormatAsText(bool remove_comments = false, NdrFormatterTextFormat format = NdrFormatterTextFormat.Idl)
    {
        NdrFormatterFlags flags = remove_comments ? NdrFormatterFlags.RemoveComments : NdrFormatterFlags.None;
        return FormatAsText(flags, format);
    }

    /// <summary>
    /// Format the RPC server as text.
    /// </summary>
    /// <param name="flags">Flags for the formatter..</param>
    /// <param name="format">Output text format type.</param>
    /// <returns>The formatted RPC server.</returns>
    public string FormatAsText(NdrFormatterFlags flags, NdrFormatterTextFormat format = NdrFormatterTextFormat.Idl)
    {
        if (Server.DceSyntaxInfo == null)
            throw new ArgumentException("No DCE NDR syntax available.");

        NdrFormatter formatter = NdrFormatter.Create(format, flags: flags);
        formatter.HeaderText.Add($"DllOffset: 0x{Offset:X}");
        formatter.HeaderText.Add($"DllPath {FilePath}");
        if (!string.IsNullOrWhiteSpace(ServiceName))
        {
            formatter.HeaderText.Add($"ServiceName: {ServiceName}");
            formatter.HeaderText.Add($"ServiceDisplayName: {ServiceDisplayName}");
        }

        if (EndpointCount > 0)
        {
            formatter.HeaderText.Add($"Endpoints: {EndpointCount}");
            foreach (var ep in Endpoints)
            {
                formatter.HeaderText.Add($"{ep.BindingString}");
            }
        }

        formatter.ComplexTypes.AddRange(ComplexTypes);
        formatter.RpcServers.Add(Server);
        return formatter.Format();
    }

    /// <summary>
    /// Serialize the RPC server to a stream.
    /// </summary>
    /// <param name="stm">The stream to hold the serialized server.</param>
    /// <remarks>Only use the output of this method with the Deserialize method. No guarantees of compatibility is made between
    /// versions of the library or the specific format used.</remarks>
    public void Serialize(Stream stm)
    {
        RpcServerSerializer.Serialize(this, stm);
    }

    /// <summary>
    /// Serialize the RPC server to a byte array.
    /// </summary>
    /// <returns>The serialized data.</returns>
    /// <remarks>Only use the output of this method with the Deserialize method. No guarantees of compatibility is made between
    /// versions of the library or the specific format used.</remarks>
    public byte[] Serialize()
    {
        MemoryStream stm = new();
        Serialize(stm);
        return stm.ToArray();
    }

    #endregion

    #region Public Properties

    /// <summary>
    /// The RPC server interface.
    /// </summary>
    public Guid InterfaceId => Server.InterfaceId.Uuid;
    /// <summary>
    /// The RPC server interface version.
    /// </summary>
    public RpcVersion InterfaceVersion => Server.InterfaceId.Version;
    /// <summary>
    /// The number of RPC procedures.
    /// </summary>
    public int ProcedureCount { get; }
    /// <summary>
    /// The NDR RPC server.
    /// </summary>
    public RpcServerInterface Server { get; }
    /// <summary>
    /// The list of DCE RPC procedures.
    /// </summary>
    public IEnumerable<NdrProcedureDefinition> Procedures => Server.DceSyntaxInfo?.Procedures ?? Array.Empty<NdrProcedureDefinition>();
    /// <summary>
    /// List of parsed DCE complex types.
    /// </summary>
    public IEnumerable<NdrComplexTypeReference> ComplexTypes => Server.DceSyntaxInfo?.ComplexTypes ?? Array.Empty<NdrComplexTypeReference>();
    /// <summary>
    /// The list of NDR64 RPC procedures.
    /// </summary>
    public IEnumerable<Ndr64ProcedureDefinition> Ndr64Procedures => Server.Ndr64SyntaxInfo?.Procedures ?? Array.Empty<Ndr64ProcedureDefinition>();
    /// <summary>
    /// List of parsed DCE complex types.
    /// </summary>
    public IEnumerable<Ndr64ComplexTypeReference> Ndr64ComplexTypes => Server.Ndr64SyntaxInfo?.ComplexTypes ?? Array.Empty<Ndr64ComplexTypeReference>();
    /// <summary>
    /// Path to the PE file this server came from (if known)
    /// </summary>
    public string FilePath { get; }
    /// <summary>
    /// Name of the the PE file this server came from (if known)
    /// </summary>
    public string Name => string.IsNullOrWhiteSpace(FilePath) ? string.Empty : Path.GetFileName(FilePath);
    /// <summary>
    /// Offset into the PE file this server was parsed from.
    /// </summary>
    public long Offset { get; }
    /// <summary>
    /// Name of the service this server would run in (if known).
    /// </summary>
    public string ServiceName { get; }
    /// <summary>
    /// Display name of the service this server would run in (if known).
    /// </summary>
    public string ServiceDisplayName { get; }
    /// <summary>
    /// True if the service is currently running.
    /// </summary>
    public bool IsServiceRunning { get; }
    /// <summary>
    /// List of endpoints for this service if running.
    /// </summary>
    public IEnumerable<RpcEndpoint> Endpoints => GetEndpoints();
    /// <summary>
    /// Count of endpoints for this service if running.
    /// </summary>
    public int EndpointCount => Endpoints.Count();
    /// <summary>
    /// This parsed interface represents a client.
    /// </summary>
    public bool Client { get; }
    /// <inheritdoc/>
    public bool HasDceSyntaxInfo => Server.DceSyntaxInfo != null;
    #endregion

    #region Static Methods

    /// <summary>
    /// Parse all RPC servers from a PE file.
    /// </summary>
    /// <param name="file">The PE file to parse.</param>
    /// <param name="dbghelp_path">Path to a DBGHELP DLL to resolve symbols.</param>
    /// <param name="symbol_path">Symbol path for DBGHELP</param>
    /// <remarks>This only works for PE files with the same bitness as the current process.</remarks>
    /// <returns>A list of parsed RPC server.</returns>
    public static IEnumerable<RpcServer> ParsePeFile(string file, string dbghelp_path, string symbol_path)
    {
        return ParsePeFile(file, dbghelp_path, symbol_path, false, false);
    }

    /// <summary>
    /// Parse all RPC servers from a PE file.
    /// </summary>
    /// <param name="file">The PE file to parse.</param>
    /// <param name="dbghelp_path">Path to a DBGHELP DLL to resolve symbols.</param>
    /// <param name="symbol_path">Symbol path for DBGHELP</param>
    /// <param name="parse_clients">True to parse client RPC interfaces.</param>
    /// <remarks>This only works for PE files with the same bitness as the current process.</remarks>
    /// <returns>A list of parsed RPC server.</returns>
    public static IEnumerable<RpcServer> ParsePeFile(string file, string dbghelp_path, string symbol_path, bool parse_clients)
    {
        return ParsePeFile(file, dbghelp_path, symbol_path, parse_clients, false);
    }

    /// <summary>
    /// Parse all RPC servers from a PE file.
    /// </summary>
    /// <param name="file">The PE file to parse.</param>
    /// <param name="dbghelp_path">Path to a DBGHELP DLL to resolve symbols.</param>
    /// <param name="symbol_path">Symbol path for DBGHELP</param>
    /// <param name="parse_clients">True to parse client RPC interfaces.</param>
    /// <param name="ignore_symbols">Ignore symbol resolving.</param>
    /// <remarks>This only works for PE files with the same bitness as the current process.</remarks>
    /// <returns>A list of parsed RPC server.</returns>
    public static IEnumerable<RpcServer> ParsePeFile(string file, string dbghelp_path, string symbol_path, bool parse_clients, bool ignore_symbols)
    {
        RpcServerParserFlags flags = RpcServerParserFlags.None;
        if (parse_clients)
            flags |= RpcServerParserFlags.ParseClients;
        if (ignore_symbols)
            flags |= RpcServerParserFlags.IgnoreSymbols;

        return ParsePeFile(file, dbghelp_path, symbol_path, flags);
    }

    /// <summary>
    /// Parse all RPC servers from a PE file.
    /// </summary>
    /// <param name="file">The PE file to parse.</param>
    /// <param name="dbghelp_path">Path to a DBGHELP DLL to resolve symbols.</param>
    /// <param name="symbol_path">Symbol path for DBGHELP</param>
    /// <param name="flags">Flags for the RPC parser.</param>
    /// <remarks>This only works for PE files with the same bitness as the current process.</remarks>
    /// <returns>A list of parsed RPC server.</returns>
    public static IEnumerable<RpcServer> ParsePeFile(string file, string dbghelp_path, string symbol_path, RpcServerParserFlags flags)
    {
        if (!NtObjectUtils.IsWindows)
        {
            var image_file = ImageFile.Parse(file, default, false);
            if (!image_file.IsSuccess)
                return Array.Empty<RpcServer>();
            return ParseImageFile(image_file.Result, dbghelp_path, symbol_path, flags);
        }
        else
        {
            var lib = SafeLoadLibraryHandle.LoadLibrary(file, LoadLibraryFlags.DontResolveDllReferences, false);
            if (!lib.IsSuccess)
            {
                lib = SafeLoadLibraryHandle.LoadLibrary(file, LoadLibraryFlags.AsDataFile, false);
            }
            using (lib)
            {
                if (!lib.IsSuccess)
                    return Array.Empty<RpcServer>();
                return ParseImageFile(lib.Result.GetImageFile(), dbghelp_path, symbol_path, flags);
            }
        }
    }

    /// <summary>
    /// Parse all RPC servers from a PE image file.
    /// </summary>
    /// <param name="image_file">The PE image file to parse.</param>
    /// <param name="dbghelp_path">Path to a DBGHELP DLL to resolve symbols.</param>
    /// <param name="symbol_path">Symbol path for DBGHELP</param>
    /// <param name="flags">Flags for the RPC parser.</param>
    /// <returns>A list of parsed RPC server.</returns>
    public static IEnumerable<RpcServer> ParseImageFile(ImageFile image_file,
        string dbghelp_path, string symbol_path, RpcServerParserFlags flags)
    {
        var sections = image_file.ImageSections;
        var offsets = sections.SelectMany(s => FindRpcServerInterfaces(s, image_file.Is64bit,
                flags.HasFlagSet(RpcServerParserFlags.ParseClients)));
        if (!offsets.Any())
        {
            return Array.Empty<RpcServer>();
        }

        using ISymbolResolver? sym_resolver = CreateSymbolResolver(image_file, dbghelp_path, symbol_path, flags);

        NdrParserFlags parser_flags = NdrParserFlags.IgnoreUserMarshal;
        if (flags.HasFlagSet(RpcServerParserFlags.ResolveStructureNames))
            parser_flags |= NdrParserFlags.ResolveStructureNames;

        if (flags.HasFlagSet(RpcServerParserFlags.IgnoreNdr64))
            parser_flags |= NdrParserFlags.IgnoreNdr64;

        List<RpcServer> servers = new();
        foreach (var offset in offsets)
        {
            IMemoryReader reader = image_file.ToMemoryReader();
            NdrParser parser = new(reader, sym_resolver, parser_flags);
            IntPtr ifspec = new(image_file.OriginalImageBase + offset.Offset);
            try
            {
                var rpc = parser.ReadFromRpcServerInterface(ifspec, new IntPtr(image_file.OriginalImageBase));
                servers.Add(new RpcServer(rpc, image_file.FileName, offset.Offset, offset.Client));
            }
            catch (NdrParserException)
            {
            }
        }
        return servers.AsReadOnly();
    }

    /// <summary>
    /// Deserialize an RPC server instance from a stream.
    /// </summary>
    /// <param name="stm">The stream to deserialize from.</param>
    /// <returns>The RPC server instance.</returns>
    /// <remarks>The data used by this method should only use the output from serialize. No guarantees of compatibility is made between
    /// versions of the library or the specific format used.</remarks>
    public static RpcServer Deserialize(Stream stm)
    {
        return RpcServerSerializer.Deserialize(stm);
    }

    /// <summary>
    /// Deserialize an RPC server instance from a byte array.
    /// </summary>
    /// <param name="ba">The byte array to deserialize from.</param>
    /// <returns>The RPC server instance.</returns>
    /// <remarks>The data used by this method should only use the output from serialize. No guarantees of compatibility is made between
    /// versions of the library or the specific format used.</remarks>
    public static RpcServer Deserialize(byte[] ba)
    {
        return Deserialize(new MemoryStream(ba));
    }

    /// <summary>
    /// Get the default RPC server security descriptor.
    /// </summary>
    /// <returns>The default security descriptor.</returns>
    public static SecurityDescriptor GetDefaultSecurityDescriptor()
    {
        Win32Error result = NativeMethods.I_RpcGetDefaultSD(out IntPtr sd);
        if (result != Win32Error.SUCCESS)
        {
            result.ToNtException();
        }

        try
        {
            return new SecurityDescriptor(sd);
        }
        finally
        {
            NativeMethods.I_RpcFree(sd);
        }
    }

    #endregion

    #region Private Methods
    struct RpcOffset
    {
        public long Offset;
        public bool Client;
        public RpcOffset(long offset, bool client)
        {
            Offset = offset;
            Client = client;
        }
    }

    private static Dictionary<string, ServiceInstance> GetExesToServices()
    {
        Dictionary<string, ServiceInstance> services = new(StringComparer.OrdinalIgnoreCase);
        if (!NtObjectUtils.IsWindows)
            return services;
        foreach (var entry in ServiceUtils.GetServices())
        {
            services[entry.ImagePath] = entry;
            if (!string.IsNullOrWhiteSpace(entry.ServiceDll))
            {
                services[entry.ServiceDll ?? string.Empty] = entry;
            }
        }

        return services;
    }

    private static Lazy<Dictionary<string, ServiceInstance>> _exes_to_service = new(GetExesToServices);

    private RpcServer(RpcServerInterface server, string filepath, long offset, bool client)
    {
        Server = server;
        FilePath = Path.GetFullPath(filepath);
        Offset = offset;
        var services = _exes_to_service.Value;
        if (services.ContainsKey(FilePath))
        {
            ServiceName = services[FilePath].Name;
            ServiceDisplayName = services[FilePath].DisplayName;
            IsServiceRunning = services[FilePath].Status == ServiceStatus.Running;
        }
        else
        {
            ServiceName = string.Empty;
            ServiceDisplayName = string.Empty;
        }
        Client = client;
        ProcedureCount = Server.DceSyntaxInfo?.Procedures.Count ?? Server.Ndr64SyntaxInfo?.Procedures.Count ?? 0;
    }

    static IEnumerable<int> FindBytes(byte[] buffer, byte[] bytes)
    {
        int max_length = buffer.Length - bytes.Length;
        for (int i = 0; i < max_length; ++i)
        {
            int j = 0;
            for (; j < bytes.Length; ++j)
            {
                if (buffer[i + j] != bytes[j])
                {
                    break;
                }
            }

            if (j == bytes.Length)
            {
                yield return i;
            }
        }
    }

    private static IEnumerable<RpcOffset> FindRpcServerInterfaces(ImageSection sect, bool is_64bit, bool return_clients)
    {
        if (!sect.Characteristics.HasFlagSet(ImageSectionCharacteristics.Read))
            yield break;
        byte[] rdata = sect.ToArray();
        foreach (int ofs in FindBytes(rdata, NdrNativeUtils.DCE_TransferSyntax.ToByteArray()).Concat(FindBytes(rdata, NdrNativeUtils.NDR64_TransferSyntax.ToByteArray())))
        {
            if (ofs < 24)
            {
                continue;
            }
            int expected_size = is_64bit ? 0x60 : 0x44;
            if (expected_size != BitConverter.ToInt32(rdata, ofs - 24))
            {
                continue;
            }

            long ptr;
            if (is_64bit)
            {
                ptr = BitConverter.ToInt64(rdata, ofs + 20);
            }
            else
            {
                ptr = BitConverter.ToInt32(rdata, ofs + 20);
            }

            // No dispatch table, likely to be a RPC_CLIENT_INTERFACE.
            if (ptr == 0 && !return_clients)
            {
                continue;
            }

            yield return new RpcOffset(ofs + sect.RelativeVirtualAddress - 24, ptr == 0);
        }
    }

    private static ISymbolResolver? CreateSymbolResolver(ImageFile image_file, string dbghelp_path, 
        string symbol_path, RpcServerParserFlags flags)
    {
        if (flags.HasFlagSet(RpcServerParserFlags.IgnoreSymbols) || !NtObjectUtils.IsWindows)
        {
            return null;
        }

        if (image_file.MappedAsImage)
        {
            SymbolResolverFlags symbol_flags = flags.HasFlagSet(RpcServerParserFlags.SymSrvFallback) ? SymbolResolverFlags.SymSrvFallback : SymbolResolverFlags.None;
            symbol_flags |= SymbolResolverFlags.DisableExportSymbols;
            return SymbolResolver.Create(NtProcess.Current, dbghelp_path, symbol_path, symbol_flags, null);
        }
        return SymbolResolver.Create(image_file, symbol_path);
    }

    private IEnumerable<RpcEndpoint> GetEndpoints()
    {
        try
        {
            return RpcEndpointMapper.QueryEndpointsForInterface(null, Server.InterfaceId.Uuid);
        }
        catch
        {
            return Array.Empty<RpcEndpoint>();
        }
    }

    #endregion
}
