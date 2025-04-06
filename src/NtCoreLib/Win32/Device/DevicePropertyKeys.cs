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

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using NtCoreLib.Win32.Device.Interop;

namespace NtCoreLib.Win32.Device;

[StructLayout(LayoutKind.Sequential)]
internal struct DEVPROPKEY
{
    public Guid fmtid;
    public int pid;

    public DEVPROPKEY(uint l, ushort w1, ushort w2, byte b1, byte b2, byte b3, byte b4, byte b5, byte b6, byte b7, byte b8, int pid_value)
    {
        fmtid = new Guid(l, w1, w2, b1, b2, b3, b4, b5, b6, b7, b8);
        pid = pid_value;
    }

    public Tuple<Guid, int> ToTuple()
    {
        return Tuple.Create(fmtid, pid);
    }
};

internal static class DevicePropertyKeys
{
    public static DEVPROPKEY DEVPKEY_DeviceClass_Characteristics = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 29);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_DeviceClass_ClassCoInstallers = new(0x713d1703, 0xa2e2, 0x49f5, 0x92, 0x14, 0x56, 0x47, 0x2e, 0xf3, 0xda, 0x5c, 2);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceClass_ClassInstaller = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 5);      // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_ClassName = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 3);      // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_DHPRebalanceOptOut = new(0xd14d3ef3, 0x66cf, 0x4ba2, 0x9d, 0x38, 0x0d, 0xdb, 0x37, 0xab, 0x47, 0x01, 2);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_DefaultService = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 11);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_DevType = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 27);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_DeviceClass_Exclusive = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 28);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_Icon = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 4);      // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_IconPath = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 12);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceClass_LowerFilters = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 20);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceClass_Name = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 2);      // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_NoDisplayClass = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 8);      // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_NoInstallClass = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 7);      // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_NoUseClass = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 10);     // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_PropPageProvider = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 6);      // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_Security = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 25);    // DEVPROP_TYPE_SECURITY_DESCRIPTOR
    public static DEVPROPKEY DEVPKEY_DeviceClass_SecuritySDS = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 26);    // DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING
    public static DEVPROPKEY DEVPKEY_DeviceClass_SilentInstall = new(0x259abffc, 0x50a7, 0x47ce, 0xaf, 0x8, 0x68, 0xc9, 0xa7, 0xd7, 0x33, 0x66, 9);      // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceClass_UpperFilters = new(0x4321918b, 0xf69e, 0x470d, 0xa5, 0xde, 0x4d, 0x88, 0xc7, 0x5a, 0xd2, 0x4b, 19);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Address = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 51);    // DEVPROP_TYPE_STRING | DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_AlwaysShowDeviceAsConnected = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 101);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_AssociationArray = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 80);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_BaselineExperienceId = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 78);    // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Category = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 90);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_CategoryGroup_Desc = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 94);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_CategoryGroup_Icon = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 95);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Category_Desc_Plural = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 92);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Category_Desc_Singular = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 91);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Category_Icon = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 93);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_ConfigFlags = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 105);   // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_DeviceContainer_CustomPrivilegedPackageFamilyNames = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 107);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_DeviceDescription1 = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 81);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_DeviceDescription2 = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 82);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_DeviceFunctionSubRank = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 100);   // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_DeviceContainer_DiscoveryMethod = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 52);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_ExperienceId = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 89);    // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_DeviceContainer_FriendlyName = new(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD, 12288); // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_HasProblem = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 83);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Icon = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 57);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_InstallInProgress = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 9);     // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsAuthenticated = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 54);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsConnected = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 55);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsDefaultDevice = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 86);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsDeviceUniquelyIdentifiable = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 79);        // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsEncrypted = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 53);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsLocalMachine = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 70);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsMetadataSearchInProgress = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 72);          // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsNetworkDevice = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 85);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsNotInterestingForDisplay = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 74);          // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsPaired = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 56);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsRebootRequired = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 108);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsSharedDevice = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 84);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_IsShowInDisconnectedState = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 68);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Last_Connected = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 67);    // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Last_Seen = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 66);    // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_DeviceContainer_LaunchDeviceStageFromExplorer = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 77);       // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_LaunchDeviceStageOnDeviceConnect = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 76);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Manufacturer = new(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD, 8192);  // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_MetadataCabinet = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 87);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_MetadataChecksum = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 73);            // DEVPROP_TYPE_BINARY
    public static DEVPROPKEY DEVPKEY_DeviceContainer_MetadataPath = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 71);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_ModelName = new(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD, 8194);  // DEVPROP_TYPE_STRING (localizable)
    public static DEVPROPKEY DEVPKEY_DeviceContainer_ModelNumber = new(0x656A3BB3, 0xECC0, 0x43FD, 0x84, 0x77, 0x4A, 0xE0, 0x40, 0x4A, 0x96, 0xCD, 8195);  // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_PrimaryCategory = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 97);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceContainer_PrivilegedPackageFamilyNames = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 106);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DeviceContainer_RequiresPairingElevation = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 88);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_RequiresUninstallElevation = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 99);  // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_UnpairUninstall = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 98);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceContainer_Version = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 65);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterfaceClass_DefaultInterface = new(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64, 2); // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterfaceClass_Name = new(0x14c83a99, 0x0b3f, 0x44b7, 0xbe, 0x4c, 0xa1, 0x78, 0xd3, 0x99, 0x05, 0x64, 3); // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterface_ClassGuid = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 4);   // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_DeviceInterface_Enabled = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 3);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceInterface_FriendlyName = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 2);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterface_ReferenceString = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 5);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterface_Restricted = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 6);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_DeviceInterface_SchematicName = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 9);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DeviceInterface_UnrestrictedAppCapabilities = new(0x026e516e, 0xb814, 0x414b, 0x83, 0xcd, 0x85, 0x6d, 0x6f, 0xef, 0x48, 0x22, 8); // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_AdditionalSoftwareRequested = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 19); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_Address = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 30);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_AssignedToGuest = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 24);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_BaseContainerId = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 38);    // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_Device_BiosDeviceName = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 10);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_BusNumber = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 23);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_BusRelations = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 7);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_BusReportedDeviceDesc = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 4);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_BusTypeGuid = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 21);    // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_Device_Capabilities = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 17);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_Characteristics = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 29);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_Children = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 9);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_Class = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 9);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_ClassGuid = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 10);    // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_Device_CompatibleIds = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 4);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_ConfigFlags = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 12);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_ConfigurationId = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 7);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_ContainerId = new(0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c, 2);     // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_Device_DHP_Rebalance_Policy = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 2);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DebuggerSafe = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 12);   // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DependencyDependents = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 21);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_DependencyProviders = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 20);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_DevNodeStatus = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 2);     // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DevType = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 27);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DeviceDesc = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 2);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_Driver = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 11);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverCoInstallers = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 11);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_DriverDate = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 2);     // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_DriverDesc = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 4);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverInfPath = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 5);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverInfSection = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 6);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverInfSectionExt = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 7);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverLogoLevel = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 15);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DriverProblemDesc = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 11);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverPropPageProvider = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 10);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverProvider = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 9);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_DriverRank = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 14);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_DriverVersion = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 3);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_EjectionRelations = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 4);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_EnumeratorName = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 24);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_Exclusive = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 28);    // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_ExtendedAddress = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 23);   // DEVPROP_TYPE_UINT64
    public static DEVPROPKEY DEVPKEY_Device_ExtendedConfigurationIds = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 15);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_FirmwareDate = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 17);   // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_FirmwareRevision = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 19);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_FirmwareVersion = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 18);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_FirstInstallDate = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 101);   // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_FriendlyName = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 14);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_FriendlyNameAttributes = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 3); // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_GenericDriverInstalled = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 18); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_HardwareIds = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 3);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_HasProblem = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 6);    // DEVPROP_TYPE_BOOL
    public static DEVPROPKEY DEVPKEY_Device_InLocalMachineContainer = new(0x8c7ed206, 0x3f8a, 0x4827, 0xb3, 0xab, 0xae, 0x9e, 0x1f, 0xae, 0xfc, 0x6c, 4);     // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_InstallDate = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 100);   // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_InstallState = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 36);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_InstanceId = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 256);   // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_IsAssociateableByUserAction = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 7); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_IsPresent = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 5);    // DEVPROP_TYPE_BOOL
    public static DEVPROPKEY DEVPKEY_Device_IsRebootRequired = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 16);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_LastArrivalDate = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 102);   // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_LastRemovalDate = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 103);   // DEVPROP_TYPE_FILETIME
    public static DEVPROPKEY DEVPKEY_Device_Legacy = new(0x80497100, 0x8c73, 0x48b9, 0xaa, 0xd9, 0xce, 0x38, 0x7e, 0x19, 0xc5, 0x6e, 3);  // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_LegacyBusType = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 22);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_LocationInfo = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 15);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_LocationPaths = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 37);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_LowerFilters = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 20);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_Manufacturer = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 13);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_ManufacturerAttributes = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 4); // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_MatchingDeviceId = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 8);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_Model = new(0x78c34fc8, 0x104a, 0x4aca, 0x9e, 0xa4, 0x52, 0x4d, 0x52, 0x99, 0x6e, 0x57, 39);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_ModelId = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 2); // DEVPROP_TYPE_GUID
    public static DEVPROPKEY DEVPKEY_Device_NoConnectSound = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 17); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_Numa_Node = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 3);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_Numa_Proximity_Domain = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 1);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_PDOName = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 16);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_Parent = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 8);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_PhysicalDeviceLocation = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 9);    // DEVPROP_TYPE_BINARY
    public static DEVPROPKEY DEVPKEY_Device_PostInstallInProgress = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 13);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_PowerData = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 32);    // DEVPROP_TYPE_BINARY
    public static DEVPROPKEY DEVPKEY_Device_PowerRelations = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 6);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_PresenceNotForDevice = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 5); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_ProblemCode = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 3);     // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_ProblemStatus = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 12);     // DEVPROP_TYPE_NTSTATUS
    public static DEVPROPKEY DEVPKEY_Device_RemovalPolicy = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 33);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_RemovalPolicyDefault = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 34);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_RemovalPolicyOverride = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 35);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_RemovalRelations = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 5);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_Reported = new(0x80497100, 0x8c73, 0x48b9, 0xaa, 0xd9, 0xce, 0x38, 0x7e, 0x19, 0xc5, 0x6e, 2);  // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_ReportedDeviceIdsHash = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 8);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_ResourcePickerExceptions = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 13);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_ResourcePickerTags = new(0xa8b865dd, 0x2e3d, 0x4094, 0xad, 0x97, 0xe5, 0x93, 0xa7, 0xc, 0x75, 0xd6, 12);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_SafeRemovalRequired = new(0xafd97640, 0x86a3, 0x4210, 0xb6, 0x7c, 0x28, 0x9c, 0x41, 0xaa, 0xbe, 0x55, 2); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_SafeRemovalRequiredOverride = new(0xafd97640, 0x86a3, 0x4210, 0xb6, 0x7c, 0x28, 0x9c, 0x41, 0xaa, 0xbe, 0x55, 3); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_Security = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 25);    // DEVPROP_TYPE_SECURITY_DESCRIPTOR
    public static DEVPROPKEY DEVPKEY_Device_SecuritySDS = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 26);    // DEVPROP_TYPE_SECURITY_DESCRIPTOR_STRING
    public static DEVPROPKEY DEVPKEY_Device_Service = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 6);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_SessionId = new(0x83da6326, 0x97a6, 0x4088, 0x94, 0x53, 0xa1, 0x92, 0x3f, 0x57, 0x3b, 0x29, 6);     // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_ShowInUninstallUI = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 8); // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_Siblings = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 10);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_SignalStrength = new(0x80d81ea6, 0x7473, 0x4b0c, 0x82, 0x16, 0xef, 0xc1, 0x1a, 0x2c, 0x4c, 0x8b, 6); // DEVPROP_TYPE_INT32
    public static DEVPROPKEY DEVPKEY_Device_SoftRestartSupported = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 22);   // DEVPROP_TYPE_BOOLEAN
    public static DEVPROPKEY DEVPKEY_Device_Stack = new(0x540b947e, 0x8b40, 0x45bc, 0xa8, 0xa2, 0x6a, 0x0b, 0x89, 0x4c, 0xbd, 0xa2, 14);   // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_TransportRelations = new(0x4340a6c5, 0x93fa, 0x4706, 0x97, 0x2c, 0x7b, 0x64, 0x80, 0x08, 0xa5, 0xa7, 11);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_Device_UINumber = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 18);    // DEVPROP_TYPE_UINT32
    public static DEVPROPKEY DEVPKEY_Device_UINumberDescFormat = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 31);    // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_Device_UpperFilters = new(0xa45c254e, 0xdf1c, 0x4efd, 0x80, 0x20, 0x67, 0xd1, 0x46, 0xa8, 0x50, 0xe0, 19);    // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DrvPkg_BrandingIcon = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 7);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DrvPkg_DetailedDescription = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 4);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DrvPkg_DocumentationLink = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 5);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DrvPkg_Icon = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 6);     // DEVPROP_TYPE_STRING_LIST
    public static DEVPROPKEY DEVPKEY_DrvPkg_Model = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 2);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_DrvPkg_VendorWebSite = new(0xcf73bb51, 0x3abf, 0x44a2, 0x85, 0xe0, 0x9a, 0x3d, 0xc7, 0xa1, 0x21, 0x32, 3);     // DEVPROP_TYPE_STRING
    public static DEVPROPKEY DEVPKEY_NAME = new(0xb725f130, 0x47ef, 0x101a, 0xa5, 0xf1, 0x02, 0x60, 0x8c, 0x9e, 0xeb, 0xac, 10);    // DEVPROP_TYPE_STRING

    internal static string KeyToName(DEVPROPKEY key)
    {
        return PopulateDictionary().GetOrAdd(key.ToTuple(), _ => GetPropertyName(key));
    }

    private static string GetPropertyName(DEVPROPKEY key)
    {
        if (NativeMethods.PSGetNameFromPropertyKey(key, out string name) == 0)
        {
            return name;
        }
        return $"{key.fmtid:B}-{key.pid}";
    }

    private static ConcurrentDictionary<Tuple<Guid, int>, string> PopulateDictionary()
    {
        if (_key_to_names != null)
        {
            return _key_to_names;
        }
        Dictionary<Tuple<Guid, int>, string> dict = new();
        foreach (var f in typeof(DevicePropertyKeys).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            if (f.FieldType == typeof(DEVPROPKEY))
            {
                DEVPROPKEY key = (DEVPROPKEY)f.GetValue(null);
                dict[key.ToTuple()] = f.Name;
            }
        }
        _key_to_names = new ConcurrentDictionary<Tuple<Guid, int>, string>(dict);
        return _key_to_names;
    }

    private static ConcurrentDictionary<Tuple<Guid, int>, string> _key_to_names;
}
