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

using NtCoreLib.Security.Authorization;
using System.Collections.Generic;

namespace NtCoreLib.Win32.Security.Sam;

internal class SamFakeTypeFactory : NtFakeTypeFactory
{
    public override IEnumerable<NtType> CreateTypes()
    {
        return new NtType[] {
            new NtType(SamUtils.SAM_SERVER_NT_TYPE_NAME, SamUtils.GetSamServerGenericMapping(),
                    typeof(SamServerAccessRights), typeof(SamServerAccessRights), MandatoryLabelPolicy.NoWriteUp),
             new NtType(SamUtils.SAM_DOMAIN_NT_TYPE_NAME, SamUtils.GetSamDomainGenericMapping(),
                    typeof(SamDomainAccessRights), typeof(SamDomainAccessRights), MandatoryLabelPolicy.NoWriteUp),
             new NtType(SamUtils.SAM_USER_NT_TYPE_NAME, SamUtils.GetSamUserGenericMapping(),
                    typeof(SamUserAccessRights), typeof(SamUserAccessRights), MandatoryLabelPolicy.NoWriteUp),
             new NtType(SamUtils.SAM_GROUP_NT_TYPE_NAME, SamUtils.GetSamGroupGenericMapping(),
                    typeof(SamGroupAccessRights), typeof(SamGroupAccessRights), MandatoryLabelPolicy.NoWriteUp),
             new NtType(SamUtils.SAM_ALIAS_NT_TYPE_NAME, SamUtils.GetSamAliasGenericMapping(),
                    typeof(SamAliasAccessRights), typeof(SamAliasAccessRights), MandatoryLabelPolicy.NoWriteUp)
        };
    }
}
