namespace WhatsappCrmIA.Application.Interfaces;

/// <summary>
/// Segredos que são da PLATAFORMA (você), não de cada empresa cliente — hoje
/// só a chave da Anthropic, que voltou a ser global: o custo da IA agora sai
/// da sua conta, embutido na mensalidade de cada plano.
/// </summary>
public interface IGlobalSecretsProvider
{
    string GetAnthropicApiKey();
}
