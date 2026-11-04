﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AspNetSampleApp.Models
{
	public class HomeModel
	{
		public string Path { get; set; }
		public string PermissionCheckResult { get; set; }
		public bool PermissionCheckFailed { get; set; }
		public IDictionary<string, string> EnvironmentVariables { get; set; }
		public string LogFilePath { get; set; }
		public string LogMessage { get; set; }
		public string LogResult { get; set; }
		public bool LogFailed { get; set; }
		public string EnvironmentVariablesError { get; set; }
	}
}
