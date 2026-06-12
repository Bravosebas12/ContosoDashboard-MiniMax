# Performance Quality Checklist: Document Upload and Management

**Purpose**: Validate that the requirements for performance SLOs, scalability, load testing profiles, and resource management are complete, unambiguous, and measurable.
**Created**: 2026-06-12
**Feature**: [spec.md](../spec.md) §5 Performance Requirements
**Constitution Reference**: v1.1.0, Principle III (Rendimiento y Escalabilidad) + V.4 (Pirámide de Rendimiento)

## Performance SLOs (from spec §5)

- [x] CHK001 - **Upload SLO**: Is the upload latency SLO (≤ 30s for 25 MB on LAN 100 Mbps) explicit and measurable? [Measurability, AC-5.1.1, SC-006]
- [x] CHK002 - **Upload baseline**: Is the network baseline (LAN 100 Mbps) explicit, or is the SLO environment-agnostic? [Clarity, AC-5.1.1]
- [x] CHK003 - **List SLO**: Is the list rendering SLO (≤ 2s for 500 documents per user) defined? [Measurability, AC-5.2.1, SC-008]
- [x] CHK004 - **List pagination**: Is the pagination (page size 25, max 50) explicit and tied to the SLO? [Clarity, FR-013, AC-2.1.1]
- [x] CHK005 - **Search SLO**: Is the search latency SLO (≤ 2s p95 for 10k documents) defined? [Measurability, AC-5.3.1, SC-007]
- [x] CHK006 - **Search baseline**: Is the search baseline (10k documents per user) explicit? [Clarity, AC-5.3.1]
- [x] CHK007 - **Preview SLO**: Is the preview rendering SLO (≤ 3s for PDF 2 MB) defined? [Measurability, AC-5.4.1, SC-009]
- [x] CHK008 - **Preview size limit**: Is the size limit for preview (10 MB) defined? [Clarity, §StakeholderDoc §9.3]

## Error Rate and Throughput

- [x] CHK009 - **Error rate under load**: Is the error rate threshold (< 0.1% under nominal load) defined? [Measurability, V.4]
- [x] CHK010 - **Error rate under stress**: Is the error rate threshold under stress (< 0.5%) defined? [Clarity, V.5]
- [x] CHK011 - **Throughput target**: Is the RPS (requests per second) target documented for each endpoint? [Gap, V.4]
- [x] CHK012 - **Concurrent users**: Is the target concurrent user count defined (N for nominal, 2N for stress, 5N for spike)? [Clarity, V.4]

## Resource Utilization

- [x] CHK013 - **Memory growth (soak)**: Is the memory growth threshold (< 10% over baseline in 24h soak) defined? [Measurability, V.4]
- [x] CHK014 - **CPU saturation (load)**: Is the CPU saturation threshold (< 75% average, < 90% p95) under load defined? [Measurability, V.4]
- [x] CHK015 - **GC pressure**: Are the GC pressure metrics (Gen0/1/2 collections, LOH allocations) tracked? [Gap, V.4]
- [x] CHK016 - **Memory allocation (hot path)**: Are the memory allocations per operation tracked (BenchmarkDotNet)? [Clarity, V.4 Micro]

## Performance Test Pyramid (V.4)

- [x] CHK017 - **Micro benchmarks**: Are micro-benchmarks required for hot-path methods (`GuidGenerator`, `FilePathBuilder`, `MimeTypeValidator`)? [Coverage, V.4]
- [x] CHK018 - **Component perf**: Is the per-endpoint latency test (NBomber or k6) required for each of the public endpoints? [Coverage, V.4]
- [x] CHK019 - **Integration perf**: Is the throughput test (NBomber + Testcontainers SQL Server) required for upload endpoint? [Coverage, V.4]
- [x] CHK020 - **Contract perf**: Is the latency SLO per endpoint in the Pact contract required? [Clarity, V.4]
- [x] CHK021 - **E2E system perf**: Is the E2E load test (k6 or JMeter) required? [Coverage, V.4]
- [x] CHK022 - **Resilience perf**: Are stress (break limits), spike (sudden peaks), and soak (sustained) tests required? [Coverage, V.4]

## 5 Load Profiles

