# Architecture Tests & CI Pipeline Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Implement architecture tests with NetArchTest.Rules (T16, issue #61) and complete the GitHub Actions CI pipeline to run all test suites (T17, issue #66).

**Architecture:** Architecture tests use NetArchTest.Rules to verify Clean Architecture dependency rules and naming conventions by loading assemblies via `typeof()` anchors in project references. The CI pipeline extends the existing `.github/workflows/ci.yml` (which already handles build, unit tests, and caching) with architecture tests and integration tests steps.

**Tech Stack:** NetArchTest.Rules 1.3.2 (netstandard2.0, compatible with .NET 10), xUnit 2.9.3, GitHub Actions ubuntu-latest (Docker available natively for Testcontainers integration tests).

---

## File Map

| File                                                                        | Action | Responsibility                                                     |
| --------------------------------------------------------------------------- | ------ | ------------------------------------------------------------------ |
| `Directory.Packages.props`                                                  | Modify | Add `NetArchTest.Rules` version entry                              |
| `test/AuctionKings.ArchitectureTests/AuctionKings.ArchitectureTests.csproj` | Modify | Add NetArchTest.Rules package + project refs for all source layers |
| `test/AuctionKings.ArchitectureTests/UnitTest1.cs`                          | Delete | Placeholder — replaced by real tests                               |
| `test/AuctionKings.ArchitectureTests/CleanArchitectureTests.cs`             | Create | Layer dependency tests (6 tests)                                   |
| `test/AuctionKings.ArchitectureTests/NamingConventionTests.cs`              | Create | Handler naming convention tests (2 tests)                          |
| `test/AuctionKings.ArchitectureTests/DomainRulesTests.cs`                   | Create | Domain event and entity base class tests (2 tests)                 |
| `.github/workflows/ci.yml`                                                  | Modify | Add architecture tests step + integration tests step               |

---

## Task 1: Add NetArchTest.Rules Dependency

**Files:**

- Modify: `Directory.Packages.props`
- Modify: `test/AuctionKings.ArchitectureTests/AuctionKings.ArchitectureTests.csproj`

> **Risk note:** NetArchTest.Rules 1.3.2 targets `netstandard2.0` and uses Mono.Cecil for IL analysis. It is compatible with .NET 10 assemblies. If you encounter runtime errors like "BadImageFormatException", fall back to `ArchUnitNET` (add `PackageVersion Include="ArchUnitNET" Version="0.11.0"` instead and rewrite tests using its fluent API).

- [ ] **Step 1: Add NetArchTest.Rules to Directory.Packages.props**

  Open `Directory.Packages.props` and add the following inside the `<ItemGroup Label="Test">` block, after the last `xunit` entry:

  ```xml
  <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
  ```

  The Test `ItemGroup` should look like this after the change:

  ```xml
  <ItemGroup Label="Test">
    <PackageVersion Include="coverlet.collector" Version="6.0.4" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageVersion Include="NetArchTest.Rules" Version="1.3.2" />
    <PackageVersion Include="NSubstitute" Version="5.3.0" />
    <PackageVersion Include="Testcontainers.PostgreSql" Version="4.2.0" />
    <PackageVersion Include="Testcontainers.Redis" Version="4.2.0" />
    <PackageVersion Include="xunit" Version="2.9.3" />
    <PackageVersion Include="xunit.runner.visualstudio" Version="3.1.4" />
  </ItemGroup>
  ```

- [ ] **Step 2: Replace ArchitectureTests csproj content**

  Replace the full contents of `test/AuctionKings.ArchitectureTests/AuctionKings.ArchitectureTests.csproj` with:

  ```xml
  <Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
      <IsPackable>false</IsPackable>
    </PropertyGroup>

    <ItemGroup>
      <PackageReference Include="coverlet.collector" />
      <PackageReference Include="Microsoft.NET.Test.Sdk" />
      <PackageReference Include="NetArchTest.Rules" />
      <PackageReference Include="xunit" />
      <PackageReference Include="xunit.runner.visualstudio" />
    </ItemGroup>

    <ItemGroup>
      <Using Include="Xunit" />
    </ItemGroup>

    <ItemGroup>
      <ProjectReference Include="..\..\src\common\AuctionKings.Shared\AuctionKings.Shared.csproj" />
      <ProjectReference Include="..\..\src\services\AuctionKings.Domain\AuctionKings.Domain.csproj" />
      <ProjectReference Include="..\..\src\services\AuctionKings.Application\AuctionKings.Application.csproj" />
      <ProjectReference Include="..\..\src\services\AuctionKings.Infrastructure\AuctionKings.Infrastructure.csproj" />
      <ProjectReference Include="..\..\src\services\AuctionKings.API\AuctionKings.API.csproj" />
    </ItemGroup>

  </Project>
  ```

