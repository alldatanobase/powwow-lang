# JavaScript port: .NET parity notes

A JavaScript port of the interpreter is planned so templates can be rendered in the browser in real time (live preview), alongside the existing .NET interpreter used for server-side rendering. The hard requirement is **output parity**: for the same template and data, the browser preview and the server render must produce byte-identical strings. If they drift, a user tweaks a template until the preview looks right and then gets a different result in the actual email.

This document enumerates the places where naive JavaScript will *not* match the .NET interpreter, and the approach each requires. The behaviors below are taken from the current implementation (`src/dotnet/Interpreter/Types/` and the built-ins in `FunctionRegistry.cs`).

**The golden rule:** reimplement these behaviors in the port; do not delegate to JavaScript's native number/date/string formatting. Back the port with a shared golden-file corpus (template + data → expected output) that both implementations must pass.

## Numbers — the deepest divergence

Powwow's `Number` is a .NET **`decimal`**: 128-bit, base-10, with **preserved scale**. JavaScript has only IEEE-754 **`double`**. This single difference is the largest source of parity risk.

| Concern | .NET behavior | Naive JS | Required approach |
|---|---|---|---|
| Rendering | `decimal.ToString()` preserves trailing scale: `2.5 + 1.5` → `4.0`, `10 / 4` → `2.5`, but `2 + 3` → `5`. Small magnitudes render in full: `-0.000000004` (no exponent). | `(4).toString()` → `"4"`; `(0.000000004).toString()` → `"4e-9"`. | Use a decimal library (e.g. decimal.js / big.js) for the numeric type, and replicate `decimal.ToString()` output formatting (scale tracking, no exponential notation in these ranges). |
| Arithmetic precision | Base-10 decimal: `0.1 + 0.2` → `0.3` exactly. | `0.1 + 0.2` → `0.30000000000000004`. | All `+ - * /` go through the decimal type, not native `+`. |
| Rounding mode | `round()` and integer coercion (`Convert.ToInt32` in `mod`, `at`, `substring`, etc.) use **banker's rounding** (half-to-even): `round(2.5)` → `2`, `round(3.5)` → `4`. | `Math.round(2.5)` → `3` (half-up). | Implement half-to-even rounding explicitly; never use `Math.round`. |
| `floor` / `ceil` | `Math.Floor` / `Math.Ceiling` on decimal. | Aligns for in-range values. | Apply on the decimal type to avoid precision loss. |
| Parsing | `number(s)` uses `decimal.TryParse` (culture-sensitive, decimal-precise). | `Number(s)` / `parseFloat` differ on format and precision. | Parse into the decimal type with culture-invariant rules (decide and pin the culture — see Strings). |
| Division by zero | Raises a `TemplateEvaluationException`. | `1 / 0` → `Infinity`. | Throw to match. |

## Dates

Powwow's `DateTime` has three behaviors that JavaScript `Date`/`Intl` do not reproduce.

| Concern | .NET behavior | Naive JS | Required approach |
|---|---|---|---|
| Default rendering | `Output()` uses `ToString("o")` (round-trip): `2024-01-08T10:10:10.0000000` — **7** fractional digits, and **no `Z`/offset** for an unspecified-kind value (the common case from `datetime("…")`). | `toISOString()` → `2024-01-08T10:10:10.000Z` — 3 digits, always `Z`. | Implement the `"o"` format exactly: 7 fractional digits, offset suffix only when the value carries a zone. |
| `format(d, fmt)` | `fmt` is a **.NET custom format string** (`yyyy`, `MM`, `MMM`, `d`, `HH`, `mm`, `ss`, …) passed to `DateTime.ToString(fmt)`. | No native equivalent; `Intl.DateTimeFormat` uses different tokens and locale rules. | Reimplement a .NET-compatible format-string evaluator (including month/day name tables and the invariant culture). |
| Parsing | `datetime(s)` uses `DateTime.Parse` — lenient, culture-aware, accepts the space form `"2024-01-08 10:10:10"`. | `new Date("2024-01-08 10:10:10")` is implementation-dependent and may return `Invalid Date`. | Implement a parser matching the accepted .NET formats; do not rely on `new Date(string)`. |
| Zone / kind | A `DateTime` carries a `Kind` (Unspecified/Utc/Local) but no offset data; equality is by instant/ticks. Dataverse values arrive as **UTC**. | `Date` is always a UTC instant displayed in the host's local zone. | Model an explicit, zone-aware date value; never let the browser's local zone leak into rendering. This is also a prerequisite for the proposed `toTimeZone`/`toLocal` functions (see [Dataverse future](dataverse-future.md)). |
| `now()` / `utcNow()` | Server clock and zone. | Browser clock and zone. | Define which clock the preview uses; for deterministic preview, allow the host to inject a fixed "now". |

