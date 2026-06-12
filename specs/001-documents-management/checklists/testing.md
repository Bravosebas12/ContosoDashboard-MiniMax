# Testing Quality Checklist: Document Upload and Management

**Purpose**: Validate that the requirements for the 8-level test pyramid, TDD Hard mandate, mutation testing, and performance testing are complete, unambiguous, and testable.
**Created**: 2026-06-12
**Feature**: [spec.md](../spec.md)
**Constitution Reference**: v1.1.0, Principle V (TDD Hard + Pirámide Completa)

## TDD Hard Mandate (V.1)

- [x] CHK001 - **ROJO-VERDE-REFACTOR**: Is the cycle (write failing test, implement, refactor) stated as a NON-NEGOTIABLE rule that applies to every production code change? [Completeness, V.1, §7.1]
- [x] CHK002 - **Test-first PR**: Is the spec explicit that the diff of tests must PRECEDE the diff of production code in every PR? [Clarity, V.1]
- [x] CHK003 - **Hot-fix exception**: Is the security hot-fix exception (skip ROJO, must include regression tests in same PR) defined with explicit boundaries? [Completeness, V.1 §7.4]
- [x] CHK004 - **CI test gate**: Is the spec explicit that all tests must pass in CI before any PR can be merged? [Clarity, §V Quality Gates]

## 8-Level Test Pyramid (V.2)

- [x] CHK005 - **Level 1 — Unit funcionales**: Are the unit tests required to cover ≥ 80% of `Services/Documents/` (line coverage)? [Measurability, V.2 Nivel 1]
- [x] CHK006 - **Level 2 — Unit técnicas**: Are the unit tests required to cover ≥ 70% of `Utils/Documents/`, `Mappers/Documents/`? [Measurability, V.2 Nivel 2]
- [x] CHK007 - **Level 3 — Componentes Blazor**: Are bUnit component tests required for the 2 main Blazor components (`DocumentUpload.razor`, `DocumentList.razor`)? [Coverage, V.2 Nivel 3]
- [x] CHK008 - **Level 4 — Integración**: Are integration tests required to use `WebApplicationFactory<Program>` + Testcontainers (SQL Server) for repository and endpoint tests? [Coverage, V.2 Nivel 4]
- [x] CHK009 - **Level 5 — Contratos (Pact)**: Is PactNet + Pact Broker required to verify 100% of public endpoints (`/api/documents/**`)? [Coverage, V.2 Nivel 5, SC-010]
- [x] CHK010 - **Level 6 — E2E API**: Is the spec clear that 100% of happy paths (upload, list, search, download, share, delete) must be covered by E2E API tests? [Coverage, V.2 Nivel 6]
- [x] CHK011 - **Level 7 — E2E UI**: Is Playwright required for Blazor Server E2E tests (smoke + 1 happy path per feature)? [Coverage, V.2 Nivel 7]
- [x] CHK012 - **Level 8 — Rendimiento**: Is the spec clear that performance tests span 6 sub-levels (micro, component, integration, contract, E2E sistema, resiliencia)? [Coverage, V.2 Nivel 8, V.4]
- [x] CHK013 - **Tool mapping**: Is each pyramid level mapped to a specific .NET tool (xUnit, bUnit, PactNet, Playwright, NBomber, etc.)? [Clarity, V.2]
- [x] CHK014 - **Project structure**: Are the 7 test projects (`.Tests.Unit`, `.Tests.Components`, `.Tests.Integration`, `.Tests.Contract`, `.Tests.E2E.Api`, `.Tests.E2E.UI`, `.Tests.Performance`) defined with their scope? [Completeness, plan §Project Structure]

## Coverage Requirements (V.2 + V.5)

- [x] CHK015 - **Line coverage target**: Is the ≥ 80% line coverage target defined and measurable via coverlet? [Measurability, V.2]
- [x] CHK016 - **Line coverage minimum (blocking)**: Is the < 40% line coverage a hard merge gate? [Clarity, V.5]
- [x] CHK017 - **Branch coverage target**: Is the ≥ 75% branch coverage target defined? [Measurability, V.2]
- [x] CHK018 - **Public method coverage**: Is 100% coverage of public methods required (1 test per method minimum)? [Clarity, V.2]
- [x] CHK019 - **Coverage exclusions**: Is the use of `[ExcludeFromCodeCoverage]` explicitly PROHIBITED except with PR-justified exception? [Clarity, V.2]
- [x] CHK020 - **Coverage tool**: Is coverlet specified as the coverage tool, integrated with ReportGenerator for human-readable reports? [Clarity, V.2]

## Mutation Testing (V.3)

