# Powwow Lang — JavaScript port

A TypeScript port of the Powwow interpreter, intended to render templates in the browser in real time (live preview) alongside the canonical .NET interpreter used for server-side rendering.

The non-negotiable goal is **output parity** with .NET: for the same template and data, this port must produce byte-identical output. See [js-port-parity.md](./js-port-parity.md) for the full list of behaviors that differ between .NET and JavaScript and must be reimplemented (not delegated to native JS).

## Status

Early, incremental, and built test-first — each layer lands with passing parity tests before the next begins.

| Component | Status |
|---|---|
| `PowwowNumber` (faithful .NET `System.Decimal`: scale preservation, banker's rounding) | ✅ implemented + tested |
| Value types (String, Boolean, Array, Object, Function, Type) + `Output()`/`jsonSerialize()` forms + `PowwowValue` cell | ✅ implemented + tested |
| Lexer (port of `Lexer.cs`, incl. whitespace-control tokens, comments, literal blocks) | ✅ implemented + tested |
| AST nodes + Parser (port of `Parser.cs`) | ✅ implemented + tested |
| Execution contexts + evaluator + top-level `Interpreter` (all directives, operators, closures, recursion, includes) | ✅ implemented + tested (end-to-end) |
| Built-in functions | 🟡 ~38 of ~70 ported; DateTime family, JSON, encoding, uri, order/group remain |
| `DateTime` value + parsing + .NET-compatible `format()` strings | ⬜ |
| Golden-file conformance corpus (shared with .NET, .NET as source of truth) | ⬜ |

69 parity tests passing (`number`, `values`, `lexer`, `parser`, `evaluator`). The evaluator renders whole templates; the end-to-end suite covers arithmetic scale, whitespace control, if/for/capture/literal/comments, closures, recursion, reference semantics, and includes.

## Why a custom number type

A general decimal library (decimal.js, big.js) is **not** sufficient: those normalize away trailing-zero scale, so they cannot reproduce `2.5 + 1.5` → `"4.0"` or `10 / 4` → `"2.5"`. `PowwowNumber` models the .NET `decimal` directly (BigInt mantissa + explicit scale) and replicates .NET's per-operation scale rules and round-half-to-even. It has no runtime dependencies.

## Running the tests

Requires Node 22+ (uses built-in TypeScript type-stripping and the built-in test runner — no build step, no `npm install`):

```sh
node --experimental-strip-types --test "src/**/*.test.ts"
```

## Layout

Mirrors the .NET project under `../dotnet/Interpreter` where practical, so the two implementations are easy to diff:

```
src/
  number.ts        PowwowNumber (System.Decimal port)
  number.test.ts   parity tests
```
