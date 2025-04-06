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

using NtCoreLib.Ndr.Interop;
using NtCoreLib.Ndr.Rpc;
using NtCoreLib.Utilities.Text;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace NtCoreLib.Ndr.Marshal;

#pragma warning disable 1591
/// <summary>
/// A buffer to marshal NDR data to.
/// </summary>
/// <remarks>This class is primarily for internal use only.</remarks>
public class NdrMarshalBuffer : INdrMarshalBuffer
{
    #region Private Members
    private readonly MemoryStream _stm;
    private readonly BinaryWriter _writer;
    private readonly List<NdrSystemHandle> _handles;
    private readonly NdrDeferralStack _deferred_writes;
    private readonly bool _ndr64;
    private int _referent;
    private long? _conformance_position;

    private int ValueSize => _ndr64 ? 8 : 4;

    private void WriteSizedValue(int value)
    {
        if (_ndr64)
            WriteInt64(value);
        else
            WriteInt32(value);
    }

    private bool WriteReferent<T>(T obj) where T : class
    {
        if (obj == null)
        {
            WriteSizedValue(0);
            return false;
        }
        else
        {
            WriteSizedValue(_referent);
            _referent += 4;
            return true;
        }
    }

    private bool WriteReferent<T>(T? obj) where T : struct
    {
        if (!obj.HasValue)
        {
            WriteSizedValue(0);
            return false;
        }
        else
        {
            WriteSizedValue(_referent);
            _referent += 4;
            return true;
        }
    }

    private void WriteEmbeddedPointer<T>(NdrEmbeddedPointer<T> pointer, Action writer)
    {
        if (WriteReferent(pointer))
        {
            _deferred_writes.Add(writer);
        }
    }

    private void WriteStringArray(string[] array, Action<string> writer, int count)
    {
        array ??= new string[0];

        for (int i = 0; i < count; ++i)
        {
            string value = i < array.Length ? array[i] : string.Empty;
            WriteReferent(value);
            _deferred_writes.Add(() => writer(value));
        }
    }

    private void WriteStructPointerArray<T>(T?[] array, int count) where T : struct, INdrStructure
    {
        array ??= new T?[0];

        for (int i = 0; i < count; ++i)
        {
            INdrStructure value = i < array.Length ? array[i] : null;
            WriteReferent(value);
            _deferred_writes.Add(() => WriteStruct(value));
        }
    }

    private void WriteConformance(params int[] conformance)
    {
        if (_conformance_position.HasValue)
        {
            long current_position = _stm.Position;
            _stm.Position = _conformance_position.Value;
            byte[] data = new byte[conformance.Length * ValueSize];
            if (_ndr64)
            {
                long[] long_conformance = conformance.Cast<long>().ToArray();
                Buffer.BlockCopy(long_conformance, 0, data, 0, data.Length);
            }
            else
            {
                Buffer.BlockCopy(conformance, 0, data, 0, data.Length);
            }
            _stm.Write(data, 0, data.Length);
            _stm.Position = current_position;
            _conformance_position = null;
        }
        else
        {
            foreach (var i in conformance)
            {
                WriteSizedValue(i);
            }
        }
    }

    private bool SetupConformance(int dimensions)
    {
        if (dimensions == 0 || _conformance_position.HasValue)
        {
            return false;
        }

        for (int i = 0; i < dimensions; ++i)
        {
            WriteSizedValue(0x77777777);
        }

        _conformance_position = _stm.Position - (dimensions * ValueSize);

        return true;
    }

    private void WriteStructInternal(INdrStructure structure)
    {
        Align(structure);
        structure.Marshal(this);
    }

    private void WriteUnionInternal(INdrNonEncapsulatedUnion union, long selector)
    {
        Align(union);
        union.Marshal(this, selector);
    }

    private void Align(INdrStructure structure)
    {
        Align(structure.GetAlignment());
    }

    private void Align(int alignment)
    {
        byte[] buffer = new byte[NdrNativeUtils.CalculateAlignment((int)_stm.Length, alignment)];
        _stm.Write(buffer, 0, buffer.Length);
    }

