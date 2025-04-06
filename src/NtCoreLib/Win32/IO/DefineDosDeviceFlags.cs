﻿//  Copyright 2023 Google LLC. All Rights Reserved.
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

namespace NtCoreLib.Win32.IO;

/// <summary>
/// Flags for DefineDosDevice
/// </summary>
[Flags]
public enum DefineDosDeviceFlags
{
    /// <summary>
    /// None
    /// </summary>
    None = 0,
    /// <summary>
    /// Specify a raw target path
    /// </summary>
    RawTargetPath = 1,
    /// <summary>
    /// Remove existing definition
    /// </summary>
    RemoveDefinition = 2,
    /// <summary>
    /// Only remove exact matches to the target
    /// </summary>
    ExactMatchOnRemove = 4,
    /// <summary>
    /// Don't broadcast changes to the system
    /// </summary>
    NoBroadcastSystem = 8,
}
