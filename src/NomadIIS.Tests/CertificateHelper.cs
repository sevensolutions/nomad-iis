using Org.BouncyCastle.Asn1.Pkcs;
using Org.BouncyCastle.Asn1.X509;
using Org.BouncyCastle.Asn1;
using Org.BouncyCastle.Crypto.Generators;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Crypto.Prng;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.Utilities;
using Org.BouncyCastle.X509;
using System;
using System.Security.Cryptography.X509Certificates;
using Org.BouncyCastle.Math;
using Org.BouncyCastle.Crypto.Operators;
using System.IO;

namespace NomadIIS.Tests;

public static class CertificateHelper
{
	public static string GenerateSelfSignedCertificate ( string commonName, string targetFilePath, string? password )
	{
		var ca = CreateCertificateAuthorityCertificate( $"CN={commonName} CA" );
		using var certificate = CreateSelfSignedCertificateBasedOnCAPrivateKey( $"CN={commonName}", $"CN={commonName} CA", ca.PrivateKey );

		var pfxBytes = certificate.Export( X509ContentType.Pfx, password );
		File.WriteAllBytes( targetFilePath, pfxBytes );

		return certificate.Thumbprint;
	}

	private static X509Certificate2 CreateSelfSignedCertificateBasedOnCAPrivateKey ( string subjectName, string issuerName, AsymmetricKeyParameter issuerPrivKey )
	{
		const int keyStrength = 2048;

		// Generating Random Numbers
		using var randomGenerator = new CryptoApiRandomGenerator();
		var random = new SecureRandom( randomGenerator );

		var signatureFactory = new Asn1SignatureFactory( "SHA512WITHRSA", issuerPrivKey, random );

		// The Certificate Generator
		var certificateGenerator = new X509V3CertificateGenerator();

		certificateGenerator.AddExtension( X509Extensions.ExtendedKeyUsage, true,
			new ExtendedKeyUsage( [new DerObjectIdentifier( "1.3.6.1.5.5.7.3.1" )] ) );

		// Serial Number
		var serialNumber = BigIntegers.CreateRandomInRange( BigInteger.One, BigInteger.ValueOf( long.MaxValue ), random );
		certificateGenerator.SetSerialNumber( serialNumber );

		// Signature Algorithm
		//const string signatureAlgorithm = "SHA512WITHRSA";
		//certificateGenerator.SetSignatureAlgorithm(signatureAlgorithm);

		// Issuer and Subject Name
		certificateGenerator.SetIssuerDN( new X509Name( issuerName ) );
		certificateGenerator.SetSubjectDN( new X509Name( subjectName ) );

		// Valid For
		var notBefore = DateTime.UtcNow.Date;
		var notAfter = notBefore.AddDays( 2 );

		certificateGenerator.SetNotBefore( notBefore );
		certificateGenerator.SetNotAfter( notAfter );

		// Subject Public Key
		var keyGenerationParameters = new KeyGenerationParameters( random, keyStrength );
		var keyPairGenerator = new RsaKeyPairGenerator();

		keyPairGenerator.Init( keyGenerationParameters );

		var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

		certificateGenerator.SetPublicKey( subjectKeyPair.Public );

		// Generating the Certificate
		var certificate = certificateGenerator.Generate( signatureFactory );

		// Correcponding private key
		var pkInfo = PrivateKeyInfoFactory.CreatePrivateKeyInfo( subjectKeyPair.Private );

		// Merge into X509Certificate2
		using var x509 = new X509Certificate2( certificate.GetEncoded() );

		Asn1Sequence seq = (Asn1Sequence)Asn1Object.FromByteArray( pkInfo.ParsePrivateKey().GetDerEncoded() );
		if ( seq.Count != 9 )
			throw new PemException( "Malformed sequence in RSA private key." );

		var rsa = RsaPrivateKeyStructure.GetInstance( seq );
		var rsaparams = new RsaPrivateCrtKeyParameters(
			rsa.Modulus, rsa.PublicExponent, rsa.PrivateExponent, rsa.Prime1, rsa.Prime2, rsa.Exponent1, rsa.Exponent2, rsa.Coefficient );

		var finalCertificate = x509.CopyWithPrivateKey( DotNetUtilities.ToRSA( rsaparams ) );
		return finalCertificate;

	}
	private static (X509Certificate2 Certificate, AsymmetricKeyParameter PrivateKey) CreateCertificateAuthorityCertificate ( string subjectName )
	{
		const int keyStrength = 2048;

		// Generating Random Numbers
		using var randomGenerator = new CryptoApiRandomGenerator();
		var random = new SecureRandom( randomGenerator );

		// The Certificate Generator
		var certificateGenerator = new X509V3CertificateGenerator();

		// Serial Number
		var serialNumber = BigIntegers.CreateRandomInRange( BigInteger.One, BigInteger.ValueOf( long.MaxValue ), random );
		certificateGenerator.SetSerialNumber( serialNumber );

		// Signature Algorithm
		//const string signatureAlgorithm = "SHA256WithRSA";
		//certificateGenerator.SetSignatureAlgorithm(signatureAlgorithm);

		// Issuer and Subject Name
		var subjectDN = new X509Name( subjectName );
		var issuerDN = subjectDN;
		certificateGenerator.SetIssuerDN( issuerDN );
		certificateGenerator.SetSubjectDN( subjectDN );

		// Valid For
		var notBefore = DateTime.UtcNow.Date;
		var notAfter = notBefore.AddDays( 2 );

		certificateGenerator.SetNotBefore( notBefore );
		certificateGenerator.SetNotAfter( notAfter );

		// Subject Public Key
		var keyGenerationParameters = new KeyGenerationParameters( random, keyStrength );
		var keyPairGenerator = new RsaKeyPairGenerator();

		keyPairGenerator.Init( keyGenerationParameters );
		var subjectKeyPair = keyPairGenerator.GenerateKeyPair();

		certificateGenerator.SetPublicKey( subjectKeyPair.Public );

		// Generating the Certificate
		var signatureFactory = new Asn1SignatureFactory( "SHA512WITHRSA", subjectKeyPair.Private, random );

		// Selfsign certificate
		var certificate = certificateGenerator.Generate( signatureFactory );
		var x509 = new X509Certificate2( certificate.GetEncoded() );

		return (x509, subjectKeyPair.Private);
	}
}
