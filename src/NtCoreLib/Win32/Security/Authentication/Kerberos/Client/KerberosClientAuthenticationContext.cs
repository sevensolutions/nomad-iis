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

using NtCoreLib.Net;
using NtCoreLib.Utilities.ASN1;
using NtCoreLib.Utilities.Security.Cryptography;
using NtCoreLib.Win32.Security.Authentication.Kerberos.Builder;
using NtCoreLib.Win32.Security.Buffers;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Client;

/// <summary>
/// A basic implementation of a client authentication context using an existing Keberos credential.
/// </summary>
public sealed class KerberosClientAuthenticationContext : IClientAuthenticationContext
{
    #region Private Members
    private readonly KerberosExternalTicket _ticket;
    private readonly KerberosChecksumGSSApiFlags _gssapi_flags;
    private readonly KerberosAuthenticationKey _subkey;
    private readonly InitializeContextReqFlags _request_attributes;
    private readonly KerberosClientAuthenticationContextConfig _config;
    private KerberosAuthenticationKey _acceptor_subkey;
    private long _send_sequence_number;
    private long _recv_sequence_number;

    private static KerberosChecksumGSSApiFlags ConvertRequestToGSSAPI(InitializeContextReqFlags request_attributes)
    {
        KerberosChecksumGSSApiFlags ret = KerberosChecksumGSSApiFlags.None;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Confidentiality))
            ret |= KerberosChecksumGSSApiFlags.Confidentiality | KerberosChecksumGSSApiFlags.Integrity
                | KerberosChecksumGSSApiFlags.Replay | KerberosChecksumGSSApiFlags.Sequence;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.ExtendedError))
            ret |= KerberosChecksumGSSApiFlags.ExtendedError;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.MutualAuth))
            ret |= KerberosChecksumGSSApiFlags.Mutual;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.ReplayDetect))
            ret |= KerberosChecksumGSSApiFlags.Replay | KerberosChecksumGSSApiFlags.Integrity;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.SequenceDetect))
            ret |= KerberosChecksumGSSApiFlags.Sequence | KerberosChecksumGSSApiFlags.Integrity;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Integrity))
            ret |= KerberosChecksumGSSApiFlags.Integrity;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Delegate))
            ret |= KerberosChecksumGSSApiFlags.Delegate;

        return ret;
    }

    private static InitializeContextRetFlags ConvertRequestToReturn(InitializeContextReqFlags request_attributes)
    {
        InitializeContextRetFlags ret = InitializeContextRetFlags.None;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Confidentiality))
            ret |= InitializeContextRetFlags.Confidentiality | InitializeContextRetFlags.Integrity
                | InitializeContextRetFlags.ReplayDetect | InitializeContextRetFlags.SequenceDetect;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.MutualAuth))
            ret |= InitializeContextRetFlags.MutualAuth;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.ReplayDetect))
            ret |= InitializeContextRetFlags.Integrity | InitializeContextRetFlags.ReplayDetect;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.SequenceDetect))
            ret |= InitializeContextRetFlags.Integrity | InitializeContextRetFlags.SequenceDetect;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Delegate))
            ret |= InitializeContextRetFlags.Delegate;
        if (request_attributes.HasFlagSet(InitializeContextReqFlags.Integrity))
            ret |= InitializeContextRetFlags.Integrity;
        return ret;
    }

    private bool UseSequenceNumber()
    {
        return _gssapi_flags.HasFlagSet(KerberosChecksumGSSApiFlags.Replay) || _gssapi_flags.HasFlagSet(KerberosChecksumGSSApiFlags.Sequence);
    }

    private const int GSSAPI_HEADER_SIZE = 13;
    private const int SECURITY_HEADER_SIZE = 16;

    private byte[] GenerateAESChecksumHeader()
    {
        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        writer.Write(new byte[] {
            0x04, 0x04, // TOK_ID
            0x00,       // Flags
            0xFF, 0xFF, 0xFF, 0xFF, 0xFF // Filler
        });

        if (UseSequenceNumber())
        {
            writer.Write(_send_sequence_number++.SwapEndian());
        }
        else
        {
            writer.Write(0L);
        }
        return stm.ToArray();
    }

    private byte[] GenerateWrapHeader()
    {
        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        writer.Write(new byte[] {
            0x05, 0x04, // TOK_ID
            0x02,       // Flags
            0xFF,       // Filler
            0, 0,       // Extra Count
            0, 0        // Right rotation count
        });

        if (UseSequenceNumber())
        {
            writer.Write(_send_sequence_number++.SwapEndian());
        }
        else
        {
            writer.Write(0L);
        }
        return stm.ToArray();
    }

    private void PopulateBuffers(List<ISecurityBufferInOut> data_buffers, byte[] data, int offset, int length)
    {
        MemoryStream stm = new(data, offset, length);
        BinaryReader reader = new(stm);
        foreach (var buffer in data_buffers)
        {
            buffer.Update(SecurityBufferType.Data, reader.ReadAllBytes(buffer.Size));
        }
    }

    private void EncryptMessageNoSignatureAES(IEnumerable<SecurityBuffer> messages)
    {
        // The message buffer gets the last X bytes of the encrypted data. The rest goes into the signature.
        // To make it simpler you could rotate the bytes, but that isn't really necessary for us as we're not putting that much effort in.
        List<ISecurityBufferInOut> data_buffers = messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly)
                                                    .OfType<ISecurityBufferInOut>().ToList();
        if (data_buffers.Count == 0)
            throw new ArgumentException("Must specify a buffer to encrypt.");
        ISecurityBufferOut token_buffer = messages.Where(b => b.Type == SecurityBufferType.Token && !b.ReadOnly)
                                        .OfType<ISecurityBufferOut>().FirstOrDefault();
        if (token_buffer == null)
            throw new ArgumentException("Must specify a buffer for the token signature.");

        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        foreach (var buffer in data_buffers)
        {
            writer.Write(buffer.ToArray());
        }
        byte[] header = GenerateWrapHeader();
        writer.Write(header);

        byte[] cipher_text = _subkey.Encrypt(stm.ToArray(), KerberosKeyUsage.InitiatorSeal);
        stm = new MemoryStream(cipher_text);
        BinaryReader reader = new(stm);
        byte[] confounder = reader.ReadAllBytes(16);
        foreach (var buffer in data_buffers)
        {
            byte[] data = reader.ReadAllBytes(buffer.Size);
            buffer.Update(SecurityBufferType.Data, data);
        }

        stm = new MemoryStream();
        writer = new BinaryWriter(stm);
        header[7] = 0x1C;
        writer.Write(header);
        writer.Write(reader.ReadToEnd());
        writer.Write(confounder);
        token_buffer.Update(SecurityBufferType.Token, stm.ToArray());
    }

    private void EncryptMessageNoSignatureRC4(IEnumerable<SecurityBuffer> messages)
    {
        List<ISecurityBufferInOut> data_buffers = messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly)
                                                    .OfType<ISecurityBufferInOut>().ToList();
        if (data_buffers.Count == 0)
            throw new ArgumentException("Must specify a buffer to encrypt.");

        ISecurityBufferOut token_buffer = messages.Where(b => b.Type == SecurityBufferType.Token && !b.ReadOnly)
                            .OfType<ISecurityBufferOut>().FirstOrDefault();
        if (token_buffer == null)
            throw new ArgumentException("Must specify a buffer for the token signature.");

        MemoryStream stm = new();
        BinaryWriter writer = new(stm);

        byte[] header = GenerateRC4Header(true);
        writer.Write(header);
        byte[] confounder = new byte[8];
        new Random().NextBytes(confounder);
        writer.Write(confounder);
        foreach (var buffer in data_buffers)
        {
            writer.Write(buffer.ToArray());
        }

        byte[] data_to_hash = stm.ToArray();

        byte[] hash = _subkey.ComputeHash(data_to_hash, KerberosKeyUsage.KrbPriv);
        Array.Resize(ref hash, 8);

        int seq_no = (int)_send_sequence_number;
        _send_sequence_number++;

        byte[] seq_bytes = BitConverter.GetBytes(seq_no.SwapEndian());

        byte[] enc_data = EncryptRC4Plain(true, seq_bytes, data_to_hash, header.Length, data_to_hash.Length - header.Length);

        Array.Resize(ref seq_bytes, 8);

        stm.SetLength(0);
        writer.Write(header);
        writer.Write(EncryptRC4Plain(false, hash, seq_bytes));
        writer.Write(hash);
        writer.Write(enc_data);

        byte[] token = GSSAPIUtils.Wrap(OIDValues.KERBEROS, stm.ToArray());
        Array.Resize(ref token, token.Length - enc_data.Length + confounder.Length);
        token_buffer.Update(SecurityBufferType.Token, token);
        PopulateBuffers(data_buffers, enc_data, confounder.Length, enc_data.Length - confounder.Length);
    }

    private KerberosAuthenticationKey GetSubKey(bool acceptor)
    {
        if (!acceptor)
            return _subkey;
        return _acceptor_subkey ?? throw new InvalidOperationException("Not negotiated acceptor key.");
    }

    private void DecryptMessageNoSignatureAES(IEnumerable<SecurityBuffer> messages)
    {
        List<ISecurityBufferInOut> data_buffers = messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly)
                                                    .OfType<ISecurityBufferInOut>().ToList();
        if (data_buffers.Count == 0)
            throw new ArgumentException("Must specify a buffer to encrypt.");
        ISecurityBufferIn token_buffer = messages.Where(b => b.Type == SecurityBufferType.Token)
                                        .OfType<ISecurityBufferIn>().FirstOrDefault();
        if (token_buffer == null)
            throw new ArgumentException("Must specify a buffer for the token signature.");

        byte[] signature = token_buffer.ToArray();

        if (signature.Length < SecurityTrailerSize)
        {
            throw new ArgumentException("Encryption token is too small.");
        }

        Array.Resize(ref signature, SecurityTrailerSize);

        ulong header = BitConverter.ToUInt64(signature, 0);
        bool use_acceptor_key = (header & 0x40000) != 0;
        header &= ~0x40000UL;

        if (header != 0x1C000000FF030405UL)
        {
            throw new ArgumentException("Invalid signature buffer header.");
        }

        if (UseSequenceNumber())
        {
            if (BitConverter.ToInt64(signature, 8) != _recv_sequence_number++.SwapEndian())
            {
                throw new ArgumentException("Invalid sequence number.");
            }
        }

        MemoryStream stm = new();
        // Confounder.
        stm.Write(signature, signature.Length - 16, 16);
        foreach (var buffer in data_buffers)
        {
            byte[] ba = buffer.ToArray();
            stm.Write(ba, 0, ba.Length);
        }
        stm.Write(signature, 16, signature.Length - 32);

        byte[] plain_text = GetSubKey(use_acceptor_key).Decrypt(stm.ToArray(), KerberosKeyUsage.AcceptorSeal);
        stm = new MemoryStream(plain_text);
        BinaryReader reader = new(stm);
        foreach (var buffer in data_buffers)
        {
            buffer.Update(SecurityBufferType.Data, reader.ReadAllBytes(buffer.Size));
        }

        _ = reader.ReadAllBytes(16);
        // We should perhaps verify the trailing header to be sure?
    }

    private void DecryptMessageNoSignatureRC4(IEnumerable<SecurityBuffer> messages)
    {
        List<ISecurityBufferInOut> data_buffers = messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly)
                                                    .OfType<ISecurityBufferInOut>().ToList();
        if (data_buffers.Count == 0)
            throw new ArgumentException("Must specify a buffer to encrypt.");
        ISecurityBufferIn token_buffer = messages.Where(b => b.Type == SecurityBufferType.Token)
                                        .OfType<ISecurityBufferIn>().FirstOrDefault();
        if (token_buffer == null)
            throw new ArgumentException("Must specify a buffer for the token signature.");

        byte[] signature = token_buffer.ToArray();

        if (signature.Length < SecurityTrailerSize)
        {
            throw new ArgumentException("Encryption token is too small.");
        }

        // The APIs seem to include the encrypted data in the GSS-API blob, so add that extra data length.
        Array.Resize(ref signature, SecurityTrailerSize + data_buffers.Sum(b => b.Size));

        if (!GSSAPIUtils.TryParse(signature, out signature, out string oid))
        {
            throw new ArgumentException("Signature not a GSS-API token.");
        }

        if (oid != OIDValues.KERBEROS)
        {
            throw new ArgumentException("Signature has invalid OID.");
        }

        byte[] header = new byte[8];
        Buffer.BlockCopy(signature, 0, header, 0, 8);
        if (!NtObjectUtils.EqualByteArray(header, GenerateRC4Header(true)))
        {
            throw new ArgumentException("Signature has invalid header.");
        }

        byte[] seq_bytes = new byte[8];
        Buffer.BlockCopy(signature, 8, seq_bytes, 0, 8);
        byte[] hash = new byte[8];
        Buffer.BlockCopy(signature, 16, hash, 0, 8);
        byte[] confounder = new byte[8];
        Buffer.BlockCopy(signature, 24, confounder, 0, 8);

        seq_bytes = EncryptRC4Plain(false, hash, seq_bytes);
        Array.Resize(ref seq_bytes, 4);
        int seq_no = BitConverter.ToInt32(seq_bytes, 0).SwapEndian();

        if (UseSequenceNumber())
        {
            if (seq_no != (int)_recv_sequence_number++)
            {
                throw new ArgumentException("Invalid sequence number.");
            }
        }

        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        writer.Write(confounder);
        foreach (var buffer in data_buffers)
        {
            writer.Write(buffer.ToArray());
        }

        byte[] dec_data = EncryptRC4Plain(true, seq_bytes, stm.ToArray());
        stm.SetLength(0);
        writer.Write(header);
        writer.Write(dec_data);

        byte[] data_to_hash = stm.ToArray();

        byte[] check_hash = _subkey.ComputeHash(data_to_hash, KerberosKeyUsage.KrbPriv);
        if (!NtObjectUtils.EqualByteArray(check_hash, hash, 8))
        {
            throw new ArgumentException("Invalid checksum.");
        }

        stm = new MemoryStream(dec_data, 8, dec_data.Length - 8);
        BinaryReader reader = new(stm);
        foreach (var buffer in data_buffers)
        {
            buffer.Update(SecurityBufferType.Data, reader.ReadAllBytes(buffer.Size));
        }
    }

    private byte[] MakeSignatureAES(IEnumerable<SecurityBuffer> messages, int sequence_no)
    {
        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        foreach (var buffer in messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly))
        {
            writer.Write(buffer.ToArray());
        }

        byte[] header = GenerateAESChecksumHeader();
        writer.Write(header);
        byte[] hash = _subkey.ComputeHash(stm.ToArray(), KerberosKeyUsage.InitiatorSign);
        byte[] ret = new byte[header.Length + hash.Length];
        Buffer.BlockCopy(header, 0, ret, 0, header.Length);
        Buffer.BlockCopy(hash, 0, ret, header.Length, hash.Length);
        return ret;
    }

    private static byte[] GenerateRC4Header(bool encrypt)
    {
        if (encrypt)
        {
            return new byte[] {
                0x02, 0x01, // TOK_ID
                0x11, 0x00,
                0x10, 0x00,
                0xFF, 0xFF
            };
        }
        else
        {
            return new byte[] {
                0x01, 0x01, // TOK_ID
                0x11, 0x00,
                0xFF, 0xFF,
                0xFF, 0xFF
            };
        }
    }

    private byte[] EncryptRC4Plain(bool local, byte[] checksum, byte[] data, int offset, int length)
    {
        byte[] key = local ? _subkey.Key.Select(b => (byte)(b ^ 0xF0)).ToArray() : _subkey.Key;

        HMACMD5 hmac = new(key);
        var tmpkey = hmac.ComputeHash(new byte[4]);
        hmac = new HMACMD5(tmpkey);
        key = hmac.ComputeHash(checksum);
        return ARC4.Transform(data, offset, length, key);
    }

    private byte[] EncryptRC4Plain(bool local, byte[] checksum, byte[] data)
    {
        return EncryptRC4Plain(local, checksum, data, 0, data.Length);
    }

    private byte[] MakeSignatureRC4(IEnumerable<SecurityBuffer> messages, int sequence_no)
    {
        MemoryStream stm = new();
        BinaryWriter writer = new(stm);

        byte[] header = GenerateRC4Header(false);
        writer.Write(header);
        foreach (var buffer in messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly))
        {
            writer.Write(buffer.ToArray());
        }

        byte[] hash = _subkey.ComputeHash(stm.ToArray(), KerberosKeyUsage.KrbSafe);
        Array.Resize(ref hash, 8);

        stm.SetLength(0);

        writer.Write(header);

        int seq_no = (int)_send_sequence_number;
        _send_sequence_number++;
        byte[] seq_bytes = BitConverter.GetBytes(seq_no.SwapEndian());
        Array.Resize(ref seq_bytes, 8);
        writer.Write(EncryptRC4Plain(false, hash, seq_bytes));
        writer.Write(hash);

        return GSSAPIUtils.Wrap(OIDValues.KERBEROS, stm.ToArray());
    }

    private bool VerifySignatureAES(IEnumerable<SecurityBuffer> messages, byte[] signature, int sequence_no)
    {
        if (signature is null)
        {
            throw new ArgumentNullException(nameof(signature));
        }

        if (signature.Length < MaxSignatureSize)
        {
            throw new ArgumentException("Signature token is too small.");
        }

        ulong header = BitConverter.ToUInt64(signature, 0);
        bool use_acceptor_key = (header & 0x40000) != 0;
        header &= ~0x40000UL;

        if (header != 0xFFFFFFFFFF010404U)
        {
            return false;
        }

        if (UseSequenceNumber())
        {
            if (BitConverter.ToInt64(signature, 8) != _recv_sequence_number++.SwapEndian())
            {
                return false;
            }
        }

        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        foreach (var buffer in messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly))
        {
            writer.Write(buffer.ToArray());
        }
        writer.Write(signature, 0, SECURITY_HEADER_SIZE);
        var subkey = GetSubKey(use_acceptor_key);
        byte[] hash = subkey.ComputeHash(stm.ToArray(), KerberosKeyUsage.AcceptorSign);
        byte[] verify_hash = new byte[subkey.ChecksumSize];
        Buffer.BlockCopy(signature, 16, verify_hash, 0, verify_hash.Length);
        return NtObjectUtils.EqualByteArray(hash, verify_hash);
    }

    private bool VerifySignatureRC4(IEnumerable<SecurityBuffer> messages, byte[] signature, int sequence_no)
    {
        if (signature is null)
        {
            throw new ArgumentNullException(nameof(signature));
        }

        if (signature.Length < MaxSignatureSize)
        {
            throw new ArgumentException("Signature token is too small.");
        }

        if (!GSSAPIUtils.TryParse(signature, out signature, out string oid))
        {
            return false;
        }

        if (oid != OIDValues.KERBEROS)
        {
            return false;
        }

        if (BitConverter.ToUInt64(signature, 0) != 0xFFFFFFFF00110101U)
        {
            return false;
        }

        MemoryStream stm = new();
        BinaryWriter writer = new(stm);
        byte[] header = GenerateRC4Header(false);
        writer.Write(header);
        foreach (var buffer in messages.Where(b => b.Type == SecurityBufferType.Data && !b.ReadOnly))
        {
            writer.Write(buffer.ToArray());
        }

        byte[] hash = _subkey.ComputeHash(stm.ToArray(), KerberosKeyUsage.KrbSafe);
        Array.Resize(ref hash, 8);

        if (UseSequenceNumber())
        {
            int seq_no = BitConverter.ToInt32(EncryptRC4Plain(false, hash, signature, 8, 8), 0).SwapEndian();
            int recv_seq_no = (int)_recv_sequence_number++;
            if (seq_no != recv_seq_no)
            {
                return false;
            }
        }

        byte[] verify_hash = new byte[8];
        Buffer.BlockCopy(signature, 16, verify_hash, 0, 8);

        return NtObjectUtils.EqualByteArray(hash, verify_hash);
    }

    private static KerberosExternalTicket GetExternalTicketFromCredentials(AuthenticationCredentials credential, string target)
    {
        if (credential is null)
        {
            return null;
        }

        if (credential is KerberosTicketCacheAuthenticationCredentials cache_creds)
        {
            return cache_creds.GetTicket(target);
        }

        if (credential is KerberosTicketAuthenticationCredentials ticket_creds)
        {
            return ticket_creds.Ticket;
        } 

        return null;
    }

    private void CreateApRequest()
    {
        if (_request_attributes.HasFlagSet(InitializeContextReqFlags.NullSession))
        {
            Token = KerberosAPRequestAuthenticationToken.Create();
            ReturnAttributes = InitializeContextRetFlags.NullSession;
            Done = true;
        }
        else
        {
            ReturnAttributes = ConvertRequestToReturn(_request_attributes);
            KerberosCredential delegate_cred = null;
            if (_gssapi_flags.HasFlagSet(KerberosChecksumGSSApiFlags.Delegate))
            {
                if (_config?.DelegationTicket == null)
                    throw new ArgumentException($"Must specify {nameof(_config.DelegationTicket)} credentials to enable delegation.", nameof(_config));
                delegate_cred = _config.DelegationTicket.Encrypt(_subkey);
            }

            bool mutual_auth_required = _gssapi_flags.HasFlagSet(KerberosChecksumGSSApiFlags.Mutual);
            byte[] channel_binding = _config?.ChannelBinding?.ComputeHash() ?? new byte[16];
            var cksum = new KerberosChecksumGSSApi(_gssapi_flags, channel_binding, 1, delegate_cred);
            int sequence_number = KerberosBuilderUtils.GetRandomNonce();
            _send_sequence_number = _recv_sequence_number = sequence_number;

            KerberosAPRequestOptions opts = KerberosAPRequestOptions.None;
            if (mutual_auth_required)
                opts |= KerberosAPRequestOptions.MutualAuthRequired;
            if (_config?.SessionKeyTicket != null || _request_attributes.HasFlagSet(InitializeContextReqFlags.UseSessionKey))
                opts |= KerberosAPRequestOptions.UseSessionKey;

            List<KerberosAuthorizationData> auth_data = _config?.AuthorizationData ?? new List<KerberosAuthorizationData>();
            var authenticator = KerberosAuthenticator.Create(_ticket.TargetDomainName, _ticket.ClientName,
                KerberosTime.Now, 0, cksum, _subkey, sequence_number, auth_data.Count > 0 ? auth_data : null);
            Token = KerberosAPRequestAuthenticationToken.Create(_ticket.Ticket,
                authenticator, opts, authenticator_key: _ticket.SessionKey);
            Done = !mutual_auth_required;
        }
    }

    #endregion

    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="credential">The credentials the context.</param>
    /// <param name="target">The target SPN to use.</param>
    /// <param name="request_attributes">Request attributes for the context.</param>
    /// <param name="config">Additional configuration for the context.</param>
    /// <param name="initialize">Specify to initialize the context.</param>
    public KerberosClientAuthenticationContext(AuthenticationCredentials credential, string target, InitializeContextReqFlags request_attributes,
        KerberosClientAuthenticationContextConfig config = null, bool initialize = true) 
        : this(GetExternalTicketFromCredentials(credential, target), request_attributes, config, initialize)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="credential">The kerberos ticket for the target.</param>
    /// <param name="request_attributes">Request attributes for the context.</param>
    /// <param name="config">Additional configuration for the context.</param>
    /// <param name="initialize">Specify to initialize the context.</param>
    public KerberosClientAuthenticationContext(KerberosCredential credential, InitializeContextReqFlags request_attributes,
        KerberosClientAuthenticationContextConfig config = null, bool initialize = true) 
        : this(credential?.ToExternalTicket(), request_attributes, config, initialize)
    {
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="ticket">The kerberos ticket for the target.</param>
    /// <param name="request_attributes">Request attributes for the context.</param>
    /// <param name="config">Additional configuration for the context.</param>
    /// <param name="initialize">Specify to initialize the context.</param>
    public KerberosClientAuthenticationContext(KerberosExternalTicket ticket, InitializeContextReqFlags request_attributes,
        KerberosClientAuthenticationContextConfig config = null, bool initialize = true)
    {
        _request_attributes = request_attributes;
        _config = config;
        _ticket = ticket;
        if (ticket is null)
        {
            if (!request_attributes.HasFlagSet(InitializeContextReqFlags.NullSession))
                throw new ArgumentNullException(nameof(ticket));
        }
        else
        {
            _ticket = ticket;
            _subkey = config?.SubKey ?? KerberosAuthenticationKey.GenerateKey(config?.SubKeyEncryptionType ?? _ticket.SessionKey.KeyEncryption);
            _gssapi_flags = ConvertRequestToGSSAPI(request_attributes);

            switch (_subkey.KeyEncryption)
            {
                case KerberosEncryptionType.ARCFOUR_HMAC_MD5:
                    MaxSignatureSize = GSSAPI_HEADER_SIZE + 24;
                    SecurityTrailerSize = GSSAPI_HEADER_SIZE + 32;
                    break;
                default:
                    MaxSignatureSize = SECURITY_HEADER_SIZE + _subkey.ChecksumSize;
                    SecurityTrailerSize = (SECURITY_HEADER_SIZE * 2) + _subkey.AdditionalEncryptionSize;
                    break;
            }
        }
        if (initialize)
        {
            ContinueInternal(null);
        }
    }

    private void ContinueInternal(byte[] token)
    {
        if (Done)
            return;

        if (Token == null)
        {
            CreateApRequest();
            return;
        }

        if (!_gssapi_flags.HasFlagSet(KerberosChecksumGSSApiFlags.Mutual))
        {
            Done = true;
            Token = new AuthenticationToken();
            return;
        }

        if (token is null)
            throw new InvalidDataException("Missing AP-REP from server.");
        var ap_rep = KerberosAuthenticationToken.Parse(token) as KerberosAPReplyAuthenticationToken;
        if (ap_rep is null)
            throw new InvalidDataException("Invalid AP-REP from server.");
        ap_rep = (KerberosAPReplyAuthenticationToken)ap_rep.Decrypt(_ticket.SessionKey);
        if (!(ap_rep.EncryptedPart is KerberosAPReplyEncryptedPart ap_rep_enc))
            throw new InvalidDataException("Couldn't decrypt AP-REP from server.");

        if (ap_rep_enc.SequenceNumber.HasValue)
            _recv_sequence_number = ap_rep_enc.SequenceNumber.Value;
        if (ap_rep_enc.SubKey != null)
            _acceptor_subkey = ap_rep_enc.SubKey;

        Done = true;
        Token = new AuthenticationToken();
    }

    #endregion

    #region IClientAuthenticationContext Implementation.
#pragma warning disable CS1591 // Missing XML comment for publicly visible type or member
    public SecPkgLastClientTokenStatus LastTokenStatus => Done ? SecPkgLastClientTokenStatus.Yes : SecPkgLastClientTokenStatus.No;

    public AuthenticationToken Token { get; private set; }

    public bool Done { get; private set; }

    public long Expiry => long.MaxValue;

    public byte[] SessionKey => Done ? _subkey.Key.CloneBytes() : throw new InvalidOperationException("Security context not completed.");

    public string PackageName => AuthenticationPackage.KERBEROS_NAME;

    public int MaxSignatureSize { get; }

    public int SecurityTrailerSize { get; }

    public X509Certificate2 LocalCertificate => null;

    public X509Certificate2 RemoteCertificate => null;

    public int StreamHeaderSize => 0;

    public int StreamTrailerSize => 0;

    public int StreamBufferCount => 0;

    public int StreamMaxMessageSize => 0;

    public int StreamBlockSize => 0;

    public InitializeContextReqFlags RequestAttributes => _request_attributes;

    public InitializeContextRetFlags ReturnAttributes { get; private set; }

    public void Continue(AuthenticationToken token)
    {
        ContinueInternal(token?.ToArray());
    }

    public void Continue(AuthenticationToken token, IEnumerable<SecurityBuffer> additional_input)
    {
        ContinueInternal(token?.ToArray());
    }

    public void Continue(AuthenticationToken token, IEnumerable<SecurityBuffer> additional_input, IEnumerable<SecurityBuffer> additional_output)
    {
        ContinueInternal(token?.ToArray());
    }

    public void Continue(IEnumerable<SecurityBuffer> input_buffers, IEnumerable<SecurityBuffer> additional_output)
    {
        var token_buffer = input_buffers?.FirstOrDefault(b => b.Type == SecurityBufferType.Token);
        ContinueInternal(token_buffer?.ToArray());
    }

    public void Continue()
    {
        ContinueInternal(null);
    }

    public byte[] DecryptMessage(EncryptedMessage message, int sequence_no)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        SecurityBuffer buffer = new SecurityBufferInOut(SecurityBufferType.Data, message.Message);
        DecryptMessage(new[] { buffer }, message.Signature, sequence_no);
        return buffer.ToArray();
    }

    public void DecryptMessage(IEnumerable<SecurityBuffer> messages, byte[] signature, int sequence_no)
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        if (signature is null)
        {
            throw new ArgumentNullException(nameof(signature));
        }

        List<SecurityBuffer> sig_buffers = new(messages);
        sig_buffers.Add(new SecurityBufferInOut(SecurityBufferType.Token | SecurityBufferType.ReadOnly, signature));
        DecryptMessageNoSignature(sig_buffers, sequence_no);
    }

    public void DecryptMessageNoSignature(IEnumerable<SecurityBuffer> messages, int sequence_no)
    {
        if (!Done)
            throw new InvalidOperationException("Security context not completed.");
        var subkey = _acceptor_subkey ?? _subkey;
        switch (subkey.KeyEncryption)
        {
            case KerberosEncryptionType.AES128_CTS_HMAC_SHA1_96:
            case KerberosEncryptionType.AES256_CTS_HMAC_SHA1_96:
                DecryptMessageNoSignatureAES(messages);
                break;
            case KerberosEncryptionType.ARCFOUR_HMAC_MD5:
                DecryptMessageNoSignatureRC4(messages);
                break;
            default:
                throw new InvalidDataException($"Unsupported encryption algorithm {subkey.KeyEncryption}");
        }
    }

    public void Dispose()
    {
    }

    public EncryptedMessage EncryptMessage(byte[] message, SecurityQualityOfProtectionFlags quality_of_protection, int sequence_no)
    {
        if (message is null)
        {
            throw new ArgumentNullException(nameof(message));
        }

        SecurityBuffer buffer = new SecurityBufferInOut(SecurityBufferType.Data, message);
        var signature = EncryptMessage(new[] { buffer }, quality_of_protection, sequence_no);
        return new EncryptedMessage(buffer.ToArray(), signature);
    }

    public byte[] EncryptMessage(IEnumerable<SecurityBuffer> messages, SecurityQualityOfProtectionFlags quality_of_protection, int sequence_no)
    {
        if (messages is null)
        {
            throw new ArgumentNullException(nameof(messages));
        }

        List<SecurityBuffer> sig_buffers = new(messages);
        var out_sig_buffer = new SecurityBufferOut(SecurityBufferType.Token, SecurityTrailerSize);
        sig_buffers.Add(out_sig_buffer);
        EncryptMessageNoSignature(sig_buffers, quality_of_protection, sequence_no);
        return out_sig_buffer.ToArray();
    }

    public void EncryptMessageNoSignature(IEnumerable<SecurityBuffer> messages, SecurityQualityOfProtectionFlags quality_of_protection, int sequence_no)
    {
        if (!Done)
            throw new InvalidOperationException("Security context not completed.");
        if (quality_of_protection != SecurityQualityOfProtectionFlags.None)
            throw new ArgumentException("Quality of protection flags unsupported.", nameof(quality_of_protection));
        switch (_subkey.KeyEncryption)
        {
            case KerberosEncryptionType.AES128_CTS_HMAC_SHA1_96:
            case KerberosEncryptionType.AES256_CTS_HMAC_SHA1_96:
                EncryptMessageNoSignatureAES(messages);
                break;
            case KerberosEncryptionType.ARCFOUR_HMAC_MD5:
                EncryptMessageNoSignatureRC4(messages);
                break;
            default:
                throw new InvalidDataException($"Unsupported encryption algorithm {_subkey.KeyEncryption}");
        }
    }

    public ExportedSecurityContext Export()
    {
        throw new NotImplementedException();
    }

    public AuthenticationPackage GetAuthenticationPackage()
    {
        return new KerberosManagedAuthenticationPackage();
    }

    public byte[] MakeSignature(byte[] message, int sequence_no)
    {
        return MakeSignature(new SecurityBuffer[]
            { new SecurityBufferInOut(SecurityBufferType.Data,
                message) }, sequence_no);
    }

    public byte[] MakeSignature(IEnumerable<SecurityBuffer> messages, int sequence_no)
    {
        if (!Done)
            throw new InvalidOperationException("Security context not completed.");

        return _subkey.KeyEncryption switch
        {
            KerberosEncryptionType.AES128_CTS_HMAC_SHA1_96 or KerberosEncryptionType.AES256_CTS_HMAC_SHA1_96 => MakeSignatureAES(messages, sequence_no),
            KerberosEncryptionType.ARCFOUR_HMAC_MD5 => MakeSignatureRC4(messages, sequence_no),
            _ => throw new InvalidDataException($"Unsupported encryption algorithm {_subkey.KeyEncryption}"),
        };
    }

    public bool VerifySignature(byte[] message, byte[] signature, int sequence_no)
    {
        return VerifySignature(new SecurityBuffer[]
            { new SecurityBufferInOut(SecurityBufferType.Data,
                message) }, signature, sequence_no);
    }

    public bool VerifySignature(IEnumerable<SecurityBuffer> messages, byte[] signature, int sequence_no)
    {
        if (!Done)
            throw new InvalidOperationException("Security context not completed.");

        var subkey = _acceptor_subkey ?? _subkey;
        return subkey.KeyEncryption switch
        {
            KerberosEncryptionType.AES128_CTS_HMAC_SHA1_96 or KerberosEncryptionType.AES256_CTS_HMAC_SHA1_96 => VerifySignatureAES(messages, signature, sequence_no),
            KerberosEncryptionType.ARCFOUR_HMAC_MD5 => VerifySignatureRC4(messages, signature, sequence_no),
            _ => throw new InvalidDataException($"Unsupported encryption algorithm {subkey.KeyEncryption}"),
        };
    }
#pragma warning restore CS1591 // Missing XML comment for publicly visible type or member
    #endregion
}
