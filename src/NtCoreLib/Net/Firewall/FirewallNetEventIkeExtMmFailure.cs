﻿//  Copyright 2021 Google LLC. All Rights Reserved.
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

using NtCoreLib.Utilities.Memory;
using NtCoreLib.Win32;
using System.Collections.Generic;
using System.Linq;

namespace NtCoreLib.Net.Firewall;

/// <summary>
///  Class to represent an IKEEXT main mode failure event.
/// </summary>
public sealed class FirewallNetEventIkeExtMmFailure : FirewallNetEvent
{
    /// <summary>
    /// Windows error code for the failure
    /// </summary>
    public Win32Error FailureErrorCode { get; }
    /// <summary>
    /// Point of failure
    /// </summary>
    public IPsecFailurePoint FailurePoint { get; }
    /// <summary>
    /// Flags for the failure event
    /// </summary>
    public IkeExtMmFailureFlags FailureFlags { get; }
    /// <summary>
    /// IKE or Authip.
    /// </summary>
    public IkeExtKeyModuleType KeyingModuleType { get; }
    /// <summary>
    /// Main mode state
    /// </summary>
    public IkeExtMmSaState MmState { get; }
    /// <summary>
    /// Initiator or Responder
    /// </summary>
    public IkeExtSaRole SaRole { get; }
    /// <summary>
    /// Authentication method
    /// </summary>
    public IkeExtAuthenticationMethodType MmAuthMethod { get; }
    /// <summary>
    /// Hash (SHA thumbprint) of the end certificate corresponding to failures 
    /// that happen during building or validating certificate chains.
    /// </summary>
    public byte[] EndCertHash { get; }
    /// <summary>
    /// LUID for the MM SA
    /// </summary>
    public long MmId { get; }
    /// <summary>
    /// Main mode filter ID
    /// </summary>
    public ulong MmFilterId { get; }
    /// <summary>
    /// Name of local security principal that was authenticated, if available. 
    /// If not available, an empty string will be stored.
    /// </summary>
    public string LocalPrincipalNameForAuth { get; }
    /// <summary>
    /// Name of remote security principal that was authenticated, if available.
    /// If not available, an empty string will be stored. 
    /// </summary>
    public string RemotePrincipalNameForAuth { get; }

    /// <summary>
    /// Array of group SIDs corresponding to the local security principal that 
    /// was authenticated, if available.
    /// </summary>
    public IReadOnlyList<string> LocalPrincipalGroupSids { get; }

    /// <summary>
    /// Array of group SIDs corresponding to the remote security principal that 
    /// was authenticated, if available.
    /// </summary>
    public IReadOnlyList<string> RemotePrincipalGroupSids { get; }

    internal FirewallNetEventIkeExtMmFailure(IFwNetEvent net_event) : base(net_event)
    {
        var ev = net_event.Value.ReadStruct<FWPM_NET_EVENT_IKEEXT_MM_FAILURE1>();
        FailureErrorCode = ev.failureErrorCode;
        FailurePoint = ev.failurePoint;
        FailureFlags = ev.flags;
        KeyingModuleType = ev.keyingModuleType;
        MmState = ev.mmState;
        SaRole = ev.saRole;
        MmAuthMethod = ev.mmAuthMethod;
        EndCertHash = ev.endCertHash;
        MmId = ev.mmId;
        MmFilterId = ev.mmFilterId;
        LocalPrincipalNameForAuth = ev.localPrincipalNameForAuth;
        RemotePrincipalNameForAuth = ev.remotePrincipalNameForAuth;
        LocalPrincipalGroupSids = ev.localPrincipalGroupSids.ReadStringArray(ev.numLocalPrincipalGroupSids)?.ToList().AsReadOnly();
        RemotePrincipalGroupSids = ev.remotePrincipalGroupSids.ReadStringArray(ev.numRemotePrincipalGroupSids)?.ToList().AsReadOnly();
    }
}