- [ ] **Step 3: Verify the project restores cleanly**

  Run: `dotnet restore test/AuctionKings.ArchitectureTests/`

  Expected: output ends with `Restore complete` — no errors. If you see `error NU1202: Package NetArchTest.Rules 1.3.2 is not compatible`, open a GitHub issue for NetArchTest and use ArchUnitNET instead.

- [ ] **Step 4: Commit**

  ```bash
  git add Directory.Packages.props test/AuctionKings.ArchitectureTests/AuctionKings.ArchitectureTests.csproj
  git commit -m "chore(arch-tests): add NetArchTest.Rules and source project references"
  ```

---

## Task 2: Layer Dependency Tests

**Files:**

- Create: `test/AuctionKings.ArchitectureTests/CleanArchitectureTests.cs`

Clean Architecture rule: `API → Application → Domain ← Infrastructure`. Domain and Shared are the innermost layers and must not reference outer layers.

- [ ] **Step 1: Create CleanArchitectureTests.cs**

  Create `test/AuctionKings.ArchitectureTests/CleanArchitectureTests.cs` with:

  ```csharp
  using System.Reflection;
  using NetArchTest.Rules;

  namespace AuctionKings.ArchitectureTests;

  public class CleanArchitectureTests
  {
      // Type anchors — one type per assembly, chosen for stability.
      private static readonly Assembly DomainAssembly =
          typeof(AuctionKings.Domain.Events.AuctionClosedEvent).Assembly;

      private static readonly Assembly ApplicationAssembly =
          typeof(AuctionKings.Application.Auctions.Commands.CloseAuction.CloseAuctionCommandHandler).Assembly;

      private static readonly Assembly InfrastructureAssembly =
          typeof(AuctionKings.Infrastructure.DependencyInjection).Assembly;

      private static readonly Assembly ApiAssembly =
          typeof(AuctionKings.API.Hubs.AuctionHub).Assembly;

      private static readonly Assembly SharedAssembly =
          typeof(AuctionKings.Shared.Common.Entity).Assembly;

      [Fact]
      public void Domain_ShouldNotDependOn_Application()
      {
          var result = Types.InAssembly(DomainAssembly)
              .ShouldNot()
              .HaveDependencyOn("AuctionKings.Application")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Domain layer must not reference Application. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Domain_ShouldNotDependOn_Infrastructure()
      {
          var result = Types.InAssembly(DomainAssembly)
              .ShouldNot()
              .HaveDependencyOn("AuctionKings.Infrastructure")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Domain layer must not reference Infrastructure. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Domain_ShouldNotDependOn_API()
      {
          var result = Types.InAssembly(DomainAssembly)
              .ShouldNot()
              .HaveDependencyOn("AuctionKings.API")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Domain layer must not reference API. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Application_ShouldNotDependOn_Infrastructure()
      {
          var result = Types.InAssembly(ApplicationAssembly)
              .ShouldNot()
              .HaveDependencyOn("AuctionKings.Infrastructure")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Application layer must not reference Infrastructure. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Application_ShouldNotDependOn_API()
      {
          var result = Types.InAssembly(ApplicationAssembly)
              .ShouldNot()
              .HaveDependencyOn("AuctionKings.API")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Application layer must not reference API. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Shared_ShouldNotDependOn_AnyProjectLayer()
      {
          var result = Types.InAssembly(SharedAssembly)
              .ShouldNot()
              .HaveDependencyOnAny(
                  "AuctionKings.Domain",
                  "AuctionKings.Application",
                  "AuctionKings.Infrastructure",
                  "AuctionKings.API")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Shared layer must not depend on any project layer. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }
  }
  ```

- [ ] **Step 2: Build to catch type-not-found errors**

  Run: `dotnet build test/AuctionKings.ArchitectureTests/ --configuration Release`

  Expected: `Build succeeded. 0 Error(s)`. If you see CS0246 (type not found) errors, the namespace or class name has changed — find the correct type with `grep -r "class AuctionClosedEvent" src/` and update the anchor.

