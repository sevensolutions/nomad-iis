﻿//  Copyright 2021 Google Inc. All Rights Reserved.
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

using NtCoreLib.Ndr.Marshal;
using NtCoreLib.Ndr.Rpc;
using NtCoreLib.Win32.Rpc.Transport.PDU;
using NtCoreLib.Win32.Security.Authentication;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace NtCoreLib.Win32.Rpc.Transport;

/// <summary>
/// Base class for a DCE/RPC connected client transport. This implements the common functions
/// of the DCE/RPC specs for connected network based RPC transports.
/// </summary>
public abstract class RpcConnectedClientTransport : IRpcClientTransport
{
    #region Protected Members

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="max_recv_fragment">The initial maximum receive fragment length.</param>
    /// <param name="max_send_fragment">The initial maximum send fragment length.</param>
    /// <param name="transport_security">The security for the transport.</param>
    /// <param name="config">The transport configuration for the connection.</param>
    /// <param name="data_rep">The data representation.</param>
    protected RpcConnectedClientTransport(ushort max_recv_fragment, ushort max_send_fragment, 
        NdrDataRepresentation data_rep, RpcTransportSecurity transport_security, RpcConnectedClientTransportConfiguration config)
    {
        _max_recv_fragment = max_recv_fragment;
        _max_send_fragment = max_send_fragment;
        _data_rep = data_rep;
        _security_context = new Dictionary<int, RpcTransportSecurityContext>();
        _current_security_context = new RpcTransportSecurityContext(this, transport_security, _current_context_id++);
        _security_context[_current_security_context.ContextId] = _current_security_context;
        switch (transport_security.AuthenticationLevel)
        {
            case RpcAuthenticationLevel.PacketIntegrity:
            case RpcAuthenticationLevel.PacketPrivacy:
                _auth_data_required = true;
                break;
        }

        if (config != null && config.DisableBindTimeFeatureNegotiation)
        {
            _bind_time_features = BindTimeFeatureNegotiation.None;
        }
        _assoc_group_id = config?.AssociationGroupId ?? 0;
        _transfer_syntax_bind = config?.TransferSyntax ?? RpcClientTransportTransferSyntax.Dce;
        if (_transfer_syntax_bind == RpcClientTransportTransferSyntax.Ndr64)
        {
            throw new ArgumentException("Transport doesn't support NDR64.");
        }
    }

    /// <summary>
    /// Read the next fragment from the transport.
    /// </summary>
    /// <param name="max_recv_fragment">The maximum receive fragment length.</param>
    /// <returns>The read fragment.</returns>
    protected abstract byte[] ReadFragment(int max_recv_fragment);

    /// <summary>
    /// Write the fragment to the transport.
    /// </summary>
    /// <param name="fragment">The fragment to write.</param>
    /// <returns>True if successfully wrote the fragment.</returns>
    protected abstract bool WriteFragment(byte[] fragment);
    #endregion

    #region Private Members
    private readonly NdrDataRepresentation _data_rep;
    private readonly Dictionary<int, RpcTransportSecurityContext> _security_context;
    private readonly RpcClientTransportTransferSyntax _transfer_syntax_bind;
    private RpcTransportSecurityContext _current_security_context;
    private int _current_context_id;
    private ushort _max_recv_fragment;
    private ushort _max_send_fragment;
    private int _assoc_group_id;
    private int _recv_sequence_no;
    private int _send_sequence_no;
    private BindTimeFeatureNegotiation? _bind_time_features;
    private bool _transport_bound;
    private RpcSyntaxIdentifier _interface_id;
    private RpcSyntaxIdentifier _transfer_syntax;
    private bool _auth_data_required;

    private PDUBase CheckFault(PDUBase pdu)
    {
        if (pdu is PDUShutdown)
        {
            Dispose();
            throw new RpcTransportException("Forced connection shutdown.");
        }

        if (pdu is PDUFault fault)
        {
            throw new RpcFaultException(fault);
        }

        return pdu;
    }

