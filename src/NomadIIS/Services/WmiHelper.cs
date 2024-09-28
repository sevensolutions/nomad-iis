using System.Management;
using System;

namespace NomadIIS.Services;

internal static class WmiHelper
{
	public static (ulong KernelModeTime, ulong UserModeTime, ulong WorkingSetPrivate) QueryWmiStatistics ( params int[] processIds )
	{
		if ( processIds is null || processIds.Length <= 0 )
			throw new ArgumentNullException( nameof( processIds ) );

		var condition = string.Format( "ProcessID={0}", string.Join( " OR ProcessID=", processIds ) );

		// https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process
		var query = new SelectQuery( "Win32_Process", condition );


		using var ipsearcher = new ManagementObjectSearcher( query );
		using var ipresults = ipsearcher.Get();

		var kernelModeTime = 0UL;
		var userModeTime = 0UL;

		foreach ( var r in ipresults )
		{
			kernelModeTime += (ulong)r.GetPropertyValue( "KernelModeTime" );
			userModeTime += (ulong)r.GetPropertyValue( "UserModeTime" );
		}

		// 
		condition = string.Format( "IDProcess={0}", string.Join( " OR IDProcess=", processIds ) );

		query = new SelectQuery( "Win32_PerfFormattedData_PerfProc_Process", condition );

		using var ipsearcher2 = new ManagementObjectSearcher( query );
		using var ipresults2 = ipsearcher2.Get();

		var workingSetPrivate = 0UL;

		foreach ( var r in ipresults2 )
		{
			workingSetPrivate += (ulong)r.GetPropertyValue( "WorkingSetPrivate" );
		}

		// Need to multiply cpu stats by one hundred to align with nomad method CpuStats.Percent's expected decimal placement
		//kernelModeTime *= 100;
		//userModeTime *= 100;

		return (kernelModeTime, userModeTime, workingSetPrivate);
	}
}
