using System.Management;
using System.Collections.Generic;
using System;

namespace NomadIIS.Services;

internal sealed class WmiHelper : IDisposable
{
	private static ManagementObjectSearcher? _currentMemorySearcher;
	private static ManagementObjectSearcher? _currentCpuSearcher;

	public Dictionary<string, UsageStatistics> QueryWorkerProcesses ( IDictionary<int, string> w3wpProcessToAppPoolNameMapping )
	{
		var result = new Dictionary<string, UsageStatistics>( StringComparer.InvariantCultureIgnoreCase );

		if ( _currentMemorySearcher is null )
		{
			_currentMemorySearcher = new ManagementObjectSearcher(
				new SelectQuery( "Win32_PerfFormattedData_PerfProc_Process", "Name LIKE 'w3wp%'", ["IDProcess", "WorkingSetPrivate"] ) );
		}

		using var mocMemory = _currentMemorySearcher.Get();

		foreach ( var mo in mocMemory )
		{
			try
			{
				var pid = (int)(uint)mo.GetPropertyValue( "IDProcess" );
				var workingSetPrivate = (ulong)mo.GetPropertyValue( "WorkingSetPrivate" );

				if ( w3wpProcessToAppPoolNameMapping.TryGetValue( pid, out var appPoolName ) )
				{
					if ( result.TryGetValue( appPoolName, out var stats ) )
						stats.WorkingSetPrivate += workingSetPrivate;
					else
						result.Add( appPoolName, new UsageStatistics( 0UL, 0UL, workingSetPrivate ) );
				}
			}
			finally
			{
				mo.Dispose();
			}
		}

		if ( _currentCpuSearcher is null )
		{
			_currentCpuSearcher = new ManagementObjectSearcher(
				new SelectQuery( "Win32_Process", "Name LIKE 'w3wp%'", ["ProcessID", "KernelModeTime", "UserModeTime"] ) );
		}

		using var mocCpu = _currentCpuSearcher.Get();

		foreach ( var mo in mocCpu )
		{
			try
			{
				var pid = (int)(uint)mo.GetPropertyValue( "ProcessID" );
				var kernelModeTime = (ulong)mo.GetPropertyValue( "KernelModeTime" );
				var userModeTime = (ulong)mo.GetPropertyValue( "UserModeTime" );

				if ( w3wpProcessToAppPoolNameMapping.TryGetValue( pid, out var appPoolName ) )
				{
					if ( result.TryGetValue( appPoolName, out var stats ) )
					{
						stats.KernelModeTime += kernelModeTime;
						stats.UserModeTime += userModeTime;
					}
					else
						result.Add( appPoolName, new UsageStatistics( kernelModeTime, userModeTime, 0UL ) );
				}
			}
			finally
			{
				mo.Dispose();
			}
		}

		return result;
	}

	public void Dispose ()
	{
		_currentMemorySearcher?.Dispose();
		_currentCpuSearcher?.Dispose();
	}
}
