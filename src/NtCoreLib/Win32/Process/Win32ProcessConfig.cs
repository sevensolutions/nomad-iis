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

using NtCoreLib.Native.SafeBuffers;
using NtCoreLib.Security;
using NtCoreLib.Security.Authorization;
using NtCoreLib.Utilities.Collections;
using NtCoreLib.Win32.Process.Interop;
using NtCoreLib.Win32.Security;
using NtCoreLib.Win32.Security.Authentication;
using NtCoreLib.Win32.Security.Interop;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

namespace NtCoreLib.Win32.Process;

/// <summary>
/// Win32 process creation configuration.
/// </summary>
public class Win32ProcessConfig
{
    #region Public Properties
    /// <summary>
    /// Specify security descriptor of process.
    /// </summary>
    public SecurityDescriptor ProcessSecurityDescriptor { get; set; }
    /// <summary>
    /// Specify process handle is inheritable.
    /// </summary>
    public bool InheritProcessHandle { get; set; }
    /// <summary>
    /// Specify security descriptor of thread.
    /// </summary>
    public SecurityDescriptor ThreadSecurityDescriptor { get; set; }
    /// <summary>
    /// Specify thread handle is inheritable.
    /// </summary>
    public bool InheritThreadHandle { get; set; }
    /// <summary>
    /// Specify to inherit handles.
    /// </summary>
    public bool InheritHandles { get; set; }
    /// <summary>
    /// Specify parent process.
    /// </summary>
    public NtProcess ParentProcess { get; set; }
    /// <summary>
    /// Specify path to application executable.
    /// </summary>
    public string ApplicationName { get; set; }
    /// <summary>
    /// Specify command line.
    /// </summary>
    public string CommandLine { get; set; }
    /// <summary>
    /// Specify creation flags.
    /// </summary>
    public CreateProcessFlags CreationFlags { get; set; }
    /// <summary>
    /// Specify environment block.
    /// </summary>
    public byte[] Environment { get; set; }
    /// <summary>
    /// Specify current directory.
    /// </summary>
    public string CurrentDirectory { get; set; }
    /// <summary>
    /// Specify desktop name.
    /// </summary>
    public string Desktop { get; set; }
    /// <summary>
    /// Specify window title.
    /// </summary>
    public string Title { get; set; }
    /// <summary>
    /// True to terminate the process when it's disposed.
    /// </summary>
    public bool TerminateOnDispose { get; set; }
    /// <summary>
    /// Specify the mitigation options.
    /// </summary>
    public ProcessMitigationOptions MitigationOptions { get; set; }
    /// <summary>
    /// Specify the mitigation options 2.
    /// </summary>
    public ProcessMitigationOptions2 MitigationOptions2 { get; set; }
    /// <summary>
    /// Specify win32k filter flags.
    /// </summary>
    public Win32kFilterFlags Win32kFilterFlags { get; set; }
    /// <summary>
    /// Specify win32k filter level.
    /// </summary>
    public int Win32kFilterLevel { get; set; }
    /// <summary>
    /// Specify PP level.
    /// </summary>
    public ProtectionLevel ProtectionLevel { get; set; }
    /// <summary>
    /// Specify list of handles to inherit.
    /// </summary>
    public List<IntPtr> InheritHandleList { get; }
    /// <summary>
    /// Specify the appcontainer Sid.
    /// </summary>
    public Sid AppContainerSid { get; set; }
    /// <summary>
    /// Specify the appcontainer capabilities.
    /// </summary>
    public List<Sid> Capabilities { get; }
    /// <summary>
    /// Specify LPAC.
    /// </summary>
    public bool LowPrivilegeAppContainer { get; set; }
    /// <summary>
    /// Set child process mitigation flags.
    /// </summary>
    public ChildProcessMitigationFlags ChildProcessMitigations { get; set; }
    /// <summary>
    /// Specify new process policy when creating a desktop bridge application.
    /// </summary>
    public ProcessDesktopAppBreakawayFlags DesktopAppBreakaway { get; set; }
    /// <summary>
    /// Specify a token to use for the new process.
    /// </summary>
    public NtToken Token { get; set; }
    /// <summary>
    /// Specify a stdin handle for the new process (you must inherit the handle).
    /// </summary>
    public IntPtr StdInputHandle { get; set; }
    /// <summary>
    /// Specify a stdout handle for the new process (you must inherit the handle).
    /// </summary>
    public IntPtr StdOutputHandle { get; set; }
    /// <summary>
    /// Specify a stderror handle for the new process (you must inherit the handle).
    /// </summary>
    public IntPtr StdErrorHandle { get; set; }
    /// <summary>
    /// Specify the package name to use.
    /// </summary>
    public string PackageName { get; set; }
    /// <summary>
    /// Specify handle to pseudo console.
    /// </summary>
    public IntPtr PseudoConsole { get; set; }
    /// <summary>
    /// Specify Base Named Objects isolation prefix.
    /// </summary>
    public string BnoIsolationPrefix { get; set; }
    /// <summary>
    /// Specify the safe open prompt original claim.
    /// </summary>
    public byte[] SafeOpenPromptOriginClaim { get; set; }
    /// <summary>
    /// When specifying the debug flags use this debug object instead of the current thread's object.
    /// </summary>
    public NtDebug DebugObject { get; set; }
    /// <summary>
    /// When specified do not fallback to using CreateProcessWithToken if CreateProcessWithUser fails.
    /// </summary>
    public bool NoTokenFallback
    {
        get => TokenCall == Win32ProcessConfigTokenCallFlags.AsUser;
        set => TokenCall = value ? Win32ProcessConfigTokenCallFlags.AsUser : Win32ProcessConfigTokenCallFlags.Both;
    }
    /// <summary>
    /// Specify additional extended flags.
    /// </summary>
    public ProcessExtendedFlags ExtendedFlags { get; set; }
    /// <summary>
    /// Specify list of handles to inherit.
    /// </summary>
    public List<NtJob> JobList { get; }
    /// <summary>
    /// Specify a service window station and desktop.
    /// </summary>
    public bool ServiceDesktop { get; set; }
    /// <summary>
    /// Specify authentication credentials for CreateProcessWithLogon.
    /// </summary>
    public UserCredentials Credentials { get; set; }
    /// <summary>
    /// Specify logon flags for the Credentials or when calling CreateProcessWithToken.
    /// </summary>
    public CreateProcessLogonFlags LogonFlags { get; set; }
    /// <summary>
    /// Specify the type of API to call when specifying a token.
    /// </summary>
    public Win32ProcessConfigTokenCallFlags TokenCall { get; set; }
    /// <summary>
    /// Specify component filter flags.
    /// </summary>
    public ProcessComponentFilterFlags ComponentFilter { get; set; }
    /// <summary>
    /// Specify the machine type for the new process. 
    /// </summary>
    public DllMachineType? MachineType { get; set; }
    #endregion

