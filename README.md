# Zappy CRM — WhatsApp CRM com IA

SaaS multi-tenant para pequenas empresas (clínicas, oficinas, advocacia, imobiliárias
e afins) não perderem clientes por demora no WhatsApp: conecta o número da empresa,
responde automaticamente com IA, agenda retornos e envia lembretes sozinho, gera
propostas comerciais, e dá visibilidade de tudo isso num dashboard.

## Stack

- **Backend**: .NET 8, Clean Architecture (Domain / Application / Infrastructure / Api),
  EF Core + PostgreSQL, MediatR (CQRS), Hangfire (jobs/lembretes), SignalR (tempo real)
- **Frontend**: Angular 18 (standalone components, Signals), Chart.js (dashboard)
- **WhatsApp**: [Evolution API](https://doc.evolution-api.com) (self-hosted, não-oficial —
  Baileys). Trocar para WhatsApp Cloud API no futuro exige só uma nova implementação de
  `IWhatsAppGateway`, sem tocar no domínio.
- **IA**: Claude (Anthropic) — **cada tenant usa a própria chave de API**, configurada
  pelo próprio cliente em Configurações → Agente de IA. O custo da IA nunca sai da sua
  conta, sai da conta de cada empresa cliente.
- **Infra**: Docker Compose (Postgres, Redis, Evolution API, API, Frontend)

## O que já está implementado

- **Multi-tenant** de verdade: cada empresa (tenant) tem seus próprios dados, isolados
  por `TenantId` com query filters automáticos no EF Core
- **Autenticação** (registro com confirmação por código de e-mail — ajuda contra
  bots — e login com JWT), gestão de equipe (convidar atendentes, ativar/desativar)
- **Múltiplos números de WhatsApp por empresa**, com QR code, desconectar, remover,
  e configuração automática de webhook (você não precisa configurar nada manualmente
  na Evolution API — o sistema faz isso sozinho ao criar o número)
- **Inbox em tempo real** (SignalR + polling de segurança), com:
  - Foto de perfil e telefone do contato
  - Modelos de mensagem reutilizáveis, por escopo (cobrança, lembrete, boas-vindas...)
  - Iniciar conversa nova com um número que ainda não escreveu
  - Deletar conversa
- **IA que realmente age, não só responde**:
  - Gera resposta automática baseada no histórico da conversa
  - Pode exigir aprovação humana antes de enviar (configurável) — a sugestão aparece
    no Inbox com botões de enviar como está / editar / descartar
  - **Cria agendamentos de verdade** via "tool use" da Anthropic — quando o cliente
    confirma data e horário na conversa, a IA chama uma ferramenta que cria o
    `Appointment` no banco, com checagem de conflito de horário e lembretes automáticos
  - Respeita horário de atendimento configurado (fora do horário, não responde sozinha)
  - Manda mensagem de fallback configurável quando não consegue responder (sem chave,
    sem crédito, erro, fora do horário) — o cliente nunca fica sem resposta nenhuma
- **Agendamentos + lembretes automáticos**: lembretes em minutos customizáveis antes
  do horário, disparados por jobs do Hangfire mesmo com ninguém no painel
- **Propostas comerciais geradas por IA**: a partir de uma conversa, gera um rascunho
  que o atendente revisa, edita e envia pelo WhatsApp
- **Página de Contatos**: busca, filtro por status, edição (nome/observações/bloqueio)
- **Campanhas de mensagem em lote**: manda mensagem pra vários contatos de uma vez,
  com segmentação (sem agendamento há X dias, sem conversa há X dias, busca) e envio
  gradual em segundo plano (Hangfire) com intervalo configurável entre cada mensagem —
  protege o número contra bloqueio por disparo em massa
- **Cobrança da assinatura via Mercado Pago**: 3 planos fixos (Starter/Pro/Business),
  checkout hospedado pelo Mercado Pago (Pix, cartão, boleto), limites de número de
  WhatsApp e usuários aplicados automaticamente por plano, trial de 14 dias
- **Dashboard** com métricas (contatos, mensagens, conversas, agendamentos, propostas,
  uso de IA) e gráficos (Chart.js)
- **Painel administrativo da plataforma** (`/admin`) — só visível pra quem administra
  o SaaS, não pros clientes: lista todas as empresas cadastradas com métricas agregadas,
  permite suspender/reativar uma empresa
- **Modo escuro**, **totalmente responsivo** (sidebar vira menu hambúrguer e o Inbox
  vira tela cheia no celular), **loading global** que bloqueia ações duplicadas
  durante requisições, sistema de **toast** pra feedback
- Segredos sensíveis (chave da Anthropic de cada tenant) são **criptografados** no
  banco via Data Protection do ASP.NET Core, nunca ficam em texto puro

## O que ainda não existe

- **Cobrança do próprio SaaS**: hoje todo tenant nasce como "Trial" pra sempre — não
  tem Stripe/Mercado Pago integrado ainda pra você cobrar dos seus clientes
