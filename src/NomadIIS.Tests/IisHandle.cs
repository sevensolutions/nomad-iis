using Microsoft.Web.Administration;
using System;
using System.Linq;

namespace NomadIIS.Tests;

public sealed class IisHandle : IDisposable
{
	private readonly ServerManager _serverManager = new();

	public IisHandle ()
	{
	}

	internal ServerManager ServerManager => _serverManager;

	public IisAppPoolHandle AppPool ( string name )
		=> new IisAppPoolHandle( this, name );
	public IisWebsiteHandle Website ( string name )
		=> new IisWebsiteHandle( this, name );

	public void Dispose ()
	{
		_serverManager.Dispose();
	}
}

public sealed class IisAppPoolHandle
{
	private readonly IisHandle _owner;
	private readonly string _name;

	public IisAppPoolHandle ( IisHandle owner, string name )
	{
		_owner = owner;
		_name = name;
	}

	public void ShouldExist () => GetApplicationPool();
	public void ShouldNotExist ()
	{
		if ( FindApplicationPool() is not null )
			Assert.Fail( $"Application Pool with name \"{_name}\" exists but shouldn't." );
	}
	public void ShouldHaveEnvironmentVariable ( string name, string? expectedValue = null )
	{
		var appPool = GetApplicationPool();

		var envVarsCollection = appPool.GetCollection( "environmentVariables" );

		var element = envVarsCollection.FirstOrDefault( x => x.GetAttribute( "name" ).Value.ToString() == name );

		if ( element is null )
			Assert.Fail( $"Application Pool \"{_name}\" doesn't have an environment variable with name \"{name}\"." );

		if ( expectedValue is not null )
			Assert.Equal( expectedValue, element.GetAttributeValue( "value" )?.ToString() );
	}
	public void ShouldHaveManagedPipelineMode ( ManagedPipelineMode mode )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( mode, appPool.ManagedPipelineMode );
	}
	public void ShouldHaveManagedRuntimeVersion ( string version )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( version, appPool.ManagedRuntimeVersion );
	}
	public void ShouldHaveStartMode ( StartMode mode )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( mode, appPool.StartMode );
	}
	public void ShouldHaveIdleTimeout ( TimeSpan value )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( value, appPool.ProcessModel.IdleTimeout );
	}
	public void ShouldHaveDisableOverlappedRecycle ( bool value )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( value, appPool.Recycling.DisallowOverlappingRotation );
	}
	public void ShouldHavePeriodicRestart ( TimeSpan value )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( value, appPool.Recycling.PeriodicRestart.Time );
	}
	public void ShouldHaveIdentityType ( ProcessModelIdentityType identityType )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( identityType, appPool.ProcessModel.IdentityType );
	}
	public void ShouldHaveUsername ( string username )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( username, appPool.ProcessModel.UserName );
	}
	public void ShouldHavePassword ( string password )
	{
		var appPool = GetApplicationPool();
		Assert.Equal( password, appPool.ProcessModel.Password );
	}
	public void ShouldHaveEmptyPassword ()
	{
		var appPool = GetApplicationPool();
		Assert.True( string.IsNullOrEmpty( appPool.ProcessModel.Password ) );
	}

	private ApplicationPool GetApplicationPool ()
	{
		var appPool = FindApplicationPool();

		if ( appPool is null )
			Assert.Fail( $"Application Pool with name \"{_name}\" doesn't exist." );

		return appPool;
	}

	private ApplicationPool? FindApplicationPool ()
		=> _owner.ServerManager.ApplicationPools.FirstOrDefault( x => x.Name == _name );
}

public sealed class IisWebsiteHandle
{
	private readonly IisHandle _owner;
	private readonly string _name;

	public IisWebsiteHandle ( IisHandle owner, string name )
	{
		_owner = owner;
		_name = name;
	}

	public void ShouldExist () => GetWebsite();
	public void ShouldNotExist ()
	{
		if ( FindWebsite() is not null )
			Assert.Fail( $"Website with name \"{_name}\" exists but shouldn't." );
	}

	public IisWebsiteBindingHandle Binding ( int index )
		=> new IisWebsiteBindingHandle( GetWebsite().Bindings[index] );

	public IisApplicationHandle Application ( string path )
		=> new IisApplicationHandle( GetWebsite(), path );

	private Site GetWebsite ()
	{
		var website = FindWebsite();

		if ( website is null )
			Assert.Fail( $"Website with name \"{_name}\" doesn't exist." );

		return website;
	}

	private Site? FindWebsite ()
		=> _owner.ServerManager.Sites.FirstOrDefault( x => x.Name == _name );
}

public sealed class IisWebsiteBindingHandle
{
	private readonly Binding _binding;

	public IisWebsiteBindingHandle ( Binding binding )
	{
		_binding = binding;
	}

	public void IsHttps ()
	{
		if ( _binding.Protocol is null || _binding.Protocol != "https" )
			Assert.Fail( "Binding is not https but should be." );
	}

	public void CertificateThumbprintIs ( string certificateThumbprint )
	{
		if ( _binding.CertificateHash is null || _binding.CertificateHash.Length == 0 )
			Assert.Fail( "The binding has no certificate set." );

		var tp = Convert.ToHexString( _binding.CertificateHash );

		if ( !string.Equals( tp, certificateThumbprint, StringComparison.InvariantCultureIgnoreCase ) )
			Assert.Fail( $"Certificate hash should be {certificateThumbprint}, but is {tp}." );
	}
}

public sealed class IisApplicationHandle
{
	private readonly Site _site;
	private readonly string _path;

	public IisApplicationHandle ( Site site, string path )
	{
		_site = site;
		_path = path;
	}

	public void ShouldExist () => GetApplication();
	public void ShouldNotExist ()
	{
		if ( FindApplication() is not null )
			Assert.Fail( $"Application with path \"{_path}\" exists but shouldn't." );
	}
	public void ShouldRunOnApplicationPool ( string poolName )
	{
		var application = GetApplication();

		Assert.Equal( poolName, application.ApplicationPoolName );
	}

	private Application GetApplication ()
	{
		var application = FindApplication();

		if ( application is null )
			Assert.Fail( $"Application with path \"{_path}\" doesn't exist." );

		return application;
	}

	private Application? FindApplication ()
		=> _site.Applications.FirstOrDefault( x => x.Path == _path );
}
