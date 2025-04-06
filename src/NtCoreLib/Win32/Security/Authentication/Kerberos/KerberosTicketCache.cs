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

using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos;

/// <summary>
/// Class to query the Kerberos Ticket Cache from LSASS.
/// </summary>
public static class KerberosTicketCache 
{
    #region Private Members
    private static NtResult<LsaCallPackageResponse> CallPackage(this SafeLsaLogonHandle handle, string package_name, SafeBuffer buffer, bool throw_on_error)
    {
        var package = handle.LookupAuthPackage(package_name ?? AuthenticationPackage.KERBEROS_NAME, throw_on_error);
        if (!package.IsSuccess)
            return package.Cast<LsaCallPackageResponse>();
        return handle.CallPackage(package.Result, buffer, throw_on_error);
    }

    private static NtResult<LsaCallPackageResponse> CallPackage(this NtResult<SafeLsaLogonHandle> handle, string package_name, SafeBuffer buffer, bool throw_on_error)
    {
        return CallPackage(handle.Result, package_name, buffer, throw_on_error);
    }

    private static NtResult<KerberosTicketCacheInfo[]> QueryTicketCacheList<T>(string package_name,
        KERB_PROTOCOL_MESSAGE_TYPE query_type, SafeLsaLogonHandle handle, Luid logon_id, Func<T, KerberosTicketCacheInfo> map_fn, bool throw_on_error) where T : struct
    {
        var request_struct = new KERB_QUERY_TKT_CACHE_REQUEST()
        {
            LogonId = logon_id,
            MessageType = query_type
        };
        using var request = request_struct.ToBuffer();
        using var result = handle.CallPackage(package_name, request, throw_on_error);
        if (!result.IsSuccess)
            return result.Cast<KerberosTicketCacheInfo[]>();
        if (!result.Result.Status.IsSuccess())
            return result.Result.Status.CreateResultFromError<KerberosTicketCacheInfo[]>(throw_on_error);
        var response = result.Result.Buffer.Read<KERB_QUERY_TKT_CACHE_RESPONSE_HEADER>(0);
        if (response.CountOfTickets == 0)
            return new KerberosTicketCacheInfo[0].CreateResult();
        var buffer = SafeBufferUtils.GetStructAtOffset<KERB_QUERY_TKT_CACHE_RESPONSE>(result.Result.Buffer, 0);
        T[] infos = new T[response.CountOfTickets];
        buffer.Data.ReadArray(0, infos, 0, response.CountOfTickets);
        return infos.Select(map_fn).ToArray().CreateResult();
    }

    private static NtResult<SafeLsaReturnBufferHandle> QueryCachedTicketBuffer(SafeLsaLogonHandle handle, string target_name, KerberosRetrieveTicketFlags flags,
        Luid logon_id, SecHandle sec_handle, KerberosTicketFlags ticket_flags, KerberosEncryptionType encryption_type, bool throw_on_error)
    {
        int string_length = (target_name.Length) * 2;
        int max_string_length = string_length + 2;
        using var request = new SafeStructureInOutBuffer<KERB_RETRIEVE_TKT_REQUEST>(max_string_length, true);
        request.Data.WriteUnicodeString(target_name + '\0');
        var request_str = new KERB_RETRIEVE_TKT_REQUEST()
        {
            CacheOptions = flags,
            CredentialsHandle = sec_handle,
            LogonId = logon_id,
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbRetrieveEncodedTicketMessage,
            TicketFlags = ((uint)ticket_flags).RotateBits(),
            EncryptionType = encryption_type,
            TargetName = new UnicodeStringOut()
            {
                Length = (ushort)string_length,
                MaximumLength = (ushort)max_string_length,
                Buffer = request.Data.DangerousGetHandle()
            }
        };
        request.Result = request_str;
        using var result = handle.CallPackage(null, request, throw_on_error);
        if (!result.IsSuccess)
            return result.Cast<SafeLsaReturnBufferHandle>();
        if (!result.Result.Status.IsSuccess())
            return result.Result.Status.CreateResultFromError<SafeLsaReturnBufferHandle>(throw_on_error);
        return result.Result.Buffer.Detach().CreateResult();
    }

