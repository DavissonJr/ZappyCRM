# Zappy CRM — WhatsApp CRM com IA

Sistema de gerenciamento de clientes via WhatsApp, operado por você e ofertado pra
comércios locais (clínicas, oficinas, advocacia, imobiliárias e afins). Não é um SaaS
de auto-cadastro — **você cria e gerencia cada empresa cliente pelo seu painel
administrativo**; depois de criada, o dono da empresa consegue editar as próprias
configurações, trocar senha, etc. O sistema conecta o número da empresa, responde
automaticamente com IA, agenda retornos e envia lembretes sozinho, gera propostas
comerciais, e dá visibilidade de tudo isso num dashboard.

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
- **Você cria cada empresa direto pelo painel Admin** (nome, segmento, plano, dono,
  senha temporária) — não existe auto-cadastro público. O dono da empresa loga com
  essas credenciais e edita o próprio perfil/senha depois em Configurações
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
- **Página de Contatos**: busca, filtro por status, **filtro de inatividade** (sem
  conversa/sem agendamento há X dias — pra achar quem não retorna), edição
  (nome/observações/bloqueio)
- **Campanhas de mensagem em lote** — código pronto, mas **bloqueado no menu de
  propósito**: com a Evolution API (WhatsApp não-oficial), o risco de bloqueio do
  número em disparo em massa é alto demais. Fica reservado pra quando integrarmos a
  API oficial da Meta (Graph API)
- **Dashboard** com métricas (contatos, mensagens, conversas, agendamentos, propostas,
  uso de IA) e gráficos (Chart.js)
- **Painel administrativo** (`/admin`) — só visível pra quem administra o sistema (você):
  cria empresas novas, troca o plano/limite de cada uma manualmente (sem cobrança
  automática — você decide e ajusta conforme o combinado com o cliente), lista todas
  com métricas agregadas, permite suspender/reativar uma empresa
- **Modo escuro**, **totalmente responsivo** (sidebar vira menu hambúrguer e o Inbox
  vira tela cheia no celular), **loading global** que bloqueia ações duplicadas
  durante requisições, sistema de **toast** pra feedback
- Segredos sensíveis (chave da Anthropic de cada tenant) são **criptografados** no
  banco via Data Protection do ASP.NET Core, nunca ficam em texto puro

## O que ainda não existe

- **Testes automatizados**
- **HTTPS/segurança de produção**: segredos de exemplo, sem rate limiting
- **Rate limiting no login** (proteção anti-força-bruta na tela de login)
- **Integração oficial com a Meta (WhatsApp Cloud API)**: enquanto isso não existir,
  Campanhas fica bloqueada de propósito (ver acima)
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

A chave da Anthropic **não é mais global** — cada empresa tem a própria chave,
cadastrada pela tela de Configurações depois que você cria a conta dela. O que
precisa mesmo estar no `.env` é o **SMTP**, usado pra e-mails transacionais.

#### Configurando o SMTP

Sem isso configurado, o sistema não trava — funciona normalmente, só não manda
e-mails de verdade.

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

### 6. Bootstrap: criar sua própria conta e virar admin

Não existe mais tela pública de cadastro — mas o endpoint da API que cria conta
continua existindo por baixo (só a telinha pública que tiramos). Usa ele **uma
única vez**, pra criar a sua própria conta:

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/register" -Method Post -ContentType "application/json" -Body (@{
  companyName = "Minha Empresa (admin)"
  segment = "outro"
  fullName = "Seu Nome"
  email = "seu-email@aqui.com"
  password = "uma-senha-forte-aqui"
} | ConvertTo-Json)
```

Isso manda um código de 6 dígitos pro seu e-mail (ou aparece no log da API, se o
SMTP não estiver configurado — `docker compose logs api --since 2m`). Confirma com:

```powershell
Invoke-RestMethod -Uri "http://localhost:5000/api/auth/verify-registration" -Method Post -ContentType "application/json" -Body (@{
  email = "seu-email@aqui.com"
  code = "123456"
} | ConvertTo-Json)
```

Agora vira admin da plataforma (troca o e-mail pelo que você usou acima):

```powershell
$sql = @'
UPDATE "Users" SET "IsPlatformAdmin" = true WHERE "Email" = 'seu-email@aqui.com';
'@
$sql | docker compose exec -T postgres psql -U postgres -d whatsappcrmia
```

Pronto — vai em **http://localhost:4200/login**, entra com esse e-mail/senha, e o
item **"Admin"** (laranja) aparece na sidebar.

### 7. Criar sua primeira empresa cliente

No painel `/admin` → **"+ Criar empresa"** — preenche nome, segmento, plano, e os
dados do dono. Isso cria o Tenant e o usuário dono direto, e mostra a senha
temporária **uma vez só** (anota ela) — repassa e-mail/senha pro cliente.

### 8. Conectar um número de WhatsApp

Desloga e loga como o dono da empresa que você acabou de criar (ou abre uma aba
anônima). Tela **Números WhatsApp** → "Conectar número" → escaneia o QR code. O
webhook é configurado automaticamente, não precisa fazer nada manual na Evolution API.

### 9. Configurar a IA

Tela **Configurações → Agente de IA** → cadastra a chave da Anthropic dessa
empresa (pega em https://console.anthropic.com) → ajusta o `system prompt`,
horário de atendimento, e se quer aprovação humana antes de enviar.

## Serviços e URLs

| Serviço | URL |
|---|---|
| Frontend (Angular) | http://localhost:4200 |
| API (Swagger) | http://localhost:5000/swagger |
| Evolution API | http://localhost:8081 |
| Painel de jobs (Hangfire) | http://localhost:5000/jobs |

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

1. **Integração oficial com a Meta (WhatsApp Cloud API)** — pré-requisito pra
   desbloquear Campanhas com segurança
2. **Segurança pra produção**: segredos fortes gerados de verdade, HTTPS, rate limiting
   no login
3. **Testes automatizados**, principalmente nos fluxos críticos (webhook, envio de
   mensagem, auth, tool use da IA)
4. **Fuso horário configurável por tenant** (hoje fixo em América/São Paulo)
5. **Página de boas-vindas guiada** pro dono da empresa, logo no primeiro login (hoje
   só tem o checklist de primeiros passos dentro do Inbox)
