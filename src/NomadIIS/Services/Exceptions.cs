using System;

namespace NomadIIS.Services;

public sealed class TaskNotFoundException : Exception
{
	public TaskNotFoundException ( string taskId )
		: base( $"Task {taskId} could not be found." )
	{
	}
}