- [ ] **Step 3: Run these tests**

  Run: `dotnet test test/AuctionKings.ArchitectureTests/ --configuration Release`

  Expected: all 6 tests pass. If `Application_ShouldNotDependOn_Infrastructure` fails, inspect the failing type names in the output — Application may have accidentally imported an Infrastructure type. Fix it in the source, not in the test.

- [ ] **Step 4: Commit**

  ```bash
  git add test/AuctionKings.ArchitectureTests/CleanArchitectureTests.cs
  git commit -m "test(arch): add layer dependency tests (T16)"
  ```

---

## Task 3: Naming Convention Tests

**Files:**

- Create: `test/AuctionKings.ArchitectureTests/NamingConventionTests.cs`

Handler naming convention: types in `Commands` sub-namespaces that end with `Handler` must end with `CommandHandler`. Same rule for `Queries` → `QueryHandler`.

- [ ] **Step 1: Create NamingConventionTests.cs**

  Create `test/AuctionKings.ArchitectureTests/NamingConventionTests.cs` with:

  ```csharp
  using System.Reflection;
  using NetArchTest.Rules;

  namespace AuctionKings.ArchitectureTests;

  public class NamingConventionTests
  {
      private static readonly Assembly ApplicationAssembly =
          typeof(AuctionKings.Application.Auctions.Commands.CloseAuction.CloseAuctionCommandHandler).Assembly;

      [Fact]
      public void CommandHandlers_ShouldFollowNamingConvention()
      {
          // All non-abstract classes living in a *.Commands.* namespace whose
          // name ends with "Handler" must end with "CommandHandler".
          var result = Types.InAssembly(ApplicationAssembly)
              .That()
              .AreClasses()
              .And()
              .AreNotAbstract()
              .And()
              .ResideInNamespaceMatching(@"AuctionKings\.Application\..*\.Commands\..*")
              .And()
              .HaveNameEndingWith("Handler")
              .Should()
              .HaveNameEndingWith("CommandHandler")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Command handlers must be named *CommandHandler. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void QueryHandlers_ShouldFollowNamingConvention()
      {
          // All non-abstract classes living in a *.Queries.* namespace whose
          // name ends with "Handler" must end with "QueryHandler".
          var result = Types.InAssembly(ApplicationAssembly)
              .That()
              .AreClasses()
              .And()
              .AreNotAbstract()
              .And()
              .ResideInNamespaceMatching(@"AuctionKings\.Application\..*\.Queries\..*")
              .And()
              .HaveNameEndingWith("Handler")
              .Should()
              .HaveNameEndingWith("QueryHandler")
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Query handlers must be named *QueryHandler. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }
  }
  ```

- [ ] **Step 2: Run tests**

  Run: `dotnet test test/AuctionKings.ArchitectureTests/ --configuration Release`

  Expected: all 8 tests pass (6 from CleanArchitectureTests + 2 new). If a naming test fails, it means a handler was added with the wrong name suffix — fix the production type name, not the test.

- [ ] **Step 3: Commit**

  ```bash
  git add test/AuctionKings.ArchitectureTests/NamingConventionTests.cs
  git commit -m "test(arch): add handler naming convention tests (T16)"
  ```

---

## Task 4: Domain Rules Tests

**Files:**

- Create: `test/AuctionKings.ArchitectureTests/DomainRulesTests.cs`

Domain rules: all concrete types ending with `Event` in the Domain assembly must implement `IDomainEvent`. All non-abstract classes in `AuctionKings.Domain.Aggregates` must inherit from `Entity` (directly or transitively via `AggregateRoot`).