- **Testes automatizados**
- **HTTPS/segurança de produção**: segredos de exemplo, sem rate limiting
- Fuso horário do tenant é fixo em `America/Sao_Paulo` — não tem tela pra trocar ainda

## Estrutura

```
whatsapp-crm-ia/
├── backend/
│   ├── WhatsappCrmIA.sln
│   └── src/
│       ├── WhatsappCrmIA.Domain/          # entidades e enums, sem dependências externas
│       ├── WhatsappCrmIA.Application/     # casos de uso (MediatR), interfaces
│       ├── WhatsappCrmIA.Infrastructure/  # EF Core, Evolution API, Claude, Hangfire
│       └── WhatsappCrmIA.Api/             # controllers, Program.cs, SignalR hub
├── frontend/                               # Angular 18 (Dashboard, Inbox, Admin, etc.)
├── infra/postgres-init/                    # script que cria o banco da Evolution API
├── docker-compose.yml
└── .env.example
```

## Como rodar do zero

### 1. Pré-requisitos
- .NET 8 SDK
- Node.js 20+
- Docker Desktop

No Windows, depois de instalar o `dotnet-ef` (`dotnet tool install --global dotnet-ef
--version 8.0.8`) e o Docker, adicione ao PATH (via "Editar variáveis de ambiente do
sistema", não via `setx` no terminal — trunca em 1024 caracteres e pode corromper o PATH):
```
C:\Users\<voce>\.dotnet\tools
C:\Users\<voce>\AppData\Local\Programs\DockerDesktop\resources\bin
```

### 2. Variáveis de ambiente

```bash
cp .env.example .env
```

A chave da Anthropic **não é mais global** — cada empresa cadastra a própria pela
tela de Configurações. O que precisa mesmo estar no `.env` é o **SMTP**, usado pra
mandar o código de confirmação de e-mail no cadastro (ver seção abaixo).

#### Configurando o SMTP (obrigatório pra cadastro funcionar de verdade)

Sem isso configurado, o sistema não trava — ele só **loga** o código no console
da API em vez de mandar e-mail de verdade (útil pra testar localmente sem SMTP).

Exemplo rápido com Gmail:
1. Ative a verificação em duas etapas na sua conta Google (se ainda não tiver)
2. Gere uma "Senha de app" em https://myaccount.google.com/apppasswords
3. No `.env`:
```
SMTP_HOST=smtp.gmail.com
SMTP_PORT=587
SMTP_USERNAME=seu-email@gmail.com
SMTP_PASSWORD=a-senha-de-app-gerada-ali-em-cima
SMTP_FROM_EMAIL=seu-email@gmail.com
```

Qualquer outro provedor SMTP (SendGrid, Mailgun, Amazon SES, etc.) funciona do
mesmo jeito — só trocar host/porta/credenciais.

#### Configurando o Mercado Pago (cobrança da assinatura)

1. Cria uma aplicação em https://www.mercadopago.com.br/developers/panel/app
2. Pega o **Access Token de TESTE** primeiro (tem um seletor Produção/Teste no
   painel) — assim você testa sem mexer com dinheiro de verdade
3. O Mercado Pago precisa mandar notificações de pagamento pra uma URL
   **pública** da sua API (não aceita `localhost`). Pra testar local, expõe
   sua API com [ngrok](https://ngrok.com/download):
   ```powershell
   ngrok http 5000
   ```
   Isso te dá uma URL tipo `https://abc123.ngrok-free.app`.
4. No `.env`:
   ```
   MERCADOPAGO_ACCESS_TOKEN=TEST-xxxxxxxx
   MERCADOPAGO_WEBHOOK_BASE_URL=https://abc123.ngrok-free.app
   ```
5. `docker compose up --build` de novo (toda vez que a URL do ngrok mudar,
   que muda a cada reinício dele no plano grátis, atualiza essa variável e
   sobe de novo)

Cada assinatura já nasce configurada pra notificar essa URL automaticamente —
não precisa clicar em nada no painel do Mercado Pago.

### 3. Gerar e aplicar as migrations

```bash
cd backend
dotnet tool install --global dotnet-ef --version 8.0.8
dotnet restore
dotnet ef migrations add InitialCreate \
  --project src/WhatsappCrmIA.Infrastructure \
  --startup-project src/WhatsappCrmIA.Api
```

No Windows/PowerShell, sempre em uma linha só (sem `\` no final):
```powershell
dotnet ef migrations add InitialCreate --project src/WhatsappCrmIA.Infrastructure --startup-project src/WhatsappCrmIA.Api
```

### 4. Subir a stack

```bash
cd ..
docker compose up --build
```

### 5. Aplicar a migration no banco

```powershell
cd backend
dotnet ef database update --project src/WhatsappCrmIA.Infrastructure --startup-project src/WhatsappCrmIA.Api --connection "Host=localhost;Port=5432;Database=whatsappcrmia;Username=postgres;Password=postgres"
```

### 6. Criar sua conta

Abra **http://localhost:4200/register**, cadastre sua empresa. Você cai direto no
Dashboard, autenticado.

### 7. Conectar um número de WhatsApp

Tela **Números WhatsApp** → "Conectar número" → escaneia o QR code. O webhook é
configurado automaticamente, não precisa fazer nada manual na Evolution API.

### 8. Configurar a IA

Tela **Configurações → Agente de IA** → cadastra sua chave da Anthropic (pega em
https://console.anthropic.com) → ajusta o `system prompt`, horário de atendimento,
e se quer aprovação humana antes de enviar.

## Serviços e URLs

| Serviço | URL |
|---|---|
| Frontend (Angular) | http://localhost:4200 |
| API (Swagger) | http://localhost:5000/swagger |
| Evolution API | http://localhost:8081 |
| Painel de jobs (Hangfire) | http://localhost:5000/jobs |

## Virando admin da plataforma (você, não seus clientes)

O painel `/admin` (lista todas as empresas cadastradas) só é visível pra quem tem a
flag `IsPlatformAdmin = true` no próprio usuário — não tem como ativar isso pela
interface por segurança, é ligado direto no banco.

**No PowerShell** (o `UPDATE` com aspas aninhadas quebra se você tentar com `-c`
direto — use um here-string em vez disso):
```powershell
$sql = @'
UPDATE "Users" SET "IsPlatformAdmin" = true WHERE "Email" = 'seu-email@aqui.com';
'@
$sql | docker compose exec -T postgres psql -U postgres -d whatsappcrmia
```

**No Linux/Mac** (bash), o `-c` direto funciona normalmente:
```bash
docker compose exec postgres psql -U postgres -d whatsappcrmia -c "UPDATE \"Users\" SET \"IsPlatformAdmin\" = true WHERE \"Email\" = 'seu-email@aqui.com';"
```

Depois disso, **deslogue e logue de novo** (o JWT precisa ser gerado de novo pra
carregar a claim nova) — o item "Admin" aparece na sidebar, com destaque em laranja.

## Toda vez que mudar uma entidade (schema novo)

```powershell
cd backend
$env:PATH += ";C:\Users\<voce>\.dotnet\tools"   # se ainda não estiver no PATH
dotnet ef migrations add NomeDaMudanca --project src/WhatsappCrmIA.Infrastructure --startup-project src/WhatsappCrmIA.Api
cd ..
docker compose up --build
cd backend
dotnet ef database update --project src/WhatsappCrmIA.Infrastructure --startup-project src/WhatsappCrmIA.Api --connection "Host=localhost;Port=5432;Database=whatsappcrmia;Username=postgres;Password=postgres"
```

## Arquitetura — decisões importantes

- **Multi-tenancy**: via `TenantId` em cada linha + *global query filter* no EF Core
  (`AppDbContext.OnModelCreating`). O webhook do WhatsApp e os jobs do Hangfire rodam
  **sem usuário autenticado**, então usam `.IgnoreQueryFilters()` explicitamente nas
  consultas onde o `TenantId` já vem validado por outra via (URL do webhook, ou o
  próprio registro do Reminder/Appointment). O painel `/admin` também usa
  `.IgnoreQueryFilters()` de propósito, protegido pela policy `PlatformAdmin`.
- **Trocar Evolution API pela Cloud API oficial**: implemente `WhatsAppCloudApiGateway
  : IWhatsAppGateway` e troque o registro no `Program.cs` — nada no domínio muda.
- **Trocar Claude por outro LLM**: mesma lógica, nova classe implementando
  `IAiAgentService`.
- **Chave da Anthropic por tenant**: criptografada via Data Protection do ASP.NET Core,
  persistida num volume Docker (`dataprotection_keys`) — se esse volume for perdido,
  todas as chaves salvas ficam ilegíveis e cada tenant precisa recadastrar a própria.
- **Créditos de IA**: não existe controle interno de saldo — cada tenant paga direto
  na própria conta da Anthropic. O que o sistema mostra em Configurações → Créditos de
  IA é só um relatório de uso (tokens/custo estimado), não um saldo controlado por nós.

## Roadmap sugerido (próximos passos)

1. **Cobrança do SaaS**: planos, limites por plano, Stripe ou Mercado Pago
2. **Segurança pra produção**: segredos fortes gerados de verdade, HTTPS, rate limiting
   no login/registro/webhook
3. **Testes automatizados**, principalmente nos fluxos críticos (webhook, envio de
   mensagem, auth, tool use da IA)
4. **Fuso horário configurável por tenant** (hoje fixo em América/São Paulo)
5. **Página de boas-vindas guiada** logo após o cadastro (hoje só tem o checklist de
   primeiros passos dentro do Inbox)
