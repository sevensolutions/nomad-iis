using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace NomadIIS.Services;

public static class NativeFunctions
{
	// Available since Windows 8.1/Server 2012 R2.
	private const int ProcessCommandLineInformation = 60;

	public static string? GetProcessCommandLine ( Process process )
	{
		// Query the required buffer size first.
		_ = NtQueryInformationProcess( process.Handle, ProcessCommandLineInformation, IntPtr.Zero, 0, out var requiredLength );

		if ( requiredLength == 0 )
			return null;

		var buffer = Marshal.AllocHGlobal( (int)requiredLength );

		try
		{
			var status = NtQueryInformationProcess( process.Handle, ProcessCommandLineInformation, buffer, requiredLength, out _ );

			if ( status != 0 )
				return null;

			var unicodeString = Marshal.PtrToStructure<UNICODE_STRING>( buffer );

			return Marshal.PtrToStringUni( unicodeString.Buffer, unicodeString.Length / 2 );
		}
		finally
		{
			Marshal.FreeHGlobal( buffer );
		}
	}

	public static ulong GetPrivateWorkingSet ( Process process )
	{
		var cbEx2 = (uint)Marshal.SizeOf<PROCESS_MEMORY_COUNTERS_EX2>();

		if ( GetProcessMemoryInfo( process.Handle, out var counters, cbEx2 ) )
		{
			// PrivateWorkingSetSize is only available since Windows Server 2022.
			// On older versions we fall back to the private bytes.
			return counters.PrivateWorkingSetSize > 0UL ? counters.PrivateWorkingSetSize : counters.PrivateUsage;
		}

		// Older Windows versions may reject the EX2 struct size, so retry with the EX layout.
		var cbEx = cbEx2 - (uint)( IntPtr.Size + sizeof( ulong ) );

		if ( GetProcessMemoryInfo( process.Handle, out counters, cbEx ) )
			return counters.PrivateUsage;

		return 0UL;
	}

	[DllImport( "ntdll.dll" )]
	private static extern int NtQueryInformationProcess ( IntPtr hProcess, int processInformationClass, IntPtr processInformation, uint processInformationLength, out uint returnLength );
	[DllImport( "psapi.dll", SetLastError = true )]
	[return: MarshalAs( UnmanagedType.Bool )]
	private static extern bool GetProcessMemoryInfo ( IntPtr hProcess, out PROCESS_MEMORY_COUNTERS_EX2 ppsmemCounters, uint cb );

	[StructLayout( LayoutKind.Sequential )]
	private struct PROCESS_MEMORY_COUNTERS_EX2
	{
		public uint cb;
		public uint PageFaultCount;
		public nuint PeakWorkingSetSize;
		public nuint WorkingSetSize;
		public nuint QuotaPeakPagedPoolUsage;
		public nuint QuotaPagedPoolUsage;
		public nuint QuotaPeakNonPagedPoolUsage;
		public nuint QuotaNonPagedPoolUsage;
		public nuint PagefileUsage;
		public nuint PeakPagefileUsage;
		public nuint PrivateUsage;
		public nuint PrivateWorkingSetSize;
		public ulong SharedCommitUsage;
	}

	[StructLayout( LayoutKind.Sequential )]
	private struct UNICODE_STRING
	{
		public ushort Length;
		public ushort MaximumLength;
		public IntPtr Buffer;
	}
}
