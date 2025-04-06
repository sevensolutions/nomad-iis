﻿//  Copyright 2019 Google Inc. All Rights Reserved.
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
using NtCoreLib.Native.SafeHandles;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NtCoreLib.Utilities.IO;

internal sealed class NtAsyncResult : IDisposable
{
    private readonly NtObject _object;
    private readonly NtEvent _event;
    private readonly SafeIoStatusBuffer _io_status;
    private IoStatusBlock _result;

    internal NtAsyncResult(NtObject obj)
    {
        _object = obj;
        if (!_object.CanSynchronize)
        {
            _event = NtEvent.Create(null,
                EventType.SynchronizationEvent, false);
        }
        _io_status = new SafeIoStatusBuffer();
        _result = null;
    }

    internal SafeKernelObjectHandle EventHandle => _event.GetHandle();

    internal NtStatus CompleteCall(NtStatus status, NtWaitTimeout timeout)
    {
        if (status == NtStatus.STATUS_PENDING)
        {
            if (WaitForComplete(timeout))
            {
                status = _io_status.Result.Status;
            }
        }
        else if (status.IsSuccess())
        {
            _result = _io_status.Result;
        }
        return status;
    }

    internal NtStatus CompleteCall(NtStatus status)
    {
        return CompleteCall(status, NtWaitTimeout.Infinite);
    }

    internal async Task<NtStatus> CompleteCallAsync(NtStatus status, CancellationToken token)
    {
        try
        {
            if (status == NtStatus.STATUS_PENDING)
            {
                if (await WaitForCompleteAsync(token))
                {
                    return _result.Status;
                }
            }
            else if (status.IsSuccess())
            {
                _result = _io_status.Result;
            }
            return status;
        }
        catch (TaskCanceledException)
        {
            // Cancel and then rethrow.
            Cancel();
            throw;
        }
    }

    /// <summary>
    /// Wait for the result to complete. This could be waiting on an event
    /// or the file handle.
    /// </summary>
    /// <param name="timeout">Wait timeout. Will cancel the operation if it times out.</param>
    /// <returns>Returns true if the wait completed successfully.</returns>
    /// <remarks>If true is returned then status and information can be read out.</remarks>
    internal bool WaitForComplete(NtWaitTimeout timeout)
    {
        if (_result != null)
        {
            return true;
        }

        NtStatus status;
        if (_event != null)
        {
            status = _event.Wait(timeout).ToNtException();
        }
        else
        {
            status = _object.Wait(timeout).ToNtException();
        }

        if (status == NtStatus.STATUS_SUCCESS)
        {
            _result = _io_status.Result;
            return true;
        }
        else if (status == NtStatus.STATUS_TIMEOUT)
        {
            Cancel();
        }

        return false;
    }

    /// <summary>
    /// Wait for the result to complete asynchronously. This could be waiting on an event
    /// or the file handle.
    /// </summary>
    /// <param name="token">Cancellation token.</param>
    /// <returns>Returns true if the wait completed successfully.</returns>
    /// <remarks>If true is returned then status and information can be read out.</remarks>
    internal async Task<bool> WaitForCompleteAsync(CancellationToken token)
    {
        if (_result != null)
        {
            return true;
        }

        bool success;

        using (NtWaitHandle wait_handle = _event?.DuplicateAsWaitHandle() ?? _object.DuplicateAsWaitHandle())
        {
            success = await wait_handle.WaitAsync(Timeout.Infinite, token);
        }

        if (success)
        {
            _result = _io_status.Result;
            return true;
        }

        return false;
    }

    private IoStatusBlock GetIoStatus()
    {
        return _result ?? throw new NtException(NtStatus.STATUS_PENDING);
    }

    /// <summary>
    /// Return the status information field.
    /// </summary>
    /// <exception cref="NtException">Thrown if not complete.</exception>
    internal long Information => GetIoStatus().Information.ToInt64();

    /// <summary>
    /// Return the status information field. (32 bit)
    /// </summary>
    /// <exception cref="NtException">Thrown if not complete.</exception>
    internal int Information32 => GetIoStatus().Information.ToInt32();

    /// <summary>
    /// Get completion status code.
    /// </summary>
    /// <exception cref="NtException">Thrown if not complete.</exception>
    internal NtStatus Status => GetIoStatus().Status;

    internal IoStatusBlock Result => GetIoStatus();

    /// <summary>
    /// Returns true if the call is pending.
    /// </summary>
    internal bool IsPending => _result == null;

    internal SafeIoStatusBuffer IoStatusBuffer => _io_status;

    /// <summary>
    /// Dispose object.
    /// </summary>
    public void Dispose()
    {
        _event?.Close();
        _io_status?.Close();
    }

    /// <summary>
    /// Reset the file result so it can be reused.
    /// </summary>
    internal void Reset()
    {
        _result = null;
        _event?.Clear();
    }

    /// <summary>
    /// Cancel the pending IO operation.
    /// </summary>
    internal void Cancel()
    {
        Cancel(true);
    }

    /// <summary>
    /// Cancel the pending IO operation.
    /// </summary>
    /// <param name="throw_on_error">True to throw on error.</param>
    /// <returns>The NT status code.</returns>
    internal NtStatus Cancel(bool throw_on_error)
    {
        if (_object is NtFile)
        {
            IoStatusBlock io_status = new();
            return NtSystemCalls.NtCancelIoFileEx(_object.Handle,
                _io_status, io_status).ToNtException(throw_on_error);
        }
        return NtStatus.STATUS_SUCCESS;
    }
}
