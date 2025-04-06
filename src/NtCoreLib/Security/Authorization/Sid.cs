﻿//  Copyright 2016 Google Inc. All Rights Reserved.
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

using NtCoreLib.Native.SafeHandles;
using NtCoreLib.Utilities.Memory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;

namespace NtCoreLib.Security.Authorization;

/// <summary>
/// Class to represent a Security Identifier.
/// </summary>
public sealed class Sid
{
    #region Private Members

    private Sid()
    {
    }

    private NtStatus InitializeFromPointer(IntPtr sid)
    {
        if (!NtRtl.RtlValidSid(sid))
            return NtStatus.STATUS_INVALID_SID;

        IntPtr authority = NtRtl.RtlIdentifierAuthoritySid(sid);
        Authority = authority.ReadStruct<SidIdentifierAuthority>();
        int sub_authority_count = Marshal.ReadByte(NtRtl.RtlSubAuthorityCountSid(sid));
        List<uint> sub_auth = new();
        for (int i = 0; i < sub_authority_count; ++i)
        {
            sub_auth.Add((uint)Marshal.ReadInt32(NtRtl.RtlSubAuthoritySid(sid, i), 0));
        }
        SubAuthorities = sub_auth.AsReadOnly();
        return NtStatus.STATUS_SUCCESS;
    }

    private NtStatus InitializeFromBinaryReader(BinaryReader reader)
    {
        try
        {
            byte revision = reader.ReadByte();
            if (revision != 1)
                return NtStatus.STATUS_INVALID_SID;
            byte sub_authority_count = reader.ReadByte();
            Authority = new SidIdentifierAuthority(reader.ReadAllBytes(6));
            List<uint> sub_auth = new();
            for (int i = 0; i < sub_authority_count; ++i)
            {
                sub_auth.Add(reader.ReadUInt32());
            }
            SubAuthorities = sub_auth.AsReadOnly();
            return NtStatus.STATUS_SUCCESS;
        }
        catch (EndOfStreamException)
        {
            return NtStatus.STATUS_INVALID_SID;
        }
    }

    private NtStatus InitializeFromBytes(byte[] sid)
    {
        if (sid.Length < 8)
            return NtStatus.STATUS_INVALID_SID;

        MemoryStream stm = new(sid);
        return InitializeFromBinaryReader(new BinaryReader(stm));
    }

    #endregion

    #region Public Properties
    /// <summary>
    /// Maximum size of a SID buffer.
    /// </summary>
    public const int MaximumSidSize = 256;

    /// <summary>
    /// The SIDs authority.
    /// </summary>
    public SidIdentifierAuthority Authority { get; private set; }

    /// <summary>
    /// List of the SIDs sub authorities.
    /// </summary>
    public IReadOnlyList<uint> SubAuthorities { get; private set; }

    /// <summary>
    /// Get the account name of the SID or the SDDL form if no corresponding name.
    /// </summary>
    public string Name => NtSecurity.GetNameForSid(this).QualifiedName;

    /// <summary>
    /// Get the parent SID. Returns null if there's no parent.
    /// </summary>
    public Sid Parent => SubAuthorities.Count > 0 ? new Sid(Authority, SubAuthorities.Take(SubAuthorities.Count - 1).ToArray()) : null;
    #endregion

    #region Constructors
    /// <summary>
    /// Constructor for authority and sub authorities.
    /// </summary>
    /// <param name="authority">The identifier authority.</param>
    /// <param name="sub_authorities">The sub authorities.</param>
    public Sid(SidIdentifierAuthority authority, params uint[] sub_authorities)
    {
        Authority = new SidIdentifierAuthority(authority.Value);
        SubAuthorities = new List<uint>(sub_authorities);
    }

    /// <summary>
    /// Constructor for authority and sub authorities.
    /// </summary>
    /// <param name="authority">The identifier authority.</param>
    /// <param name="sub_authorities">The sub authorities.</param>
    public Sid(SecurityAuthority authority, params uint[] sub_authorities)
        : this(new SidIdentifierAuthority(authority), sub_authorities)
    {
    }

