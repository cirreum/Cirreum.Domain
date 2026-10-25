# Cirreum.Domain 5.1.0 — Domain telemetry that records every dispatch, under the platform's names

## Why this release exists

A review of Domain's instrumentation found three defects in what reached a metrics backend, one in
how `RemoteClient` traces nest, and a naming scheme that no other Cirreum meter shares. The fixes
change the series a dashboard sees, and so do the renames. Shipping them together means dashboards
are reworked once. A follow-up test pass then found two defects outside telemetry —
`RemoteClient.WithRetryAsync` and the grant orchestrator's lifetime — which ship here too.

Nothing in this release changes a signature. Every public constant keeps its name.

## Conductor metrics no longer depend on tracing

Operation dispatch and domain-event publishing recorded their metrics inside a method that
returned early when the span was `null`:

```csharp
if (activity is null) {
	return;
}
```

`ActivitySource.StartActivity` returns `null` whenever no tracing listener wants the span — no
listener registered, or a sampler that dropped it. The metrics shared that fate:

| Host configuration | Before | After |
| ------------------ | ------ | ----- |
| Metrics on, tracing off | No Conductor metrics at all | Every dispatch recorded |
| Tracing with a 10% sampler | Counters and histograms showed ~10% of traffic | Every dispatch recorded |
| Tracing at 100% | Correct | Correct |

Metrics are now recorded on every dispatch and publish. Span calls remain no-ops when the span is
`null`.

## Cache metrics: the cache key is no longer a tag

`cirreum.cache.operations` and `cirreum.cache.duration` carried `cirreum.cache.caller`, set to the
cache key. A metric series exists for every distinct combination of tag values, and cache keys are
not a small set:

| Consumer | Key shape | Distinct values |
| -------- | --------- | --------------- |
| Query caching | per query and parameter set | grows with data |
| Grant resolution | `grant:v{n}:{callerId}:{feature}:{signature}` | grows with users |

The result was unbounded series growth, and caller ids exported to the metrics backend. Cache
metrics are now tagged with `cirreum.cache.status` (`hit` / `miss`) and `cirreum.cache.consumer`
only.

## RemoteClient metrics: the request path is no longer a tag

`url.path` held the endpoint as called, so `api/orders/1001` and `api/orders/1002` were separate
series. The tag is removed from the metrics and stays on the span.

## RemoteClient: the request starts inside its span

Every `RemoteClient` verb passed `this.Client.SendAsync(...)` (or `GetAsync` / `DeleteAsync`) as an
argument. C# evaluates arguments before the call, so the request was already running when the
processor started the `HTTP {method}` span:

```csharp
// Before — the send begins here, under the caller's activity
return this.ProcessWithTelemetryAsync(method, endpoint, this.Client.SendAsync(request, ct));

// After — the processor invokes the send after StartActivity, inside its try block
return this.ProcessWithTelemetryAsync(method, endpoint, () => this.Client.SendAsync(request, ct));
```

| Effect before | After |
| ------------- | ----- |
| An instrumented `HttpClient` span was a sibling of the `RemoteClient` span | It is the span's child |
| Recorded duration excluded the synchronous portion of the send | Duration covers the whole request |
| A send that threw synchronously escaped `Result` handling | It is handled like any other request failure |

## RemoteClient: `WithRetryAsync` returns its result

`WithRetryAsync` was written for operations that throw. The `RemoteClient` verbs return a failed
`Result` instead, so when every attempt failed, the exception the loop meant to rethrow had never
been recorded:

```csharp
throw lastException!;   // lastException is null — throws NullReferenceException
```

It also retried every failed result, transient or not.

| Case | Before | After |
| ---- | ------ | ----- |
| Every attempt fails transiently | `NullReferenceException` | The last failed `Result` |
| 404, validation, or other non-transient failure | Retried with 1 s, 2 s, 4 s delays, then `NullReferenceException` | Returned at once |
| Delay after the final attempt | Yes | No |
| `maxAttempts` below 1 | `NullReferenceException` | `ArgumentOutOfRangeException` |

A failed result is now treated like a thrown exception: retried when `IsTransient` returns `true` for
its error and attempts remain. Override `IsTransient` to widen what counts as transient — an
`ApiException` from a 503, for example.

