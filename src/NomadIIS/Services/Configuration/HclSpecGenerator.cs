using Hashicorp.Nomad.Plugins.Shared.Hclspec;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;

namespace NomadIIS.Services.Configuration;

// https://pkg.go.dev/github.com/hashicorp/nomad/plugins/shared/hclspec
// https://github.com/hashicorp/nomad/blob/v1.6.3/plugins/shared/hclspec/hcl_spec.pb.go

internal sealed class HclSpecGenerator
{
	public static Spec Generate<T> () => Generate( typeof( T ) );
	public static Spec Generate ( Type type )
	{
		var spec = new Spec();

		var obj = new Hashicorp.Nomad.Plugins.Shared.Hclspec.Object();

		var properties = type.GetProperties( BindingFlags.Public | BindingFlags.Instance );

		foreach ( var property in properties )
		{
			var defaultValueAttribute = property.GetCustomAttribute<DefaultValueAttribute>();

			var fieldAttribute = property.GetCustomAttribute<ConfigurationField>();
			var collectionFieldAttribute = property.GetCustomAttribute<ConfigurationCollectionField>();

			if ( fieldAttribute is not null )
			{
				var isRequired = property.GetCustomAttribute<RequiredAttribute>() is not null;

				var propertySpec = new Spec();

				var attr = new Attr()
				{
					Name = fieldAttribute.Name,
					Type = GetSchemaType( property.PropertyType ),
					Required = isRequired
				};

				if ( defaultValueAttribute is not null )
				{
					propertySpec.Default = new Default()
					{
						Primary = new Spec() { Attr = attr },
						Default_ = new Spec()
						{
							Literal = new Literal()
							{
								Value = ConvertDefaultValue( defaultValueAttribute.Value )
							}
						}
					};
				}
				else
					propertySpec.Attr = attr;

				obj.Attributes.Add( fieldAttribute.Name, propertySpec );
			}
			else if ( collectionFieldAttribute is not null )
			{
				var propertySpec = new Spec()
				{
					BlockList = new BlockList()
					{
						Name = collectionFieldAttribute.BlockName,
						MinItems = (ulong)collectionFieldAttribute.MinCount,
						MaxItems = (ulong)( collectionFieldAttribute.MaxCount ?? 0 ),
						Nested = Generate( property.PropertyType.GetElementType()! )
					}
				};

				obj.Attributes.Add( collectionFieldAttribute.CollectionName, propertySpec );
			}
		}

		spec.Object = obj;

		return spec;
	}

	private static string GetSchemaType ( Type type )
	{
		if ( type == typeof( string ) )
			return "string";
		if ( type == typeof( bool ) || type == typeof( bool? ) )
			return "bool";
		if ( type == typeof( TimeSpan ) || type == typeof( TimeSpan? ) )
			return "string";
		if ( type.IsEnum || Nullable.GetUnderlyingType( type ) is not null && Nullable.GetUnderlyingType( type )!.IsEnum )
			return "string";
		if ( type.IsArray )
			return $"list({GetSchemaType( type.GetElementType()! )})";

		throw new NotSupportedException();
	}
	private static string ConvertDefaultValue ( object? value )
	{
		if ( value is string strValue )
			return $"\"{strValue}\"";
		if ( value is bool bValue )
			return bValue ? "true" : "false";

		return "";
	}
}
