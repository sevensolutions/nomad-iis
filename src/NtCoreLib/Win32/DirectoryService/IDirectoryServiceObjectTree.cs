﻿//  Copyright 2021 Google Inc. All Rights Reserved.
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

using NtCoreLib.Utilities.Security.Authorization;
using System;

namespace NtCoreLib.Win32.DirectoryService;

/// <summary>
/// Interface to convert a directory object to a tree for access checking.
/// </summary>
public interface IDirectoryServiceObjectTree
{
    /// <summary>
    /// The name of the object.
    /// </summary>
    string Name { get; }

    /// <summary>
    /// The ID of the object.
    /// </summary>
    Guid Id { get; }

    /// <summary>
    /// Convert the schema class to an object type tree.
    /// </summary>
    /// <returns>The tree of object types.</returns>
    ObjectTypeTree ToObjectTypeTree();
}