    private void WritePrimitivePipeBlock<T>(T[] block) where T : struct
    {
        byte[] ba = new byte[Buffer.ByteLength(block)];
        Buffer.BlockCopy(block, 0, ba, 0, ba.Length);
        WriteBytes(ba);
    }

    private void WriteStructuredPipeBlock(IEnumerable<INdrStructure> block)
    {
        foreach (var value in block)
        {
            WriteStruct(value);
        }
    }

    #endregion

    #region Constructors
    public NdrMarshalBuffer() : this(default)
    {
    }

    public NdrMarshalBuffer(NdrDataRepresentation data_representation, bool ndr64 = false)
    {
        _stm = new MemoryStream();
        _writer = new BinaryWriter(_stm, Encoding.Unicode);
        _handles = new List<NdrSystemHandle>();
        _referent = 0x20000;
        _deferred_writes = new NdrDeferralStack();
        _ndr64 = ndr64;
        NdrUnmarshalBuffer.CheckDataRepresentation(data_representation);
        DataRepresentation = data_representation;
    }
    #endregion

    #region Misc Methods
    public void WriteSystemHandle<T>(T handle, uint desired_access = 0) where T : NtObject
    {
        if (handle != null)
        {
            _handles.Add(new(handle, desired_access));
            WriteInt32(_handles.Count);
        }
        else
        {
            WriteInt32(0);
        }
        if (!NtObjectUtils.IsWindows81OrLess)
        {
            WriteInt32(0);
        }
    }

    public void WriteUnsupported(NdrUnsupported type, string name)
    {
        throw new NotImplementedException($"Writing type {name} is unsupported");
    }

    public void WriteEmpty(NdrEmpty empty)
    {
        // Do nothing.
    }

    public void WriteInterfacePointer(NdrInterfacePointer intf)
    {
        WriteStruct(intf);
    }

    public void WritePipeArray<T>(T[] pipe_array) where T : struct
    {
        if (pipe_array is null)
        {
            throw new ArgumentNullException(nameof(pipe_array));
        }

        WritePipe(new NdrPipe<T>(pipe_array));
    }

    public void WritePipe<T>(NdrPipe<T> pipe) where T : struct
    {
        if (pipe is null)
        {
            throw new ArgumentNullException(nameof(pipe));
        }

        Type type = typeof(T);

        Action<T[]> writer;
        if (type == typeof(byte) || type == typeof(sbyte))
        {
            writer = (T[] a) => WriteBytes((byte[])(object)a);
        }
        else if (type.IsPrimitive)
        {
            writer = a => WritePrimitivePipeBlock(a);
        }
        else if (typeof(INdrStructure).IsAssignableFrom(type))
        {
            writer = (T[] a) => WriteStructuredPipeBlock(a.Cast<T, INdrStructure>());
        }
        else
        {
            throw new NotImplementedException("Pipes only support primitive and NDR structures.");
        }

        foreach (var block in pipe.Blocks)
        {
            if (block.Length == 0)
                continue;
            WriteInt32(block.Length);
            writer(block);
        }
        WriteInt32(0);
    }

    public byte[] ToArray()
    {
        byte[] ret = _stm.ToArray();
        int alignment = NdrNativeUtils.CalculateAlignment(ret.Length, 8);
        if (alignment > 0)
        {
            Array.Resize(ref ret, ret.Length + alignment);
        }

        return ret;
    }

    public NdrPickledType ToPickledType()
    {
        return new NdrPickledType(ToArray(), DataRepresentation, 
            RpcSyntaxIdentifier.DCETransferSyntax);
    }

    #endregion

    #region Primitive Types
    public void WriteByte(byte b)
    {
        _writer.Write(b);
    }

    public void WriteByte(byte? b)
    {
        if (b.HasValue)
        {
            WriteByte(b.Value);
        }
    }

    public void WriteSByte(sbyte b)
    {
        _writer.Write(b);
    }

    public void WriteSByte(sbyte? b)
    {
        if (b.HasValue)
        {
            WriteSByte(b.Value);
        }
    }