## Grants: the orchestrator is always scoped

`AddOperationGrants<TResolver>(lifetime)` applied `lifetime` to both the resolver and
`OperationGrantFactory`. The factory memoizes resolved grants in a plain dictionary meant to live for
one request. Registered as a singleton, that dictionary lived for the process:

* A revoked grant stayed granted until restart. Neither the L2 cache's expiry nor
  `IOperationGrantCacheInvalidator` reaches the in-memory copy.
* Concurrent requests read and wrote one unsynchronized `Dictionary`.

The factory is now registered as scoped, and `lifetime` applies to the resolver only. A singleton
resolver remains a supported choice.

## Instrument names

Every other Cirreum meter names its instruments `cirreum.<area>.*` — `cirreum.authz.*`,
`cirreum.authn.*`, `cirreum.identity.*`, `cirreum.cache.*`. Conductor and `RemoteClient` were the
exceptions.

| Before | After |
| ------ | ----- |
| `conductor.operations.total` | `cirreum.conductor.operations` |
| `conductor.operations.failed` | `cirreum.conductor.operations.failed` |
| `conductor.operations.canceled` | `cirreum.conductor.operations.canceled` |
| `conductor.operations.duration` | `cirreum.conductor.operation.duration` |
| `conductor.domain_events.total` | `cirreum.conductor.domain_events` |
| `conductor.domain_events.failed` | `cirreum.conductor.domain_events.failed` |
| `conductor.domain_events.no_handlers` | `cirreum.conductor.domain_events.no_handlers` |
| `conductor.domain_events.duration` | `cirreum.conductor.domain_event.duration` |
| `remote_services.client.requests` | `cirreum.remote_services.client.requests` |
| `remote_services.client.requests.failed` | `cirreum.remote_services.client.requests.failed` |
| `remote_services.client.requests.canceled` | `cirreum.remote_services.client.requests.canceled` |
| `remote_services.client.request.duration` | `cirreum.remote_services.client.request.duration` |

The `.total` suffix is gone. A Prometheus exporter appends `_total` to every counter, so
`conductor.operations.total` exported as `conductor_operations_total_total`. The new counter
exports as `cirreum_conductor_operations_total`.

Durations stay in milliseconds, matching every other Cirreum meter.

## Tags

| Change | Detail |
| ------ | ------ |
| Domain-event tags are snake_case | `domainEvent.type` → `domain_event.type`, likewise `.strategy`, `.handler_count`, `.status`, `.canceled` |
| Publish span renamed | `Publish domainEvent` → `Publish Domain Event`, matching `Dispatch Operation` |
| Boolean outcome tags removed from metrics | `operation.succeeded` / `.failed` / `.canceled` and `remote_services.request.*` equivalents restated the status tag — filter on `operation.status` or `remote_services.request.status` |
| Canceled domain events | now carry `domain_event.strategy` and `domain_event.handler_count` |

The `failed` and `canceled` booleans remain on the spans, where they mark the span itself.

## Compatibility

* **No signature changes.** Public constants keep their names. New constants for the domain-event
  tags are additive.
* **`const` values are compiled into the assemblies that read them.** An application that
  references a `ConductorTelemetry` or `CacheTelemetry` constant sees the new value after it is
  rebuilt against this version.
* **`CacheTelemetry.CallerTag`, the `caller` parameter of `CacheTelemetry.RecordOperation`, and
  `ConductorTelemetry.OperationSucceededTag`** remain for compatibility and are not recorded.
* **Dashboards and alerts** need the instrument and tag renames above. Queries on the old names
  return nothing from this version on, rather than reporting wrong values. Conductor metrics may
  rise sharply after upgrading if your host traces with sampling, or appear for the first time if
  it does not trace at all. Neither reflects a change in traffic.
* **Traces:** where `HttpClient` instrumentation is enabled, its span now nests under the
  `RemoteClient` span.
* **`WithRetryAsync`:** a caller that relied on non-transient failures being retried should
  override `IsTransient`. A caller that caught `NullReferenceException` now receives the failed
  `Result`.
* **`AddOperationGrants`:** no registration change is needed. A singleton resolver now sits behind
  a scoped orchestrator, so revoked grants take effect on the next request.
