using Google.Protobuf;
using MessagePack;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Linq;
using System.Reflection;

namespace NomadIIS.Services.Configuration;

public sealed class MessagePackHelper
{
	public static T Deserialize<T> ( ByteString data )
		where T : class, new()
	{
		var config = MessagePackSerializer.Deserialize<Dictionary<object, object>>( data.Memory );

		return (T)Deserialize( config, typeof( T ) );
	}

	private static object Deserialize ( IDictionary<object, object> data, Type targetType )
	{
		var properties = targetType.GetProperties( BindingFlags.Public | BindingFlags.Instance );

		var t = Activator.CreateInstance( targetType )!;

		foreach ( var property in properties )
		{
			var fieldAttribute = property.GetCustomAttribute<ConfigurationField>();
			var collectionFieldAttribute = property.GetCustomAttribute<ConfigurationCollectionField>();
			if ( fieldAttribute is not null )
			{
				var isRequired = property.GetCustomAttribute<RequiredAttribute>() is not null;
				var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();

				if ( data.TryGetValue( fieldAttribute.Name, out var rawValue ) )
				{
					property.SetValue( t, ConvertValue( fieldAttribute.Name, rawValue, property.PropertyType ) );
				}
				else if ( defaultValueAttribute?.Value is not null )
					property.SetValue( t, ConvertValue( fieldAttribute.Name, defaultValueAttribute.Value, property.PropertyType ) );
				else if ( isRequired )
					throw new ArgumentException( $"Missing required value {fieldAttribute.Name}." );
			}
			else if ( collectionFieldAttribute is not null )
			{
				if ( data.TryGetValue( collectionFieldAttribute.CollectionName, out var rawValue ) && rawValue is object[] rawArray )
				{
					if ( rawArray.Length < collectionFieldAttribute.MinCount )
						throw new ArgumentException( $"At least {collectionFieldAttribute.MinCount} {collectionFieldAttribute.BlockName} must be specified." );
					if ( collectionFieldAttribute.MaxCount is not null && rawArray.Length > collectionFieldAttribute.MaxCount )
						throw new ArgumentException( $"Maximum {collectionFieldAttribute.MaxCount} {collectionFieldAttribute.BlockName} can be specified." );

					var elementType = property.PropertyType.GetElementType()!;

					var list = Array.CreateInstance( elementType, rawArray.Length );

					for ( var i = 0; i < rawArray.Length; i++ )
					{
						if ( rawArray[i] is not Dictionary<object, object> rawDict )
							throw new NotSupportedException();

						var itemValue = Deserialize( rawDict, elementType );
						list.SetValue( itemValue, i );
					}

					property.SetValue( t, list );
				}
				else
					throw new NotSupportedException();
			}
		}

		return t;
	}

	private static object? ConvertValue ( string fieldName, object rawValue, Type targetType )
	{
		if ( rawValue is null )
			return null;

		if ( rawValue is string strValue )
		{
			if ( targetType == typeof( string ) )
				return strValue;
			if ( targetType == typeof( bool ) || targetType == typeof( bool? ) )
				return strValue == "true" ? true : strValue == "false" ? false : throw new ArgumentException( $"Invalid boolean value {strValue}." );
			if ( targetType == typeof( TimeSpan ) || targetType == typeof( TimeSpan? ) )
				return TimeSpanHelper.Parse( strValue );
			if ( targetType == typeof( int ) || targetType == typeof( int? ) )
				return int.Parse( strValue );
			if ( targetType.IsEnum || Nullable.GetUnderlyingType( targetType ) is not null && Nullable.GetUnderlyingType( targetType )!.IsEnum )
			{
				if ( Enum.TryParse( Nullable.GetUnderlyingType( targetType ) ?? targetType, strValue, true, out var enumValue ) )
					return enumValue;
				throw new ArgumentException( $"Invalid value \"{strValue}\" for {fieldName}." );
			}
		}

		if ( rawValue is byte byteValue && ( targetType == typeof( int ) || targetType == typeof( int? ) ) )
			return (int)byteValue;
		if ( rawValue is ushort ushortValue && ( targetType == typeof( int ) || targetType == typeof( int? ) ) )
			return (int)ushortValue;
		if ( rawValue is int intValue && ( targetType == typeof( int ) || targetType == typeof( int? ) ) )
			return intValue;

		if ( rawValue is bool bValue && targetType == typeof( bool ) )
			return bValue;

		if ( rawValue is IEnumerable rawEnumerableValue )
		{
			if ( targetType == typeof( string[] ) )
			{
				var list = new List<string>();

				foreach ( var raw in rawEnumerableValue )
				{
					if ( raw is string str )
						list.Add( str );
					else
						throw new ArgumentException( $"Invalid item value \"{raw}\" for string array." );
				}

				return list.ToArray();
			}
		}

		throw new NotSupportedException();
	}
}
