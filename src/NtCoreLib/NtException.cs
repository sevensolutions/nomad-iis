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

namespace NtCoreLib;

/// <summary>
/// Exception class representing an NT status error.
/// </summary>
[Serializable]
public class NtException : ApplicationException
{
    private string GetMessage()
    {
        string message = NtObjectUtils.GetNtStatusMessage(Status);
        if (!string.IsNullOrEmpty(message))
            return message;

        if (Enum.IsDefined(typeof(NtStatus), Status))
        {
            return Status.ToString();
        }

        var error = Status.MapNtStatusToDosError();
        if (Enum.IsDefined(error.GetType(), error))
        {
            return error.ToString();
        }

        return "Unknown NTSTATUS";
    }

    /// <summary>
    /// Constructor
    /// </summary>
    /// <param name="status">Status result</param>
    public NtException(NtStatus status) 
    {
        Status = status;
    }

    /// <summary>
    /// Returns the contained NT status code
    /// </summary>
    public NtStatus Status { get; }

    /// <summary>
    /// Returns a string form of the NT status code.
    /// </summary>
    public override string Message => $"(0x{(uint)Status:X08}) - {GetMessage()}";
}
