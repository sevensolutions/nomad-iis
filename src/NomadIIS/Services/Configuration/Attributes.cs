using System;
using System.Collections;

namespace NomadIIS.Services.Configuration;

[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
public sealed class ConfigurationField : Attribute
{
	public ConfigurationField ( string name )
	{
		Name = name;
	}

	public string Name { get; }
}

[AttributeUsage( AttributeTargets.Property, AllowMultiple = false )]
public sealed class ConfigurationCollectionField : Attribute
{
	public ConfigurationCollectionField ( string collectionName, string blockName, int minCount = 0, int maxCount = int.MaxValue )
	{
		CollectionName = collectionName;
		BlockName = blockName;
		MinCount = minCount;
		MaxCount = maxCount == int.MaxValue ? null : maxCount;
	}

	public string CollectionName { get; }
	public string BlockName { get; }
	public int MinCount { get; }
	public int? MaxCount { get; }
}
