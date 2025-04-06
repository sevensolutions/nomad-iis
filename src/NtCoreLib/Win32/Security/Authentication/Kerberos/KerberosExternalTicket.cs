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
using NtCoreLib.Utilities.ASN1;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NtCoreLib.Win32.Security.Authentication.Kerberos;

/// <summary>
/// Class to represent a cached external ticket.
/// </summary>
public sealed class KerberosExternalTicket
{
    #region Private Members
    private static KerberosPrincipalName ParseName(IntPtr ptr)
    {
        if (ptr == IntPtr.Zero)
            return new KerberosPrincipalName();
        KerberosNameType name_type = (KerberosNameType)Marshal.ReadInt16(ptr, 0);
        int count = Marshal.ReadInt16(ptr, 2);
        if (count == 0)
            return new KerberosPrincipalName(name_type, new string[0]);

        var name = new SafeStructureInOutBuffer<KERB_EXTERNAL_NAME>(ptr, Marshal.SizeOf(typeof(KERB_EXTERNAL_NAME)) 
            + Marshal.SizeOf(typeof(UnicodeStringOut)) * count, false);
        UnicodeStringOut[] names = new UnicodeStringOut[count];
        name.Data.ReadArray(0, names, 0, count);
        return new KerberosPrincipalName(name_type, names.Select(u => u.ToString()));
    }

    private static KerberosAuthenticationKey ParseKey(KerberosPrincipalName server_name, string realm, KERB_CRYPTO_KEY key)
    {
        byte[] key_data = new byte[key.Length];
        Marshal.Copy(key.Value, key_data, 0, key.Length);
        return new KerberosAuthenticationKey(key.KeyType, key_data, server_name.NameType, realm, server_name.Names, DateTime.Now, 0);
    }

    private KerberosExternalTicket()
    {
    }
    #endregion

    #region Internal Members
    internal KerberosExternalTicket(KerberosCredential credential)
    {
        if (credential is null)
        {
            throw new ArgumentNullException(nameof(credential));
        }

        if (credential.Tickets.Count != 1)
        {
            throw new ArgumentException("Credential must only have one ticket.", nameof(credential));
        }

        if (!(credential.EncryptedPart is KerberosCredentialEncryptedPart enc_part))
        {
            throw new ArgumentException("Credential must be decrypted.", nameof(credential));
        }

        if (enc_part.TicketInfo.Count != 1)
        {
            throw new ArgumentException("Credential must only have one ticket information.", nameof(enc_part.TicketInfo));
        }

        var ticket_info = enc_part.TicketInfo[0];

        ServiceName = ticket_info.ServerName;
        TargetName = ticket_info.ServerName;
        ClientName = ticket_info.ClientName;
        DomainName = ticket_info.Realm;
        TargetDomainName = ticket_info.ClientRealm;
        AltTargetDomainName = TargetDomainName;
        SessionKey = ticket_info.Key;
        TicketFlags = ticket_info.TicketFlags ?? KerberosTicketFlags.None;
        KeyExpirationTime = DateTime.MinValue;
        StartTime = ticket_info.StartTime?.ToDateTime() ?? DateTime.MinValue;
        EndTime = ticket_info.EndTime?.ToDateTime() ?? DateTime.MinValue;
        RenewUntil = ticket_info.RenewTill?.ToDateTime() ?? DateTime.MinValue;
        Ticket = credential.Tickets[0];
        Credential = credential;
    }

    internal static bool TryParse(KERB_EXTERNAL_TICKET ticket, bool krb_cred, out KerberosExternalTicket result)
    {
        result = null;
        try
        {
            var ret = new KerberosExternalTicket();
            ret.ServiceName = ParseName(ticket.ServiceName);
            ret.TargetName = ParseName(ticket.TargetName);
            ret.ClientName = ParseName(ticket.ClientName);
            ret.DomainName = ticket.DomainName.ToString();
            ret.TargetDomainName = ticket.TargetDomainName.ToString();
            ret.AltTargetDomainName = ticket.AltTargetDomainName.ToString();
            ret.SessionKey = ParseKey(ret.ServiceName, ret.DomainName, ticket.SessionKey);
            ret.TicketFlags = (KerberosTicketFlags)ticket.TicketFlags.RotateBits();
            ret.Flags = ticket.Flags;
            ret.KeyExpirationTime = ticket.KeyExpirationTime.ToDateTime();
            ret.StartTime = ticket.StartTime.ToDateTime();
            ret.EndTime = ticket.EndTime.ToDateTime();
            ret.RenewUntil = ticket.RenewUntil.ToDateTime();
            ret.TimeSkew = new TimeSpan(ticket.TimeSkew.QuadPart);
            byte[] ticket_data = ticket.ReadTicket();
            DERValue[] values = DERParser.ParseData(ticket_data, 0);
            if (values.Length != 1)
                return false;
            if (krb_cred)
            {
                if (!KerberosCredential.TryParse(ticket_data, values, out KerberosCredential cred))
                    return false;
                ret.Credential = cred;
                ret.Ticket = cred.Tickets.FirstOrDefault();
            }
            else
            {
                ret.Ticket = KerberosTicket.Parse(values[0]);
            }
            result = ret;
            return true;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Overridden ToString method.
    /// </summary>
    /// <returns>The ticket as a string.</returns>
    public override string ToString() => ServiceName.ToString();
    #endregion

    #region Public Properties
    /// <summary>
    /// Service name.
    /// </summary>
    public KerberosPrincipalName ServiceName { get; private set; }
    /// <summary>
    /// Target name.
    /// </summary>
    public KerberosPrincipalName TargetName { get; private set; }
    /// <summary>
    /// Client name.
    /// </summary>
    public KerberosPrincipalName ClientName { get; private set; }
    /// <summary>
    /// Domain name.
    /// </summary>
    public string DomainName { get; private set; }
    /// <summary>
    /// Target domain name.
    /// </summary>
    public string TargetDomainName { get; private set; }
    /// <summary>
    /// Alt target domain name.
    /// </summary>
    public string AltTargetDomainName { get; private set; }
    /// <summary>
    /// Session key for ticket.
    /// </summary>
    public KerberosAuthenticationKey SessionKey { get; private set; }
    /// <summary>
    /// Ticket flags.
    /// </summary>
    public KerberosTicketFlags TicketFlags { get; private set; }
    /// <summary>
    /// Additional reserved flags.
    /// </summary>
    public int Flags { get; private set; }
    /// <summary>
    /// Key expiration time.
    /// </summary>
    public DateTime KeyExpirationTime { get; private set; }
    /// <summary>
    /// Ticket start time.
    /// </summary>
    public DateTime StartTime { get; private set; }
    /// <summary>
    /// Ticket end time.
    /// </summary>
    public DateTime EndTime { get; private set; }
    /// <summary>
    /// Ticket renew time.
    /// </summary>
    public DateTime RenewUntil { get; private set; }
    /// <summary>
    /// Time skew.
    /// </summary>
    public TimeSpan TimeSkew { get; private set; }
    /// <summary>
    /// Ticket.
    /// </summary>
    public KerberosTicket Ticket { get; private set; }
    /// <summary>
    /// The ticket if a KRB_CRED was requested.
    /// </summary>
    public KerberosCredential Credential { get; private set; }
    #endregion

    #region Conversion Operators
    /// <summary>
    /// Explicit conversion to a KerberosCredential.
    /// </summary>
    /// <param name="ticket">The external ticket.</param>
    public static explicit operator KerberosCredential(KerberosExternalTicket ticket) => ticket.Credential;
    #endregion
}