    public void WriteInt16(short s)
    {
        Align(2);
        _writer.Write(s);
    }

    public void WriteInt16(short? s)
    {
        if (s.HasValue)
        {
            WriteInt16(s.Value);
        }
    }

    public void WriteUInt16(ushort s)
    {
        Align(2);
        _writer.Write(s);
    }

    public void WriteUInt16(ushort? s)
    {
        if (s.HasValue)
        {
            WriteUInt16(s.Value);
        }
    }

    public void WriteInt32(int i)
    {
        Align(4);
        _writer.Write(i);
    }

    public void WriteInt32(int? i)
    {
        if (i.HasValue)
        {
            WriteInt32(i.Value);
        }
    }

    public void WriteUInt32(uint i)
    {
        Align(4);
        _writer.Write(i);
    }

    public void WriteUInt32(uint? i)
    {
        if (i.HasValue)
        {
            WriteUInt32(i.Value);
        }
    }

    public void WriteInt64(long l)
    {
        Align(8);
        _writer.Write(l);
    }

    public void WriteInt64(long? l)
    {
        if (l.HasValue)
        {
            WriteInt64(l.Value);
        }
    }

    public void WriteUInt64(ulong l)
    {
        Align(8);
        _writer.Write(l);
    }

    public void WriteUInt64(ulong? l)
    {
        if (l.HasValue)
        {
            WriteUInt64(l.Value);
        }
    }

    public void WriteFloat(float f)
    {
        Align(4);
        _writer.Write(f);
    }

    public void WriteFloat(float? f)
    {
        if (f.HasValue)
        {
            WriteFloat(f.Value);
        }
    }

    public void WriteDouble(double d)
    {
        Align(8);
        _writer.Write(d);
    }

    public void WriteDouble(double? d)
    {
        if (d.HasValue)
        {
            WriteDouble(d.Value);
        }
    }

    public void WriteChar(char c)
    {
        Align(2);
        _writer.Write(c);
    }

    public void WriteChar(char? c)
    {
        if (c.HasValue)
        {
            WriteChar(c.Value);
        }
    }

    public void WriteInt3264(NdrInt3264 p)
    {
        WriteInt32(p.Value);
    }

    public void WriteInt3264(NdrInt3264? p)
    {
        if (p.HasValue)
        {
            WriteInt3264(p.Value);
        }
    }

    public void WriteUInt3264(NdrUInt3264 p)
    {
        WriteUInt32(p.Value);
    }

    public void WriteUInt3264(NdrUInt3264? p)
    {
        if (p.HasValue)
        {
            WriteUInt3264(p.Value);
        }
    }

    public void WriteEnum16(NdrEnum16 e)
    {
        if (_ndr64)
        {
            WriteInt32(e.Value);
        }
        else
        {
            WriteInt16((short)e.Value);
        }
    }

    public void WriteEnum16(NdrEnum16? p)
    {
        if (p.HasValue)
        {
            WriteEnum16(p.Value);
        }
    }

    #endregion

    #region String Types

    public void WriteTerminatedString(string str)
    {
        WriteConformantVaryingString(str, -1);
    }

    public void WriteTerminatedAnsiString(string str)
    {
        WriteConformantVaryingAnsiString(str, -1);
    }

    public void WriteConformantVaryingString(string str, long conformance)
    {
        if (str == null)
        {
            return;
        }

        char[] values = (str + '\0').ToCharArray();
        if (conformance < 0)
        {
            conformance = values.Length;
        }

        // Maximum count.
        WriteConformance((int)conformance);
        // Offset.
        WriteSizedValue(0);
        // Actual count.
        WriteSizedValue(values.Length);
        WriteChars(values);
    }

    public void WriteConformantVaryingAnsiString(string str, long conformance)
    {
        if (str == null)
        {
            return;
        }

        byte[] values = BinaryEncoding.Instance.GetBytes(str + '\0');
        if (conformance < 0)
        {
            conformance = values.Length;
        }

        // Maximum count.
        WriteConformance((int)conformance);
        // Offset.
        WriteSizedValue(0);
        // Actual count.
        WriteSizedValue(values.Length);
        WriteBytes(values);
    }