    private RpcTransportSecurityContext GetContext(int context_id)
    {
        if (!_security_context.TryGetValue(context_id, out RpcTransportSecurityContext context))
        {
            throw new RpcTransportException($"Invalid security context ID - {context_id}.");
        }
        return context;
    }

    private Tuple<PDUBase, AuthData> SendReceivePDU(int call_id, PDUBase send_pdu, byte[] auth_data, 
        bool receive_pdu, RpcTransportSecurityContext security_context)
    {
        try
        {
            int trailing_auth_length = auth_data.Length > 0 ? auth_data.Length + AuthData.PDU_AUTH_DATA_HEADER_SIZE : 0;

            PDUHeader pdu_header = new()
            {
                MajorVersion = PDUHeader.RPC_VERSION_MAJOR,
                MinorVersion = PDUHeader.RPC_VERSION_MINOR,
                DataRep = _data_rep,
                CallId = CallId,
                Type = send_pdu.PDUType,
                Flags = PDUFlags.LastFrag | PDUFlags.FirstFrag,
                AuthLength = checked((ushort)auth_data.Length)
            };

            byte[] pdu_data = send_pdu.ToArray();
            int pdu_data_length = pdu_data.Length + PDUHeader.PDU_HEADER_SIZE;
            int auth_padding = 0;
            if (auth_data.Length > 0 && (pdu_data_length & 15) != 0 && send_pdu.PDUType != PDUType.Auth3)
            {
                auth_padding = 16 - (pdu_data_length & 15);
            }

            pdu_header.FragmentLength = checked((ushort)(pdu_data.Length + PDUHeader.PDU_HEADER_SIZE + trailing_auth_length + auth_padding));
            MemoryStream send_stm = new();
            BinaryWriter writer = new(send_stm);
            pdu_header.Write(writer);
            writer.Write(pdu_data);
            if (auth_data.Length > 0)
            {
                writer.Write(new byte[auth_padding]);
                new AuthData(security_context.TransportSecurity.AuthenticationType, security_context.TransportSecurity.AuthenticationLevel, 
                    auth_padding, security_context.ContextId, auth_data).Write(writer, auth_padding);
            }
            byte[] fragment = send_stm.ToArray();
            RpcTransportUtils.DumpBuffer(TraceFlags, RpcTransportTraceFlags.Transport, $"{GetType().Name} Send Buffer", fragment);
            if (!WriteFragment(fragment))
                throw new RpcTransportException("Failed to write out PDU buffer.");
            _send_sequence_no++;
            if (!receive_pdu)
                return null;

            var pdu = ReadPDU(0);
            var curr_header = pdu.Item1;
            if (!curr_header.Flags.HasFlagAllSet(PDUFlags.LastFrag | PDUFlags.FirstFrag))
            {
                throw new RpcTransportException($"Invalid PDU flags {curr_header.Flags}.");
            }

            if (curr_header.CallId != call_id)
            {
                throw new RpcTransportException($"Mismatching call ID - {curr_header.CallId} should be {call_id}.");
            }

            if (pdu.Item3.IsValid && pdu.Item3.ContextId != security_context.ContextId)
            {
                throw new RpcTransportException($"Mismatching context ID - {pdu.Item3.ContextId} should be {security_context.ContextId}.");
            }

            _recv_sequence_no++;

            return Tuple.Create(CheckFault(curr_header.ToPDU(pdu.Item2)), pdu.Item3);
        }
        catch (EndOfStreamException)
        {
            throw new RpcTransportException("End of stream.");
        }
    }

