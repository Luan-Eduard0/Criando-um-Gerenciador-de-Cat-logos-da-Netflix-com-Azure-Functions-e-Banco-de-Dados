using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace FnPostDataStorage
{
    public class FnPostDataStorage
    {
                private readonly ILogger<FnPostDataStorage> _logger;
        
        // Construtor para inicializar o logger da função usando injeção de dependência
        public FnPostDataStorage(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<FnPostDataStorage>();
        }
        // Função que será acionada pelo gatilho HTTP
        [Function("DataStorage")]
        public async Task<IActionResult> Run([HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
        {
            _logger.LogInformation("Upload de arquivo para o Azure Storage");

            try
            {
                // Verifica se o cabeçalho "file-type" está presente
                if(!req.Headers.TryGetValue("file-type", out var fileTypeHeader))
                {
                    return new BadRequestObjectResult("Cabeçalho de tipo de arquivo ausente");
                }

                var fileType = fileTypeHeader.ToString();
                var form = await req.ReadFormAsync();
                var file = form.Files["file"];

                // Verifica se o arquivo está presente e não está vazio
                if (file == null || file.Length == 0 || string.IsNullOrEmpty(file.FileName))
                {
                    return new BadRequestObjectResult("Arquivo invalido ou vazio");
                }

                // Obtém a string de conexão das variáveis de ambiente
                // a ? é para tratar o caso de a variável não existir
                string? connectionString = Environment.GetEnvironmentVariable("AzureWebJobsStorage");
                string containerName = fileType;
                BlobClient blobClient = new BlobClient(connectionString, containerName, file.FileName);
                BlobContainerClient containerClient = new BlobContainerClient(connectionString, containerName);

                // Cria o container se ele não existir
                await containerClient.CreateIfNotExistsAsync();
                await containerClient.SetAccessPolicyAsync(PublicAccessType.Blob);

                string blobName = file.FileName;
                var blob = containerClient.GetBlobClient(blobName);

                // Faz o upload do arquivo para o blob storage
                using (var stream = file.OpenReadStream())
                {
                    await blob.UploadAsync(stream, true);
                }

                _logger.LogInformation($"Arquivo {file.FileName} enviado para o Azure Storage");

                // Retorna a resposta de sucesso com a URL do blob
                return new OkObjectResult(new
                {
                    Message = "Arquivo enviado com sucesso",
                    BlobUrl = blobClient.Uri
                });

            } catch (Exception ex)
            {
                // se der erro mostra no log para debug
                _logger.LogError(ex, "Erro ao enviar arquivo para o Azure Storage");
            }

            return new BadRequestObjectResult("Erro ao enviar arquivo para o Azure Storage");
        }
    }
}