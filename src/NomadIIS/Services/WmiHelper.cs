using System.Management;
using System.Collections.Generic;

namespace NomadIIS.Services;

internal static class WmiHelper
{
	private static string? _currentMemoryQuery;
	private static ManagementObjectSearcher? _currentMemorySearcher;
	private static string? _currentCpuQuery;
	private static ManagementObjectSearcher? _currentCpuSearcher;

	public static Dictionary<int, ulong> QueryPrivateWorkingSet ( int[] processIds )
	{
		var condition = string.Format( "IDProcess={0}", string.Join( " OR IDProcess=", processIds ) );

		if ( _currentMemoryQuery is null || _currentMemoryQuery != condition )
		{
			// Query has changes, so we dispose the older ManagementObjectSearcher and create a new one.
			_currentMemorySearcher?.Dispose();

			_currentMemoryQuery = condition;

			var query = new SelectQuery( "Win32_PerfFormattedData_PerfProc_Process", _currentMemoryQuery );

			_currentMemorySearcher = new ManagementObjectSearcher( query );
		}

		var results = new Dictionary<int, ulong>();

		if ( _currentMemorySearcher is not null )
		{
			using var moc = _currentMemorySearcher.Get();

			foreach ( var mo in moc )
			{
				try
				{
					var pid = (int)(uint)mo.GetPropertyValue( "IDProcess" );
					var workingSetPrivate = (ulong)mo.GetPropertyValue( "WorkingSetPrivate" );

					if ( results.ContainsKey( pid ) )
						results[pid] += workingSetPrivate;
					else
						results[pid] = workingSetPrivate;
				}
				finally
				{
					mo.Dispose();
				}
			}
		}

		return results;
	}
	public static Dictionary<int, (ulong KernelModeTime, ulong UserModeTime)> QueryCpuUsage ( int[] processIds )
	{
		var condition = string.Format( "ProcessID={0}", string.Join( " OR ProcessID=", processIds ) );

		if ( _currentCpuQuery is null || _currentCpuQuery != condition )
		{
			// Query has changes, so we dispose the older ManagementObjectSearcher and create a new one.
			_currentCpuSearcher?.Dispose();

			_currentCpuQuery = condition;

			// https://learn.microsoft.com/en-us/windows/win32/cimwin32prov/win32-process
			var query = new SelectQuery( "Win32_Process", _currentCpuQuery );

			_currentCpuSearcher = new ManagementObjectSearcher( query );
		}

		var results = new Dictionary<int, (ulong KernelModeTime, ulong UserModeTime)>();

		if ( _currentCpuSearcher is not null )
		{
			using var moc = _currentCpuSearcher.Get();

			foreach ( var mo in moc )
			{
				try
				{
					var pid = (int)(uint)mo.GetPropertyValue( "ProcessID" );
					var kernelModeTime = (ulong)mo.GetPropertyValue( "KernelModeTime" );
					var userModeTime = (ulong)mo.GetPropertyValue( "UserModeTime" );

					if ( results.TryGetValue( pid, out var existing ) )
						results[pid] = (existing.KernelModeTime + kernelModeTime, existing.UserModeTime + userModeTime);
					else
						results[pid] = (kernelModeTime, userModeTime);
				}
				finally
				{
					mo.Dispose();
				}
			}
		}

		return results;
	}
}
