# Roadmap

A working list of potential improvements to Powwow. Nothing here is committed work — it is a place to capture and prioritize ideas. Items are grouped by theme with a rough effort estimate. Two areas have their own detailed documents:

* [Dataverse: current behavior and proposed improvements](docs/dataverse-future.md)
* [JavaScript port: .NET parity notes](src/js/js-port-parity.md)

## Near-term, high impact

These are small and immediately felt by anyone writing templates — human or LLM.

* **Null-safe access** — a `default(value, fallback)` built-in and/or safe-navigation accessor. *(medium)* The top ergonomics win generally, and **essential** for Dataverse, where `fetch` drops null attributes so field access throws. See [Dataverse #1](docs/dataverse-future.md).
* **Loop metadata** — expose an index / first / last / count in `for` (e.g. `{{ for x, i in xs }}` or a `loop` object). *(low/medium)*
* **String built-ins** — `replace`, `padLeft`/`padRight`, `repeat`. *(low)* `replace` is a real hole for templating.
* **Math built-ins** — `abs`, `min`, `max`, `pow`, `sqrt`. *(low)*
* **Array built-ins** — `sum`, `count`, `avg`, `distinct`, `reverse`, plus `contains`/`indexOf` for arrays and array subscript (`xs[0]`). *(low)* Aggregates matter for summarizing Dataverse record sets.

## Language features

* **Structural equality** — `equals(a, b)` deep compare, since `==` is reference identity for arrays/objects. *(low)*
* **Safe-navigation operator** — `?.`-style field access as an alternative to `default()`. *(medium)*
* **Date arithmetic** — a `diff`/`between` (inverse of the `addX` family), `dayOfWeek`, and a `datetime(string, format)` overload for explicit parsing. *(low/medium)*
* **Time-zone conversion** — `toTimeZone`/`toLocal`; Dataverse dates arrive as UTC but notifications want local time. *(medium)* Coordinate with the [JS-port date parity](src/js/js-port-parity.md).
* **Number/currency formatting** — a `format(number, pattern)` for currency and grouping. *(low/medium)*
* **Regex** — a `match`/`replace` with patterns. *(medium)*

## Safety for untrusted / generated templates

Relevant because templates may be authored by non-engineers or produced by an LLM, and run server-side in a plugin.

* **Resource limits** — caps on loop iterations, output size, and evaluation time (the recursion cap exists; these do not). *(medium)*
* **`xmlEncode`** — for safely interpolating values into FetchXML (and any XML); prevents breakage and injection. *(low)* See [Dataverse #3](docs/dataverse-future.md).
* **Auto-escaping mode** — optional HTML-safe-by-default rendering with explicit opt-out, for the email use case. *(medium)*
* **`fetch` governance** — memoize identical queries within a render and cap the query count, to respect plugin execution and API limits. *(medium)* See [Dataverse #7](docs/dataverse-future.md).

## Tooling & developer experience

* **Syntax-highlighting grammar** — a TextMate/Prism grammar so templates render with color on GitHub and in editors. *(low)*
* **CLI / REPL** — `powwow render template.pow data.json` and an interactive REPL; doubles as the verified-examples / golden-file harness the docs need. *(medium)*
* **Language server (LSP)** — completions for the built-ins, hover docs, and diagnostics from the existing `SourceLocation`. *(large)*

## Bigger bets

* **JavaScript port** — render templates in the browser in real time for live preview. The major work is .NET output parity, especially numbers and dates. See [JavaScript port: .NET parity notes](src/js/js-port-parity.md). *(large)*
* **Modern .NET build with compiler targets** — already named as a goal in [the overview](docs/overview.md); the current interpreter is intentionally unoptimized. *(large)*
* **Dataverse integration improvements** — preserve the `EntityReference` display name, richer value handling, and the items above. See [Dataverse future](docs/dataverse-future.md). *(varies)*

## Project hygiene

* Add `LICENSE`, `CHANGELOG.md`, and `CONTRIBUTING.md`. *(low)*
* A conformance / golden-file test corpus shared by the .NET interpreter and the future JS port. *(medium)*
* A docs-wide markdownlint pass (fence language hints, blank lines) if lint-clean docs are wanted. *(low)*

## Verification debt

The example outputs in the documentation were derived by reading the interpreter, not by running it (the project is .NET Framework and needs MSBuild/Visual Studio plus the Xrm packages to build). A local build or the CLI above would let the doc examples be machine-verified; spot-check `toJson` formatting, `urlEncode` spaces, decimal scale, and banker's rounding first.