    /// <summary>
    /// Constructor from an unmanged buffer.
    /// </summary>
    /// <param name="sid">A pointer to a buffer containing a valid SID.</param>
    /// <exception cref="NtException">Thrown if the buffer is not valid.</exception>
    public Sid(IntPtr sid)
    {
        InitializeFromPointer(sid).ToNtException();
    }

    /// <summary>
    /// Constructor from an unmanged buffer.
    /// </summary>
    /// <param name="sid">A safe buffer containing a valid SID.</param>
    /// <exception cref="NtException">Thrown if the buffer is not valid.</exception>
    public Sid(SafeBuffer sid)
        : this(sid.DangerousGetHandle())
    {
    }

    /// <summary>
    /// Constructor from a safe SID handle.
    /// </summary>
    /// <param name="sid">A safe SID handle containing a valid SID.</param>
    /// <exception cref="NtException">Thrown if the buffer is not valid.</exception>
    public Sid(SafeSidBufferHandle sid)
        : this(sid.DangerousGetHandle())
    {
    }

    /// <summary>
    /// Constructor from an manged buffer.
    /// </summary>
    /// <param name="sid">A buffer containing a valid SID.</param>
    /// <exception cref="NtException">Thrown if the buffer is not valid.</exception>
    public Sid(byte[] sid)
    {
        InitializeFromBytes(sid).ToNtException();
    }

    /// <summary>
    /// Constructor from existing Sid.
    /// </summary>
    /// <param name="sid">The existing Sid.</param>
    public Sid(Sid sid) : this(sid.Authority, sid.SubAuthorities.ToArray())
    {
    }

    /// <summary>
    /// Constructor from an SDDL string.
    /// </summary>
    /// <param name="sid">The SID in SDDL format.</param>
    /// <example>
    /// new Sid("S-1-0-0");
    /// new Sid("WD");
    /// </example>
    public Sid(string sid) : this(NtSecurity.SidFromSddl(sid))
    {
    }

    /// <summary>
    /// Constructor from a SID name.
    /// </summary>
    /// <param name="sid_name">The SID name.</param>
    public Sid(SidName sid_name)
        : this(sid_name.Sid.Authority, sid_name.Sid.SubAuthorities.ToArray())
    {
    }

