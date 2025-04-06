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

using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Security.Authorization;
using NtCoreLib.Utilities.Collections;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.Security;

namespace NtCoreLib.Win32.Security.Sam;

/// <summary>
/// Class to represent a SAM user.
/// </summary>
public class SamUser : SamObject
{
    #region Private Members
    private NtResult<T> Query<T, S>(UserInformationClass info_class, Func<S, T> func, bool throw_on_error) where S : struct
    {
        return SecurityNativeMethods.SamQueryInformationUser(Handle, info_class, out SafeSamMemoryBuffer buffer).CreateResult(throw_on_error, () =>
        {
            using (buffer)
            {
                buffer.Initialize<S>(1);
                return func(buffer.Read<S>(0));
            }
        });
    }

    private static bool CheckHash(bool present, ref byte[] old_hash, ref byte[] new_hash)
    {
        if (!present)
        {
            old_hash = new byte[16];
            new_hash = old_hash;
            return true;
        }
        if (old_hash?.Length != 16)
            return false;
        if (new_hash?.Length != 16)
            return false;
        return true;
    }
    #endregion

    #region Internal Members
    internal SamUser(SafeSamHandle handle, SamUserAccessRights granted_access, string server_name, string user_name, Sid sid)
        : base(handle, granted_access, SamUtils.SAM_USER_NT_TYPE_NAME, user_name, server_name)
    {
        Sid = sid;
        Name = user_name;
    }
    #endregion

    #region Public Methods
    /// <summary>
    /// Get full name for the user.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The full name of the user.</returns>
    public NtResult<string> GetFullName(bool throw_on_error)
    {
        return Query(UserInformationClass.UserFullNameInformation, (USER_FULL_NAME_INFORMATION f) => f.FullName.ToString(), throw_on_error);
    }

    /// <summary>
    /// Get home directory for the user.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The home directory of the user.</returns>
    public NtResult<string> GetHomeDirectory(bool throw_on_error)
    {
        return Query(UserInformationClass.UserHomeInformation, (USER_HOME_INFORMATION f) => f.HomeDirectory.ToString(), throw_on_error);
    }

    /// <summary>
    /// Get primary group ID for the user.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The primary group ID of the user.</returns>
    public NtResult<uint> GetPrimaryGroupId(bool throw_on_error)
    {
        return Query(UserInformationClass.UserPrimaryGroupInformation, (USER_PRIMARY_GROUP_INFORMATION f) => f.PrimaryGroupId, throw_on_error);
    }

    /// <summary>
    /// Get user account control flags for the user.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The user account control flags of the user.</returns>
    public NtResult<UserAccountControlFlags> GetUserAccountControl(bool throw_on_error)
    {
        return Query(UserInformationClass.UserControlInformation, (USER_CONTROL_INFORMATION f) => (UserAccountControlFlags)f.UserAccountControl, throw_on_error);
    }

    /// <summary>
    /// Change a user's password.
    /// </summary>
    /// <param name="old_password">The old password.</param>
    /// <param name="new_password">The new password.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public NtStatus ChangePassword(SecureString old_password, SecureString new_password, bool throw_on_error)
    {
        using var list = new DisposableList();
        var old_pwd_buf = list.AddResource(new SecureStringMarshalBuffer(old_password));
        var new_pwd_buf = list.AddResource(new SecureStringMarshalBuffer(new_password));

        return SecurityNativeMethods.SamChangePasswordUser(Handle,
            new UnicodeStringSecure(old_pwd_buf, old_password.Length),
            new UnicodeStringSecure(new_pwd_buf, new_password.Length)).ToNtException(throw_on_error);
    }

    /// <summary>
    /// Change a user's password.
    /// </summary>
    /// <param name="old_password">The old password.</param>
    /// <param name="new_password">The new password.</param>
    public void ChangePassword(SecureString old_password, SecureString new_password)
    {
        ChangePassword(old_password, new_password, true);
    }

