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

using NtCoreLib.Net.Dns;
using NtCoreLib.Utilities.ASN1.Builder;
using NtCoreLib.Win32.Security.Authentication.Kerberos.Builder;
using NtCoreLib.Win32.Security.Authentication.Kerberos.PkInit;
using System;
using System.Collections.Generic;
using System.DirectoryServices.ActiveDirectory;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos.Client;

/// <summary>
/// A class to make requests to a KDC.
/// </summary>
public sealed class KerberosKDCClient
{
    #region Private Members
    private static Lazy<string> _default_kdc_name = new(GetDefaultKDCHostNameNative);
    private readonly IKerberosKDCClientTransport _transport;
    private readonly IKerberosKDCClientTransport _password_transport;

    private KerberosAuthenticationToken ExchangeKDCTokensWithError(KerberosKDCRequestAuthenticationToken token)
    {
        var data = _transport.SendReceive(token.ToArray());
        if (KerberosErrorAuthenticationToken.TryParse(data, out KerberosErrorAuthenticationToken error))
            return error;
        if (KerberosKDCReplyAuthenticationToken.TryParse(data, out KerberosKDCReplyAuthenticationToken result))
            return result;
        throw new KerberosKDCClientException("Unknown KDC reply.");
    }

    private KerberosKDCReplyAuthenticationToken ExchangeKDCTokens(KerberosKDCRequestAuthenticationToken token)
    {
        var result = ExchangeKDCTokensWithError(token);
        if (result is KerberosErrorAuthenticationToken error)
            throw new KerberosKDCClientException(error);
        return (KerberosKDCReplyAuthenticationToken)result;
    }

    private KerberosChangePasswordStatus ChangePassword(KerberosExternalTicket ticket, ushort protocol_version, byte[] user_data)
    {
        if (_password_transport is null)
            throw new ArgumentException("Password transport not specified.");

        var auth_builder = new KerberosAuthenticatorBuilder
        {
            SequenceNumber = KerberosBuilderUtils.GetRandomNonce(),
            SubKey = KerberosAuthenticationKey.GenerateKey(ticket.SessionKey.KeyEncryption),
            ClientName = ticket.ClientName,
            ClientRealm = ticket.DomainName,
            ClientTime = KerberosTime.Now
        };

        var ap_req = KerberosAPRequestAuthenticationToken.Create(ticket.Ticket, auth_builder.Create(), 
            KerberosAPRequestOptions.None, ticket.SessionKey, raw_token: true);

        var priv_part = KerberosPrivateEncryptedPart.Create(user_data,
                KerberosHostAddress.FromIPAddress(IPAddress.Any), auth_builder.SequenceNumber);
        var priv = KerberosPrivate.Create(priv_part.Encrypt(auth_builder.SubKey, KerberosKeyUsage.KrbPriv));
        var chpasswd = new KerberosKDCChangePasswordPacket(protocol_version, ap_req, priv);

        var bytes = _password_transport.SendReceive(chpasswd.ToArray());
        if (KerberosKDCChangePasswordPacket.TryParse(bytes, out KerberosKDCChangePasswordPacket reply_packet))
        {
            var dec_token = reply_packet.Token.Decrypt(ticket.SessionKey);
            var dec_priv = reply_packet.Message.Decrypt(auth_builder.SubKey);

            var result = new KerberosKDCChangePasswordPacket(reply_packet.ProtocolVersion, (KerberosAuthenticationToken)dec_token, (KerberosPrivate)dec_priv);
            if (!(result.Message.EncryptedPart is KerberosPrivateEncryptedPart enc_part))
                throw new KerberosKDCClientException("Couldn't decrypt the reply.");
            if (enc_part.UserData.Length < 2)
                throw new KerberosKDCClientException("Invalid user data.");
            return (KerberosChangePasswordStatus)((enc_part.UserData[0] << 8) | enc_part.UserData[1]);
        }
        if (KerberosErrorAuthenticationToken.TryParse(bytes, out KerberosErrorAuthenticationToken error))
            throw new KerberosKDCClientException(error);
        throw new KerberosKDCClientException("Unknown KDC reply.");
    }

    private static KerberosASReply ProcessASReply(KerberosKDCRequestAuthenticationToken request, KerberosKDCReplyAuthenticationToken reply, KerberosAuthenticationKey key)
    {
        // RC4 encryption uses TgsRep for the AsRep.
        if (!reply.EncryptedData.TryDecrypt(key, KerberosKeyUsage.AsRepEncryptedPart, out KerberosEncryptedData reply_dec))
            reply_dec = reply.EncryptedData.Decrypt(key, KerberosKeyUsage.TgsRepEncryptedPart);
        if (!KerberosKDCReplyEncryptedPart.TryParse(reply_dec.CipherText, out KerberosKDCReplyEncryptedPart reply_part))
        {
            throw new KerberosKDCClientException("Invalid KDC reply encrypted part.");
        }

        return new KerberosASReply(request, reply, reply_part, key);
    }

