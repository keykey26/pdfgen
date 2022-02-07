using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Newtonsoft.Json;
using PuppeteerSharp;
using RazorEngine;
using RazorEngine.Templating;
using System.IO;
using System.Threading.Tasks;

namespace PDFGeneration
{
    public static class PDFGen
	{
		[FunctionName("PDFGen")]
		public static async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post", Route = null)] HttpRequest req, ILogger log, Microsoft.Azure.WebJobs.ExecutionContext context)
		{
			log.LogInformation("Run PDF genration function");

			var config = new ConfigurationBuilder()
			.SetBasePath(context.FunctionAppDirectory)
			.AddJsonFile("local.settings.json", optional: true, reloadOnChange: true)
			.AddEnvironmentVariables()
			.Build();

			string requestBody = await new StreamReader(req.Body).ReadToEndAsync();
			CallObject data = JsonConvert.DeserializeObject<CallObject>(requestBody);

			try
			{
				var storageCredentials = new StorageCredentials(config["StorageAccountName"], config["StorageAccountKey"]);
				var cloudStorageAccount = new CloudStorageAccount(storageCredentials, true);
				var cloudBlobClient = cloudStorageAccount.CreateCloudBlobClient();

				var container = cloudBlobClient.GetContainerReference(config["StorageAccountContainer"]);
				await container.CreateIfNotExistsAsync();

				string viewResult = "";

				foreach (var page in data.Pages)
				{
					log.LogInformation("Render " + page.TemplateName);
					if (Engine.Razor.IsTemplateCached(page.TemplateName, modelType: null))
					{
						viewResult += Engine.Razor.Run(page.TemplateName, modelType: null, model: page.Data);
					}
					else
					{
						var newBlob = container.GetBlockBlobReference(page.TemplateName);
						string template = await newBlob.DownloadTextAsync();
						log.LogInformation("Compile " + page.TemplateName);
						viewResult += Engine.Razor.RunCompile(template, page.TemplateName, modelType: null, model: page.Data);
					}
				}

				log.LogInformation("get remote chrome at: " + config["RemoteHeadlessChrome"]);
				var browser = await Puppeteer.ConnectAsync(new ConnectOptions { BrowserWSEndpoint = config["RemoteHeadlessChrome"] });

				using (var page = await browser.NewPageAsync())
				{
					await page.SetContentAsync(viewResult);
					var result = await page.GetContentAsync();
					log.LogInformation("Render PDF complete");
					return new FileStreamResult(await page.PdfStreamAsync(), "application/pdf") { FileDownloadName = data.FileName };
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