    #region Public Methods
    /// <summary>
    /// Add an object's handle to the list of inherited handles. 
    /// </summary>
    /// <param name="obj">The object to add.</param>
    /// <returns>The raw handle value.</returns>
    /// <remarks>Note that this doesn't maintain a reference to the object. It should be kept
    /// alive until the process has been created.</remarks>
    public IntPtr AddInheritedHandle(NtObject obj)
    {
        obj.Inherit = true;
        IntPtr handle = obj.Handle.DangerousGetHandle();
        InheritHandleList.Add(handle);
        return handle;
    }

    /// <summary>
    /// Add an AppContainer capability by name.
    /// </summary>
    /// <param name="capability_name">The name of the capability.</param>
    public void AddNamedCapability(string capability_name)
    {
        Capabilities.Add(NtSecurity.GetCapabilitySid(capability_name));
    }

    /// <summary>
    /// Add an AppContainer capability by name.
    /// </summary>
    /// <param name="capability">The capability SID.</param>
    public void AddCapability(Sid capability)
    {
        if (!NtSecurity.IsCapabilitySid(capability))
            throw new ArgumentException("Must specify a capability SID.", nameof(capability));
        Capabilities.Add(capability);
    }

    /// <summary>
    /// Set AppContainer SID from a package name.
    /// </summary>
    /// <param name="package_name">The package name.</param>
    public void SetAppContainerSidFromName(string package_name)
    {
        AppContainerSid = Win32Security.DerivePackageSidFromName(package_name);
    }

    /// <summary>
    /// Set the credentials for the process creation.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="domain">The domain.</param>
    /// <param name="password">The password.</param>
    public void SetCredentials(string username, string domain, string password)
    {
        Credentials = new UserCredentials(username, domain, password);
    }

