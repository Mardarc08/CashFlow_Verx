# 💰 CashFlow — Controle de Fluxo de Caixa

Sistema de controle de fluxo de caixa diário com microsserviços independentes para registro de lançamentos e consolidação diária de saldos.

---

## 📋 Sumário

- [Visão Geral](#visão-geral)
- [Arquitetura](#arquitetura)
- [Decisões Técnicas](#decisões-técnicas)
- [Domínios e Capacidades](#domínios-e-capacidades)
- [Requisitos Funcionais e Não-Funcionais](#requisitos)
- [Pré-requisitos](#pré-requisitos)
- [Como Rodar Localmente](#como-rodar-localmente)
- [Endpoints da API](#endpoints-da-api)
- [Testes](#testes)
- [Observabilidade](#observabilidade)
- [Infraestrutura GCP](#infraestrutura-gcp)
- [Estimativa de Custos](#estimativa-de-custos)
- [Evoluções Futuras](#evoluções-futuras)

---

## Visão Geral

Um comerciante precisa controlar seu fluxo de caixa diário com lançamentos de débitos e créditos, além de um relatório que disponibilize o saldo diário consolidado.

O sistema é composto por **dois microsserviços independentes**:

| Serviço | Responsabilidade | Porta |
|---|---|---|
| `CashFlow.Lancamentos.Api` | Registrar e consultar lançamentos (débitos/créditos) | 8080 |
| `CashFlow.Consolidado.Api` | Calcular e disponibilizar o saldo consolidado por dia | 8081 |

**Princípio de isolamento:** o serviço de lançamentos **nunca** fica indisponível por causa do consolidado. A comunicação entre eles é 100% assíncrona via mensageria.

---

## Arquitetura

```
┌─────────────┐     HTTPS/JWT      ┌──────────────────┐
│   Clientes  │ ─────────────────► │   API Gateway    │
└─────────────┘                    │  + Cloud Armor   │
                                   └────────┬─────────┘
                                            │
                      ┌─────────────────────┴──────────────────────┐
                      │                                             │
               ┌──────▼──────┐                            ┌────────▼───────┐
               │ Lançamentos │                            │  Consolidado   │
               │   API       │                            │     API        │
               │ (.NET 8)    │                            │  (.NET 8)      │
               └──────┬──────┘                            └────────┬───────┘
                      │                                            │
                      │  Publica evento                            │ Consome evento
                      │  (assíncrono)                              │ (BackgroundService)
                      │                                            │
                      └──────────► Cloud Pub/Sub ◄────────────────┘
                                       │
                      ┌────────────────┼────────────────┐
                      │                │                 │
               ┌──────▼──────┐  ┌─────▼──────┐  ┌──────▼──────┐
               │  Cloud SQL  │  │ Cloud SQL  │  │  Redis      │
               │ (Lanç. DB)  │  │ (Cons. DB) │  │  (Cache)    │
               └─────────────┘  └────────────┘  └─────────────┘
```

### Fluxo de dados

1. Cliente envia `POST /api/lancamentos` com JWT
2. API valida, persiste no banco e publica evento `LancamentoRegistrado` no Pub/Sub
3. Retorna `202 Accepted` imediatamente — **independente do estado do consolidado**
4. Serviço de Consolidado consome o evento como `BackgroundService`
5. Atualiza saldo diário e invalida o cache Redis
6. `GET /api/consolidado/{data}` → Redis hit em < 5ms na maioria dos casos

---

## Decisões Técnicas

### Por que Microsserviços?
O requisito não-funcional crítico — *"o serviço de lançamentos não deve ficar indisponível se o consolidado cair"* — impõe isolamento de falha. Microsserviços com bancos de dados separados garantem que uma falha em um serviço não propaga para o outro.

### Por que Event-Driven com Pub/Sub?
Comunicação síncrona (REST direto entre serviços) criaria acoplamento temporal: se o consolidado cair, o lançamento travaria. O Pub/Sub garante que mensagens ficam retidas e são processadas quando o consumidor voltar, com retry automático e Dead Letter Topic para falhas persistentes.

### Por que Cloud Run?
- Auto-scaling transparente de 0 a N instâncias
- Suporta picos de 50 req/s no consolidado sem configuração de cluster
- Pay-per-use — sem custo em horários ociosos
- Deploy via container sem gerenciar infraestrutura

### Por que Redis (Cache-aside)?
Absorve os 50 req/s de pico no consolidado sem bater no banco a cada requisição. TTL de 5 minutos equilibra consistência eventual com performance. Falha no Redis degrada graciosamente para o banco — nunca derruba a API.

### Por que Clean Architecture?
- Regras de negócio isoladas no `Domain` — sem dependência de frameworks
- Use cases testáveis sem infraestrutura real (mocks via NSubstitute)
- Fácil substituição de banco, cache ou mensageria sem alterar o domínio

### Por que CQRS com MediatR?
- Separa intenção de escrita (`RegistrarLancamentoCommand`) de leitura (`ListarLancamentosQuery`)
- Cada handler tem responsabilidade única
- Facilita evolução independente de leitura e escrita

### Por que dois bancos PostgreSQL separados?
- Cada microsserviço é dono dos seus dados (Database per Service pattern)
- Schema pode evoluir independentemente
- Falha em um banco não afeta o outro

---

## Domínios e Capacidades

### Domínio: Gestão Financeira

**Subdomínio Core — Lançamentos**
- Registrar lançamento (débito ou crédito)
- Validar regras de negócio (valor > 0, data não futura, tipo válido)
- Consultar lançamentos por data
- Publicar evento de lançamento registrado

**Subdomínio Supporting — Consolidado Diário**
- Consumir eventos de lançamento
- Calcular e persistir saldo diário agregado
- Servir consolidado com cache
- Invalidar cache ao receber novos lançamentos

---

## Requisitos

### Funcionais
| ID | Descrição |
|---|---|
| RF01 | Registrar lançamento com tipo (débito/crédito), valor, descrição e data |
| RF02 | Consultar lançamentos por data |
| RF03 | Consultar saldo consolidado de um dia específico |
| RF04 | Consolidado atualizado automaticamente a cada lançamento |

### Não-Funcionais
| ID | Requisito | Meta |
|---|---|---|
| RNF01 | Disponibilidade do serviço de lançamentos | 99,9% — independente do consolidado |
| RNF02 | Throughput do consolidado em pico | 50 req/s |
| RNF03 | Tolerância a perda no consolidado | ≤ 5% |
| RNF04 | Latência do lançamento (p99) | < 200ms |
| RNF05 | Consistência do consolidado | Eventual |
| RNF06 | Autenticação | JWT Bearer em todos os endpoints |

---

## Pré-requisitos

- [Docker](https://www.docker.com/) 24+
- [Docker Compose](https://docs.docker.com/compose/) v2+
- [.NET SDK 8.0](https://dotnet.microsoft.com/download/dotnet/8.0) (para rodar testes localmente)

---

## Como Rodar Localmente

### 1. Clone o repositório

```bash
git clone https://github.com/seu-usuario/cashflow.git
cd cashflow
```

### 2. Suba toda a infraestrutura com Docker Compose

```bash
docker compose up --build
```

Isso sobe automaticamente:
- PostgreSQL para Lançamentos (porta 5432)
- PostgreSQL para Consolidado (porta 5433)
- Redis (porta 6379)
- Emulador do Google Pub/Sub (porta 8085)
- API de Lançamentos (porta 8080)
- API de Consolidado (porta 8081)

Aguarde as mensagens de saúde dos containers antes de fazer requisições:
```
cashflow-lancamentos  | Application started. Press Ctrl+C to shut down.
cashflow-consolidado  | Application started. Press Ctrl+C to shut down.
```

### 3. Gerar um token JWT para os testes

Como é um ambiente de dev, você pode gerar um token com qualquer ferramenta JWT usando:
- **Key:** `sua-chave-secreta-minimo-32-caracteres-aqui`
- **Issuer:** `cashflow-api`
- **Audience:** `cashflow-clients`

Ou use o site https://jwt.io com o algoritmo HS256 e o payload:
```json
{
  "sub": "usuario-teste",
  "iss": "cashflow-api",
  "aud": "cashflow-clients",
  "exp": 9999999999
}
```

### 4. Acesse o Swagger

| Serviço | URL |
|---|---|
| Lançamentos | http://localhost:8080/swagger |
| Consolidado | http://localhost:8081/swagger |

---

## Endpoints da API

### Lançamentos — `http://localhost:8080`

#### `POST /api/lancamentos`
Registra um novo lançamento.

**Headers:**
```
Authorization: Bearer {seu-token-jwt}
Content-Type: application/json
```

**Body:**
```json
{
  "valor": 150.00,
  "tipo": 2,
  "descricao": "Venda à vista",
  "data": "2025-01-15"
}
```

> `tipo`: `1` = Débito, `2` = Crédito

**Resposta 201 Created:**
```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "criadoEm": "2025-01-15T14:30:00Z"
}
```

---

#### `GET /api/lancamentos?data=2025-01-15`
Lista todos os lançamentos de uma data.

**Resposta 200 OK:**
```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "valor": 150.00,
    "tipo": 2,
    "tipoDescricao": "Credito",
    "descricao": "Venda à vista",
    "data": "2025-01-15",
    "criadoEm": "2025-01-15T14:30:00Z"
  }
]
```

---

### Consolidado — `http://localhost:8081`

#### `GET /api/consolidado/{data}`
Retorna o saldo consolidado de um dia.

**Exemplo:** `GET /api/consolidado/2025-01-15`

**Resposta 200 OK:**
```json
{
  "data": "2025-01-15",
  "totalCreditos": 1500.00,
  "totalDebitos": 300.00,
  "saldoFinal": 1200.00,
  "atualizadoEm": "2025-01-15T18:00:00Z"
}
```

**Resposta 404 Not Found** (nenhum lançamento no dia):
```json
{
  "message": "Nenhum consolidado encontrado para 2025-01-15."
}
```

---

### Health Checks

```
GET http://localhost:8080/health  → Lançamentos + PostgreSQL
GET http://localhost:8081/health  → Consolidado + PostgreSQL + Redis
```

---

## Testes

### Rodar todos os testes

```bash
dotnet test
```

### Rodar por projeto

```bash
# Testes de Lançamentos
dotnet test tests/CashFlow.Lancamentos.Tests/

# Testes de Consolidado
dotnet test tests/CashFlow.Consolidado.Tests/
```

### Cobertura de testes

```bash
dotnet test --collect:"XPlat Code Coverage"
```

### O que é testado

**Lançamentos (Unit):**
- Registro com dados válidos persiste e publica evento
- Valor zero/negativo lança `ValidationException`
- Descrição vazia lança `ValidationException`
- Data futura lança `ValidationException`
- Falha no Pub/Sub não propaga erro (resiliência)
- Entidade de domínio cria IDs únicos

**Consolidado (Unit):**
- Cache hit não consulta o banco
- Cache miss busca no banco e popula cache
- Consolidado inexistente retorna null (404)
- AplicarCredito soma corretamente
- AplicarDebito subtrai corretamente do saldo

---

## Observabilidade

### Métricas e Logs (GCP)

No ambiente GCP, o sistema utiliza:

- **Cloud Monitoring** — métricas de latência, throughput e taxa de erro por serviço
- **Cloud Logging** — logs estruturados em JSON com correlação por `TraceId`
- **Cloud Trace** — rastreamento distribuído entre serviços

### Alertas recomendados

| Alerta | Condição | Ação |
|---|---|---|
| Alta latência Lançamentos | p99 > 500ms por 5min | Escalar instâncias |
| Taxa de erro > 1% | 5xx por 5min | Notificar on-call |
| Dead Letter Topic com mensagens | DLQ > 0 | Investigar consumer |
| Cache miss rate > 80% | Redis miss/total | Verificar TTL / Redis |

### Logs estruturados (exemplo)

```json
{
  "timestamp": "2025-01-15T14:30:00Z",
  "level": "Information",
  "message": "Lançamento {Id} registrado com sucesso.",
  "Id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "traceId": "abc123",
  "service": "lancamentos-api"
}
```

---

## Infraestrutura GCP

Os recursos são provisionados via **Terraform** na pasta `/infra`.

### Recursos criados

| Recurso | Produto GCP | Finalidade |
|---|---|---|
| API de Lançamentos | Cloud Run | Microsserviço stateless com auto-scale |
| API de Consolidado | Cloud Run | Microsserviço stateless com auto-scale |
| Banco Lançamentos | Cloud SQL (PostgreSQL 16) | Persistência com HA e failover |
| Banco Consolidado | Cloud SQL (PostgreSQL 16) | Persistência isolada |
| Cache | Memorystore (Redis 7) | Cache de consolidado |
| Mensageria | Cloud Pub/Sub | Comunicação assíncrona |
| Gateway | Cloud Endpoints / Apigee | Rate limit, auth, roteamento |
| Firewall | Cloud Armor | WAF, proteção DDoS |
| Imagens | Artifact Registry | Docker images |
| CI/CD | Cloud Build | Pipeline automatizado |
| Segredos | Secret Manager | Chaves JWT, strings de conexão |

### Deploy no GCP

```bash
# 1. Autentique no GCP
gcloud auth login
gcloud config set project seu-projeto-gcp

# 2. Provisione a infraestrutura
cd infra
terraform init
terraform plan
terraform apply

# 3. Build e push das imagens
gcloud builds submit --config=cloudbuild.yaml

# 4. Deploy no Cloud Run (feito automaticamente pelo Cloud Build)
```

---

## Estimativa de Custos

Estimativa mensal para ambiente de produção com carga moderada (50 req/s em pico, 8h/dia):

| Recurso | Configuração | Custo Est./mês |
|---|---|---|
| Cloud Run (2 serviços) | 1 vCPU, 512MB, ~5M req/mês | ~US$ 15 |
| Cloud SQL (2 instâncias) | db-f1-micro, HA, 10GB SSD | ~US$ 60 |
| Memorystore Redis | 1GB Basic | ~US$ 35 |
| Cloud Pub/Sub | ~5M mensagens/mês | ~US$ 2 |
| Cloud Armor | WAF básico | ~US$ 10 |
| Outros (logs, build, egress) | — | ~US$ 10 |
| **Total estimado** | | **~US$ 132/mês** |

> Valores baseados na tabela de preços GCP (us-east1). Produção com mais carga pode exigir instâncias maiores de Cloud SQL.

---

## Evoluções Futuras

### Funcionalidades
- **Autenticação completa** — endpoint de login com geração de JWT e refresh token
- **Multi-tenant** — suporte a múltiplos comerciantes com isolamento por tenant
- **Categorias de lançamento** — classificação por tipo (alimentação, fornecedor, etc.)
- **Relatórios por período** — consolidado semanal e mensal além do diário
- **Webhooks** — notificações quando o saldo diário atingir limites configurados

### Técnicas
- **Outbox Pattern** — garantia de entrega do evento Pub/Sub mesmo com falha após persistência (eliminar o try/catch atual do handler)
- **Idempotência no consumer** — deduplificação por `LancamentoId` para evitar dupla contagem em reprocessamentos
- **Circuit Breaker** — Polly com circuit breaker no acesso ao Redis e ao banco
- **Migrations automáticas via CI/CD** — rodar migrations como job separado no pipeline, não em startup
- **gRPC interno** — se surgir necessidade de comunicação síncrona entre serviços, preferir gRPC a REST por performance
- **Testes de integração** — usar Testcontainers para subir PostgreSQL e Redis reais nos testes
- **Testes de carga** — k6 ou NBomber para validar os 50 req/s com menos de 5% de perda
- **OpenTelemetry** — instrumentação padronizada para traces e métricas exportados para o GCP

### Arquitetura de Transição (de legado)

Se o sistema atual for um monolito legado com tudo junto (lançamentos + consolidado no mesmo processo e banco):

**Fase 1 — Strangler Fig:**
Adicionar o API Gateway na frente do monolito. Novas requisições de lançamento já vão para o microsserviço novo. Monolito ainda serve as leituras.

**Fase 2 — Extração do consolidado:**
Criar o consumer Pub/Sub. O monolito começa a publicar eventos de lançamento (dual-write temporário). Consolidado migra para o novo serviço.

**Fase 3 — Desligamento do legado:**
Após validação, monolito é descomissionado. Migração de dados históricos via script ETL.
