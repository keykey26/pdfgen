using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using PuppeteerSharp;
using RazorEngine;
using RazorEngine.Templating;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PDFGeneration
{
	public static class PDFGen
	{
		[FunctionName("PDFGen")]
		public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
		{
			log.LogInformation("C# HTTP trigger function processed a request.");

			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			List<CallObject> data = JsonConvert.DeserializeObject<List<CallObject>>(requestBody);

			try
			{
				string viewResult = "";

				foreach (var page in data)
				{
					string template = File.ReadAllText(page.TemplateName);

					viewResult += Engine.Razor.RunCompile(template, "testTemp", modelType: null, model: (object)page.Data);
				}

				await new BrowserFetcher().DownloadAsync(BrowserFetcher.DefaultRevision);

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
				var errorMessage = new ContentResult();
				errorMessage.Content = ex.Message;
				errorMessage.StatusCode = 500;
				return errorMessage;
			}
		}
	}
}