    /// <summary>
    /// Create the process based on the configuration.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The created win32 process.</returns>
    public NtResult<Win32Process> Create(bool throw_on_error)
    {
        if (Token != null)
        {
            return TokenCall switch
            {
                Win32ProcessConfigTokenCallFlags.Both => CreateProcessFallback(throw_on_error),
                Win32ProcessConfigTokenCallFlags.AsUser => CreateProcessAsUser(throw_on_error),
                Win32ProcessConfigTokenCallFlags.WithToken => CreateProcessWithToken(throw_on_error),
                _ => throw new ArgumentException("invalid token call type."),
            };
        }

        if (Credentials != null)
        {
            return CreateProcessWithLogon(throw_on_error);
        }

        using var resources = new DisposableList();
        SECURITY_ATTRIBUTES proc_attr = ProcessSecurityAttributes(resources);
        SECURITY_ATTRIBUTES thread_attr = ThreadSecurityAttributes(resources);

        using var debug_object = SetDebugObject();
        return NativeMethods.CreateProcess(ApplicationName, CommandLine, proc_attr, thread_attr, InheritHandles,
                CreationFlags | CreateProcessFlags.ExtendedStartupInfoPresent,
                Environment, CurrentDirectory, ToStartupInfoEx(resources),
                out PROCESS_INFORMATION proc_info).CreateWin32Result(throw_on_error, () => new Win32Process(proc_info, TerminateOnDispose));
    }

    /// <summary>
    /// Create process.
    /// </summary>
    /// <returns>The created win32 process.</returns>
    public Win32Process Create() => Create(true).Result;
    #endregion

    #region Constructors
    /// <summary>
    /// Constructor.
    /// </summary>
    public Win32ProcessConfig()
    {
        InheritHandleList = new List<IntPtr>();
        Capabilities = new List<Sid>();
        StdInputHandle = InvalidHandle;
        StdOutputHandle = InvalidHandle;
        StdErrorHandle = InvalidHandle;
        JobList = new List<NtJob>();
    }
    #endregion

    #region Private Members
    private static IntPtr InvalidHandle => new(-1);

    private NtResult<Win32Process> CreateProcessWithToken(bool throw_on_error)
    {
        return NativeMethods.CreateProcessWithTokenW(Token.Handle, LogonFlags,
            ApplicationName, CommandLine,
            CreationFlags, Environment, CurrentDirectory,
            ToStartupInfo(), out PROCESS_INFORMATION proc_info)
            .CreateWin32Result(throw_on_error, () => new Win32Process(proc_info, TerminateOnDispose));
    }

    private NtResult<Win32Process> CreateProcessWithLogon(bool throw_on_error)
    {
        STARTUPINFO start_info = ToStartupInfo();
        PROCESS_INFORMATION proc_info = new();
        using var password = Credentials.GetPassword();
        return NativeMethods.CreateProcessWithLogonW(Credentials.UserName, Credentials.Domain,
            password, LogonFlags, ApplicationName, CommandLine, CreationFlags,
            Environment, CurrentDirectory, start_info,
            out proc_info).CreateWin32Result(throw_on_error, () => new Win32Process(proc_info, TerminateOnDispose));
    }

    private NtResult<Win32Process> CreateProcessAsUser(bool throw_on_error)
    {
        using var resources = new DisposableList();
        PROCESS_INFORMATION proc_info = new();
        STARTUPINFOEX start_info = ToStartupInfoEx(resources);
        SECURITY_ATTRIBUTES proc_attr = ProcessSecurityAttributes(resources);
        SECURITY_ATTRIBUTES thread_attr = ThreadSecurityAttributes(resources);

        using var debug_object = SetDebugObject();
        return NativeMethods.CreateProcessAsUser(Token.Handle, ApplicationName, CommandLine,
                proc_attr, thread_attr, InheritHandles, CreationFlags
                | CreateProcessFlags.ExtendedStartupInfoPresent, Environment,
                CurrentDirectory, start_info, out proc_info)
            .CreateWin32Result(throw_on_error, () => new Win32Process(proc_info, TerminateOnDispose));
    }

    private NtResult<Win32Process> CreateProcessFallback(bool throw_on_error)
    {
        var result = CreateProcessAsUser(false);
        return result.IsSuccess ? result : CreateProcessWithToken(throw_on_error);
    }