    private Tuple<PDUHeader, byte[], AuthData> ReadPDU(int frag_count)
    {
        byte[] buffer = ReadFragment(_max_recv_fragment);
        RpcTransportUtils.DumpBuffer(TraceFlags, RpcTransportTraceFlags.Transport, $"{GetType().Name} Receive Buffer - Fragment {frag_count}", buffer);
        MemoryStream stm = new(buffer);
        BinaryReader reader = new(stm);
        PDUHeader header = PDUHeader.Read(reader);
        NdrUnmarshalBuffer.CheckDataRepresentation(header.DataRep);
        AuthData auth_data = new();
        int auth_trailing_length = header.AuthLength > 0 ? header.AuthLength + AuthData.PDU_AUTH_DATA_HEADER_SIZE : 0;
        byte[] data = reader.ReadAllBytes(header.FragmentLength - PDUHeader.PDU_HEADER_SIZE - auth_trailing_length);
        if (auth_trailing_length > 0)
        {
            stm.Seek(header.FragmentLength - auth_trailing_length, SeekOrigin.Begin);
            auth_data = AuthData.Read(reader, header.AuthLength);
        }
        return Tuple.Create(header, data, auth_data);
    }

    private byte[] SendReceiveRequestPDU(int proc_num, Guid? objuuid, byte[] stub_data, RpcTransportSecurityContext security_context)
    {
        try
        {
            CallId++;
            PDURequest request_pdu = new()
            {
                OpNum = (short)proc_num,
                ObjectUUID = objuuid ?? Guid.Empty
            };

            int max_fragment = _max_send_fragment - request_pdu.HeaderLength;
            int auth_data_length = 0;

            if (_auth_data_required)
            {
                auth_data_length = security_context.AuthDataLength;
                max_fragment -= (auth_data_length + AuthData.PDU_AUTH_DATA_HEADER_SIZE);
                max_fragment &= ~0xF;
            }

            List<byte[]> fragments = PDURequest.DoFragment(stub_data, max_fragment);
            for (int i = 0; i < fragments.Count; ++i)
            {
                PDUHeader pdu_header = new()
                {
                    MajorVersion = PDUHeader.RPC_VERSION_MAJOR,
                    MinorVersion = PDUHeader.RPC_VERSION_MINOR,
                    DataRep = _data_rep,
                    CallId = CallId,
                    Type = PDUType.Request
                };

                if (i == 0)
                {
                    pdu_header.Flags |= PDUFlags.FirstFrag;
                }
                if (i == fragments.Count - 1)
                {
                    pdu_header.Flags |= PDUFlags.LastFrag;
                }

                byte[] stub_fragment = fragments[i];
                byte[] auth_data = new byte[0];
                byte[] header = request_pdu.ToArray(pdu_header, stub_fragment.Length, 0);
                if (_auth_data_required)
                {
                    int auth_data_padding = 0;
                    int auth_trailing_size = (header.Length + stub_fragment.Length + AuthData.PDU_AUTH_DATA_HEADER_SIZE) & 0xF;
                    if (auth_trailing_size != 0)
                    {
                        auth_data_padding = 16 - auth_trailing_size;
                        Array.Resize(ref stub_fragment, stub_fragment.Length + auth_data_padding);
                    }

                    header = request_pdu.ToArray(pdu_header, stub_fragment.Length + AuthData.PDU_AUTH_DATA_HEADER_SIZE, auth_data_length);
                    auth_data = security_context.ProtectPDU(header, ref stub_fragment, auth_data_padding, _send_sequence_no);
                }

                MemoryStream send_stm = new();
                BinaryWriter writer = new(send_stm);
                writer.Write(header);
                writer.Write(stub_fragment);
                writer.Write(auth_data);
                byte[] fragment = send_stm.ToArray();
                string name = fragments.Count == 1 ? $"{GetType().Name} Send Buffer" : $"{GetType().Name} Send Buffer - Fragment {i}";
                RpcTransportUtils.DumpBuffer(TraceFlags, RpcTransportTraceFlags.Transport, name, fragment);
                if (!WriteFragment(fragment))
                    throw new RpcTransportException("Failed to write out PDU buffer.");
                _send_sequence_no++;
            }

            MemoryStream recv_stm = new();
            PDUHeader curr_header = new();
            int frag_count = 0;
            while ((curr_header.Flags & PDUFlags.LastFrag) == 0)
            {
                var pdu = ReadPDU(frag_count++);
                curr_header = pdu.Item1;
                AuthData auth_data = pdu.Item3;
                if (curr_header.CallId != CallId)
                {
                    throw new RpcTransportException($"Mismatching call ID - {curr_header.CallId} should be {CallId}.");
                }

                if (auth_data.IsValid && auth_data.ContextId != security_context.ContextId)
                {
                    security_context = GetContext(auth_data.ContextId);
                }

                var recv_pdu = CheckFault(curr_header.ToPDU(pdu.Item2));
                if (recv_pdu is PDUResponse resp_pdu)
                {
                    byte[] resp_stub_data = _auth_data_required ? security_context.UnprotectPDU(resp_pdu.ToArray(curr_header), 
                        resp_pdu.StubData, auth_data, _recv_sequence_no) : resp_pdu.StubData;
                    _recv_sequence_no++;
                    recv_stm.Write(resp_stub_data, 0, resp_stub_data.Length);
                }
                else
                {
                    throw new RpcTransportException($"Unexpected {recv_pdu.PDUType} PDU from server.");
                }
            }

            return recv_stm.ToArray();
        }
        catch (EndOfStreamException)
        {
            throw new RpcTransportException("End of stream.");
        }
    }