    private static NtResult<KerberosExternalTicket> QueryCachedTicket(SafeLsaLogonHandle handle, string target_name, KerberosRetrieveTicketFlags flags,
        Luid logon_id, SecHandle sec_handle, KerberosTicketFlags ticket_flags, KerberosEncryptionType encryption_type, bool throw_on_error)
    {
        using var buffer = QueryCachedTicketBuffer(handle, target_name, flags, logon_id, sec_handle, ticket_flags, encryption_type, throw_on_error);
        if (!buffer.IsSuccess)
            return buffer.Cast<KerberosExternalTicket>();

        KERB_EXTERNAL_TICKET ticket = buffer.Result.Read<KERB_EXTERNAL_TICKET>(0);
        if (!KerberosExternalTicket.TryParse(ticket, flags.HasFlagSet(KerberosRetrieveTicketFlags.AsKerbCred), out KerberosExternalTicket ret))
            return NtStatus.STATUS_INVALID_PARAMETER.CreateResultFromError<KerberosExternalTicket>(throw_on_error);
        return ret.CreateResult();
    }

    private static NtResult<KerberosTicketCacheInfo[]> QueryTicketCacheList(SafeLsaLogonHandle handle, string package_name, Luid logon_id, bool throw_on_error)
    {
        var ret = QueryTicketCacheList<KERB_TICKET_CACHE_INFO_EX3>(package_name, KERB_PROTOCOL_MESSAGE_TYPE.KerbQueryTicketCacheEx3Message,
handle, logon_id, t => new KerberosTicketCacheInfo(t), false);
        if (ret.IsSuccess)
            return ret;
        return QueryTicketCacheList<KERB_TICKET_CACHE_INFO_EX2>(package_name, KERB_PROTOCOL_MESSAGE_TYPE.KerbQueryTicketCacheEx2Message,
handle, logon_id, t => new KerberosTicketCacheInfo(t), throw_on_error);
    }
    #endregion

    #region Internal Members
    internal static NtResult<IEnumerable<KerberosTicketCacheInfo>> QueryTicketCacheInfo(string package_name, Luid logon_id, bool throw_on_error)
    {
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Cast<IEnumerable<KerberosTicketCacheInfo>>();
        return QueryTicketCacheList(handle.Result, package_name, logon_id, throw_on_error).Cast<IEnumerable<KerberosTicketCacheInfo>>();
    }

    internal static NtStatus PurgeTicketCacheEx(string package_name, bool purge_all_tickets, Luid logon_id, KerberosTicketCacheInfo ticket_template, bool throw_on_error)
    {
        var builder = new KERB_PURGE_TKT_CACHE_EX_REQUEST()
        {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbPurgeTicketCacheExMessage,
            LogonId = logon_id,
            Flags = purge_all_tickets ? KerberosPurgeTicketCacheExFlags.PurgeAllTickets : 0
        }.ToBuilder();

        if (ticket_template != null)
        {
            KERB_TICKET_CACHE_INFO_EX ticket_info = new();
            ticket_info.EncryptionType = ticket_template.EncryptionType;
            ticket_info.EndTime = ticket_template.EndTime.ToLargeIntegerStruct();
            ticket_info.StartTime = ticket_template.StartTime.ToLargeIntegerStruct();
            ticket_info.RenewTime = ticket_template.RenewTime.ToLargeIntegerStruct();
            ticket_info.TicketFlags = ((uint)ticket_template.TicketFlags).RotateBits();
            var sub_builder = builder.GetSubBuilder(nameof(KERB_PURGE_TKT_CACHE_EX_REQUEST.TicketTemplate), ticket_info);
            sub_builder.AddUnicodeString(nameof(KERB_TICKET_CACHE_INFO_EX.ClientName), ticket_template.ClientName);
            sub_builder.AddUnicodeString(nameof(KERB_TICKET_CACHE_INFO_EX.ClientRealm), ticket_template.ClientRealm);
            sub_builder.AddUnicodeString(nameof(KERB_TICKET_CACHE_INFO_EX.ServerName), ticket_template.ServerName);
            sub_builder.AddUnicodeString(nameof(KERB_TICKET_CACHE_INFO_EX.ServerRealm), ticket_template.ServerRealm);
        }

        using var buffer = builder.ToBuffer();
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Status;
        using var result = handle.CallPackage(package_name, buffer, throw_on_error);
        return result.Result.Status.ToNtException(throw_on_error);
    }
    #endregion