    private void PopulateStartupInfo(ref STARTUPINFO start_info)
    {
        start_info.lpDesktop = ServiceDesktop ? CreateServiceDesktopName() : Desktop;
        start_info.lpTitle = Title;
        if (StdInputHandle != InvalidHandle ||
            StdOutputHandle != InvalidHandle ||
            StdErrorHandle != InvalidHandle)
        {
            start_info.hStdInput = StdInputHandle;
            start_info.hStdOutput = StdOutputHandle;
            start_info.hStdError = StdErrorHandle;
            start_info.dwFlags = STARTF.STARTF_USESTDHANDLES;
        }
    }

    private STARTUPINFO ToStartupInfo()
    {
        if (GetAttributeCount() > 0)
            throw new ArgumentException("API doesn't support process attributes.");
        STARTUPINFO start_info = new();
        start_info.cb = Marshal.SizeOf(start_info);
        PopulateStartupInfo(ref start_info);
        return start_info;
    }

    private int GetAttributeCount()
    {
        int count = 0;
        if (ParentProcess != null)
        {
            count++;
        }
        if (MitigationOptions != ProcessMitigationOptions.None
            || MitigationOptions2 != ProcessMitigationOptions2.None)
        {
            count++;
        }

        if (Win32kFilterFlags != Win32kFilterFlags.None)
        {
            count++;
        }

        if (CreationFlags.HasFlagSet(CreateProcessFlags.ProtectedProcess) && ProtectionLevel != ProtectionLevel.None)
        {
            count++;
        }

        if (InheritHandleList.Count > 0)
        {
            count++;
        }

        if (AppContainerSid != null)
        {
            count++;
        }

        if (LowPrivilegeAppContainer)
        {
            count++;
        }

        if (ChildProcessMitigations != 0)
        {
            count++;
        }

        if (DesktopAppBreakaway != ProcessDesktopAppBreakawayFlags.None)
        {
            count++;
        }

        if (!string.IsNullOrWhiteSpace(PackageName))
        {
            count++;
        }

        if (PseudoConsole != IntPtr.Zero)
        {
            count++;
        }

        if (!string.IsNullOrEmpty(BnoIsolationPrefix))
        {
            count++;
        }

        if (SafeOpenPromptOriginClaim != null)
        {
            count++;
        }

        if (ExtendedFlags != ProcessExtendedFlags.None)
        {
            count++;
        }

        if (JobList.Count > 0)
        {
            count++;
        }

        if (ComponentFilter != ProcessComponentFilterFlags.None)
        {
            count++;
        }

        if (MachineType.HasValue)
        {
            count++;
        }

        return count;
    }

