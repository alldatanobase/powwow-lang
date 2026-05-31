# Powwow

Powwow is a small, embeddable templating language for .NET. It was built to render highly dynamic, data-driven text — originally HTML email templates inside Microsoft Dataverse plugins — with a syntax that reads like JavaScript so you can pick it back up months later and be productive in minutes.

The interpreter is written in **.NET Framework with zero third-party dependencies**, so it can be compiled directly into existing assemblies (such as Dataverse plugins) where only Microsoft dependencies are available.

```
{{- let user = obj(name: "Ada", premium: true) -}}
Hi {{ user.name }}, {{ if(user.premium, "thanks for being premium!", "enjoy your free account!") }}
```
```
Output:
Hi Ada, thanks for being premium!
```

## Features

* Variables (`let`) and mutation (`mut`)
* Conditionals (`if` / `elseif` / `else`) and loops (`for`)
* Numbers, strings, booleans, arrays, prototype-less objects, and dates
* Lambdas, first-class functions, closures, and recursion
* Higher-order helpers (`map`, `filter`, `reduce`, `order`, `group`, …)
* ~70 built-in functions for strings, numbers, dates, JSON, and web encoding
* Template composition via `include`
* Whitespace control for clean output
* Easy host integration: bind data, register custom functions, and resolve templates from your own store

## A quick tour

Interpolate data with `{{ ... }}`. Directives that begin or end with a hyphen (`{{- ... -}}`) trim the surrounding whitespace and newline, which keeps generated output tidy:

```
{{- let xs = ["red", "green", "blue"] -}}
{{- for x in xs -}}
- {{x}}
{{ /for -}}
```
```
Output:
- red
- green
- blue
```

Values have familiar types and a library of built-ins:

```
{{- let scores = [90, 75, 88] -}}
{{- let total = reduce(scores, (acc, s) => acc + s, 0) -}}
Highest: {{ last(order(scores)) }}
Total: {{ total }}
```
```
Output:
Highest: 90
Total: 253
```

## Using it from .NET

Construct an `Interpreter` and call `Interpret` with your template and a data object:

```csharp
using PowwowLang.Runtime;
using System.Collections.Generic;
using System.Dynamic;

var interpreter = new Interpreter();

var data = new ExpandoObject();
((IDictionary<string, object>)data)["name"] = "Ada";

string output = interpreter.Interpret("Hello, {{name}}!", data);
// output: "Hello, Ada!"
```

You can extend the language for your host:

* **Custom functions** — register your own built-ins with `interpreter.RegisterFunction(...)`.
* **Template composition** — supply an `ITemplateResolver` so `{{ include header }}` can pull templates from your own store (files, a database, Dataverse, etc.). Circular references are detected and reported.
* **Dataverse** — supply an `IDataverseService` to enable the `fetch` function for FetchXML queries.

## Documentation

* [Overview](docs/overview.md) — motivation and design philosophy
* [Data types](docs/datatypes.md) — numbers, strings, booleans, arrays, objects, and dates
* [Operators](docs/operators.md) — arithmetic, comparison, logical, and unary operators
* [Template syntax and directives](docs/directives.md) — directives, whitespace control, conditionals, loops, capture, and composition
* [Built-in functions](docs/functions.md) — the full function reference
* [Embedding](docs/embedding.md) — host integration: data binding, custom functions, composition, and Dataverse
* [Grammar](grammar.md) — the formal language grammar
* [llms.txt](llms.txt) — a dense, single-file reference designed to be loaded as context for an LLM

Forward-looking:

* [Roadmap](ROADMAP.md) — potential improvements and future work
* [Dataverse: current behavior and proposed improvements](docs/dataverse-future.md)
* [JavaScript port: .NET parity notes](docs/js-port-parity.md)

## Status

This project is in an early stage. The current .NET interpreter is intentionally simple — it has no performance goals or optimizations and is used to render relatively small templates at modest request volumes. A future goal is a modern .NET build with sensible compiler targets. Use at your own risk.