    /// <summary>
    /// Construct a SID from a binary reader.
    /// </summary>
    /// <param name="reader">The binary reader.</param>
    internal Sid(BinaryReader reader)
    {
        InitializeFromBinaryReader(reader).ToNtException();
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Convert the SID to a safe buffer.
    /// </summary>
    /// <returns>The safe buffer containing the SID.</returns>
    public SafeSidBufferHandle ToSafeBuffer()
    {
        SafeSidBufferHandle sid;
        try
        {
            NtRtl.RtlAllocateAndInitializeSidEx(Authority,
                (byte)SubAuthorities.Count, SubAuthorities.ToArray(), out sid).ToNtException();
        }
        catch (EntryPointNotFoundException)
        {
            // If not found then we're on a downlevel platform, try and use the old version 
            // which is limited to 8 subauthorities.
            uint[] sub_authories = SubAuthorities.ToArray();
            if (sub_authories.Length != 8)
            {
                Array.Resize(ref sub_authories, 8);
            }
            NtRtl.RtlAllocateAndInitializeSid(Authority, (byte)SubAuthorities.Count,
                sub_authories[0], sub_authories[1], sub_authories[2], sub_authories[3],
                sub_authories[4], sub_authories[5], sub_authories[6], sub_authories[7],
                out sid).ToNtException();
        }
        return sid;
    }

    /// <summary>
    /// Convert to a managed byte array.
    /// </summary>
    /// <returns>The managed byte array.</returns>
    public byte[] ToArray()
    {
        MemoryStream stm = new();
        BinaryWriter writer = new(stm);

        writer.Write((byte)1);
        writer.Write((byte)SubAuthorities.Count);
        writer.Write(Authority.Value);
        foreach (uint sub in SubAuthorities)
        {
            writer.Write(sub);
        }
        return stm.ToArray();
    }

    /// <summary>
    /// Compares two sids to see if their prefixes are the same. The sids must have the same number of subauthorities.
    /// </summary>
    /// <param name="sid">The sid to compare against</param>
    /// <returns>True if the sids share a prefix.</returns>
    public bool EqualPrefix(Sid sid)
    {
        using SafeSidBufferHandle sid1 = ToSafeBuffer(), sid2 = sid.ToSafeBuffer();
        return NtRtl.RtlEqualPrefixSid(sid1, sid2);
    }

    /// <summary>
    /// Compare two Sids.
    /// </summary>
    /// <param name="obj">The other Sid to compare.</param>
    /// <returns>True if the Sids are equal.</returns>
    public override bool Equals(object obj)
    {
        if (obj is not Sid sid)
        {
            return false;
        }

        if (!Authority.Equals(sid.Authority))
        {
            return false;
        }

        if (SubAuthorities.Count != sid.SubAuthorities.Count)
        {
            return false;
        }

        for (int i = 0; i < SubAuthorities.Count; ++i)
        {
            if (SubAuthorities[i] != sid.SubAuthorities[i])
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Equality operator.
    /// </summary>
    /// <param name="a">Sid 1</param>
    /// <param name="b">Sid 2</param>
    /// <returns>True if the Sids are equal.</returns>
    public static bool operator ==(Sid a, Sid b)
    {
        if (ReferenceEquals(a, b))
        {
            return true;
        }

        if (a is null)
        {
            return false;
        }

        if (b is null)
        {
            return false;
        }

        return a.Equals(b);
    }

    /// <summary>
    /// Inequality operator.
    /// </summary>
    /// <param name="a">Sid 1</param>
    /// <param name="b">Sid 2</param>
    /// <returns>True if the Sids are not equal.</returns>
    public static bool operator !=(Sid a, Sid b)
    {
        return !(a == b);
    }

    /// <summary>
    /// Get hash code.
    /// </summary>
    /// <returns>The hash code.</returns>
    public override int GetHashCode()
    {
        int sub_hash_code = 0;
        foreach (uint sub_auth in SubAuthorities)
        {
            sub_hash_code ^= sub_auth.GetHashCode();
        }
        return Authority.GetHashCode() ^ sub_hash_code;
    }

    /// <summary>
    /// Convert to an SDDL format string.
    /// </summary>
    /// <returns>The SDDL format string (e.g. S-1-1-0)</returns>
    public override string ToString()
    {
        if (SubAuthorities.Count == 0)
        {
            return $"S-1-{Authority.ToInt64()}";
        }
        return $"S-1-{Authority.ToInt64()}-{string.Join("-", SubAuthorities)}";
    }

    /// <summary>
    /// Does this SID dominate another.
    /// </summary>
    /// <param name="sid">The other SID.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>True if the sid dominates.</returns>
    public NtResult<bool> Dominates(Sid sid, bool throw_on_error)
    {
        return NtRtl.RtlSidDominates(ToArray(), sid.ToArray(),
            out bool result).CreateResult(throw_on_error, () => result);
    }

    /// <summary>
    /// Does this SID dominate another.
    /// </summary>
    /// <param name="sid">The other SID.</param>
    /// <returns>True if the sid dominates.</returns>
    public bool Dominates(Sid sid) => Dominates(sid, true).Result;

    /// <summary>
    /// Does this SID dominate another for trust.
    /// </summary>
    /// <param name="sid">The other SID.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>True if the sid dominates.</returns>
    public NtResult<bool> DominatesForTrust(Sid sid, bool throw_on_error)
    {
        return NtRtl.RtlSidDominatesForTrust(ToArray(), sid.ToArray(),
            out bool result).CreateResult(throw_on_error, () => result);
    }

    /// <summary>
    /// Does this SID dominate another for trust.
    /// </summary>
    /// <param name="sid">The other SID.</param>
    /// <returns>True if the sid dominates.</returns>
    public bool DominatesForTrust(Sid sid) => DominatesForTrust(sid, true).Result;

    /// <summary>
    /// Checks if the SID starts with the specified SID.
    /// </summary>
    /// <param name="sid">The specified SID to check against.</param>
    /// <returns>True if the current SID starts with the specified SID.</returns>
    public bool StartsWith(Sid sid)
    {
        if (!Authority.Equals(sid.Authority))
            return false;
        if (sid.SubAuthorities.Count > SubAuthorities.Count)
            return false;
        for (int i = 0; i < sid.SubAuthorities.Count; i++)
        {
            if (sid.SubAuthorities[i] != SubAuthorities[i])
                return false;
        }
        return true;
    }

    /// <summary>
    /// Create a SID relative to this one.
    /// </summary>
    /// <param name="rids">The list of RIDs.</param>
    /// <returns>The relative SID.</returns>
    public Sid CreateRelative(params uint[] rids)
    {
        if (rids.Length < 1)
            throw new ArgumentException("Must specify at least one RID.");
        List<uint> new_rids = new(SubAuthorities);
        new_rids.AddRange(rids);
        return new Sid(Authority, new_rids.ToArray());
    }

    /// <summary>
    /// Create a random relative SID with a known RID count.
    /// </summary>
    /// <param name="rid_count">The number of random RIDs to add.</param>
    /// <returns>The random SID.</returns>
    public Sid CreateRandom(int rid_count)
    {
        if (rid_count < 1)
            throw new ArgumentOutOfRangeException(nameof(rid_count), "RID count should be at least 1.");

        byte[] random_rids = new byte[rid_count * 4];
        new Random().NextBytes(random_rids);
        uint[] rids = new uint[rid_count];
        Buffer.BlockCopy(random_rids, 0, rids, 0, random_rids.Length);
        return CreateRelative(rids);
    }

    /// <summary>
    /// Create a SID sibling to this SID.
    /// </summary>
    /// <param name="rids">The RIDs to replace the final RID with.</param>
    /// <returns>The sibling SID.</returns>
    /// <remarks>This replaces the final RID with one or more addditional RIDs.</remarks>
    public Sid CreateSibling(params uint[] rids)
    {
        if (rids.Length < 1)
            throw new ArgumentOutOfRangeException(nameof(rids), "RID count should be at least 1.");

        if (SubAuthorities.Count < 1)
            throw new InvalidOperationException("To create a sibling SID the original must have at least 1 sub authority.");

        List<uint> new_rids = new(SubAuthorities.Take(SubAuthorities.Count - 1));
        new_rids.AddRange(rids);
        return new Sid(Authority, new_rids.ToArray());
    }

    /// <summary>
    /// Get the SID name for this SID.
    /// </summary>
    /// <param name="bypass_cache">True to bypass the SID name cache.</param>
    /// <returns>The SID name.</returns>
    public SidName GetName(bool bypass_cache)
    {
        return NtSecurity.GetNameForSid(this, bypass_cache);
    }

    /// <summary>
    /// Get the SID name for this SID.
    /// </summary>
    /// <returns>The SID name.</returns>
    public SidName GetName()
    {
        return GetName(false);
    }

    #endregion

    #region Static Methods
    /// <summary>
    /// Convert an SDDL SID string to a Sid
    /// </summary>
    /// <param name="sddl">The SDDL SID string</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The converted Sid</returns>
    /// <exception cref="NtException">Thrown if cannot convert from a SDDL string.</exception>
    public static NtResult<Sid> Parse(string sddl, bool throw_on_error)
    {
        return NtSecurity.SidFromSddl(sddl, throw_on_error);
    }

    /// <summary>
    /// Convert an SDDL SID string to a Sid
    /// </summary>
    /// <param name="sddl">The SDDL SID string</param>
    /// <returns>The converted Sid</returns>
    /// <exception cref="NtException">Thrown if cannot convert from a SDDL string.</exception>
    public static Sid Parse(string sddl)
    {
        return Parse(sddl, true).Result;
    }

    /// <summary>
    /// Parse a byte array.
    /// </summary>
    /// <param name="sid">The byte array to parse.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The parsed SID.</returns>
    public static NtResult<Sid> Parse(byte[] sid, bool throw_on_error)
    {
        Sid ret = new();
        return ret.InitializeFromBytes(sid).CreateResult(throw_on_error, () => ret);
    }

    /// <summary>
    /// Parse a byte array.
    /// </summary>
    /// <param name="sid">The pointer to parse.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The parsed SID.</returns>
    public static NtResult<Sid> Parse(IntPtr sid, bool throw_on_error)
    {
        Sid ret = new();
        return ret.InitializeFromPointer(sid).CreateResult(throw_on_error, () => ret);
    }

    #endregion
}
