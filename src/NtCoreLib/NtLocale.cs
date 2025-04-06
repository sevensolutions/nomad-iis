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

using System;
using System.Runtime.InteropServices;

namespace NtCoreLib;

#pragma warning disable 1591
public enum NlsSectionType
{
    CodePage = 11,
    Normalization = 12,
}

public static partial class NtSystemCalls
{
    [DllImport("ntdll.dll")]
    public static extern NtStatus NtGetNlsSectionPtr(NlsSectionType NlsType, int CodePage, IntPtr ContextData, out IntPtr SectionPointer, out IntPtr SectionSize);

    [DllImport("ntdll.dll")]
    public static extern NtStatus NtSetDefaultLocale(bool ThreadOrSystem, uint Locale);

    [DllImport("ntdll.dll")]
    public static extern NtStatus NtQueryDefaultLocale(bool ThreadOrSystem, out uint Locale);
}
#pragma warning restore 1591

/// <summary>
/// Class to access NT locale information
/// </summary>
public static class NtLocale
{
    /// <summary>
    /// Get mapped NLS section
    /// </summary>
    /// <param name="type">The type of section</param>
    /// <param name="codepage">The codepage number</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The mapped section if it exists.</returns>
    public static NtResult<NtMappedSection> GetNlsSectionPtr(NlsSectionType type, int codepage, bool throw_on_error)
    {
        return NtSystemCalls.NtGetNlsSectionPtr(type, codepage, IntPtr.Zero, out IntPtr ptr, out IntPtr size)
            .CreateResult(throw_on_error, () => new NtMappedSection(ptr, size.ToInt64(), NtProcess.Current, false));
    }

    /// <summary>
    /// Get mapped NLS section
    /// </summary>
    /// <param name="type">The type of section</param>
    /// <param name="codepage">The codepage number</param>
    /// <returns>The mapped section if it exists.</returns>
    public static NtMappedSection GetNlsSectionPtr(NlsSectionType type, int codepage)
    {
        return GetNlsSectionPtr(type, codepage, true).Result;
    }

    /// <summary>
    /// Get default locale ID
    /// </summary>
    /// <param name="thread">True if the locale should be the thread's, otherwise the systems</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The locale ID</returns>
    public static NtResult<uint> GetDefaultLocal(bool thread, bool throw_on_error)
    {
        return NtSystemCalls.NtQueryDefaultLocale(thread, out uint locale).CreateResult(throw_on_error, () => locale);
    }

    /// <summary>
    /// Get default locale ID
    /// </summary>
    /// <param name="thread">True if the locale should be the thread's, otherwise the systems</param>
    /// <returns>The locale ID</returns>
    public static uint GetDefaultLocal(bool thread)
    {
        return GetDefaultLocal(thread, true).Result;
    }

    /// <summary>
    /// Set default locale
    /// </summary>
    /// <param name="thread">True if the locale should be the thread's, otherwise the systems</param>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <param name="locale">The locale ID</param>
    /// <returns>The NT status code.</returns>
    public static NtStatus SetDefaultLocale(bool thread, uint locale, bool throw_on_error)
    {
        return NtSystemCalls.NtSetDefaultLocale(thread, locale).ToNtException(throw_on_error);
    }

    /// <summary>
    /// Set default locale
    /// </summary>
    /// <param name="thread">True if the locale should be the thread's, otherwise the systems</param>
    /// <param name="locale">The locale ID</param>
    public static void SetDefaultLocale(bool thread, uint locale)
    {
        SetDefaultLocale(thread, locale, true);
    }
}