    /// <summary>
    /// Set a user's password.
    /// </summary>
    /// <param name="password">The password to set.</param>
    /// <param name="expired">Whether the password has expired.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    public NtStatus SetPassword(SecureString password, bool expired, bool throw_on_error)
    {
        using var pwd_buf = new SecureStringMarshalBuffer(password);
        var set_info = new USER_SET_PASSWORD_INFORMATION();
        set_info.Password = new UnicodeStringInSecure(pwd_buf, password.Length);
        set_info.PasswordExpired = expired;
        using var buf = set_info.ToBuffer();
        return SecurityNativeMethods.SamSetInformationUser(Handle,
            UserInformationClass.UserSetPasswordInformation, buf).ToNtException(throw_on_error);
    }

    /// <summary>
    /// Set a user's password.
    /// </summary>
    /// <param name="password">The password to set.</param>
    /// <param name="expired">Whether the password has expired.</param>
    public void SetPassword(SecureString password, bool expired)
    {
        SetPassword(password, expired, true);
    }

    /// <summary>
    /// Change password using NT/LM hashes.
    /// </summary>
    /// <param name="lm_present">Is the LM hash present.</param>
    /// <param name="old_lm">The old LM hash.</param>
    /// <param name="new_lm">The new LM hash.</param>
    /// <param name="nt_present">Is the NT hash present.</param>
    /// <param name="old_nt">The old NT hash.</param>
    /// <param name="new_nt">The new NT hash.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    /// <exception cref="ArgumentException">Thrown if arguments invalid.</exception>
    public NtStatus ChangePassword(bool lm_present, byte[] old_lm, byte[] new_lm, 
        bool nt_present, byte[] old_nt, byte[] new_nt, bool throw_on_error)
    {
        if (!CheckHash(lm_present, ref old_lm, ref new_lm))
            throw new ArgumentException("Must provide valid LM hashes if present.", nameof(lm_present));

        if (!CheckHash(nt_present, ref old_nt, ref new_nt))
            throw new ArgumentException("Must provide valid NT hashes if present.", nameof(nt_present));

        return SecurityNativeMethods.SamiChangePasswordUser(Handle,
            lm_present, old_lm, new_lm, nt_present, old_nt, new_nt).ToNtException(throw_on_error);
    }

    /// <summary>
    /// Change password using NT/LM hashes.
    /// </summary>
    /// <param name="lm_present">Is the LM hash present.</param>
    /// <param name="old_lm">The old LM hash.</param>
    /// <param name="new_lm">The new LM hash.</param>
    /// <param name="nt_present">Is the NT hash present.</param>
    /// <param name="old_nt">The old NT hash.</param>
    /// <param name="new_nt">The new NT hash.</param>
    /// <exception cref="ArgumentException">Thrown if arguments invalid.</exception>
    public void ChangePassword(bool lm_present, byte[] old_lm, byte[] new_lm,
        bool nt_present, byte[] old_nt, byte[] new_nt)
    {
        ChangePassword(lm_present, old_lm, new_lm, nt_present, old_nt, new_nt, true);
    }

    #endregion

    #region Public Properties
    /// <summary>
    /// The user name.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The SID of the user.
    /// </summary>
    public Sid Sid { get; }

    /// <summary>
    /// Get full name for the user.
    /// </summary>
    public string FullName => GetFullName(true).Result;

    /// <summary>
    /// Get home directory for the user.
    /// </summary>
    public string HomeDirectory => GetHomeDirectory(true).Result;

    /// <summary>
    /// Get user account control flags for the user.
    /// </summary>
    public UserAccountControlFlags UserAccountControl => GetUserAccountControl(true).Result;

    /// <summary>
    /// Is the account disabled?
    /// </summary>
    public bool Disabled => UserAccountControl.HasFlagSet(UserAccountControlFlags.AccountDisabled);

    /// <summary>
    /// Get the primary group SID.
    /// </summary>
    public Sid PrimaryGroup => Sid.CreateSibling(GetPrimaryGroupId(true).Result);

    #endregion
}
