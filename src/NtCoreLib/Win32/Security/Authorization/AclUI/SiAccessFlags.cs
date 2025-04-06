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

namespace NtCoreLib.Win32.Security.Authorization.AclUI;

[Flags]
internal enum SiAccessFlags
{
    SI_ACCESS_SPECIFIC = 0x00010000,
    SI_ACCESS_GENERAL = 0x00020000,
    SI_ACCESS_CONTAINER = 0x00040000,
    SI_ACCESS_PROPERTY = 0x00080000,
}
