using Confluent.Kafka;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FlowForge.Outbox;

public static class OutboxServiceCollectionExtensions
{
    public static IServiceCollection AddOutboxPublisher<TDbContext>(
        this IServiceCollection services,
        IConfigurationSection configuration)
        where TDbContext : DbContext, IOutboxDbContext
    {
        services.Configure<OutboxPublisherOptions>(configuration);
        services.AddSingleton<IProducer<string, string>>(provider =>
        {
            var options = configuration.Get<OutboxPublisherOptions>() ?? new OutboxPublisherOptions();
            var producerConfig = new ProducerConfig
            {
                BootstrapServers = options.BootstrapServers,
                Acks = Acks.All,
                EnableIdempotence = true,
                MessageSendMaxRetries = 5
            };

            return new ProducerBuilder<string, string>(producerConfig).Build();
        });
        services.AddHostedService<OutboxPublisher<TDbContext>>();

        return services;
    }
}