- [ ] **Step 1: Create DomainRulesTests.cs**

  Create `test/AuctionKings.ArchitectureTests/DomainRulesTests.cs` with:

  ```csharp
  using System.Reflection;
  using AuctionKings.Shared.Common;
  using NetArchTest.Rules;

  namespace AuctionKings.ArchitectureTests;

  public class DomainRulesTests
  {
      private static readonly Assembly DomainAssembly =
          typeof(AuctionKings.Domain.Events.AuctionClosedEvent).Assembly;

      [Fact]
      public void DomainEvents_ShouldImplementIDomainEvent()
      {
          // Every concrete class ending with "Event" in the Domain assembly
          // must implement IDomainEvent. This prevents silent typo-named events
          // from bypassing the event dispatch pipeline.
          var result = Types.InAssembly(DomainAssembly)
              .That()
              .AreClasses()
              .And()
              .AreNotAbstract()
              .And()
              .HaveNameEndingWith("Event")
              .Should()
              .ImplementInterface(typeof(IDomainEvent))
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"Domain events must implement IDomainEvent. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }

      [Fact]
      public void Entities_ShouldInheritFromEntityBase()
      {
          // Every non-abstract class in the Aggregates namespace must inherit
          // from Entity (directly or via AggregateRoot). Value objects (in
          // ValueObjects namespace) are intentionally excluded.
          var result = Types.InAssembly(DomainAssembly)
              .That()
              .AreClasses()
              .And()
              .AreNotAbstract()
              .And()
              .ResideInNamespace("AuctionKings.Domain.Aggregates")
              .Should()
              .Inherit(typeof(Entity))
              .GetResult();

          Assert.True(result.IsSuccessful,
              $"All domain aggregates must inherit from Entity. Violations: {string.Join(", ", result.FailingTypeNames ?? [])}");
      }
  }
  ```

- [ ] **Step 2: Run all architecture tests**

  Run: `dotnet test test/AuctionKings.ArchitectureTests/ --configuration Release`

  Expected: all 10 tests pass. Specific expected output:

  ```
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Domain_ShouldNotDependOn_Application
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Domain_ShouldNotDependOn_Infrastructure
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Domain_ShouldNotDependOn_API
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Application_ShouldNotDependOn_Infrastructure
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Application_ShouldNotDependOn_API
  Passed AuctionKings.ArchitectureTests.CleanArchitectureTests.Shared_ShouldNotDependOn_AnyProjectLayer
  Passed AuctionKings.ArchitectureTests.NamingConventionTests.CommandHandlers_ShouldFollowNamingConvention
  Passed AuctionKings.ArchitectureTests.NamingConventionTests.QueryHandlers_ShouldFollowNamingConvention
  Passed AuctionKings.ArchitectureTests.DomainRulesTests.DomainEvents_ShouldImplementIDomainEvent
  Passed AuctionKings.ArchitectureTests.DomainRulesTests.Entities_ShouldInheritFromEntityBase
  ```

  If `DomainEvents_ShouldImplementIDomainEvent` fails with a type like `DomainEventTypeRegistry`, add a `.And().AreNotAssignableTo(typeof(DomainEventTypeRegistry))` clause (or rename the offending class to not end with "Event").

- [ ] **Step 3: Commit**

  ```bash
  git add test/AuctionKings.ArchitectureTests/DomainRulesTests.cs
  git commit -m "test(arch): add domain event and entity base class tests (T16)"
  ```

---

## Task 5: Remove Placeholder and Close T16

**Files:**

- Delete: `test/AuctionKings.ArchitectureTests/UnitTest1.cs`

- [ ] **Step 1: Delete UnitTest1.cs**

  ```bash
  rm test/AuctionKings.ArchitectureTests/UnitTest1.cs
  ```

- [ ] **Step 2: Run full solution tests to verify nothing broke**

  Run: `dotnet test --configuration Release`

  Expected: the architecture test project now shows 10 tests, all pass. Unit tests still pass.

- [ ] **Step 3: Commit**

  ```bash
  git add test/AuctionKings.ArchitectureTests/UnitTest1.cs
  git commit -m "test(arch): remove placeholder UnitTest1 — T16 complete, closes #61"
  ```

---

## Task 6: Complete CI Pipeline (T17, issue #66)

**Files:**

- Modify: `.github/workflows/ci.yml`

The existing file already has: checkout, .NET 10 setup, NuGet cache, restore, Release build, unit tests with coverage upload, TRX artifact upload.

Missing: architecture tests step, integration tests step.

Integration tests use Testcontainers — they manage their own PostgreSQL and Redis containers. No `services:` block needed in the workflow. Docker is available on `ubuntu-latest` runners by default.

