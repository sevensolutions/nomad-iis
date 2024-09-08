#if MANAGEMENT_API
using Microsoft.Playwright;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace NomadIIS.Services
{
	public static class PlaywrightHelper
	{
		private static SemaphoreSlim _semaphore = new SemaphoreSlim( 1, 1 );

		public static async Task<byte[]?> TakeScreenshotAsync ( string url, CancellationToken cancellationToken = default )
		{
			cancellationToken.ThrowIfCancellationRequested();

			using var playwright = await Playwright.CreateAsync();

			IBrowser? browser = null;

			cancellationToken.ThrowIfCancellationRequested();

			try
			{
				browser = await playwright.Chromium.LaunchAsync( new BrowserTypeLaunchOptions()
				{
					Headless = true
				} );
			}
			catch ( PlaywrightException )
			{
				// Install Playwright
				await EnsurePlaywrightInstalled( cancellationToken );

				browser = await playwright.Chromium.LaunchAsync( new BrowserTypeLaunchOptions()
				{
					Headless = true,
				} );
			}

			try
			{
				cancellationToken.ThrowIfCancellationRequested();

				var page = await browser.NewPageAsync( new BrowserNewPageOptions()
				{
					ViewportSize = new ViewportSize()
					{
						Width = 1920,
						Height = 1080
					},
					ScreenSize = new ScreenSize()
					{
						Width = 1920,
						Height = 1080
					}
				} );

				cancellationToken.ThrowIfCancellationRequested();

				await page.GotoAsync( url );

				cancellationToken.ThrowIfCancellationRequested();

				var screenshot = await page.ScreenshotAsync( new PageScreenshotOptions()
				{
					FullPage = true,
					Type = ScreenshotType.Png
				} );

				return screenshot;
			}
			finally
			{
				if ( browser is not null )
					await browser.DisposeAsync();
			}
		}

		private static async Task EnsurePlaywrightInstalled ( CancellationToken cancellationToken )
		{
			await _semaphore.WaitAsync( 30_000, cancellationToken );

			try
			{
				var exitCode = Microsoft.Playwright.Program.Main( ["install", "--with-deps", "chromium"] );
				if ( exitCode != 0 )
					throw new Exception( "Failed to install chromium." );
			}
			finally
			{
				_semaphore.Release();
			}
		}
	}
}
#endif
