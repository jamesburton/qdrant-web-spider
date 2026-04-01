#:project ./shared/Shared.csproj
#:package ModelContextProtocol@1.2.0
#:package Microsoft.Extensions.Hosting@10.*
#:property PublishAot=false

using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Server;
using QdrantWebSpider;

var config = await SpiderConfig.LoadAsync(null, args);
var qdrantHelper = new QdrantHelper(config.Qdrant);
var embeddingProvider = await EmbeddingProviderFactory.CreateAsync(config.Embedding, autoDownload: true);

var builder = Host.CreateApplicationBuilder(args);

builder.Logging.AddConsole(options => options.LogToStandardErrorThreshold = LogLevel.Trace);

builder.Services.AddSingleton(config);
builder.Services.AddSingleton(qdrantHelper);
builder.Services.AddSingleton(embeddingProvider);

builder.Services
    .AddMcpServer()
    .WithStdioServerTransport()
    .WithTools<SpiderTools>();

await builder.Build().RunAsync();
