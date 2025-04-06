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

using NtCoreLib.Win32;
using NtCoreLib.Win32.Windows.Interop;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NtCoreLib;

/// <summary>
/// Structure to represent a Window.
/// </summary>
public struct NtWindow
{
    #region Public Methods
    /// <summary>
    /// The Window Handle.
    /// </summary>
    public IntPtr Handle { get; }

    /// <summary>
    /// Get Process ID for the Window.
    /// </summary>
    public int ProcessId => Query(QueryWindowType.ProcessId);

    /// <summary>
    /// Get the Thread ID for the Window.
    /// </summary>
    public int ThreadId => Query(QueryWindowType.ThreadId);

    /// <summary>
    /// Get the real owner Process ID of the Window.
    /// </summary>
    public int Owner => Query(QueryWindowType.Owner);

    /// <summary>
    /// Get the class name for the Window.
    /// </summary>
    public string ClassName => GetClassName(false, false).GetResultOrDefault(string.Empty);
    #endregion

    #region Public Methods
    /// <summary>
    /// Send a message to the Window, Unicode.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <returns>The send result.</returns>
    public IntPtr SendMessage(int message, IntPtr wparam, IntPtr lparam)
    {
        return Win32NativeMethods.SendMessageW(Handle, message, wparam, lparam);
    }

    /// <summary>
    /// Send a message to the Window, ANSI.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <returns>The send result.</returns>
    public IntPtr SendMessageAnsi(int message, IntPtr wparam, IntPtr lparam)
    {
        return Win32NativeMethods.SendMessageA(Handle, message, wparam, lparam);
    }

    /// <summary>
    /// Post a message to the Window, Unicode.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The send result.</returns>
    public NtStatus PostMessage(int message, IntPtr wparam, IntPtr lparam, bool throw_on_error)
    {
        return PrivatePostMessage(Win32NativeMethods.PostMessageW, message, wparam, lparam, throw_on_error);
    }

    /// <summary>
    /// Post a message to the Window, Unicode.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <returns>The send result.</returns>
    public void PostMessage(int message, IntPtr wparam, IntPtr lparam)
    {
        PostMessage(message, wparam, lparam, true);
    }

    /// <summary>
    /// Send a message to the Window, ANSI.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The send result.</returns>
    public NtStatus PostMessageAnsi(int message, IntPtr wparam, IntPtr lparam, bool throw_on_error)
    {
        return PrivatePostMessage(Win32NativeMethods.PostMessageA, message, wparam, lparam, throw_on_error);
    }

    /// <summary>
    /// Send a message to the Window, ANSI.
    /// </summary>
    /// <param name="message">The message to send.</param>
    /// <param name="wparam">The WPARAM.</param>
    /// <param name="lparam">The LPARAM.</param>
    /// <returns>The send result.</returns>
    public void PostMessageAnsi(int message, IntPtr wparam, IntPtr lparam)
    {
        PostMessageAnsi(message, wparam, lparam, true);
    }

    #endregion

    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    public NtWindow(IntPtr handle)
    {
        Handle = handle;
    }

    /// <summary>
    /// Constructor.
    /// </summary>
    /// <param name="handle">Window handle.</param>
    public NtWindow(long handle)
    {
        Handle = new IntPtr(handle);
    }
    #endregion

    #region Static Properties
    /// <summary>
    /// Get the NULL window handle.
    /// </summary>
    public static NtWindow Null => new();

    /// <summary>
    /// Get the desktop window.
    /// </summary>
    public static NtWindow Desktop => Null;

    /// <summary>
    /// Get the broadcast window.
    /// </summary>
    public static NtWindow Broadcast => new(0xFFFF);

    /// <summary>
    /// Get all Top Level windows.
    /// </summary>
    public static IEnumerable<NtWindow> Windows => GetWindows(null, Null, false, true, 0);
    #endregion

    #region Static Methods
    /// <summary>
    /// Enumerate window handles.
    /// </summary>
    /// <param name="desktop">Desktop containing the Windows. Optional.</param>
    /// <param name="parent">The parent Window. Optional.</param>
    /// <param name="enum_children">True to enumerate child Windows.</param>
    /// <param name="hide_immersive">Hide immersive Windows.</param>
    /// <param name="thread_id">The thread ID that owns the Window.</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The enumerated Window Handles.</returns>
    public static NtResult<IEnumerable<NtWindow>> GetWindows(NtDesktop desktop, NtWindow parent,
        bool enum_children, bool hide_immersive, int thread_id, bool throw_on_error)
    {
        int count = 64;
        while (true)
        {
            IntPtr[] handles = new IntPtr[count];
            NtStatus status = NtSystemCalls.NtUserBuildHwndList(desktop.GetHandle(), parent.Handle, enum_children,
                hide_immersive, thread_id, handles.Length, handles, out int required_count);
            if (status.IsSuccess())
            {
                return handles.Take(required_count).Select(i => new NtWindow(i)).CreateResult();
            }
            if (status != NtStatus.STATUS_BUFFER_TOO_SMALL || count > required_count)
            {
                return status.CreateResultFromError<IEnumerable<NtWindow>>(throw_on_error);
            }
            count = required_count;
        }
    }

    /// <summary>
    /// Enumerate window handles.
    /// </summary>
    /// <param name="desktop">Desktop containing the Windows. Optional.</param>
    /// <param name="parent">The parent Window. Optional.</param>
    /// <param name="enum_children">True to enumerate child Windows.</param>
    /// <param name="hide_immersive">Hide immersive Windows.</param>
    /// <param name="thread_id">The thread ID that owns the Window.</param>
    /// <returns>The enumerated Window Handles.</returns>
    public static IEnumerable<NtWindow> GetWindows(NtDesktop desktop, NtWindow parent,
        bool enum_children, bool hide_immersive, int thread_id)
    {
        return GetWindows(desktop, parent, enum_children, hide_immersive, thread_id, true).Result;
    }

    #endregion

    #region Private Members
    private int Query(QueryWindowType query)
    {
        return NtSystemCalls.NtUserQueryWindow(Handle, query);
    }

    private NtResult<string> GetClassName(bool real_name, bool throw_on_error)
    {
        using var str = new UnicodeStringAllocated();
        int length = NtSystemCalls.NtUserGetClassName(Handle, real_name, str);
        if (length == 0)
        {
            return Win32Utils.CreateResultFromDosError<string>(throw_on_error);
        }

        str.String.Length = (ushort)(length * 2);

        return str.ToString().CreateResult();
    }

    private NtStatus PrivatePostMessage(Func<IntPtr, int, IntPtr, IntPtr, bool> func, int message, IntPtr wparam, IntPtr lparam, bool throw_on_error)
    {
        return func(Handle, message, wparam, lparam).ToNtException(throw_on_error);
    }

    #endregion
}
