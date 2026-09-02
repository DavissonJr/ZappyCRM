using Microsoft.Extensions.Configuration;
using WhatsappCrmIA.Application.Interfaces;

namespace WhatsappCrmIA.Infrastructure.Services;

public class GlobalSecretsProvider : IGlobalSecretsProvider
{
    private readonly IConfiguration _config;
    public GlobalSecretsProvider(IConfiguration config) => _config = config;

    public string GetAnthropicApiKey() => _config["Anthropic:ApiKey"] ?? string.Empty;
}