    public void WriteFixedString(string str, int fixed_count)
    {
        WriteFixedChars(str.ToCharArray(), fixed_count);
    }

    public void WriteFixedAnsiString(string str, int fixed_count)
    {
        WriteFixedByteArray(BinaryEncoding.Instance.GetBytes(str), fixed_count);
    }

    public void WriteVaryingString(string str)
    {
        if (str == null)
        {
            return;
        }

        char[] values = (str + '\0').ToCharArray();
        // Offset.
        WriteSizedValue(0);
        // Actual count.
        WriteSizedValue(values.Length);
        WriteChars(values);
    }

    public void WriteVaryingAnsiString(string str)
    {
        if (str == null)
        {
            return;
        }

        byte[] values = BinaryEncoding.Instance.GetBytes(str + '\0');
        // Offset.
        WriteSizedValue(0);
        // Actual count.
        WriteSizedValue(values.Length);
        WriteBytes(values);
    }

    public void WriteBasicString(string str)
    {
        char[] chars = str?.ToCharArray() ?? Array.Empty<char>();
        var blob = new FLAGGED_WORD_BLOB(str == null ? -1 : chars.Length * 2, chars.Length, chars);
        WriteStruct(blob);
    }

    public void WriteHString(string str)
    {
        Align(8);
        char[] chars = str?.ToCharArray() ?? Array.Empty<char>();
        WriteInt32(NdrUserMarshal.WDT_INPROC_CALL);
        WriteInt32(chars.Length * 2);
        WriteChars(chars);
    }

    #endregion

    #region Structure Types

    public void WriteGuid(Guid guid)
    {
        Align(4);
        WriteBytes(guid.ToByteArray());
    }

    public void WriteGuid(Guid? guid)
    {
        if (guid.HasValue)
        {
            WriteGuid(guid.Value);
        }
    }

    public void WriteStruct<T>(T? structure) where T : struct, INdrStructure
    {
        if (structure.HasValue)
        {
            WriteStruct(structure.Value);
        }
    }

    public void WriteStruct<T>(T structure) where T : struct, INdrStructure
    {
        WriteStruct((INdrStructure)structure);
    }

    public void WriteStruct(INdrStructure structure)
    {
        bool conformant = false;
        if (structure is INdrConformantStructure conformant_structure)
        {
            conformant = SetupConformance(conformant_structure.GetConformantDimensions());
            System.Diagnostics.Debug.Assert(_conformance_position.HasValue);
        }

        using (var queue = _deferred_writes.Push())
        {
            WriteStructInternal(structure);
        }

        if (conformant)
        {
            System.Diagnostics.Debug.Assert(!_conformance_position.HasValue);
        }
    }

    public void WriteUnion<T>(T? union, long selector) where T : struct, INdrNonEncapsulatedUnion
    {
        if (union.HasValue)
        {
            WriteUnion((INdrNonEncapsulatedUnion)union.Value, selector);
        }
    }

    public void WriteUnion<T>(T union, long selector) where T : struct, INdrNonEncapsulatedUnion
    {
        WriteUnion((INdrNonEncapsulatedUnion)union, selector);
    }

    public void WriteUnion(INdrNonEncapsulatedUnion union, long selector)
    {
        WriteUnionInternal(union, selector);
    }

    public void WriteContextHandle(NdrContextHandle handle)
    {
        WriteInt32(handle.Attributes);
        WriteGuid(handle.Uuid);
    }

    public void WriteContextHandle(NdrTypeStrictContextHandle handle)
    {
        if (handle is null)
        {
            throw new ArgumentNullException(nameof(handle));
        }
        WriteContextHandle(handle.Handle);
    }

    #endregion

    #region Pointer Types
    public void WriteEmbeddedPointer<T>(NdrEmbeddedPointer<T> pointer, Action<T> writer)
    {
        WriteEmbeddedPointer(pointer, () => writer(pointer));
    }

