using System.IO;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System.Threading.Tasks;
using PuppeteerSharp;
using RazorLight;

namespace PDFGeneration
{
	public static class PDFGen
	{
		[FunctionName("PDFGen")]
		public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
		{
			log.LogInformation("C# HTTP trigger function processed a request.");

			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			dynamic data = JsonConvert.DeserializeObject(requestBody);

			try
			{
				var engine = new RazorLightEngineBuilder()
					.UseMemoryCachingProvider()
					.Build();

				string template = "<div>Hello, @Model.Name. Welcome to RazorLight repository</div>";

				string viewResult = await engine.CompileRenderAsync("templateKey", template, data);

				var browser = await Puppeteer.LaunchAsync(new LaunchOptions
				{
					Headless = true
				});

				using (var page = await browser.NewPageAsync())
				{
					await page.SetContentAsync(viewResult);
					var result = await page.GetContentAsync();
					return new FileStreamResult(await page.PdfStreamAsync(), "application/pdf");
				}
			}
			catch (System.Exception ex)
			{
				log.LogError(ex.Message);
			}
			return null;
		}
	}
}