## Strings

Mostly aligned (both use UTF-16 code units, so `length`, `substring`, `indexOf` match), with these exceptions:

| Concern | .NET behavior | Naive JS | Required approach |
|---|---|---|---|
| Case / trim | `toUpper`/`toLower`/`trim` are **culture-sensitive** in .NET. | `toUpperCase`/`toLowerCase`/`trim` use Unicode default-case + a different whitespace set. | Pin a single culture (recommend invariant) on both sides and match its case-mapping and the set of characters `Trim()` removes. |
| Sorting | `order(xs)` natural order uses .NET's default comparer (culture-aware string comparison); `order(xs, comparer)` reduces a numeric result via `Math.Sign`. | `Array.prototype.sort` defaults to UTF-16 code-unit order. | Reimplement the chosen comparison (ordinal vs. a fixed culture) consistently; mirror `Math.Sign` semantics for custom comparers. |
| `urlEncode` | `WebUtility.UrlEncode` encodes space as **`+`**. | `encodeURIComponent` encodes space as `%20`. | Reimplement .NET's encoding (space → `+`, and match its reserved-character set), not `encodeURIComponent`. |
| `htmlEncode` | `WebUtility.HtmlEncode` (encodes `< > & "` and more). | No native equivalent. | Match the exact set and entity forms .NET produces. |
| Number/bool → string | `string(number)` via `decimal.ToString()`; `string(bool)` → lowercase `true`/`false`. | `String(n)` / `String(b)` differ (see Numbers). | Route through the decimal renderer and the lowercase boolean form. |

## Collection rendering

The default `Output()` forms must match exactly:

* **Array**: `[a, b, c]` — elements joined by `", "`, each rendered with its own `Output()`. Note **strings are not quoted** inside this form: `["admin","engineer"]` renders as `[admin, engineer]`.
* **Object**: `{key: value, ...}` — keys are **unquoted**, joined by `", "`, values via `Output()` (also unquoted strings). **Field order matters** and follows insertion order; ensure the port preserves it (JS string-keyed objects preserve insertion order; if a `Map` is used, keep parity).
* **Function**: `lambda(a, b)` listing parameter names; built-in references render as `func<name>`.

## JSON (`toJson` / `fromJson`)

* `toJson` serializes numbers via `decimal.ToString()`, so `toJson(4.0)` produces `4.0` — valid JSON but unusual; `JSON.stringify(4.0)` produces `4`. Route through the decimal renderer.
* `toJson` serializes `DateTime` as a quoted `"o"` string; match that format.
* The `formatted: true` pretty-printer uses 4-space indentation with a specific brace/comma layout (`FunctionRegistry.FormatJson`); reproduce it if pretty output must match.
* `fromJson` maps JSON numbers to the decimal type and JSON objects/arrays to Powwow objects/arrays; keep the same mapping (and the same "drop null" behavior, if retained).

## Other surfaces

* **`uri(s)`** returns an object built from `System.Uri`. The JS `URL` API exposes different fields and slightly different values (e.g. `Query` includes the leading `?` in .NET). Map fields deliberately rather than assuming `URL` matches.
* **Error parity**: the .NET interpreter throws on missing fields, out-of-bounds `at`, divide-by-zero, type mismatches, etc. The port should fail in the same cases (the exact message need not match, but the *fact* of failure should, so previews surface the same errors).
* **Recursion / limits**: mirror `maxRecursionDepth` (default 1000) and any future evaluation caps so a template that fails server-side also fails in preview.

## Conformance strategy

1. **Share a golden corpus.** Build a directory of `(template, data, expected-output)` cases — the examples in [datatypes.md](datatypes.md), [directives.md](directives.md), and [functions.md](functions.md) are a ready starting set. Both interpreters run it in CI.
2. **Generate the expected outputs from the .NET interpreter** (it is the source of truth), then make the port match. This doubles as the verification harness the docs still need.
3. **Prioritize numbers and dates** — they account for the majority of divergences and the highest user-visible impact.