    private SafeHGlobalBuffer GetAttributes(DisposableList resources)
    {
        int count = GetAttributeCount();
        if (count == 0)
        {
            return SafeHGlobalBuffer.Null;
        }

        var attr_list = resources.AddResource(new SafeProcThreadAttributeListBuffer(count));
        if (ParentProcess != null)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeParentProcess, ParentProcess.Handle.DangerousGetHandle());
        }

        if (MitigationOptions2 != ProcessMitigationOptions2.None)
        {
            MemoryStream stm = new();
            BinaryWriter writer = new(stm);

            writer.Write((ulong)MitigationOptions);
            writer.Write((ulong)MitigationOptions2);
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeMitigationPolicy, stm.ToArray());
        }
        else if (MitigationOptions != ProcessMitigationOptions.None)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeMitigationPolicy, (ulong)MitigationOptions);
        }

        if (Win32kFilterFlags != Win32kFilterFlags.None)
        {
            Win32kFilterAttribute filter = new()
            {
                Flags = Win32kFilterFlags,
                FilterLevel = Win32kFilterLevel
            };
            attr_list.AddAttributeBuffer(Win32ProcessAttributes.ProcThreadAttributeWin32kFilter, resources.AddResource(filter.ToBuffer()));
        }

        if (CreationFlags.HasFlagSet(CreateProcessFlags.ProtectedProcess) && ProtectionLevel != ProtectionLevel.None)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeProtectionLevel, (int)ProtectionLevel);
        }

        if (InheritHandleList.Count > 0)
        {
            var handle_list = resources.AddResource(InheritHandleList.ToArray().ToBuffer());
            attr_list.AddAttributeBuffer(Win32ProcessAttributes.ProcThreadAttributeHandleList, handle_list);
        }

        if (AppContainerSid != null)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeSecurityCapabilities, 
                SECURITY_CAPABILITIES.Create(AppContainerSid, Capabilities, resources));
        }

        if (LowPrivilegeAppContainer)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeAllApplicationPackagesPolicy, 1);
        }

        if (ChildProcessMitigations != 0)
        {
            int flags = (int)ChildProcessMitigations;
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeChildProcessPolicy, flags);
        }

        if (DesktopAppBreakaway != ProcessDesktopAppBreakawayFlags.None)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeDesktopAppPolicy, (int)DesktopAppBreakaway);
        }

        if (!string.IsNullOrWhiteSpace(PackageName))
        {
            byte[] str_bytes = Encoding.Unicode.GetBytes(PackageName);
            var string_buffer = resources.AddResource(new SafeHGlobalBuffer(str_bytes));
            attr_list.AddAttributeBuffer(Win32ProcessAttributes.ProcThreadAttributePackageName, string_buffer);
        }

        if (PseudoConsole != IntPtr.Zero)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributePseudoConsole, PseudoConsole);
        }

        if (!string.IsNullOrEmpty(BnoIsolationPrefix))
        {
            var prefix = new BnoIsolationAttribute() { IsolationEnabled = 1, IsolationPrefix = BnoIsolationPrefix };
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeBnoIsolation, prefix);
        }

        if (SafeOpenPromptOriginClaim != null)
        {
            var bytes = SafeOpenPromptOriginClaim.CloneBytes();
            Array.Resize(ref bytes, 524);
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeSafeOpenPromptOriginClaim, bytes);
        }

        if (ExtendedFlags != ProcessExtendedFlags.None)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeExtendedFlags, (int)ExtendedFlags);
        }

        if (JobList.Count > 0)
        {
            var job_list = resources.AddResource(SafeHandleListBuffer.CreateAndDuplicate(JobList));
            attr_list.AddAttributeBuffer(Win32ProcessAttributes.ProcThreadAttribueJobList, job_list);
        }

        if (ComponentFilter != ProcessComponentFilterFlags.None)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeComponentFilter, (int)ComponentFilter);
        }

        if (MachineType.HasValue)
        {
            attr_list.AddAttribute(Win32ProcessAttributes.ProcThreadAttributeMachineType, (ushort)MachineType.Value);
        }

        return attr_list;
    }

    private STARTUPINFOEX ToStartupInfoEx(DisposableList resources)
    {
        STARTUPINFOEX start_info = new();
        PopulateStartupInfo(ref start_info.StartupInfo);
        start_info.lpAttributeList = GetAttributes(resources);
        return start_info;
    }

    private SECURITY_ATTRIBUTES ProcessSecurityAttributes(DisposableList<IDisposable> resources)
    {
        return CreateSecurityAttributes(ProcessSecurityDescriptor, InheritProcessHandle, resources);
    }

    private SECURITY_ATTRIBUTES ThreadSecurityAttributes(DisposableList<IDisposable> resources)
    {
        return CreateSecurityAttributes(ThreadSecurityDescriptor, InheritThreadHandle, resources);
    }

    private ScopedDebugObject SetDebugObject()
    {
        if ((CreationFlags & (CreateProcessFlags.DebugProcess | CreateProcessFlags.DebugOnlyThisProcess)) == 0 || DebugObject == null)
        {
            return null;
        }
        return new ScopedDebugObject(DebugObject);
    }

    private static SECURITY_ATTRIBUTES CreateSecurityAttributes(SecurityDescriptor sd,
        bool inherit, DisposableList<IDisposable> resources)
    {
        if (sd == null && !inherit)
        {
            return null;
        }
        var ret = new SECURITY_ATTRIBUTES()
        {
            bInheritHandle = inherit
        };
        if (sd != null)
        {
            ret.lpSecurityDescriptor = resources.AddResource(sd.ToSafeBuffer());
        }
        return ret;
    }

    private string ServiceDesktopNameFromToken(NtToken token)
    {
        Luid authid = token.AuthenticationId;
        return $@"Service-0x{authid.HighPart:X}-{authid.LowPart:X}$\Default";
    }

    private string CreateServiceDesktopName()
    {
        if (Token != null)
        {
            return ServiceDesktopNameFromToken(Token);
        }

        NtProcess process = ParentProcess ?? NtProcess.Current;
        using var token = process.OpenToken();
        return ServiceDesktopNameFromToken(token);
    }
    #endregion
}