    public void WriteEmbeddedPointer<T, U>(NdrEmbeddedPointer<T> pointer, Action<T, U> writer, U arg)
    {
        WriteEmbeddedPointer(pointer, () => writer(pointer, arg));
    }

    public void WriteEmbeddedPointer<T, U, V>(NdrEmbeddedPointer<T> pointer, Action<T, U, V> writer, U arg, V arg2)
    {
        WriteEmbeddedPointer(pointer, () => writer(pointer, arg, arg2));
    }

    public void WriteReferent<T>(T obj, Action<T> writer) where T : class
    {
        if (WriteReferent(obj))
        {
            writer(obj);
        }
    }

    public void WriteReferent<T, U>(T obj, Action<T, U> writer, U arg) where T : class
    {
        if (WriteReferent(obj))
        {
            writer(obj, arg);
        }
    }

    public void WriteReferent<T, U, V>(T obj, Action<T, U, V> writer, U arg, V arg2) where T : class
    {
        if (WriteReferent(obj))
        {
            writer(obj, arg, arg2);
        }
    }

    public void WriteReferent<T>(T? obj, Action<T> writer) where T : struct
    {
        if (WriteReferent(obj))
        {
            writer(obj ?? default);
        }
    }

    public void WriteReferent<T, U>(T? obj, Action<T, U> writer, U arg) where T : struct
    {
        if (WriteReferent(obj))
        {
            writer(obj ?? default, arg);
        }
    }

    public void WriteReferent<T, U, V>(T? obj, Action<T, U, V> writer, U arg, V arg2) where T : struct
    {
        if (WriteReferent(obj))
        {
            writer(obj ?? default, arg, arg2);
        }
    }

    public void WriteIgnorePointer(IntPtr value)
    {
        WriteInt32((int)value.ToInt64());
    }

    #endregion

    #region Fixed Array Types
    public void WriteBytes(byte[] array)
    {
        _writer.Write(array);
    }

    public void WriteChars(char[] chars)
    {
        Align(2);
        _writer.Write(chars);
    }

    public void WriteFixedByteArray(byte[] array, int actual_count)
    {
        if (array.Length != actual_count)
        {
            array = array.CloneBytes();
            Array.Resize(ref array, actual_count);
        }
        _writer.Write(array);
    }

    public void WriteFixedChars(char[] chars, int fixed_count)
    {
        Align(2);
        if (chars.Length != fixed_count)
        {
            chars = (char[])chars.Clone();
            Array.Resize(ref chars, fixed_count);
        }
        _writer.Write(chars);
    }

    public void WriteFixedPrimitiveArray<T>(T[] array, int fixed_count) where T : struct
    {
        if (fixed_count == 0)
            return;
        int size = NdrNativeUtils.GetPrimitiveTypeSize<T>();
        int actual_size = array.Length * size;
        byte[] total_buffer = new byte[size * fixed_count];
        Buffer.BlockCopy(array, 0, total_buffer, 0, Math.Min(actual_size, total_buffer.Length));
        Align(size);
        WriteFixedByteArray(total_buffer, total_buffer.Length);
    }

    public void WriteFixedStructArray<T>(T[] arr, int actual_count) where T : INdrStructure, new()
    {
        using var queue = _deferred_writes.Push();
        for (int i = 0; i < actual_count; ++i)
        {
            if (i < arr.Length)
            {
                WriteStructInternal(arr[i]);
            }
            else
            {
                WriteStructInternal(new T());
            }
        }
    }

    #endregion

    #region Varying Array Types

    public void WriteVaryingByteArray(byte[] array, long variance)
    {
        // Offset.
        WriteSizedValue(0);
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Actual Count
        WriteSizedValue(var_int);
        Array.Resize(ref array, var_int);
        WriteBytes(array);
    }

    public void WriteVaryingCharArray(char[] array, long variance)
    {
        // Offset.
        WriteSizedValue(0);
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Actual Count
        WriteSizedValue(var_int);
        Array.Resize(ref array, var_int);
        WriteChars(array);
    }

