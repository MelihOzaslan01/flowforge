using FlowForge.LogIndexer;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<KafkaOptions>(builder.Configuration.GetSection("Kafka"));
builder.Services.Configure<ElasticsearchOptions>(builder.Configuration.GetSection("Elasticsearch"));
builder.Services.AddSingleton<ElasticsearchLogClient>();
builder.Services.AddHostedService<LogIndexerWorker>();

var host = builder.Build();
host.Run();