    private KerberosASReply Authenticate(KerberosASRequest request)
    {
        var as_req = request.ToBuilder();
        var req_token = as_req.Create();
        return ProcessASReply(req_token, ExchangeKDCTokens(req_token), request.Key);
    }

    private KerberosASReply Authenticate(KerberosASRequestPassword request)
    {
        var as_req = request.ToBuilder();
        var req_token = as_req.Create();
        var reply = ExchangeKDCTokensWithError(req_token);
        KerberosKDCReplyAuthenticationToken as_rep;
        KerberosAuthenticationKey key;
        if (reply is KerberosErrorAuthenticationToken error)
        {
            if (error.ErrorCode != KerberosErrorType.PREAUTH_REQUIRED)
                throw new KerberosKDCClientException(error);
            key = request.DeriveKey(KerberosEncryptionType.NULL, error.PreAuthentationData);
            as_req.AddPreAuthenticationData(KerberosPreAuthenticationDataEncTimestamp.Create(KerberosTime.Now, key));
            req_token = as_req.Create();
            as_rep = ExchangeKDCTokens(req_token);
        }
        else
        {
            as_rep = (KerberosKDCReplyAuthenticationToken)reply;
            key = request.DeriveKey(as_rep.EncryptedData.EncryptionType, as_rep.PreAuthenticationData);
        }

        return ProcessASReply(req_token, as_rep, key);
    }

    private KerberosASReply Authenticate(KerberosASRequestCertificate request, byte[] freshness_token)
    {
        var as_req = request.ToBuilder();
        as_req.EncryptionTypes.Insert(0, KerberosEncryptionType.DES_EDE3_CBC);
        as_req.EncryptionTypes.Insert(0, KerberosEncryptionType.RC2_CBC);
        KerberosPkInitPkAuthenticator pk_auth = new(0, KerberosTime.Now, KerberosBuilderUtils.GetRandomNonce(),
            SHA1.Create().ComputeHash(as_req.EncodeBody()), freshness_token);
        KerberosPkInitAuthPack auth_pack = new(pk_auth);
        as_req.AddPreAuthenticationData(KerberosPreAuthenticationDataPkAsReq.Create(auth_pack, request.Certificate));
        var req_token = as_req.Create();
        var as_rep = ExchangeKDCTokens(req_token);
        var pk_as_rep = as_rep.PreAuthenticationData.OfType<KerberosPreAuthenticationDataPkAsRep>().FirstOrDefault();
        if (pk_as_rep == null)
            throw new KerberosKDCClientException("PA-PK-AS-REP is missing from reply.");
        pk_as_rep.EncryptedKeyPack.Decrypt(new X509Certificate2Collection
        {
            request.Certificate
        });

        SignedCms signed_key_pack = new();
        signed_key_pack.Decode(pk_as_rep.EncryptedKeyPack.ContentInfo.Content);

        // TODO: Perhaps should verify the data OID and checksum?
        var reply_key_pack = KerberosPkInitReplyKeyPack.Parse(signed_key_pack.ContentInfo.Content, request.ClientName, request.Realm);

        return ProcessASReply(req_token, as_rep, reply_key_pack.ReplyKey);
    }

    private KerberosASReply Authenticate(KerberosASRequestCertificate request)
    {
        try
        {
            return Authenticate(request, null);
        }
        catch (KerberosKDCClientException ex)
        {
            if (ex.ErrorCode != KerberosErrorType.PREAUTH_REQUIRED)
                throw;
            var freshness_token = ex.Error?.PreAuthentationData?.OfType<KerberosPreAuthenticationDataAsFreshness>().FirstOrDefault();
            if (freshness_token == null)
                throw;
            return Authenticate(request, freshness_token.FreshnessToken);
        }
    }

    private static IPAddress FindDnsAddress(string realm)
    {
        HashSet<IPAddress> addrs = new();

        foreach (var nic in NetworkInterface.GetAllNetworkInterfaces())
        {
            if (nic.OperationalStatus != OperationalStatus.Up)
                continue;
            var ip = nic.GetIPProperties();
            if (ip.DnsAddresses.Count == 0)
                continue;
            if (ip.DnsSuffix.Length > 0 && realm.EndsWith(ip.DnsSuffix.ToLower()))
            {
                return ip.DnsAddresses.First();
            }

            foreach (var next_addr in ip.DnsAddresses)
            {
                addrs.Add(next_addr);
            }
        }

        return addrs.FirstOrDefault();
    }

