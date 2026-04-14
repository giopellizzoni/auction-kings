# Task 17 — Configurar CI Pipeline Real no GitHub Actions

## Contexto

O workflow de CI em `.github/workflows/ci.yml` existe mas contém apenas `echo` placeholder — não builda nem testa nada. Precisa ser configurado para: restaurar dependências, buildar a solution, rodar unit tests e architecture tests (integration tests opcionalmente, dependendo de Docker no runner).

## Arquivos a Alterar/Criar

- `.github/workflows/ci.yml` — reescrever com steps reais:
  - Checkout
  - Setup .NET 10 SDK
  - `dotnet restore`
  - `dotnet build --no-restore --configuration Release`
  - `dotnet test test/AuctionKings.UnitTests/ --no-build --configuration Release`
  - `dotnet test test/AuctionKings.ArchitectureTests/ --no-build --configuration Release`
  - (Opcional) `dotnet test test/AuctionKings.IntegrationTests/` com Docker service containers
  - Upload de resultados de teste e cobertura como artifacts

## Critérios de Aceite

- CI roda automaticamente em PRs para `develop` e `main`
- Build falha se houver warnings (tratados como errors via `TreatWarningsAsErrors`)
- Unit tests e architecture tests rodam e reportam resultados
- Integration tests rodam se Docker estiver disponível (GitHub Actions suporta Docker por padrão em ubuntu runners)
- Workflow completa em < 5 minutos para unit + arch tests
- Badge de status pode ser adicionada ao README

## Testes Obrigatórios

- N/A (esta task é sobre CI, não sobre código de teste)
- Validar que o workflow roda corretamente abrindo um PR de teste

## Riscos/Observações

- .NET 10 pode não estar disponível na action `actions/setup-dotnet` padrão — verificar e usar preview se necessário
- Integration tests com Testcontainers precisam de Docker-in-Docker ou Docker socket no runner
- Considerar cache de NuGet packages (`actions/cache`) para acelerar builds
- Não rodar load tests no CI (são lentos e instáveis)
- Secrets (connection strings) não são necessários — Testcontainers gerencia seus próprios containers

## Estimativa

**S** (Small)

## Dependências

- **Task 14** — unit tests implementados
- **Task 16** — architecture tests implementados

## Status

✅ **Done**

## GitHub Issues

| #                                                                | Title                                         | Status  |
| ---------------------------------------------------------------- | --------------------------------------------- | ------- |
| [#66](https://github.com/PellizzoniCode/auction-kings/issues/66) | Adicionar GitHub Actions: build + test + lint | 🔴 open |
