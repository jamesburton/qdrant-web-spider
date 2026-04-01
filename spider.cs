#:project ./shared/Shared.csproj
#:property PublishAot=false

using QdrantWebSpider;

var config = await SpiderConfig.LoadAsync(null, args);
using var qdrant = new QdrantHelper(config.Qdrant);
using var embedder = await EmbeddingProviderFactory.CreateAsync(config.Embedding, args.Contains("--auto-download"));
using var http = new HttpClient();
http.DefaultRequestHeaders.UserAgent.ParseAdd(config.Crawl.UserAgent);

var service = new CrawlService(qdrant, embedder, http, config);
await service.CrawlAsync(Console.WriteLine);