    private void BindNoAuth()
    {
        PDUBind bind_pdu = new(_max_send_fragment, _max_recv_fragment, _assoc_group_id, false);
        bind_pdu.Elements.Add(new ContextElement(_interface_id, _transfer_syntax));
        var recv_pdu = SendReceivePDU(++CallId, bind_pdu, new byte[0], true, _current_security_context).Item1;
        if (recv_pdu is PDUBindAck bind_ack)
        {
            if (bind_ack.ResultList.Count != 1 || bind_ack.ResultList[0].Result != PresentationResultType.Acceptance)
            {
                throw new RpcTransportException($"Bind to {_interface_id} was rejected.");
            }

            _max_recv_fragment = bind_ack.MaxRecvFrag;
            _max_send_fragment = bind_ack.MaxXmitFrag;
            _assoc_group_id = bind_ack.AssocGroupId;
        }
        else if (recv_pdu is PDUBindNack bind_nack)
        {
            throw new RpcTransportException($"Bind NACK returned with rejection reason {bind_nack.RejectionReason}");
        }
        else
        {
            throw new RpcTransportException($"Unexpected {recv_pdu.PDUType} PDU from server.");
        }
    }

    private void BindAuth(bool alter_context, RpcTransportSecurityContext security_context)
    {
        // 8 should be more than enough legs to complete authentication.
        int max_legs = security_context.MaxAuthLegs;
        int call_id = ++CallId;
        int count = 0;

        while (count++ < max_legs)
        {
            PDUBind bind_pdu = new(_max_send_fragment, _max_recv_fragment, _assoc_group_id, alter_context);

            bind_pdu.Elements.Add(new ContextElement(_interface_id, _transfer_syntax));
            if (!_bind_time_features.HasValue)
            {
                _bind_time_features = BindTimeFeatureNegotiation.None;
                bind_pdu.Elements.Add(new ContextElement(_interface_id, 
                    BindTimeFeatureNegotiation.SecurityContextMultiplexingSupported));
            }

            var recv = SendReceivePDU(call_id, bind_pdu, security_context.AuthContext.Token.ToArray(), true, security_context);
            if (recv.Item1 is PDUBindAck bind_ack)
            {
                if (bind_ack.ResultList.Count < 1 || bind_ack.ResultList[0].Result != PresentationResultType.Acceptance)
                {
                    throw new RpcTransportException($"Bind to {_interface_id} was rejected.");
                }

                if (bind_ack.ResultList.Count == 2)
                {
                    _bind_time_features = bind_ack.ResultList[1].BindTimeFeature;
                }

                if (!alter_context)
                {
                    // Only capture values from the BindAck.
                    _max_recv_fragment = bind_ack.MaxRecvFrag;
                    _max_send_fragment = bind_ack.MaxXmitFrag;
                    _assoc_group_id = bind_ack.AssocGroupId;
                    alter_context = true;
                }

                if (recv.Item2.Data == null || recv.Item2.Data.Length == 0)
                {
                    // No auth, assume success.
                    break;
                }

                security_context.AuthContext.Continue(new AuthenticationToken(recv.Item2.Data));
                if (security_context.AuthContext.Done)
                {
                    byte[] token = security_context.AuthContext.Token.ToArray();
                    if (token.Length == 0)
                        break;
                    // If we still have an NTLM token to complete then send as an Auth3 PDU.
                    if (security_context.TransportSecurity.AuthenticationType == RpcAuthenticationType.WinNT)
                    {
                        SendReceivePDU(call_id, new PDUAuth3(), token, false, security_context);
                        break;
                    }
                }
            }
            else if (recv.Item1 is PDUBindNack bind_nack)
            {
                throw new RpcTransportException($"Bind NACK returned with rejection reason {bind_nack.RejectionReason}");
            }
            else
            {
                throw new RpcTransportException($"Unexpected {recv.Item1.PDUType} PDU from server.");
            }
        }

        if (!security_context.AuthContext.Done)
        {
            throw new RpcTransportException("Failed to complete the client authentication.");
        }
        security_context.SetNegotiatedAuthType();
    }

