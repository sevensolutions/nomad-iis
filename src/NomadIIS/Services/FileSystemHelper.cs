using System.IO;
using System;
using System.Threading;

namespace NomadIIS.Services
{
	public static class FileSystemHelper
	{
		public static void CopyDirectory ( string sourcePath, string targetPath )
			=> CopyDirectory( new DirectoryInfo( sourcePath ), new DirectoryInfo( targetPath ) );
		public static void CopyDirectory ( DirectoryInfo source, DirectoryInfo target )
		{
			Directory.CreateDirectory( target.FullName );

			// Copy each file into the new directory
			foreach ( var fi in source.GetFiles() )
				fi.CopyTo( Path.Combine( target.FullName, fi.Name ), true );

			// Copy each subdirectory using recursion
			foreach ( var diSourceSubDir in source.GetDirectories() )
			{
				var nextTargetSubDir = target.CreateSubdirectory( diSourceSubDir.Name );

				CopyDirectory( diSourceSubDir, nextTargetSubDir );
			}
		}

		public static void CleanFolder ( string directoryPath )
			=> CleanFolder( new DirectoryInfo( directoryPath ) );
		public static void CleanFolder ( DirectoryInfo directory )
		{
			try
			{
				Try();
			}
			catch ( IOException )
			{
				// Wait a bit and try again
				Thread.Sleep( 100 );
				Try();
			}

			void Try()
			{
				foreach ( FileInfo file in directory.EnumerateFiles() )
					file.Delete();
				foreach ( DirectoryInfo dir in directory.EnumerateDirectories() )
					dir.Delete( true );
			}
		}
	}
}
