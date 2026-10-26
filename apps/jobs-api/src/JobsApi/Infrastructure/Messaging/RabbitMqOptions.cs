using System.ComponentModel.DataAnnotations;

namespace JobsApi.Infrastructure.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    /// <summary>AMQP URI, e.g. <c>amqp://guest:guest@localhost:5672/</c>.</summary>
    [Required]
    public Uri Uri { get; set; } = new("amqp://guest:guest@localhost:5672/");

    public string ClientName { get; set; } = "jobs-api";
}