    #endregion

    #region IRpcClientTransport implementation.

    /// <summary>
    /// Get whether the client is connected or not.
    /// </summary>
    public abstract bool Connected { get; }

    /// <summary>
    /// Get the endpoint the client is connected to.
    /// </summary>
    public abstract string Endpoint { get; }

    /// <summary>
    /// Get the transport protocol sequence.
    /// </summary>
    public abstract string ProtocolSequence { get; }

    /// <summary>
    /// Get information about the server process, if known.
    /// </summary>
    public virtual RpcServerProcessInformation ServerProcess => throw new NotImplementedException();

    /// <summary>
    /// Get whether the client has been authenticated.
    /// </summary>
    public bool Authenticated => _current_security_context.Authenticated;

    /// <summary>
    /// Get the transports authentication type.
    /// </summary>
    public RpcAuthenticationType AuthenticationType => Authenticated ? _current_security_context.NegotiatedAuthType : RpcAuthenticationType.None;

    /// <summary>
    /// Get the transports authentication level.
    /// </summary>
    public RpcAuthenticationLevel AuthenticationLevel => Authenticated ? _current_security_context.TransportSecurity.AuthenticationLevel : RpcAuthenticationLevel.None;

    /// <summary>
    /// Get the transport authentication context.
    /// </summary>
    public IClientAuthenticationContext AuthenticationContext => _current_security_context.AuthContext;

    /// <summary>
    /// Indicates if this connection supported multiple security context.
    /// </summary>
    public bool SupportsMultipleSecurityContexts => (_bind_time_features ??
        BindTimeFeatureNegotiation.None).HasFlagSet(BindTimeFeatureNegotiation.SecurityContextMultiplexingSupported);

    /// <summary>
    /// Get the list of negotiated security context.
    /// </summary>
    public IReadOnlyList<RpcTransportSecurityContext> SecurityContext => _security_context.Values.ToList().AsReadOnly();

    /// <summary>
    /// Get or set the current security context.
    /// </summary>
    public RpcTransportSecurityContext CurrentSecurityContext
    {
        get => _current_security_context;
        set => _current_security_context = value?.Check(this) ?? throw new ArgumentNullException(nameof(value));
    }

    /// <summary>
    /// Get the current Call ID.
    /// </summary>
    public int CallId { get; private set; }

    /// <summary>
    /// Get maximum receive fragment.
    /// </summary>
    public int MaxRecvFragment => _max_recv_fragment;

    /// <summary>
    /// Get maximum send fragment.
    /// </summary>
    public int MaxSendFragment => _max_send_fragment;