    private static string GetDefaultKDCHostNameNative()
    {
        try
        {
            return Domain.GetCurrentDomain().FindDomainController().Name;
        }
        catch
        {
            return null;
        }
    }

    #endregion

    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="transport">The KDC client transport.</param>
    /// <param name="password_transport">The KDC client transport for the password server.</param>
    public KerberosKDCClient(IKerberosKDCClientTransport transport, IKerberosKDCClientTransport password_transport = null)
    {
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _password_transport = password_transport;
    }
    #endregion

    #region Public Static Members
    /// <summary>
    /// Specified a default KDC hostname.
    /// </summary>
    public static string DefaultKDCHostName {
        get => _default_kdc_name.Value;
        set => _default_kdc_name = new Lazy<string>(() => value);
    }

    /// <summary>
    /// Create a TCP KDC client.
    /// </summary>
    /// <param name="hostname">The hostname of the KDC server.</param>
    /// <param name="port">The port number of the KDC server.</param>
    /// <param name="password_port">The port number of the KDC password server.</param>
    /// <returns>The created client.</returns>
    public static KerberosKDCClient CreateTCPClient(string hostname, int port = 88, int password_port = 464)
    {
        hostname = string.IsNullOrWhiteSpace(hostname) ? DefaultKDCHostName : hostname;
        return new KerberosKDCClient(new KerberosKDCClientTransportTCP(hostname, port), 
            new KerberosKDCClientTransportTCP(hostname, password_port));
    }

    /// <summary>
    /// Create a TCP KDC client for the current domain.
    /// </summary>
    /// <param name="port">The port number of the KDC server.</param>
    /// <param name="password_port">The port number of the KDC password server.</param>
    /// <returns>The created client.</returns>
    /// <remarks>Also uses the DefaultKDCHostName value if no value specified.</remarks>
    public static KerberosKDCClient CreateTCPClient(int port = 88, int password_port = 464)
    {
        return CreateTCPClient(null, port, password_port);
    }

    /// <summary>
    /// Create a TCP KDC client.
    /// </summary>
    /// <param name="kdc">The KDC SRV record.</param>
    /// <param name="password_port">The port number of the KDC password server.</param>
    /// <returns>The created client.</returns>
    public static KerberosKDCClient CreateTCPClient(DnsServiceRecord kdc, int password_port = 464)
    {
        if (kdc is null)
        {
            throw new ArgumentNullException(nameof(kdc));
        }

        return CreateTCPClient(kdc.Target, kdc.Port, password_port);
    }

    /// <summary>
    /// Query DNS for KDC SRV records for a realm.
    /// </summary>
    /// <param name="realm">The realm to query.</param>
    /// <param name="dns_server">Optional DNS server IP address. Will try and find a suitable DNS server for the query.</param>
    /// <returns>The list of DNS SRV records for the </returns>
    /// <exception cref="ArgumentException"></exception>
    public static IReadOnlyCollection<DnsServiceRecord> QueryKdcForRealm(string realm, IPAddress dns_server = null)
    {
        if (string.IsNullOrWhiteSpace(realm))
        {
            throw new ArgumentException($"'{nameof(realm)}' cannot be null or whitespace.", nameof(realm));
        }

        realm = realm.ToLower();

        dns_server = dns_server ?? FindDnsAddress(realm);
        if (dns_server == null)
            throw new ArgumentNullException(nameof(dns_server), "No suitable DNS server available.");

        return new DnsClient(dns_server)
        {
            ForceTcp = true
        }.QueryServices($"_kerberos._tcp.dc._msdcs.{realm}")
            .OrderBy(p => Tuple.Create(p.Priority, p.Weight)).ToList().AsReadOnly();
    }

    #endregion

    #region Public Methods
    /// <summary>
    /// Authenticate a user using Kerberos.
    /// </summary>
    /// <param name="request">The details of the AS request.</param>
    /// <returns>The AS reply.</returns>
    public KerberosASReply Authenticate(KerberosASRequestBase request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        if (request is KerberosASRequest key_req)
            return Authenticate(key_req);
        if (request is KerberosASRequestPassword pwd_req)
            return Authenticate(pwd_req);
        if (request is KerberosASRequestCertificate cert_req)
            return Authenticate(cert_req);

        throw new ArgumentException("Unknown AS-REQ type.");
    }