    #region Public Static Methods
    /// <summary>
    /// Retrieve a Kerberos Ticket.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="cred_handle">Optional credential handle.</param>
    /// <param name="flags">Flags for retrieving the ticket.</param>
    /// <param name="ticket_flags">Ticket flags for the ticket.</param>
    /// <param name="encryption_type">Encryption type.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static NtResult<KerberosExternalTicket> RetrieveTicket(string target_name, Luid logon_id, 
        CredentialHandle cred_handle, KerberosRetrieveTicketFlags flags, KerberosTicketFlags ticket_flags,
        KerberosEncryptionType encryption_type, bool throw_on_error)
    {
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Cast<KerberosExternalTicket>();
        return QueryCachedTicket(handle.Result, target_name, flags,
            logon_id, cred_handle?.CredHandle ?? new SecHandle(),
            ticket_flags, encryption_type, throw_on_error);
    }

    /// <summary>
    /// Retrieve a Kerberos Ticket.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="cred_handle">Optional credential handle.</param>
    /// <param name="flags">Flags for retrieving the ticket.</param>
    /// <param name="ticket_flags">Ticket flags for the ticket.</param>
    /// <param name="encryption_type">Encryption type.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static KerberosExternalTicket RetrieveTicket(string target_name, Luid logon_id,
        CredentialHandle cred_handle, KerberosRetrieveTicketFlags flags, KerberosTicketFlags ticket_flags,
        KerberosEncryptionType encryption_type)
    {
        return RetrieveTicket(target_name, logon_id, cred_handle, flags, ticket_flags, encryption_type, true).Result;
    }

    /// <summary>
    /// Get a Kerberos Ticket.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="cached_only">True to only query for cached tickets.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static NtResult<KerberosExternalTicket> GetTicket(string target_name, Luid logon_id, bool cached_only, bool throw_on_error)
    {
        KerberosRetrieveTicketFlags flags = cached_only ? KerberosRetrieveTicketFlags.UseCacheOnly : KerberosRetrieveTicketFlags.Default;
        flags |= KerberosRetrieveTicketFlags.AsKerbCred;
        return RetrieveTicket(target_name, logon_id, null, flags, KerberosTicketFlags.None, KerberosEncryptionType.NULL, throw_on_error);
    }

    /// <summary>
    /// Get a Kerberos Ticket.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="cached_only">True to only query for cached tickets.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static KerberosExternalTicket GetTicket(string target_name, Luid logon_id, bool cached_only)
    {
        return GetTicket(target_name, logon_id, cached_only, true).Result;
    }

    /// <summary>
    /// Get a Kerberos Ticket.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static KerberosExternalTicket GetTicket(string target_name, Luid logon_id)
    {
        return GetTicket(target_name, logon_id, false, true).Result;
    }

    /// <summary>
    /// Get a Kerberos Ticket from a credential handle.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="credential_handle">The credential handle to query.</param>
    /// <param name="cached_only">True to only query for cached tickets.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static NtResult<KerberosExternalTicket> GetTicket(string target_name, CredentialHandle credential_handle, bool cached_only, bool throw_on_error)
    {
        KerberosRetrieveTicketFlags flags = cached_only ? KerberosRetrieveTicketFlags.UseCacheOnly : KerberosRetrieveTicketFlags.Default;
        flags |= KerberosRetrieveTicketFlags.AsKerbCred | KerberosRetrieveTicketFlags.UseCredHandle;
        return RetrieveTicket(target_name, default, credential_handle, flags, KerberosTicketFlags.None, KerberosEncryptionType.NULL, throw_on_error);
    }

    /// <summary>
    /// Get a Kerberos Ticket from a credential handle.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="credential_handle">The credential handle to query.</param>
    /// <param name="cached_only">True to only query for cached tickets.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static KerberosExternalTicket GetTicket(string target_name, CredentialHandle credential_handle, bool cached_only)
    {
        return GetTicket(target_name, credential_handle, cached_only, true).Result;
    }

    /// <summary>
    /// Get a Kerberos Ticket from a credential handle.
    /// </summary>
    /// <param name="target_name">The target service for the Ticket.</param>
    /// <param name="credential_handle">The credential handle to query.</param>
    /// <returns>The Kerberos Ticket.</returns>
    public static KerberosExternalTicket GetTicket(string target_name, CredentialHandle credential_handle)
    {
        return GetTicket(target_name, credential_handle, false, true).Result;
    }

    /// <summary>
    /// Query Kerberos Ticket cache.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The list of cached tickets.</returns>
    public static NtResult<KerberosExternalTicket[]> QueryTicketCache(Luid logon_id, bool throw_on_error)
    {
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Cast<KerberosExternalTicket[]>();
        var list = QueryTicketCacheList(handle.Result, null, logon_id, throw_on_error);
        if (!list.IsSuccess)
            return list.Cast<KerberosExternalTicket[]>();

        var tickets = new List<KerberosExternalTicket>();
        foreach (var info in list.Result)
        {
            var ticket = QueryCachedTicket(handle.Result, $"{info.ServerName}@{info.ServerRealm}",
                KerberosRetrieveTicketFlags.UseCacheOnly | KerberosRetrieveTicketFlags.AsKerbCred,
                logon_id, new SecHandle(), KerberosTicketFlags.None, KerberosEncryptionType.NULL, false);
            if (ticket.IsSuccess)
            {
                tickets.Add(ticket.Result);
            }
        }
        return tickets.ToArray().CreateResult();
    }

    /// <summary>
    /// Query Kerberos Ticket cache.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <returns>The list of cached tickets.</returns>
    public static KerberosExternalTicket[] QueryTicketCache(Luid logon_id)
    {
        return QueryTicketCache(logon_id, true).Result;
    }

    /// <summary>
    /// Query Kerberos Ticket cache for the current logon session.
    /// </summary>
    /// <returns>The list of cached tickets.</returns>
    public static KerberosExternalTicket[] QueryTicketCache()
    {
        return QueryTicketCache(Luid.Empty);
    }

    /// <summary>
    /// Query Kerberos Ticket cache information.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The list of cached tickets.</returns>
    /// <remarks>This doesn't query the tickets themselves.</remarks>
    public static NtResult<IEnumerable<KerberosTicketCacheInfo>> QueryTicketCacheInfo(Luid logon_id, bool throw_on_error)
    {
        return QueryTicketCacheInfo(null, logon_id, throw_on_error);
    }

    /// <summary>
    /// Query Kerberos Ticket cache information.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to query.</param>
    /// <returns>The list of cached tickets.</returns>
    /// <remarks>This doesn't query the tickets themselves.</remarks>
    public static IEnumerable<KerberosTicketCacheInfo> QueryTicketCacheInfo(Luid logon_id)
    {
        return QueryTicketCacheInfo(logon_id, true).Result;
    }

    /// <summary>
    /// Query Kerberos Ticket cache information.
    /// </summary>
    /// <returns>The list of cached tickets.</returns>
    /// <remarks>This doesn't query the tickets themselves.</remarks>
    public static IEnumerable<KerberosTicketCacheInfo> QueryTicketCacheInfo()
    {
        return QueryTicketCacheInfo(Luid.Empty);
    }

    /// <summary>
    /// Query for the TGT for a logon session.
    /// </summary>
    /// <param name="logon_id">The logon session ID. Specify 0 to use the caller's logon session.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The queries TGT.</returns>
    /// <remarks>Note that the session key will only be available if running with TCB privileges or the AllowTgtSessionKey option is enabled.</remarks>
    public static NtResult<KerberosExternalTicket> QueryTgt(Luid logon_id, bool throw_on_error)
    {
        var req_struct = new KERB_QUERY_TKT_CACHE_REQUEST() {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbRetrieveTicketMessage,
            LogonId = logon_id
        };
        using var request = req_struct.ToBuffer();
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Cast<KerberosExternalTicket>();
        using var result = handle.CallPackage(null, request, throw_on_error);
        if (!result.IsSuccess)
            return result.Cast<KerberosExternalTicket>();
        if (!result.Result.Status.IsSuccess())
            return result.Result.Status.CreateResultFromError<KerberosExternalTicket>(throw_on_error);
        KERB_EXTERNAL_TICKET ticket = result.Result.Buffer.Read<KERB_EXTERNAL_TICKET>(0);
        if (!KerberosExternalTicket.TryParse(ticket, false, out KerberosExternalTicket ret))
            return NtStatus.STATUS_INVALID_PARAMETER.CreateResultFromError<KerberosExternalTicket>(throw_on_error);
        return ret.CreateResult();
    }

    /// <summary>
    /// Query for the TGT for a logon session.
    /// </summary>
    /// <param name="logon_id">The logon session ID. Specify 0 to use the caller's logon session.</param>
    /// <returns>The queries TGT.</returns>
    /// <remarks>Note that the session key will only be available if running with TCB privileges or the AllowTgtSessionKey option is enabled.</remarks>
    public static KerberosExternalTicket QueryTgt(Luid logon_id)
    {
        return QueryTgt(logon_id, true).Result;
    }

    /// <summary>
    /// Query for the TGT for the current logon session.
    /// </summary>
    /// <returns>The queries TGT.</returns>
    /// <remarks>Note that the session key will only be available if running with TCB privileges or the AllowTgtSessionKey option is enabled.</remarks>
    public static KerberosExternalTicket QueryTgt()
    {
        return QueryTgt(Luid.Empty);
    }

    /// <summary>
    /// Purge the ticket cache.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to purge.</param>
    /// <param name="server_name">The name of the service tickets to delete.</param>
    /// <param name="realm_name">The realm of the tickets to delete.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus PurgeTicketCache(Luid logon_id, string server_name, string realm_name, bool throw_on_error)
    {
        var builder = new KERB_PURGE_TKT_CACHE_REQUEST()
        {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbPurgeTicketCacheMessage,
            LogonId = logon_id
        }.ToBuilder();
        builder.AddUnicodeString(nameof(KERB_PURGE_TKT_CACHE_REQUEST.ServerName), server_name);
        builder.AddUnicodeString(nameof(KERB_PURGE_TKT_CACHE_REQUEST.RealmName), realm_name);

        using var buffer = builder.ToBuffer();
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Status;
        using var result = handle.CallPackage(null, buffer, throw_on_error);
        return result.Result.Status.ToNtException(throw_on_error);
    }

    /// <summary>
    /// Purge the ticket cache.
    /// </summary>
    /// <param name="logon_id">The Logon Session ID to purge.</param>
    /// <param name="server_name">The name of the service tickets to delete.</param>
    /// <param name="realm_name">The realm of the tickets to delete.</param>
    public static void PurgeTicketCache(Luid logon_id, string server_name, string realm_name)
    {
        PurgeTicketCache(logon_id, server_name, realm_name, true);
    }

    /// <summary>
    /// Purge the ticket cache.
    /// </summary>
    /// <param name="purge_all_tickets">Purge all tickets.</param>
    /// <param name="logon_id">The Logon Session ID to purge.</param>
    /// <param name="ticket_template">Ticket template to purge.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus PurgeTicketCacheEx(bool purge_all_tickets, Luid logon_id, KerberosTicketCacheInfo ticket_template, bool throw_on_error)
    {
        return PurgeTicketCacheEx(null, purge_all_tickets, logon_id, ticket_template, throw_on_error);
    }

    /// <summary>
    /// Purge the ticket cache.
    /// </summary>
    /// <param name="purge_all_tickets">Purge all tickets.</param>
    /// <param name="logon_id">The Logon Session ID to purge.</param>
    /// <param name="ticket_template">Ticket template to purge.</param>
    public static void PurgeTicketCacheEx(bool purge_all_tickets, Luid logon_id, KerberosTicketCacheInfo ticket_template)
    {
        PurgeTicketCacheEx(purge_all_tickets, logon_id, ticket_template, true);
    }

    /// <summary>
    /// Submit a ticket to the cache.
    /// </summary>
    /// <param name="ticket">The ticket to add in Kerberos Credential format.</param>
    /// <param name="logon_id">The Logon Session ID to submit the ticket to. 0 uses callers logon session.</param>
    /// <param name="key">Optional key to use if the credentials are encrypted.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus SubmitTicket(KerberosCredential ticket, Luid logon_id, KerberosAuthenticationKey key, bool throw_on_error)
    {
        if (ticket is null)
        {
            throw new ArgumentNullException(nameof(ticket));
        }

        byte[] ticket_data = ticket.ToArray();
        int additional_length = ticket_data.Length + (key?.Key.Length ?? 0);

        using var buffer = new SafeStructureInOutBuffer<KERB_SUBMIT_TKT_REQUEST>(additional_length, true);
        buffer.Data.WriteBytes(ticket_data);
        int base_offset = buffer.DataOffset;
        KERB_CRYPTO_KEY32 key_struct = new();
        if (key != null)
        {
            key_struct.KeyType = key.KeyEncryption;
            key_struct.Length = key.Key.Length;
            key_struct.Offset = base_offset + ticket_data.Length;
            buffer.Data.WriteBytes((ulong)ticket_data.Length, key.Key);
        }

        buffer.Result = new KERB_SUBMIT_TKT_REQUEST()
        {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbSubmitTicketMessage,
            LogonId = logon_id,
            KerbCredOffset = base_offset,
            KerbCredSize = ticket_data.Length,
            Key = key_struct
        };

        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Status;
        using var result = handle.CallPackage(null, buffer, throw_on_error);
        if (!result.IsSuccess)
            return result.Status;
        return result.Result.Status.ToNtException(throw_on_error);
    }

    /// <summary>
    /// Submit a ticket to the cache.
    /// </summary>
    /// <param name="ticket">The ticket to add in Kerberos Credential format.</param>
    /// <param name="logon_id">The Logon Session ID to submit the ticket to. 0 uses callers logon session.</param>
    /// <param name="key">Optional key to use if the credentials are encrypted.</param>
    public static void SubmitTicket(KerberosCredential ticket, Luid logon_id = default, KerberosAuthenticationKey key = null)
    {
        SubmitTicket(ticket, logon_id, key, true);
    }

    /// <summary>
    /// Set a KDC pin for this process.
    /// </summary>
    /// <param name="realm">The KDC realm name.</param>
    /// <param name="kdc_address">The KDC address.</param>
    /// <param name="dc_flags">Flags to specify the DC type.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus PinKdc(string realm, string kdc_address, int dc_flags, bool throw_on_error)
    {
        var builder = new KERB_PIN_KDC_REQUEST()
        {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbPinKdcMessage,
            DcFlags = dc_flags
        }.ToBuilder();

        builder.AddUnicodeString(nameof(KERB_PIN_KDC_REQUEST.Realm), realm, true);
        builder.AddUnicodeString(nameof(KERB_PIN_KDC_REQUEST.KdcAddress), kdc_address, true);

        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Status;
        using var buffer = builder.ToBuffer();
        using var result = handle.CallPackage(null, buffer, throw_on_error);
        return result.Result.Status.ToNtException(throw_on_error);
    }

    /// <summary>
    /// Set a KDC pin for this process.
    /// </summary>
    /// <param name="realm">The KDC realm name.</param>
    /// <param name="kdc_address">The KDC address.</param>
    /// <param name="dc_flags">Flags to specify the DC type.</param>
    public static void PinKdc(string realm, string kdc_address, int dc_flags)
    {
        PinKdc(realm, kdc_address, dc_flags, true);
    }

    /// <summary>
    /// Unpin all KDCs for this process.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus UnpinAllKdcs(bool throw_on_error)
    {
        using var handle = SafeLsaLogonHandle.Connect(throw_on_error);
        if (!handle.IsSuccess)
            return handle.Status;
        var req = new KERB_UNPIN_ALL_KDCS_REQUEST()
        {
            MessageType = KERB_PROTOCOL_MESSAGE_TYPE.KerbUnpinAllKdcsMessage
        };
        using var buffer = req.ToBuffer();
        using var result = handle.CallPackage(null, buffer, throw_on_error);
        return result.Result.Status.ToNtException(throw_on_error);
    }

    /// <summary>
    /// Unpin all KDCs for this process.
    /// </summary>
    public static void UnpinAllKdcs()
    {
        UnpinAllKdcs(true);
    }
    #endregion
}