    /// <summary>
    /// Get association group ID.
    /// </summary>
    public int AssociationGroupId => _assoc_group_id;

    /// <summary>
    /// Get whether the transport supports synchronous pipes.
    /// </summary>
    public bool SupportsSynchronousPipes => true;

    /// <summary>
    /// Bind the RPC transport to a specified interface.
    /// </summary>
    /// <param name="interface_id">The interface ID to bind to.</param>
    public void Bind(RpcSyntaxIdentifier interface_id)
    {
        if (_transport_bound)
        {
            throw new InvalidOperationException("Transport is already bound to an interface.");
        }

        _transport_bound = true;
        _interface_id = interface_id;
        _transfer_syntax = RpcSyntaxIdentifier.DCETransferSyntax;

        if (_current_security_context.TransportSecurity.AuthenticationLevel == RpcAuthenticationLevel.None)
        {
            BindNoAuth();
        }
        else
        {
            BindAuth(false, _current_security_context);
        }
    }

    /// <summary>
    /// Create a NDR marshal buffer for this transport.
    /// </summary>
    /// <returns>The NDR marshal buffer.</returns>
    public INdrMarshalBuffer CreateMarshalBuffer()
    {
        return new NdrMarshalBuffer(default, _transfer_syntax == RpcSyntaxIdentifier.NDR64TransferSyntax);
    }

    /// <summary>
    /// Add and authenticate a new security context.
    /// </summary>
    /// <param name="transport_security">The transport security for the context.</param>
    /// <returns>The created security context.</returns>
    public RpcTransportSecurityContext AddSecurityContext(RpcTransportSecurity transport_security)
    {
        if (!SupportsMultipleSecurityContexts)
            throw new InvalidOperationException("Transport doesn't support multiple security context.");
        switch (transport_security.AuthenticationLevel)
        {
            case RpcAuthenticationLevel.Connect:
            case RpcAuthenticationLevel.PacketIntegrity:
            case RpcAuthenticationLevel.PacketPrivacy:
                break;
            default:
                throw new ArgumentException("Can only create a new context with specific authentication levels.");
        }

        if (!_transport_bound)
            throw new InvalidOperationException("Transport hasn't been bound yet.");
        var context = new RpcTransportSecurityContext(this,
                transport_security, _current_context_id++);
        try
        {
            BindAuth(true, context);
            _auth_data_required = true;
            return _security_context[context.ContextId] = context;
        }
        catch
        {
            context.AuthContext?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Send and receive an RPC message.
    /// </summary>
    /// <param name="proc_num">The procedure number.</param>
    /// <param name="objuuid">The object UUID for the call.</param>
    /// <param name="ndr_buffer">Marshal NDR buffer for the call.</param>
    /// <returns>Client response from the send.</returns>
    public INdrUnmarshalBuffer SendReceive(int proc_num, Guid? objuuid, INdrMarshalBuffer ndr_buffer)
    {
        NdrUnmarshalBuffer.CheckDataRepresentation(ndr_buffer.DataRepresentation);
        return new NdrUnmarshalBuffer(SendReceiveRequestPDU(proc_num, objuuid,
            ndr_buffer.ToArray(), _current_security_context), 
            ndr64: _transfer_syntax == RpcSyntaxIdentifier.NDR64TransferSyntax);
    }

    /// <summary>
    /// Disconnect the transport.
    /// </summary>
    public abstract void Disconnect();

    /// <summary>
    /// Specify flags to various aspects of this transport.
    /// </summary>
    public RpcTransportTraceFlags TraceFlags { get; set; }

    /// <summary>
    /// The transfer syntax this transport is using.
    /// </summary>
    public RpcSyntaxIdentifier TransferSyntax => _transfer_syntax;
    #endregion

    #region IDisposable implementation.
    /// <summary>
    /// Dispose the transport.
    /// </summary>
    public virtual void Dispose()
    {
        foreach (var context in _security_context.Values)
        {
            context.AuthContext?.Dispose();
        }
    }
    #endregion
}
