﻿//  Copyright 2018 Google Inc. All Rights Reserved.
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
using System.Collections.Generic;
using System.Linq;

namespace NtCoreLib;

#pragma warning disable 1591
/// <summary>
/// Well-known IO Control codes.
/// </summary>
public static class NtWellKnownIoControlCodes
{
    public static readonly NtIoControlCode FSCTL_REQUEST_OPLOCK_LEVEL_1 = new(FileDeviceType.FILE_SYSTEM, 0, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_REQUEST_OPLOCK_LEVEL_2 = new(FileDeviceType.FILE_SYSTEM, 1, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_REQUEST_BATCH_OPLOCK = new(FileDeviceType.FILE_SYSTEM, 2, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_OPLOCK_BREAK_ACKNOWLEDGE = new(FileDeviceType.FILE_SYSTEM, 3, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_OPBATCH_ACK_CLOSE_PENDING = new(FileDeviceType.FILE_SYSTEM, 4, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_OPLOCK_BREAK_NOTIFY = new(FileDeviceType.FILE_SYSTEM, 5, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_LOCK_VOLUME = new(FileDeviceType.FILE_SYSTEM, 6, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_UNLOCK_VOLUME = new(FileDeviceType.FILE_SYSTEM, 7, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DISMOUNT_VOLUME = new(FileDeviceType.FILE_SYSTEM, 8, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_IS_VOLUME_MOUNTED = new(FileDeviceType.FILE_SYSTEM, 10, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_IS_PATHNAME_VALID = new(FileDeviceType.FILE_SYSTEM, 11, FileControlMethod.Buffered, FileControlAccess.Any); // PATHNAME_BUFFER,
    public static readonly NtIoControlCode FSCTL_MARK_VOLUME_DIRTY = new(FileDeviceType.FILE_SYSTEM, 12, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_RETRIEVAL_POINTERS = new(FileDeviceType.FILE_SYSTEM, 14, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_COMPRESSION = new(FileDeviceType.FILE_SYSTEM, 15, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_COMPRESSION = new(FileDeviceType.FILE_SYSTEM, 16, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_SET_BOOTLOADER_ACCESSED = new(FileDeviceType.FILE_SYSTEM, 19, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_OPLOCK_BREAK_ACK_NO_2 = new(FileDeviceType.FILE_SYSTEM, 20, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_INVALIDATE_VOLUMES = new(FileDeviceType.FILE_SYSTEM, 21, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_FAT_BPB = new(FileDeviceType.FILE_SYSTEM, 22, FileControlMethod.Buffered, FileControlAccess.Any); // FSCTL_QUERY_FAT_BPB_BUFFER
    public static readonly NtIoControlCode FSCTL_REQUEST_FILTER_OPLOCK = new(FileDeviceType.FILE_SYSTEM, 23, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_FILESYSTEM_GET_STATISTICS = new(FileDeviceType.FILE_SYSTEM, 24, FileControlMethod.Buffered, FileControlAccess.Any); // FILESYSTEM_STATISTICS
    public static readonly NtIoControlCode FSCTL_GET_NTFS_VOLUME_DATA = new(FileDeviceType.FILE_SYSTEM, 25, FileControlMethod.Buffered, FileControlAccess.Any); // NTFS_VOLUME_DATA_BUFFER
    public static readonly NtIoControlCode FSCTL_GET_NTFS_FILE_RECORD = new(FileDeviceType.FILE_SYSTEM, 26, FileControlMethod.Buffered, FileControlAccess.Any); // NTFS_FILE_RECORD_INPUT_BUFFER, NTFS_FILE_RECORD_OUTPUT_BUFFER
    public static readonly NtIoControlCode FSCTL_GET_VOLUME_BITMAP = new(FileDeviceType.FILE_SYSTEM, 27, FileControlMethod.Neither, FileControlAccess.Any); // STARTING_LCN_INPUT_BUFFER, VOLUME_BITMAP_BUFFER
    public static readonly NtIoControlCode FSCTL_GET_RETRIEVAL_POINTERS = new(FileDeviceType.FILE_SYSTEM, 28, FileControlMethod.Neither, FileControlAccess.Any); // STARTING_VCN_INPUT_BUFFER, RETRIEVAL_POINTERS_BUFFER
    public static readonly NtIoControlCode FSCTL_MOVE_FILE = new(FileDeviceType.FILE_SYSTEM, 29, FileControlMethod.Buffered, FileControlAccess.Any); // MOVE_FILE_DATA,
    public static readonly NtIoControlCode FSCTL_IS_VOLUME_DIRTY = new(FileDeviceType.FILE_SYSTEM, 30, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ALLOW_EXTENDED_DASD_IO = new(FileDeviceType.FILE_SYSTEM, 32, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_FIND_FILES_BY_SID = new(FileDeviceType.FILE_SYSTEM, 35, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_OBJECT_ID = new(FileDeviceType.FILE_SYSTEM, 38, FileControlMethod.Buffered, FileControlAccess.Any); // FILE_OBJECTID_BUFFER
    public static readonly NtIoControlCode FSCTL_GET_OBJECT_ID = new(FileDeviceType.FILE_SYSTEM, 39, FileControlMethod.Buffered, FileControlAccess.Any); // FILE_OBJECTID_BUFFER
    public static readonly NtIoControlCode FSCTL_DELETE_OBJECT_ID = new(FileDeviceType.FILE_SYSTEM, 40, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_REPARSE_POINT = new(FileDeviceType.FILE_SYSTEM, 41, FileControlMethod.Buffered, FileControlAccess.Any); // REPARSE_DATA_BUFFER,
    public static readonly NtIoControlCode FSCTL_GET_REPARSE_POINT = new(FileDeviceType.FILE_SYSTEM, 42, FileControlMethod.Buffered, FileControlAccess.Any); // REPARSE_DATA_BUFFER
    public static readonly NtIoControlCode FSCTL_DELETE_REPARSE_POINT = new(FileDeviceType.FILE_SYSTEM, 43, FileControlMethod.Buffered, FileControlAccess.Any); // REPARSE_DATA_BUFFER,
    public static readonly NtIoControlCode FSCTL_ENUM_USN_DATA = new(FileDeviceType.FILE_SYSTEM, 44, FileControlMethod.Neither, FileControlAccess.Any); // MFT_ENUM_DATA,
    public static readonly NtIoControlCode FSCTL_SECURITY_ID_CHECK = new(FileDeviceType.FILE_SYSTEM, 45, FileControlMethod.Neither, FileControlAccess.Read);  // BULK_SECURITY_TEST_DATA,
    public static readonly NtIoControlCode FSCTL_READ_USN_JOURNAL = new(FileDeviceType.FILE_SYSTEM, 46, FileControlMethod.Neither, FileControlAccess.Any); // READ_USN_JOURNAL_DATA, USN
    public static readonly NtIoControlCode FSCTL_SET_OBJECT_ID_EXTENDED = new(FileDeviceType.FILE_SYSTEM, 47, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CREATE_OR_GET_OBJECT_ID = new(FileDeviceType.FILE_SYSTEM, 48, FileControlMethod.Buffered, FileControlAccess.Any); // FILE_OBJECTID_BUFFER
    public static readonly NtIoControlCode FSCTL_SET_SPARSE = new(FileDeviceType.FILE_SYSTEM, 49, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_ZERO_DATA = new(FileDeviceType.FILE_SYSTEM, 50, FileControlMethod.Buffered, FileControlAccess.Write); // FILE_ZERO_DATA_INFORMATION,
    public static readonly NtIoControlCode FSCTL_QUERY_ALLOCATED_RANGES = new(FileDeviceType.FILE_SYSTEM, 51, FileControlMethod.Neither, FileControlAccess.Read);  // FILE_ALLOCATED_RANGE_BUFFER, FILE_ALLOCATED_RANGE_BUFFER
    public static readonly NtIoControlCode FSCTL_ENABLE_UPGRADE = new(FileDeviceType.FILE_SYSTEM, 52, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_SET_ENCRYPTION = new(FileDeviceType.FILE_SYSTEM, 53, FileControlMethod.Neither, FileControlAccess.Any); // ENCRYPTION_BUFFER, DECRYPTION_STATUS_BUFFER
    public static readonly NtIoControlCode FSCTL_ENCRYPTION_FSCTL_IO = new(FileDeviceType.FILE_SYSTEM, 54, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_WRITE_RAW_ENCRYPTED = new(FileDeviceType.FILE_SYSTEM, 55, FileControlMethod.Neither, FileControlAccess.Any); // ENCRYPTED_DATA_INFO, EXTENDED_ENCRYPTED_DATA_INFO
    public static readonly NtIoControlCode FSCTL_READ_RAW_ENCRYPTED = new(FileDeviceType.FILE_SYSTEM, 56, FileControlMethod.Neither, FileControlAccess.Any); // REQUEST_RAW_ENCRYPTED_DATA, ENCRYPTED_DATA_INFO, EXTENDED_ENCRYPTED_DATA_INFO
    public static readonly NtIoControlCode FSCTL_CREATE_USN_JOURNAL = new(FileDeviceType.FILE_SYSTEM, 57, FileControlMethod.Neither, FileControlAccess.Any); // CREATE_USN_JOURNAL_DATA,
    public static readonly NtIoControlCode FSCTL_READ_FILE_USN_DATA = new(FileDeviceType.FILE_SYSTEM, 58, FileControlMethod.Neither, FileControlAccess.Any); // Read the Usn Record for a file
    public static readonly NtIoControlCode FSCTL_WRITE_USN_CLOSE_RECORD = new(FileDeviceType.FILE_SYSTEM, 59, FileControlMethod.Neither, FileControlAccess.Any); // Generate Close Usn Record
    public static readonly NtIoControlCode FSCTL_EXTEND_VOLUME = new(FileDeviceType.FILE_SYSTEM, 60, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_USN_JOURNAL = new(FileDeviceType.FILE_SYSTEM, 61, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DELETE_USN_JOURNAL = new(FileDeviceType.FILE_SYSTEM, 62, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_MARK_HANDLE = new(FileDeviceType.FILE_SYSTEM, 63, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SIS_COPYFILE = new(FileDeviceType.FILE_SYSTEM, 64, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SIS_LINK_FILES = new(FileDeviceType.FILE_SYSTEM, 65, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_RECALL_FILE = new(FileDeviceType.FILE_SYSTEM, 69, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_READ_FROM_PLEX = new(FileDeviceType.FILE_SYSTEM, 71, FileControlMethod.OutDirect, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_FILE_PREFETCH = new(FileDeviceType.FILE_SYSTEM, 72, FileControlMethod.Buffered, FileControlAccess.Any); // FILE_PREFETCH
    public static readonly NtIoControlCode FSCTL_MAKE_MEDIA_COMPATIBLE = new(FileDeviceType.FILE_SYSTEM, 76, FileControlMethod.Buffered, FileControlAccess.Write); // UDFS R/W
    public static readonly NtIoControlCode FSCTL_SET_DEFECT_MANAGEMENT = new(FileDeviceType.FILE_SYSTEM, 77, FileControlMethod.Buffered, FileControlAccess.Write); // UDFS R/W
    public static readonly NtIoControlCode FSCTL_QUERY_SPARING_INFO = new(FileDeviceType.FILE_SYSTEM, 78, FileControlMethod.Buffered, FileControlAccess.Any); // UDFS R/W
    public static readonly NtIoControlCode FSCTL_QUERY_ON_DISK_VOLUME_INFO = new(FileDeviceType.FILE_SYSTEM, 79, FileControlMethod.Buffered, FileControlAccess.Any); // C/UDFS
    public static readonly NtIoControlCode FSCTL_SET_VOLUME_COMPRESSION_STATE = new(FileDeviceType.FILE_SYSTEM, 80, FileControlMethod.Buffered, FileControlAccess.Any); // VOLUME_COMPRESSION_STATE
    public static readonly NtIoControlCode FSCTL_TXFS_MODIFY_RM = new(FileDeviceType.FILE_SYSTEM, 81, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_QUERY_RM_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 82, FileControlMethod.Buffered, FileControlAccess.Read);  // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_ROLLFORWARD_REDO = new(FileDeviceType.FILE_SYSTEM, 84, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_ROLLFORWARD_UNDO = new(FileDeviceType.FILE_SYSTEM, 85, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_START_RM = new(FileDeviceType.FILE_SYSTEM, 86, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_SHUTDOWN_RM = new(FileDeviceType.FILE_SYSTEM, 87, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_READ_BACKUP_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 88, FileControlMethod.Buffered, FileControlAccess.Read);  // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_WRITE_BACKUP_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 89, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_CREATE_SECONDARY_RM = new(FileDeviceType.FILE_SYSTEM, 90, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_GET_METADATA_INFO = new(FileDeviceType.FILE_SYSTEM, 91, FileControlMethod.Buffered, FileControlAccess.Read);  // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_GET_TRANSACTED_VERSION = new(FileDeviceType.FILE_SYSTEM, 92, FileControlMethod.Buffered, FileControlAccess.Read);  // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_SAVEPOINT_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 94, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_CREATE_MINIVERSION = new(FileDeviceType.FILE_SYSTEM, 95, FileControlMethod.Buffered, FileControlAccess.Write); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_TRANSACTION_ACTIVE = new(FileDeviceType.FILE_SYSTEM, 99, FileControlMethod.Buffered, FileControlAccess.Read);  // TxF
    public static readonly NtIoControlCode FSCTL_SET_ZERO_ON_DEALLOCATION = new(FileDeviceType.FILE_SYSTEM, 101, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_REPAIR = new(FileDeviceType.FILE_SYSTEM, 102, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_REPAIR = new(FileDeviceType.FILE_SYSTEM, 103, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_WAIT_FOR_REPAIR = new(FileDeviceType.FILE_SYSTEM, 104, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_INITIATE_REPAIR = new(FileDeviceType.FILE_SYSTEM, 106, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSC_INTERNAL = new(FileDeviceType.FILE_SYSTEM, 107, FileControlMethod.Neither, FileControlAccess.Any); // CSC internal implementation
    public static readonly NtIoControlCode FSCTL_SHRINK_VOLUME = new(FileDeviceType.FILE_SYSTEM, 108, FileControlMethod.Buffered, FileControlAccess.Any); // SHRINK_VOLUME_INFORMATION
    public static readonly NtIoControlCode FSCTL_SET_SHORT_NAME_BEHAVIOR = new(FileDeviceType.FILE_SYSTEM, 109, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DFSR_SET_GHOST_HANDLE_STATE = new(FileDeviceType.FILE_SYSTEM, 110, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_TXFS_LIST_TRANSACTION_LOCKED_FILES = new(FileDeviceType.FILE_SYSTEM, 120, FileControlMethod.Buffered, FileControlAccess.Read); // TxF
    public static readonly NtIoControlCode FSCTL_TXFS_LIST_TRANSACTIONS = new(FileDeviceType.FILE_SYSTEM, 121, FileControlMethod.Buffered, FileControlAccess.Read); // TxF
    public static readonly NtIoControlCode FSCTL_QUERY_PAGEFILE_ENCRYPTION = new(FileDeviceType.FILE_SYSTEM, 122, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_RESET_VOLUME_ALLOCATION_HINTS = new(FileDeviceType.FILE_SYSTEM, 123, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_DEPENDENT_VOLUME = new(FileDeviceType.FILE_SYSTEM, 124, FileControlMethod.Buffered, FileControlAccess.Any);    // Dependency File System Filter
    public static readonly NtIoControlCode FSCTL_SD_GLOBAL_CHANGE = new(FileDeviceType.FILE_SYSTEM, 125, FileControlMethod.Buffered, FileControlAccess.Any); // Query/Change NTFS Security Descriptors
    public static readonly NtIoControlCode FSCTL_TXFS_READ_BACKUP_INFORMATION2 = new(FileDeviceType.FILE_SYSTEM, 126, FileControlMethod.Buffered, FileControlAccess.Any); // TxF
    public static readonly NtIoControlCode FSCTL_LOOKUP_STREAM_FROM_CLUSTER = new(FileDeviceType.FILE_SYSTEM, 127, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_TXFS_WRITE_BACKUP_INFORMATION2 = new(FileDeviceType.FILE_SYSTEM, 128, FileControlMethod.Buffered, FileControlAccess.Any); // TxF
    public static readonly NtIoControlCode FSCTL_FILE_TYPE_NOTIFICATION = new(FileDeviceType.FILE_SYSTEM, 129, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_FILE_LEVEL_TRIM = new(FileDeviceType.FILE_SYSTEM, 130, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_GET_BOOT_AREA_INFO = new(FileDeviceType.FILE_SYSTEM, 140, FileControlMethod.Buffered, FileControlAccess.Any); // BOOT_AREA_INFO
    public static readonly NtIoControlCode FSCTL_GET_RETRIEVAL_POINTER_BASE = new(FileDeviceType.FILE_SYSTEM, 141, FileControlMethod.Buffered, FileControlAccess.Any); // RETRIEVAL_POINTER_BASE
    public static readonly NtIoControlCode FSCTL_SET_PERSISTENT_VOLUME_STATE = new(FileDeviceType.FILE_SYSTEM, 142, FileControlMethod.Buffered, FileControlAccess.Any);  // FILE_FS_PERSISTENT_VOLUME_INFORMATION
    public static readonly NtIoControlCode FSCTL_QUERY_PERSISTENT_VOLUME_STATE = new(FileDeviceType.FILE_SYSTEM, 143, FileControlMethod.Buffered, FileControlAccess.Any);  // FILE_FS_PERSISTENT_VOLUME_INFORMATION
    public static readonly NtIoControlCode FSCTL_REQUEST_OPLOCK = new(FileDeviceType.FILE_SYSTEM, 144, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 145, FileControlMethod.Buffered, FileControlAccess.Any); // CSV_TUNNEL_REQUEST
    public static readonly NtIoControlCode FSCTL_IS_CSV_FILE = new(FileDeviceType.FILE_SYSTEM, 146, FileControlMethod.Buffered, FileControlAccess.Any); // IS_CSV_FILE
    public static readonly NtIoControlCode FSCTL_QUERY_FILE_SYSTEM_RECOGNITION = new(FileDeviceType.FILE_SYSTEM, 147, FileControlMethod.Buffered, FileControlAccess.Any); //
    public static readonly NtIoControlCode FSCTL_CSV_GET_VOLUME_PATH_NAME = new(FileDeviceType.FILE_SYSTEM, 148, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_GET_VOLUME_NAME_FOR_VOLUME_MOUNT_POINT = new(FileDeviceType.FILE_SYSTEM, 149, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_GET_VOLUME_PATH_NAMES_FOR_VOLUME_NAME = new(FileDeviceType.FILE_SYSTEM, 150, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_IS_FILE_ON_CSV_VOLUME = new(FileDeviceType.FILE_SYSTEM, 151, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CORRUPTION_HANDLING = new(FileDeviceType.FILE_SYSTEM, 152, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_OFFLOAD_READ = new(FileDeviceType.FILE_SYSTEM, 153, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_OFFLOAD_WRITE = new(FileDeviceType.FILE_SYSTEM, 154, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_CSV_INTERNAL = new(FileDeviceType.FILE_SYSTEM, 155, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_PURGE_FAILURE_MODE = new(FileDeviceType.FILE_SYSTEM, 156, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_FILE_LAYOUT = new(FileDeviceType.FILE_SYSTEM, 157, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_IS_VOLUME_OWNED_BYCSVFS = new(FileDeviceType.FILE_SYSTEM, 158, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_INTEGRITY_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 159, FileControlMethod.Buffered, FileControlAccess.Any);                  // FSCTL_GET_INTEGRITY_INFORMATION_BUFFER
    public static readonly NtIoControlCode FSCTL_SET_INTEGRITY_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 160, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write); // FSCTL_SET_INTEGRITY_INFORMATION_BUFFER
    public static readonly NtIoControlCode FSCTL_QUERY_FILE_REGIONS = new(FileDeviceType.FILE_SYSTEM, 161, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DEDUP_FILE = new(FileDeviceType.FILE_SYSTEM, 165, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DEDUP_QUERY_FILE_HASHES = new(FileDeviceType.FILE_SYSTEM, 166, FileControlMethod.Neither, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_DEDUP_QUERY_RANGE_STATE = new(FileDeviceType.FILE_SYSTEM, 167, FileControlMethod.Neither, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_DEDUP_QUERY_REPARSE_INFO = new(FileDeviceType.FILE_SYSTEM, 168, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_RKF_INTERNAL = new(FileDeviceType.FILE_SYSTEM, 171, FileControlMethod.Neither, FileControlAccess.Any); // Resume Key Filter
    public static readonly NtIoControlCode FSCTL_SCRUB_DATA = new(FileDeviceType.FILE_SYSTEM, 172, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_REPAIR_COPIES = new(FileDeviceType.FILE_SYSTEM, 173, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_DISABLE_LOCAL_BUFFERING = new(FileDeviceType.FILE_SYSTEM, 174, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_MGMT_LOCK = new(FileDeviceType.FILE_SYSTEM, 175, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_QUERY_DOWN_LEVEL_FILE_SYSTEM_CHARACTERISTICS = new(FileDeviceType.FILE_SYSTEM, 176, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ADVANCE_FILE_ID = new(FileDeviceType.FILE_SYSTEM, 177, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_SYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 178, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_QUERY_VETO_FILE_DIRECT_IO = new(FileDeviceType.FILE_SYSTEM, 179, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_WRITE_USN_REASON = new(FileDeviceType.FILE_SYSTEM, 180, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_CONTROL = new(FileDeviceType.FILE_SYSTEM, 181, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_REFS_VOLUME_DATA = new(FileDeviceType.FILE_SYSTEM, 182, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CSV_H_BREAKING_SYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 185, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_STORAGE_CLASSES = new(FileDeviceType.FILE_SYSTEM, 187, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_REGION_INFO = new(FileDeviceType.FILE_SYSTEM, 188, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_USN_TRACK_MODIFIED_RANGES = new(FileDeviceType.FILE_SYSTEM, 189, FileControlMethod.Buffered, FileControlAccess.Any); // USN_TRACK_MODIFIED_RANGES
    public static readonly NtIoControlCode FSCTL_QUERY_SHARED_VIRTUAL_DISK_SUPPORT = new(FileDeviceType.FILE_SYSTEM, 192, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SVHDX_SYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 193, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SVHDX_SET_INITIATOR_INFORMATION = new(FileDeviceType.FILE_SYSTEM, 194, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_EXTERNAL_BACKING = new(FileDeviceType.FILE_SYSTEM, 195, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_EXTERNAL_BACKING = new(FileDeviceType.FILE_SYSTEM, 196, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DELETE_EXTERNAL_BACKING = new(FileDeviceType.FILE_SYSTEM, 197, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ENUM_EXTERNAL_BACKING = new(FileDeviceType.FILE_SYSTEM, 198, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ENUM_OVERLAY = new(FileDeviceType.FILE_SYSTEM, 199, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ADD_OVERLAY = new(FileDeviceType.FILE_SYSTEM, 204, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_REMOVE_OVERLAY = new(FileDeviceType.FILE_SYSTEM, 205, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_UPDATE_OVERLAY = new(FileDeviceType.FILE_SYSTEM, 206, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_DUPLICATE_EXTENTS_TO_FILE = new(FileDeviceType.FILE_SYSTEM, 209, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_SPARSE_OVERALLOCATE = new(FileDeviceType.FILE_SYSTEM, 211, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_STORAGE_QOS_CONTROL = new(FileDeviceType.FILE_SYSTEM, 212, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_INITIATE_FILE_METADATA_OPTIMIZATION = new(FileDeviceType.FILE_SYSTEM, 215, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_FILE_METADATA_OPTIMIZATION = new(FileDeviceType.FILE_SYSTEM, 216, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SVHDX_ASYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 217, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_WOF_VERSION = new(FileDeviceType.FILE_SYSTEM, 218, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_HCS_SYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 219, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_HCS_ASYNC_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 220, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_EXTENT_READ_CACHE_INFO = new(FileDeviceType.FILE_SYSTEM, 221, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_REFS_VOLUME_COUNTER_INFO = new(FileDeviceType.FILE_SYSTEM, 222, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_CLEAN_VOLUME_METADATA = new(FileDeviceType.FILE_SYSTEM, 223, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_INTEGRITY_INFORMATION_EX = new(FileDeviceType.FILE_SYSTEM, 224, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SUSPEND_OVERLAY = new(FileDeviceType.FILE_SYSTEM, 225, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_VIRTUAL_STORAGE_QUERY_PROPERTY = new(FileDeviceType.FILE_SYSTEM, 226, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_FILESYSTEM_GET_STATISTICS_EX = new(FileDeviceType.FILE_SYSTEM, 227, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_VOLUME_CONTAINER_STATE = new(FileDeviceType.FILE_SYSTEM, 228, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_LAYER_ROOT = new(FileDeviceType.FILE_SYSTEM, 229, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_DIRECT_ACCESS_EXTENTS = new(FileDeviceType.FILE_SYSTEM, 230, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_NOTIFY_STORAGE_SPACE_ALLOCATION = new(FileDeviceType.FILE_SYSTEM, 231, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SSDI_STORAGE_REQUEST = new(FileDeviceType.FILE_SYSTEM, 232, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_DIRECT_IMAGE_ORIGINAL_BASE = new(FileDeviceType.FILE_SYSTEM, 233, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_READ_UNPRIVILEGED_USN_JOURNAL = new(FileDeviceType.FILE_SYSTEM, 234, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GHOST_FILE_EXTENTS = new(FileDeviceType.FILE_SYSTEM, 235, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_QUERY_GHOSTED_FILE_EXTENTS = new(FileDeviceType.FILE_SYSTEM, 236, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_UNMAP_SPACE = new(FileDeviceType.FILE_SYSTEM, 237, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_HCS_SYNC_NO_WRITE_TUNNEL_REQUEST = new(FileDeviceType.FILE_SYSTEM, 238, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_START_VIRTUALIZATION_INSTANCE = new(FileDeviceType.FILE_SYSTEM, 240, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_FILTER_FILE_IDENTIFIER = new(FileDeviceType.FILE_SYSTEM, 241, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_STREAMS_QUERY_PARAMETERS = new(FileDeviceType.FILE_SYSTEM, 241, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_STREAMS_ASSOCIATE_ID = new(FileDeviceType.FILE_SYSTEM, 242, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_STREAMS_QUERY_ID = new(FileDeviceType.FILE_SYSTEM, 243, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_RETRIEVAL_POINTERS_AND_REFCOUNT = new(FileDeviceType.FILE_SYSTEM, 244, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_VOLUME_NUMA_INFO = new(FileDeviceType.FILE_SYSTEM, 245, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_REFS_DEALLOCATE_RANGES = new(FileDeviceType.FILE_SYSTEM, 246, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_QUERY_REFS_SMR_VOLUME_INFO = new(FileDeviceType.FILE_SYSTEM, 247, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_REFS_SMR_VOLUME_GC_PARAMETERS = new(FileDeviceType.FILE_SYSTEM, 248, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_REFS_FILE_STRICTLY_SEQUENTIAL = new(FileDeviceType.FILE_SYSTEM, 249, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DUPLICATE_EXTENTS_TO_FILE_EX = new(FileDeviceType.FILE_SYSTEM, 250, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_QUERY_BAD_RANGES = new(FileDeviceType.FILE_SYSTEM, 251, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_DAX_ALLOC_ALIGNMENT_HINT = new(FileDeviceType.FILE_SYSTEM, 252, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DELETE_CORRUPTED_REFS_CONTAINER = new(FileDeviceType.FILE_SYSTEM, 253, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SCRUB_UNDISCOVERABLE_ID = new(FileDeviceType.FILE_SYSTEM, 254, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_NOTIFY_DATA_CHANGE = new(FileDeviceType.FILE_SYSTEM, 255, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_START_VIRTUALIZATION_INSTANCE_EX = new(FileDeviceType.FILE_SYSTEM, 256, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ENCRYPTION_KEY_CONTROL = new(FileDeviceType.FILE_SYSTEM, 257, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_VIRTUAL_STORAGE_SET_BEHAVIOR = new(FileDeviceType.FILE_SYSTEM, 258, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_SET_REPARSE_POINT_EX = new(FileDeviceType.FILE_SYSTEM, 259, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_REARRANGE_FILE = new(FileDeviceType.FILE_SYSTEM, 264, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_VIRTUAL_STORAGE_PASSTHROUGH = new(FileDeviceType.FILE_SYSTEM, 265, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_RETRIEVAL_POINTER_COUNT = new(FileDeviceType.FILE_SYSTEM, 266, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_ENABLE_PER_IO_FLAGS = new(FileDeviceType.FILE_SYSTEM, 267, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_GET_SHADOW_COPY_DATA = new(FileDeviceType.NETWORK_FILE_SYSTEM, 25, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_LMR_GET_LINK_TRACKING_INFORMATION = new(FileDeviceType.NETWORK_FILE_SYSTEM, 58, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_LMR_SET_LINK_TRACKING_INFORMATION = new(FileDeviceType.NETWORK_FILE_SYSTEM, 59, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_LMR_ARE_FILE_OBJECTS_ON_SAME_SERVER = new(FileDeviceType.NETWORK_FILE_SYSTEM, 60, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_ASSIGN_EVENT = new(FileDeviceType.NAMED_PIPE, 0, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_DISCONNECT = new(FileDeviceType.NAMED_PIPE, 1, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_LISTEN = new(FileDeviceType.NAMED_PIPE, 2, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_PEEK = new(FileDeviceType.NAMED_PIPE, 3, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_PIPE_QUERY_EVENT = new(FileDeviceType.NAMED_PIPE, 4, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_TRANSCEIVE = new(FileDeviceType.NAMED_PIPE, 5, FileControlMethod.Neither, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_PIPE_WAIT = new(FileDeviceType.NAMED_PIPE, 6, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_IMPERSONATE = new(FileDeviceType.NAMED_PIPE, 7, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_SET_CLIENT_PROCESS = new(FileDeviceType.NAMED_PIPE, 8, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_QUERY_CLIENT_PROCESS = new(FileDeviceType.NAMED_PIPE, 9, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_GET_PIPE_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 10, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_SET_PIPE_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 11, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_GET_CONNECTION_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 12, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_SET_CONNECTION_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 13, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_GET_HANDLE_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 14, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_SET_HANDLE_ATTRIBUTE = new(FileDeviceType.NAMED_PIPE, 15, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_FLUSH = new(FileDeviceType.NAMED_PIPE, 16, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_PIPE_DISABLE_IMPERSONATE = new(FileDeviceType.NAMED_PIPE, 17, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_SILO_ARRIVAL = new(FileDeviceType.NAMED_PIPE, 18, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_PIPE_CREATE_SYMLINK = new(FileDeviceType.NAMED_PIPE, 19, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_DELETE_SYMLINK = new(FileDeviceType.NAMED_PIPE, 20, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_QUERY_CLIENT_PROCESS_V2 = new(FileDeviceType.NAMED_PIPE, 21, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_PIPE_INTERNAL_READ = new(FileDeviceType.NAMED_PIPE, 2045, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_PIPE_INTERNAL_WRITE = new(FileDeviceType.NAMED_PIPE, 2046, FileControlMethod.Buffered, FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_PIPE_INTERNAL_TRANSCEIVE = new(FileDeviceType.NAMED_PIPE, 2047, FileControlMethod.Neither, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode FSCTL_PIPE_INTERNAL_READ_OVFLOW = new(FileDeviceType.NAMED_PIPE, 2048, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode FSCTL_MAILSLOT_PEEK = new(FileDeviceType.MAILSLOT, 0, FileControlMethod.Neither, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_CREATE_POINT = new(FileDeviceType.MOUNTMGR, 0, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_DELETE_POINTS = new(FileDeviceType.MOUNTMGR, 1, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_QUERY_POINTS = new(FileDeviceType.MOUNTMGR, 2, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_DELETE_POINTS_DBONLY = new(FileDeviceType.MOUNTMGR, 3, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_NEXT_DRIVE_LETTER = new(FileDeviceType.MOUNTMGR, 4, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_AUTO_DL_ASSIGNMENTS = new(FileDeviceType.MOUNTMGR, 5, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_VOLUME_MOUNT_POINT_CREATED = new(FileDeviceType.MOUNTMGR, 6, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_VOLUME_MOUNT_POINT_DELETED = new(FileDeviceType.MOUNTMGR, 7, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_CHANGE_NOTIFY = new(FileDeviceType.MOUNTMGR, 8, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_KEEP_LINKS_WHEN_OFFLINE = new(FileDeviceType.MOUNTMGR, 9, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_CHECK_UNPROCESSED_VOLUMES = new(FileDeviceType.MOUNTMGR, 10, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_VOLUME_ARRIVAL_NOTIFICATION = new(FileDeviceType.MOUNTMGR, 11, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_QUERY_DOS_VOLUME_PATH = new(FileDeviceType.MOUNTMGR, 12, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_QUERY_DOS_VOLUME_PATHS = new(FileDeviceType.MOUNTMGR, 13, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_SCRUB_REGISTRY = new(FileDeviceType.MOUNTMGR, 14, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_QUERY_AUTO_MOUNT = new(FileDeviceType.MOUNTMGR, 15, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_MOUNTMGR_SET_AUTO_MOUNT = new(FileDeviceType.MOUNTMGR, 16, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_MOUNTDEV_QUERY_DEVICE_NAME = new(FileDeviceType.MOUNTDEV, 2, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DFS_GET_REFERRALS = new(FileDeviceType.DFS, 101, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode FSCTL_DFS_GET_REFERRALS_EX = new(FileDeviceType.DFS, 108, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_GET_DRIVE_GEOMETRY = new(FileDeviceType.DISK, 0x0000, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_GET_PARTITION_INFO = new(FileDeviceType.DISK, 0x0001, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_SET_PARTITION_INFO = new(FileDeviceType.DISK, 0x0002, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_GET_DRIVE_LAYOUT = new(FileDeviceType.DISK, 0x0003, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_SET_DRIVE_LAYOUT = new(FileDeviceType.DISK, 0x0004, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_VERIFY = new(FileDeviceType.DISK, 0x0005, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_FORMAT_TRACKS = new(FileDeviceType.DISK, 0x0006, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_REASSIGN_BLOCKS = new(FileDeviceType.DISK, 0x0007, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_PERFORMANCE = new(FileDeviceType.DISK, 0x0008, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_IS_WRITABLE = new(FileDeviceType.DISK, 0x0009, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_LOGGING = new(FileDeviceType.DISK, 0x000a, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_FORMAT_TRACKS_EX = new(FileDeviceType.DISK, 0x000b, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_HISTOGRAM_STRUCTURE = new(FileDeviceType.DISK, 0x000c, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_HISTOGRAM_DATA = new(FileDeviceType.DISK, 0x000d, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_HISTOGRAM_RESET = new(FileDeviceType.DISK, 0x000e, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_REQUEST_STRUCTURE = new(FileDeviceType.DISK, 0x000f, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_REQUEST_DATA = new(FileDeviceType.DISK, 0x0010, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_CONTROLLER_NUMBER = new(FileDeviceType.DISK, 0x0011, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode SMART_GET_VERSION = new(FileDeviceType.DISK, 0x0020, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode SMART_SEND_DRIVE_COMMAND = new(FileDeviceType.DISK, 0x0021, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode SMART_RCV_DRIVE_DATA = new(FileDeviceType.DISK, 0x0022, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_CHECK_VERIFY = new(FileDeviceType.DISK, 0x0200, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_MEDIA_REMOVAL = new(FileDeviceType.DISK, 0x0201, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_EJECT_MEDIA = new(FileDeviceType.DISK, 0x0202, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_LOAD_MEDIA = new(FileDeviceType.DISK, 0x0203, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_RESERVE = new(FileDeviceType.DISK, 0x0204, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_RELEASE = new(FileDeviceType.DISK, 0x0205, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_FIND_NEW_DEVICES = new(FileDeviceType.DISK, 0x0206, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_GET_MEDIA_TYPES = new(FileDeviceType.DISK, 0x0300, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_CHECK_VERIFY = new(FileDeviceType.MASS_STORAGE, 0x0200, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_CHECK_VERIFY2 = new(FileDeviceType.MASS_STORAGE, 0x0200, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_MEDIA_REMOVAL = new(FileDeviceType.MASS_STORAGE, 0x0201, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_EJECT_MEDIA = new(FileDeviceType.MASS_STORAGE, 0x0202, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_LOAD_MEDIA = new(FileDeviceType.MASS_STORAGE, 0x0203, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_LOAD_MEDIA2 = new(FileDeviceType.MASS_STORAGE, 0x0203, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_RESERVE = new(FileDeviceType.MASS_STORAGE, 0x0204, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_RELEASE = new(FileDeviceType.MASS_STORAGE, 0x0205, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_FIND_NEW_DEVICES = new(FileDeviceType.MASS_STORAGE, 0x0206, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_EJECTION_CONTROL = new(FileDeviceType.MASS_STORAGE, 0x0250, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_MCN_CONTROL = new(FileDeviceType.MASS_STORAGE, 0x0251, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_GET_MEDIA_TYPES = new(FileDeviceType.MASS_STORAGE, 0x0300, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_GET_MEDIA_TYPES_EX = new(FileDeviceType.MASS_STORAGE, 0x0301, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_RESET_BUS = new(FileDeviceType.MASS_STORAGE, 0x0400, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_RESET_DEVICE = new(FileDeviceType.MASS_STORAGE, 0x0401, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_STORAGE_GET_DEVICE_NUMBER = new(FileDeviceType.MASS_STORAGE, 0x0420, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_STORAGE_PREDICT_FAILURE = new(FileDeviceType.MASS_STORAGE, 0x0440, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_GET_PARTITION_INFO_EX = new(FileDeviceType.DISK, 0x12, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_SET_PARTITION_INFO_EX = new(FileDeviceType.DISK, 0x13, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_GET_DRIVE_LAYOUT_EX = new(FileDeviceType.DISK, 0x14, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_SET_DRIVE_LAYOUT_EX = new(FileDeviceType.DISK, 0x15, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_CREATE_DISK = new(FileDeviceType.DISK, 0x16, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_GET_LENGTH_INFO = new(FileDeviceType.DISK, 0x17, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_PERFORMANCE_OFF = new(FileDeviceType.DISK, 0x18, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_GET_DRIVE_GEOMETRY_EX = new(FileDeviceType.DISK, 0x28, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_GROW_PARTITION = new(FileDeviceType.DISK, 0x34, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_GET_CACHE_INFORMATION = new(FileDeviceType.DISK, 0x35, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_SET_CACHE_INFORMATION = new(FileDeviceType.DISK, 0x36, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_DELETE_DRIVE_LAYOUT = new(FileDeviceType.DISK, 0x40, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_DISK_UPDATE_PROPERTIES = new(FileDeviceType.DISK, 0x50, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_DISK_REMOVE_DEVICE = new(FileDeviceType.DISK, 0x207, FileControlMethod.Buffered, FileControlAccess.Read);
    public static readonly NtIoControlCode IOCTL_DISK_UPDATE_DRIVE_SIZE = new(FileDeviceType.DISK, 0x0032, FileControlMethod.Buffered, FileControlAccess.Read | FileControlAccess.Write);
    public static readonly NtIoControlCode IOCTL_SERIAL_LSRMST_INSERT = new(FileDeviceType.SERIAL_PORT, 31, FileControlMethod.Buffered, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_REDIR_QUERY_PATH = new(FileDeviceType.NETWORK_FILE_SYSTEM, 99, FileControlMethod.Neither, FileControlAccess.Any);
    public static readonly NtIoControlCode IOCTL_REDIR_QUERY_PATH_EX = new(FileDeviceType.NETWORK_FILE_SYSTEM, 100, FileControlMethod.Neither, FileControlAccess.Any);

    private static Dictionary<NtIoControlCode, string> BuildControlCodeToName()
    {
        Dictionary<NtIoControlCode, string> result = new();
        foreach (var field in typeof(NtWellKnownIoControlCodes).GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType == typeof(NtIoControlCode))
            {
                result[(NtIoControlCode)field.GetValue(null)] = field.Name;
            }
        }
        return result;
    }

    private static Lazy<Dictionary<NtIoControlCode, string>> _control_code_to_name = new(BuildControlCodeToName);

    private static Dictionary<string, NtIoControlCode> BuildNameToControlCode()
    {
        return _control_code_to_name.Value.ToDictionary(p => p.Value, p => p.Key, StringComparer.OrdinalIgnoreCase);
    }

    private static Lazy<Dictionary<string, NtIoControlCode>> _name_to_control_code = new(BuildNameToControlCode);

    /// <summary>
    /// Convert a control code to a known name.
    /// </summary>
    /// <param name="control_code">The control code.</param>
    /// <returns>The known name, or an empty string.</returns>
    public static string KnownControlCodeToName(NtIoControlCode control_code)
    {
        if (_control_code_to_name.Value.ContainsKey(control_code))
        {
            return _control_code_to_name.Value[control_code];
        }
        return string.Empty;
    }

    /// <summary>
    /// Get a list of known control codes.
    /// </summary>
    /// <returns>The list of known control codes.</returns>
    public static IEnumerable<NtIoControlCode> GetKnownControlCodes()
    {
        return _control_code_to_name.Value.Keys;
    }


    /// <summary>
    /// Get a list of known control codes.
    /// </summary>
    /// <returns>The control code.</returns>
    /// <exception cref="KeyNotFoundException">Thrown if can't find name.</exception>
    public static NtIoControlCode GetKnownControlCodeByName(string name)
    {
        return _name_to_control_code.Value[name];
    }
}
#pragma warning restore 1591

