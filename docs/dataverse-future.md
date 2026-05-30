# Dataverse: current behavior and proposed improvements

Powwow is most often used to render Dataverse-driven notifications: a template pulls records with `fetch` (FetchXML), applies some logic, and produces an email or alert. This document records how the Dataverse integration behaves **today**, and a set of **proposed** improvements with the rationale for each. Nothing here is implemented yet — it is a working list to prioritize from.

The proposals are grounded in the current `fetch` conversion, implemented in [`DataverseService.cs`](../src/dotnet/Interpreter/Lib/DataverseService.cs).

## How `fetch` results look today

`fetch(fetchXml)` returns an **array of objects**, one per entity, whose fields are the queried attributes. Attribute values are converted as follows:

| Dataverse type | Becomes | Notes |
|---|---|---|
| (null / unset attribute) | *(field omitted)* | The key is **not added** to the object at all. |
| `EntityReference` | `String` | The record's **GUID only** — `Name` and `LogicalName` are discarded. |
| `Guid` | `String` | |
| `OptionSetValue` | `Object` `{ value, label }` | `value` is the numeric option; `label` is the formatted display text. |
| `Money` | `Number` | The raw decimal amount, with no currency formatting. |
| `AliasedValue` | *(unwrapped)* | Link-entity columns and FetchXML aggregates resolve to their underlying value. |
| `string` / `bool` / `DateTime` | `String` / `Boolean` / `DateTime` | `DateTime` is passed through as-is (typically **UTC**). |
| other numerics | `Number` | |

Understanding this table is the key to writing correct templates today, and it is the reason for most of the proposals below.

## Proposed improvements

### 1. Null-safe field access — *highest priority*

**Why.** Null attributes are dropped from the result object entirely, so a fetched row simply has no key for any field that was null in Dataverse. Because Powwow raises an error on missing-field access, almost every `{{ row.field }}` against fetched data must currently be guarded:

```
{{ if contains(row, "telephone1") }}{{ row.telephone1 }}{{ else }}N/A{{ /if }}
```

This is verbose and easy to forget, and a single unguarded access aborts the whole render — a poor failure mode for a notification.

**Proposed.** A `default(value, fallback)` built-in and/or a safe-navigation accessor, so the common case collapses to `{{ default(row.telephone1, "N/A") }}`. This is the single biggest day-to-day win for Dataverse templates. *(Effort: medium — touches evaluation of field access.)*

### 2. Preserve the related-record name on `EntityReference`

**Why.** A lookup is converted to just its GUID, so a template cannot display "Owner: Jane Smith" — only the meaningless `Owner: 7b3...`. Notifications almost always want the *name* of a related record, which currently forces a second `fetch` per lookup (or a link-entity in the FetchXML).

**Proposed.** Convert `EntityReference` to an object `{ id, name, logicalName }` instead of a bare string. `EntityReference.Name` is already available on the SDK object; this is a converter change with no language impact, and it removes a whole class of extra queries. *(Effort: low/medium — `DataverseService` only. Note: a small breaking change for any template that uses the reference as a plain GUID string; a transitional period or a config flag may be warranted.)*

### 3. `xmlEncode` for building FetchXML safely

**Why.** Templates frequently build FetchXML by concatenating in template values — a status, a name, a date range. There is an `htmlEncode` built-in but no XML-safe encoder, so a value containing `&`, `<`, `>`, or a quote will break the query, and an attacker-influenced value is a FetchXML-injection vector.

**Proposed.** An `xmlEncode(s)` built-in for interpolating values into FetchXML (and any XML output). Cheap, and it closes a correctness-and-security gap that is specific to how Powwow is used here. *(Effort: low.)*

### 4. Time-zone conversion for dates

**Why.** Dataverse returns `DateTime` values in UTC, and they flow through unchanged. Notifications are read by people who expect their *local* time, but there is currently no way to convert — only `format`, `now`, `utcNow`, and the `addX`/`rangeX` family, none of which shift time zones.

**Proposed.** Conversion functions such as `toTimeZone(d, tzId)` / `toLocal(d)` (and possibly a `utcOffset`), so a template can render a UTC instant in the recipient's zone. *(Effort: medium. See the cross-platform caveat below — this interacts with the planned JavaScript port.)*

### 5. Number and currency formatting

**Why.** `Money` becomes a raw `Number`, so rendering a human-readable amount ("$1,234.50") means manual string work today. `format` exists for dates but not for numbers.

**Proposed.** A number-formatting built-in — e.g. `format(number, pattern)` for currency, thousands separators, and fixed decimals — so monetary and quantity fields in notifications read naturally. *(Effort: low/medium.)*

### 6. Array aggregates for summarizing record sets

**Why.** Notifications routinely summarize a fetched set — "5 open cases totaling $12,300, grouped by owner." `group`, `filter`, and `reduce` exist, but the common aggregates must be hand-rolled with `reduce` each time.

**Proposed.** Add `sum`, `count`, `avg`, and `distinct` (and `min`/`max` over arrays). These compose directly with `fetch` output and make summary notifications concise. *(Effort: low.)*

### 7. `fetch` governance: caching and limits

**Why.** Powwow templates run inside plugins, which are subject to the 2-minute execution limit and Dataverse service-protection API limits. A `fetch` placed inside a `for` loop produces an N+1 query explosion that can blow those limits and fail the whole operation.

**Proposed.**
* **Memoize identical `fetch` calls** within a single render so repeated queries cost one round trip.
* **A per-render `fetch` count cap** (configurable on the interpreter, like `maxRecursionDepth`) that fails fast with a clear error instead of silently hammering the API.

*(Effort: medium.)*

## Already good (document, don't change)

* **OptionSet handling** — the `{ value, label }` shape is exactly what notifications need (`{{ row.statuscode.label }}`); it only needs to be documented, not changed.
* **Aliased values** — FetchXML aggregates and link-entity columns already unwrap cleanly.

## Cross-platform caveat (JavaScript port)

A front-end JavaScript port of the interpreter is planned so templates can be previewed in the browser in real time. Several proposals above — especially **#4 (time zones)** and **#5 (number formatting)** — depend on behavior that differs between .NET and JavaScript (the .NET `decimal` numeric type, .NET date format strings, and time-zone handling). To keep server-rendered and browser-previewed output identical, these features should be specified in a platform-neutral way rather than delegating to each runtime's native formatting. See the JavaScript-port parity notes (to be written) for the full list of divergences.