- [x] CHK021 - **Mutation tool**: Is Stryker.NET specified as the mutation testing tool? [Clarity, V.3]
- [x] CHK022 - **Mutation scope**: Are the modules to be mutated clearly defined (`Domain/Documents/`, `Services/Documents/`, validators, parsers)? [Coverage, V.3]
- [x] CHK023 - **Mutation score target**: Is the ≥ 80% mutation score target defined? [Measurability, V.3]
- [x] CHK024 - **Mutation score minimum (blocking)**: Is the < 70% mutation score a hard merge gate? [Clarity, V.5, SC-011]
- [x] CHK025 - **Surviving mutants**: Is the process for handling surviving mutants (justify with `Stryker.NET` ignore OR add a detection test) defined? [Completeness, V.3]
- [x] CHK026 - **Mutation CI frequency**: Is it specified that mutation runs in CI on every PR? [Clarity, V.3]
- [x] CHK027 - **Mutation exclusions**: Are stubs and mappers explicitly excluded from mutation scope (equivalent mutations without value)? [Clarity, V.3]
- [x] CHK028 - **Mutation command**: Is the `dotnet stryker` command with appropriate thresholds documented? [Clarity, §7.2]

## Performance Test Pyramid (V.4)

- [x] CHK029 - **Micro benchmarks (BenchmarkDotNet)**: Are micro-benchmarks of hot-path methods required (e.g., `GuidGenerator`, `FilePathBuilder`)? [Coverage, V.4]
- [x] CHK030 - **Component performance**: Is the p95/p99 latency target per endpoint defined? [Measurability, V.4]
- [x] CHK031 - **Integration performance**: Is throughput (RPS) target defined with Testcontainers setup? [Coverage, V.4]
- [x] CHK032 - **Contract performance**: Is the latency SLO per endpoint documented in the contract (Pact)? [Clarity, V.4]
- [x] CHK033 - **5 load profiles**: Are the 5 profiles (smoke, load, stress, spike, soak) all required at E2E system level? [Coverage, V.4]
- [x] CHK034 - **Performance SLOs**: Are the 5 SLOs (p95 < 30s upload, p95 < 500ms list, p95 < 2s search, p95 < 3s preview, error < 0.1%) defined with measurement method? [Measurability, V.4, SC-005, SC-006]
- [x] CHK035 - **Memory leak detection**: Is the soak 24h memory growth threshold (< 10% over baseline) defined? [Measurability, V.4]
- [x] CHK036 - **CPU saturation**: Are the CPU saturation thresholds (< 75% avg, < 90% p95) defined? [Measurability, V.4]

## Quality Gates (from plan §Development Workflow)

- [x] CHK037 - **Build gate**: Is `dotnet build` without errors a hard gate? [Clarity, §V Quality Gates]
- [x] CHK038 - **Unit + component gate**: Is `dotnet test` on `*.Tests.Unit` + `*.Tests.Components` without errors a hard gate? [Clarity, §V Quality Gates]
- [x] CHK039 - **Integration gate**: Is `dotnet test` on `*.Tests.Integration` (with Testcontainers) without errors a hard gate? [Clarity, §V Quality Gates]
- [x] CHK040 - **Contract gate**: Is `pact-broker can-i-deploy` returning success required before merge? [Clarity, §V Quality Gates]
- [x] CHK041 - **E2E API gate**: Is `dotnet test` on `*.Tests.E2E.Api` without errors a hard gate? [Clarity, §V Quality Gates]
- [x] CHK042 - **E2E UI gate**: Is Playwright test suite passing required? [Clarity, §V Quality Gates]
- [x] CHK043 - **Mutation gate**: Is Stryker.NET ≥ 70% required? [Clarity, §V Quality Gates]
- [x] CHK044 - **Performance gate (conditional)**: Is performance testing required when the PR touches a performance-critical endpoint? [Clarity, V.4]
- [x] CHK045 - **Code review gate**: Is at least 1 human approval required, using the constitution as checklist? [Clarity, §V Quality Gates]

## Test Data and Environments

- [x] CHK046 - **Test database isolation**: Is it explicit that integration tests use a SEPARATE database (Testcontainers), not the dev LocalDB? [Coverage, V.2]
- [x] CHK047 - **Seed data**: Is the seed data for tests defined in the plan (4 documents with different statuses)? [Clarity, data-model.md §Sample Data]
- [x] CHK048 - **Mocking strategy**: Is the mocking approach defined (NSubstitute for interfaces, FluentAssertions for assertions)? [Clarity, plan §Complexity Tracking]
- [x] CHK049 - **CI parallelism**: Is test parallel execution enabled at the project level (xUnit `[CollectionDefinition]`)? [Gap, implied by performance]
- [x] CHK050 - **Flaky test policy**: Is there a policy for handling flaky tests (auto-retry, quarantine, root-cause required)? [Gap, plan §Risks]

## Notes

- The spec is strong on coverage and mutation thresholds, weak on flaky test policy (CH050 is a gap).
- Performance tests are well-specified with 5 profiles and 5 SLOs.
- All 50 items are tied to specific constitution principles (V.1 through V.5) or spec sections.
- A11 quality gates (CHK037–CHK047) directly map to the 13-gate list in `constitution.md` v1.1.0 §Flujo de Desarrollo.