    public void WriteVaryingPrimitiveArray<T>(T[] array, long variance) where T : struct
    {
        WriteSizedValue(0);
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Actual Count
        WriteSizedValue(var_int);
        WriteFixedPrimitiveArray<T>(array, var_int);
    }

    public void WriteVaryingStructArray<T>(T[] array, long variance) where T : struct, INdrStructure
    {
        using var queue = _deferred_writes.Push();
        WriteVaryingArrayCallback(array, t => WriteStructInternal(t), variance);
    }

    public void WriteVaryingStructPointerArray<T>(T?[] array, long variance) where T : struct, INdrStructure
    {
        // Offset.
        WriteSizedValue(0);
        // Actual Count
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        WriteSizedValue(var_int);
        using var queue = _deferred_writes.Push();
        WriteStructPointerArray(array, (int)variance);
    }

    public void WriteVaryingArray<T>(T[] array, long variance) where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            WriteVaryingByteArray(array.Cast<T, byte>(), variance);
        }
        else if (typeof(T) == typeof(char))
        {
            WriteVaryingCharArray(array.Cast<T, char>(), variance);
        }
        else if (typeof(INdrStructure).IsAssignableFrom(typeof(T)))
        {
            using var queue = _deferred_writes.Push();
            WriteVaryingArrayCallback(array, p => WriteStructInternal((INdrStructure)p), variance);
        }
        else if (typeof(T).IsPrimitive)
        {
            WriteVaryingPrimitiveArray(array, variance);
        }
        else
        {
            throw new ArgumentException($"Invalid type {typeof(T)} for {nameof(WriteVaryingArray)}");
        }
    }

    public void WriteVaryingArrayCallback<T>(T[] array, Action<T> writer, long variance)
    {
        // Offset.
        WriteSizedValue(0);
        array ??= new T[0];
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Actual Count
        WriteSizedValue(var_int);
        for (int i = 0; i < var_int; ++i)
        {
            if (i < array.Length)
            {
                writer(array[i]);
            }
            else
            {
                writer(default);
            }
        }
    }

    public void WriteVaryingStringArray(string[] array, Action<string> writer, long variance)
    {
        // Offset.
        WriteSizedValue(0);
        // Actual Count
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        WriteSizedValue(var_int);
        using var queue = _deferred_writes.Push();
        WriteStringArray(array, writer, (int)variance);
    }

    #endregion

    #region Conformant Array Types

    public void WriteConformantByteArray(byte[] array, long conformance)
    {
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Max Count
        WriteConformance(var_int);
        Array.Resize(ref array, var_int);
        WriteBytes(array);
    }

    public void WriteConformantCharArray(char[] array, long conformance)
    {
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Max Count
        WriteConformance(var_int);
        Array.Resize(ref array, var_int);
        WriteChars(array);
    }

    public void WriteConformantPrimitiveArray<T>(T[] array, long conformance) where T : struct
    {
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Max Count
        WriteConformance(var_int);
        WriteFixedPrimitiveArray<T>(array, var_int);
    }

    public void WriteConformantStructArray<T>(T[] array, long conformance) where T : struct, INdrStructure
    {
        using var queue = _deferred_writes.Push();
        WriteConformantArrayCallback(array, t => WriteStructInternal(t), conformance);
    }

    public void WriteConformantStructPointerArray<T>(T?[] array, long conformance) where T : struct, INdrStructure
    {
        if (array == null)
            return;
        using var queue = _deferred_writes.Push();

        WriteConformantArrayCallback(array, t => WriteReferent(t), conformance);
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        for (int i = 0; i < var_int; ++i)
        {
            if (i < array.Length && array[i].HasValue)
            {
                WriteStruct(array[i]);
            }
        }
    }

    public void WriteConformantStringArray(string[] array, Action<string> writer, long conformance)
    {
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        // Max Count
        WriteConformance(var_int);
        using var queue = _deferred_writes.Push();
        WriteStringArray(array, writer, var_int);
    }

    public void WriteConformantArrayCallback<T>(T[] array, Action<T> writer, long conformance)
    {
        // Max Count
        if (array == null)
        {
            array = new T[0];
        }
        int var_int = (int)conformance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        WriteConformance(var_int);

        for (int i = 0; i < var_int; ++i)
        {
            if (i < array.Length)
            {
                writer(array[i]);
            }
            else
            {
                writer(default);
            }
        }
    }

    public void WriteConformantArray<T>(T[] array, long conformance) where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            WriteConformantByteArray(array.Cast<T, byte>(), conformance);
        }
        else if (typeof(T) == typeof(char))
        {
            WriteConformantCharArray(array.Cast<T, char>(), conformance);
        }
        else if (typeof(INdrStructure).IsAssignableFrom(typeof(T)))
        {
            using var queue = _deferred_writes.Push();
            WriteConformantArrayCallback(array, p => WriteStructInternal((INdrStructure)p), conformance);
        }
        else if (typeof(T).IsPrimitive)
        {
            WriteConformantPrimitiveArray(array, conformance);
        }
        else
        {
            throw new ArgumentException($"Invalid type {typeof(T)} for {nameof(WriteConformantArray)}");
        }
    }

    #endregion

    #region Conformant Varying Array Types

    public void WriteConformantVaryingByteArray(byte[] array, long conformance, long variance)
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        WriteVaryingByteArray(array, variance);
    }

    public void WriteConformantVaryingCharArray(char[] array, long conformance, long variance)
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        WriteVaryingCharArray(array, variance);
    }

    public void WriteConformantVaryingPrimitiveArray<T>(T[] array, long conformance, long variance) where T : struct
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        WriteVaryingPrimitiveArray(array, variance);
    }

    public void WriteConformantVaryingStructArray<T>(T[] array, long conformance, long variance) where T : struct, INdrStructure
    {
        using var queue = _deferred_writes.Push();
        WriteVaryingArrayCallback(array, t => WriteStructInternal(t), variance);
    }

    public void WriteConformantVaryingStructPointerArray<T>(T?[] array, long conformance, long variance) where T : struct, INdrStructure
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        // Offset.
        WriteSizedValue(0);
        // Actual Count
        int var_int = (int)variance;
        if (var_int < 0)
        {
            var_int = array.Length;
        }
        WriteSizedValue(var_int);
        using var queue = _deferred_writes.Push();
        WriteStructPointerArray(array, (int)variance);
    }

    public void WriteConformantVaryingStringArray(string[] array, Action<string> writer, long conformance, long variance)
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        using var queue = _deferred_writes.Push();
        WriteVaryingStringArray(array, writer, (int)variance);
    }

    public void WriteConformantVaryingArrayCallback<T>(T[] array, Action<T> writer, long conformance, long variance)
    {
        // Max Count
        int con_int = (int)conformance;
        if (con_int < 0)
        {
            con_int = array.Length;
        }
        WriteConformance(con_int);
        WriteVaryingArrayCallback(array, writer, variance);
    }

    public void WriteConformantVaryingArray<T>(T[] array, long conformance, long variance) where T : struct
    {
        if (typeof(T) == typeof(byte))
        {
            WriteConformantVaryingByteArray(array.Cast<T, byte>(), conformance, variance);
        }
        else if (typeof(T) == typeof(char))
        {
            WriteConformantVaryingCharArray(array.Cast<T, char>(), conformance, variance);
        }
        else if (typeof(INdrStructure).IsAssignableFrom(typeof(T)))
        {
            using var queue = _deferred_writes.Push();
            WriteConformantVaryingArrayCallback(array, p => WriteStructInternal((INdrStructure)p), conformance, variance);
        }
        else if (typeof(T).IsPrimitive)
        {
            WriteConformantVaryingPrimitiveArray(array, conformance, variance);
        }
        else
        {
            throw new ArgumentException($"Invalid type {typeof(T)} for {nameof(WriteConformantVaryingArray)}");
        }
    }

    #endregion

    #region Public Properties

    public IReadOnlyList<NdrSystemHandle> Handles => _handles.AsReadOnly();

    public NdrDataRepresentation DataRepresentation { get; }

    #endregion
}
#pragma warning restore 1591

