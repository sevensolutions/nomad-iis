﻿//  Copyright 2022 Google LLC. All Rights Reserved.
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

using NtCoreLib.Utilities.Reflection;

namespace NtCoreLib.Net.Smb2;

internal enum Smb2InfoType : byte
{
    [SDKName("SMB2_0_INFO_FILE")]
    File = 1,
    [SDKName("SMB2_0_INFO_FILESYSTEM")]
    FileSystem = 2,
    [SDKName("SMB2_0_INFO_SECURITY")]
    Security = 3,
    [SDKName("SMB2_0_INFO_QUOTA")]
    Quota = 4,
}