    /// <summary>
    /// Request a service ticket.
    /// </summary>
    /// <param name="request">The details of the TGS request.</param>
    /// <returns>The TGS reply.</returns>
    public KerberosTGSReply RequestServiceTicket(KerberosTGSRequest request)
    {
        if (request is null)
        {
            throw new ArgumentNullException(nameof(request));
        }

        var subkey = KerberosAuthenticationKey.GenerateKey(request.SessionKey.KeyEncryption);
        var tgs_req = request.ToBuilder();
        if (tgs_req.AuthorizationData != null)
        {
            tgs_req.AuthorizationData = tgs_req.AuthorizationData.Encrypt(subkey, KerberosKeyUsage.TgsReqKdcReqBodyAuthSubkey);
        }

        var checksum = KerberosChecksum.Create(KerberosChecksumType.RSA_MD5, tgs_req.EncodeBody());
        KerberosAuthenticator authenticator = KerberosAuthenticator.Create(request.Realm, request.ClientName, KerberosTime.Now, 0, checksum, subkey,
            KerberosBuilderUtils.GetRandomNonce(), null);
        tgs_req.AddPreAuthenticationData(new KerberosPreAuthenticationDataTGSRequest(0, request.Ticket,
            authenticator.Encrypt(request.SessionKey, KerberosKeyUsage.TgsReqPaTgsReqApReq)));
        if (request.S4UUserName != null && !string.IsNullOrEmpty(request.S4URealm))
        {
            tgs_req.AddPreAuthenticationDataForUser(request.S4UUserName, request.S4URealm, request.SessionKey);
        }
        if (request.PACOptionsFlags != KerberosPreAuthenticationPACOptionsFlags.None)
        {
            tgs_req.AddPreAuthenticationData(new KerberosPreAuthenticationPACOptions(request.PACOptionsFlags));
        }

        var req_token = tgs_req.Create();
        var reply = ExchangeKDCTokens(req_token);
        var reply_dec = reply.EncryptedData.Decrypt(subkey, KerberosKeyUsage.TgsRepEncryptedPartAuthSubkey);
        if (!KerberosKDCReplyEncryptedPart.TryParse(reply_dec.CipherText, out KerberosKDCReplyEncryptedPart reply_part))
        {
            throw new KerberosKDCClientException("Invalid KDC reply encrypted part.");
        }

        return new KerberosTGSReply(req_token, reply, reply_part);
    }

    /// <summary>
    /// Change a user's password.
    /// </summary>
    /// <param name="key">The user's authentication key.</param>
    /// <param name="new_password">The user's new password.</param>
    /// <returns>The status of the operation.</returns>
    public KerberosChangePasswordStatus ChangePassword(KerberosAuthenticationKey key, string new_password)
    {
        KerberosASRequest request = new(key, key.Name, key.Realm)
        {
            ServerName = new KerberosPrincipalName(KerberosNameType.SRV_INST, "kadmin/changepw")
        };
        return ChangePassword(Authenticate(request).ToExternalTicket(), new_password);
    }

    /// <summary>
    /// Change a user's password.
    /// </summary>
    /// <param name="ticket">The user's ticket for kadmin/changepw.</param>
    /// <param name="new_password">The user's new password.</param>
    /// <returns>The status of the operation.</returns>
    public KerberosChangePasswordStatus ChangePassword(KerberosExternalTicket ticket, string new_password)
    {
        return ChangePassword(ticket, 1, Encoding.UTF8.GetBytes(new_password));
    }

    /// <summary>
    /// Set a user's password.
    /// </summary>
    /// <param name="tgt_ticket">The TGT ticket for the service ticket request.</param>
    /// <param name="client_name">The name of the client to change.</param>
    /// <param name="realm">The realm of the client to change.</param>
    /// <param name="new_password">The user's new password.</param>
    /// <returns>The status of the operation.</returns>
    public KerberosChangePasswordStatus SetPassword(KerberosExternalTicket tgt_ticket, KerberosPrincipalName client_name, string realm, string new_password)
    {
        if (client_name is null)
        {
            throw new ArgumentNullException(nameof(client_name));
        }

        if (realm is null)
        {
            throw new ArgumentNullException(nameof(realm));
        }

        var request = new KerberosTGSRequest(tgt_ticket.Ticket, tgt_ticket.SessionKey, tgt_ticket.ClientName, tgt_ticket.DomainName);
        request.ServerName = new KerberosPrincipalName(KerberosNameType.SRV_INST, "kadmin/changepw");
        var reply = RequestServiceTicket(request);

        DERBuilder der_builder = new();
        using (var seq = der_builder.CreateSequence())
        {
            seq.WriteContextSpecific(0, Encoding.UTF8.GetBytes(new_password));
            seq.WriteContextSpecific(1, client_name);
            seq.WriteContextSpecific(2, realm);
        }

        return ChangePassword(reply.ToExternalTicket(), 0xFF80, der_builder.ToArray());
    }
    #endregion
}