- [x] CHK023 - **Smoke profile**: Is the smoke profile (1 VU, 1 min) defined with the assertion (no errors)? [Completeness, V.4]
- [x] CHK024 - **Load profile**: Is the load profile (N VUs, 10 min) defined with the assertion (capacity nominal)? [Completeness, V.4]
- [x] CHK025 - **Stress profile**: Is the stress profile (2N VUs, 5 min) defined with the assertion (break point)? [Completeness, V.4]
- [x] CHK026 - **Spike profile**: Is the spike profile (0 → 5N VUs in 30s, hold 2 min) defined with the assertion (elasticity)? [Completeness, V.4]
- [x] CHK027 - **Soak profile**: Is the soak profile (N VUs, 24h+) defined with the assertion (no memory leaks, no degradation > 10%)? [Completeness, V.4]

## EF Core Performance (per Constitution III)

- [x] CHK028 - **AsNoTracking usage**: Is the use of `AsNoTracking()` on read-only queries mandated? [Clarity, plan §Complexity Tracking]
- [x] CHK029 - **N+1 prevention**: Is the prohibition of N+1 queries explicit (use `.Include()` or projections)? [Clarity, Constitution III]
- [x] CHK030 - **Pagination default**: Is the default pagination (page size 25) explicit? [Clarity, FR-013]
- [x] CHK031 - **Connection pool**: Is the EF Core connection pool (Max Pool Size) configurable, with a default? [Gap, Constitution III]
- [x] CHK032 - **DB indexes**: Are the recommended indexes (FKs, WHERE/ORDER BY columns) defined? [Clarity, data-model.md]

## Caching (per Constitution III)

- [x] CHK033 - **Memory cache TTL**: Is the default TTL for `IMemoryCache` (≤ 5 min) defined? [Clarity, Constitution III]
- [x] CHK034 - **Cache invalidation**: Is the cache invalidation strategy defined (event-based, e.g., SignalR for live updates)? [Clarity, Constitution III]
- [x] CHK035 - **Cache scope**: Is the scope of cached data defined (user-specific vs global)? [Gap, plan §Cache]
- [x] CHK036 - **Distributed cache**: Is the future path to `IDistributedCache` documented (when scaling out)? [Clarity, Constitution III]

## Blazor Server Specific (per Constitution III)

- [x] CHK037 - **Circuit memory**: Are the per-circuit memory limits defined? [Gap, plan §Risks]
- [x] CHK038 - **RenderMode**: Is the default `RenderMode = Server` (no prerender) defined? [Clarity, Constitution III]
- [x] CHK039 - **StateHasChanged discipline**: Is the use of `StateHasChanged()` (only when needed) explicit? [Clarity, Constitution III]
- [x] CHK040 - **MemoryStream pattern**: Is the Blazor-specific MemoryStream pattern documented? [Clarity, §StakeholderDoc Blazor-Specific]

## Static Asset Performance

- [x] CHK041 - **Compression**: Is the compression (gzip/brotli) of static assets enabled? [Gap, plan §Performance]
- [x] CHK042 - **Cache headers**: Are the cache headers for static assets (long TTL with versioning) defined? [Gap, Constitution III]
- [x] CHK043 - **CDN**: Is the future CDN integration documented (for production)? [Clarity, §Open Questions]

## Observability for Performance

- [x] CHK044 - **Prometheus metrics**: Are the metrics (`_antivirus_scan_duration_seconds`, request latency histograms) defined? [Coverage, §9.5]
- [x] CHK045 - **Alerting thresholds**: Are the alerting thresholds (p95 > 10s) defined? [Clarity, §9.5]
- [x] CHK046 - **APM integration**: Is the Application Performance Monitoring (APM) tool defined or marked as future? [Gap, Constitution III]
- [x] CHK047 - **Distributed tracing**: Is the trace context propagation (W3C traceparent) documented? [Gap, Constitution III]

## Performance Test Reproducibility

- [x] CHK048 - **Warm-up**: Is the warm-up period for benchmarks defined (BenchmarkDotNet default or custom)? [Clarity, BenchmarkDotNet]
- [x] CHK049 - **Test data seeding**: Is the data seeding strategy for performance tests (10k documents) documented? [Clarity, plan §Performance]
- [x] CHK050 - **Run-to-run variance**: Is the acceptable variance (CV%) for performance tests defined? [Gap, performance testing best practice]

## Notes

- 50 items covering SLOs, error rate, resource utilization, 5 load profiles, EF Core, caching, Blazor, observability, and reproducibility.
- Strong coverage of Constitution III (Rendimiento) and V.4 (Pirámide de Rendimiento).
- Gaps: APM tool selection (CHK046), distributed tracing (CHK047), variance tolerance (CHK050) — all marked as `Gap` to be addressed during planning/implementation.
- Performance testing is one of the best-covered areas of this spec.
