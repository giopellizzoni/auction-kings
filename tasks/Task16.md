# Task 16 — Implementar Architecture Tests com NetArchTest

## Contexto

Architecture tests garantem que as regras de dependência da Clean Architecture são respeitadas ao longo do tempo. O projeto `AuctionKings.ArchitectureTests` contém apenas um teste placeholder. Usando `NetArchTest.Rules`, é possível verificar automaticamente que, por exemplo, o Domain não referencia Infrastructure, e que handlers seguem naming conventions.

## Arquivos a Alterar/Criar

- `test/AuctionKings.ArchitectureTests/AuctionKings.ArchitectureTests.csproj` — adicionar `NetArchTest.Rules`, project refs para todos os projetos de source
- `Directory.Packages.props` — adicionar versão de `NetArchTest.Rules`
- `test/AuctionKings.ArchitectureTests/CleanArchitectureTests.cs` — testes de dependência entre camadas
- `test/AuctionKings.ArchitectureTests/NamingConventionTests.cs` — testes de naming conventions
- `test/AuctionKings.ArchitectureTests/DomainRulesTests.cs` — testes de regras do domínio
- Remover `test/AuctionKings.ArchitectureTests/UnitTest1.cs`

## Critérios de Aceite

- Domain layer não tem dependência de Application, Infrastructure ou API
- Application layer não tem dependência de Infrastructure ou API
- Shared layer não tem dependência de nenhuma outra layer do projeto
- Command handlers terminam em `CommandHandler`
- Query handlers terminam em `QueryHandler`
- Domain events terminam em `Event`
- Validators terminam em `Validator`
- Entities herdam de `Entity`, aggregates herdam de `AggregateRoot`
- Todos os testes passam com `dotnet test test/AuctionKings.ArchitectureTests/`

## Testes Obrigatórios

- `Domain_ShouldNotDependOn_Application`
- `Domain_ShouldNotDependOn_Infrastructure`
- `Domain_ShouldNotDependOn_API`
- `Application_ShouldNotDependOn_Infrastructure`
- `Application_ShouldNotDependOn_API`
- `Shared_ShouldNotDependOn_AnyProjectLayer`
- `CommandHandlers_ShouldFollowNamingConvention`
- `QueryHandlers_ShouldFollowNamingConvention`
- `DomainEvents_ShouldImplementIDomainEvent`
- `Entities_ShouldInheritFromEntityBase`

## Riscos/Observações

- `NetArchTest.Rules` pode não suportar .NET 10 imediatamente — verificar compatibilidade ou usar fork atualizado
- Testes de arquitetura são "documentation as code" — mantenha mensagens de falha descritivas
- Adicionar estes testes ao CI pipeline para prevenir regressões arquiteturais
- Se `NetArchTest` não for compatível, alternativa é `ArchUnitNET`

## Estimativa

**S** (Small)

## Dependências

- **Task 01** — Shared layer com base classes
- **Task 02** — Domain layer com entities e events
- **Task 03** — Application layer com handlers e behaviors
- **Task 06** — Infrastructure layer implementada