- [ ] **Step 1: Replace .github/workflows/ci.yml with the complete version**

  Replace the full contents of `.github/workflows/ci.yml` with:

  ```yaml
  name: CI

  on:
    pull_request:
      branches:
        - develop
        - main

  jobs:
    build-and-test:
      name: Build & Test
      runs-on: ubuntu-latest

      steps:
        - name: Checkout
          uses: actions/checkout@v4

        - name: Setup .NET 10
          uses: actions/setup-dotnet@v4
          with:
            dotnet-version: "10.0.x"

        - name: Cache NuGet packages
          uses: actions/cache@v4
          with:
            path: ~/.nuget/packages
            key: ${{ runner.os }}-nuget-${{ hashFiles('**/*.csproj', '**/Directory.Packages.props') }}
            restore-keys: |
              ${{ runner.os }}-nuget-

        - name: Restore dependencies
          run: dotnet restore

        - name: Build
          run: dotnet build --no-restore --configuration Release

        - name: Run Unit Tests
          run: |
            dotnet test test/AuctionKings.UnitTests/ \
              --no-build \
              --configuration Release \
              --collect:"XPlat Code Coverage" \
              --results-directory ./coverage/unit \
              --logger "trx;LogFileName=unit-tests.trx"

        - name: Run Architecture Tests
          run: |
            dotnet test test/AuctionKings.ArchitectureTests/ \
              --no-build \
              --configuration Release \
              --results-directory ./coverage/arch \
              --logger "trx;LogFileName=arch-tests.trx"

        - name: Run Integration Tests
          run: |
            dotnet test test/AuctionKings.IntegrationTests/ \
              --no-build \
              --configuration Release \
              --results-directory ./coverage/integration \
              --logger "trx;LogFileName=integration-tests.trx"

        - name: Upload coverage results
          uses: actions/upload-artifact@v4
          if: always()
          with:
            name: coverage-results
            path: ./coverage/**/*.xml

        - name: Upload TRX results
          uses: actions/upload-artifact@v4
          if: always()
          with:
            name: trx-results
            path: ./coverage/**/*.trx
  ```

  **Key changes vs the original:**
  - Unit test output dir changed from `./coverage` to `./coverage/unit` so all three suites produce separate, non-colliding artifact paths.
  - Added `Run Architecture Tests` step (no `--collect:"XPlat Code Coverage"` — arch tests don't need coverage data).
  - Added `Run Integration Tests` step — Testcontainers spins up its own Docker containers; no extra Docker setup needed.
  - Both new steps use `--no-build` — the single build step already compiled everything.
  - Artifact upload paths use `./coverage/**/*.xml` and `./coverage/**/*.trx` so all sub-directories are captured.

- [ ] **Step 2: Validate YAML syntax locally**

  Run: `python3 -c "import yaml; yaml.safe_load(open('.github/workflows/ci.yml'))" && echo "YAML OK"`

  Expected: `YAML OK`. Fix any indentation errors (YAML uses spaces, not tabs).

- [ ] **Step 3: Commit and push**

  ```bash
  git add .github/workflows/ci.yml
  git commit -m "ci: add architecture tests and integration tests steps, closes #66"
  ```

  Then push the branch and open a PR to `develop`. The CI workflow triggers automatically on the PR.

- [ ] **Step 4: Verify CI run passes**

  Check the Actions tab in GitHub after pushing. Expected:
  - All three test steps are green.
  - TRX and coverage artifacts are downloadable under the workflow run.
  - Total run time < 5 minutes for unit + architecture tests (integration tests may take 1-2 minutes extra for Testcontainers startup).

---

## Self-Review Checklist

**Spec coverage:**

| Requirement                                    | Task            |
| ---------------------------------------------- | --------------- |
| `Domain_ShouldNotDependOn_Application`         | Task 2          |
| `Domain_ShouldNotDependOn_Infrastructure`      | Task 2          |
| `Domain_ShouldNotDependOn_API`                 | Task 2          |
| `Application_ShouldNotDependOn_Infrastructure` | Task 2          |
| `Application_ShouldNotDependOn_API`            | Task 2          |
| `Shared_ShouldNotDependOn_AnyProjectLayer`     | Task 2          |
| `CommandHandlers_ShouldFollowNamingConvention` | Task 3          |
| `QueryHandlers_ShouldFollowNamingConvention`   | Task 3          |
| `DomainEvents_ShouldImplementIDomainEvent`     | Task 4          |
| `Entities_ShouldInheritFromEntityBase`         | Task 4          |
| Remove `UnitTest1.cs`                          | Task 5          |
| CI runs on PRs to `develop` and `main`         | Task 6          |
| Unit tests in CI                               | Already present |
| Architecture tests in CI                       | Task 6          |
| Integration tests in CI                        | Task 6          |
| Test artifacts uploaded                        | Task 6          |

All requirements covered. No gaps.

