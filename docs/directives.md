# Template syntax and directives

A Powwow template is ordinary text with **directives** embedded in it. Everything outside a directive is emitted verbatim; a directive is evaluated and, depending on its kind, may emit a value, control what is emitted, or do neither.

```
Hello, {{ name }}!
```

This page covers the directive syntax, whitespace control, and every directive: comments, literal blocks, variables, conditionals, loops, capture, and template composition. For the values directives operate on, see [data types](datatypes.md); for the built-in functions they can call, see the [function reference](functions.md).

## Directives

A directive is delimited by `{{` and `}}`. The most common form is an **expression** directive, which evaluates an expression and emits the result:

```
{{- let price = 42 -}}
The price is {{ price }}.
{{ 2 + 3 }}
{{ toUpper("hello") }}
```
```
Output:
The price is 42.
5
HELLO
```

Other directives are introduced by a keyword (`let`, `mut`, `if`, `for`, `capture`, `include`, `literal`) and are described below. Whitespace inside a directive is insignificant, so `{{price}}` and `{{ price }}` are equivalent.

## Whitespace control

Because directives sit inside text, the spaces and newlines *around* them are part of the output. A directive that produces no value — such as `let` — still leaves its surrounding whitespace behind:

```
{{ let name = "Ada" }}
Hello, {{ name }}!
```
```
Output:

Hello, Ada!
```

Note the blank first line: the `{{ let ... }}` emitted nothing, but the newline after it remained. To suppress surrounding whitespace, add a hyphen to the delimiter:

* `{{-` trims whitespace and the single newline **immediately before** the directive.
* `-}}` trims whitespace and the single newline **immediately after** the directive.

The hyphens can be used independently on either side. Rewriting the example with `-}}` removes the stray blank line:

```
{{- let name = "Ada" -}}
Hello, {{ name }}!
```
```
Output:
Hello, Ada!
```

Each marker trims only the one adjacent newline (plus any spaces or tabs on that side), not an entire run of blank lines. This is the same idea as the whitespace-control markers in Liquid and Jinja, and it is how most examples in this documentation keep their output clean.

## Comments

A comment is a directive whose body is wrapped in asterisks. It is never evaluated and emits nothing. Comments may span multiple lines and may themselves contain `{{` and `}}`.

```
{{* this note will not appear in the output *}}
{{- * comments can
span multiple lines * -}}
Visible text.
```
```
Output:
Visible text.
```

Whitespace-control hyphens work on comments too (`{{-* ... *-}}`).

## Literal blocks

Inside a `literal` block, everything is emitted exactly as written — directives are *not* interpreted. This is the way to show Powwow syntax in your output (for example, in documentation) without it being evaluated.

```
{{ literal }}
This {{ price }} is printed verbatim, and {{ this }} is not evaluated.
{{ /literal }}
```
```
Output:

This {{ price }} is printed verbatim, and {{ this }} is not evaluated.

```

Literal blocks may be nested; the matching `{{ /literal }}` closes the outermost block.

## Variables: `let` and assignment

`let` declares and binds a new variable. To reassign an existing variable, or update a field of an existing object via dot notation, assign to it directly with `=` — this does not create new variables or add new object fields.

```
{{- let count = 1 -}}
{{- let user = obj(name: "Ada", age: 36) -}}
{{- count = count + 1 -}}
{{- user.age = 37 -}}
{{ count }}
{{ user.name }} is {{ user.age }}
```
```
Output:
2
Ada is 37
```

An assignment may optionally be prefixed with the `mut` keyword (`{{ mut count = count + 1 }}`), which behaves identically to the plain form above.

> **Note:** The `mut` keyword is deprecated. It still works today and existing templates will continue to run, but it may be removed in a future version. Prefer plain assignment and avoid `mut` in new templates.

See [data types](datatypes.md) for how each kind of value behaves (in particular, arrays and objects are reference types).

## Conditionals: `if` / `elseif` / `else`

An `if` directive renders its body only when its condition — which must be a boolean expression — is true. Any number of `elseif` branches and an optional `else` branch may follow, and the block is closed with `/if`.

```
{{- let score = 72 -}}
{{- if score >= 90 -}}
A
{{- elseif score >= 80 -}}
B
{{- elseif score >= 70 -}}
C
{{- else -}}
F
{{- /if -}}
```
```
Output:
C
```

For an inline conditional *expression* (rather than a block), see the [`if(...)` function](functions.md#control-flow-functions).

## Loops: `for`

A `for` directive iterates over an array, binding each element to a loop variable and rendering its body once per element. The block is closed with `/for`.

```
{{- let items = ["red", "green", "blue"] -}}
{{- for color in items -}}
- {{ color }}
{{ /for -}}
```
```
Output:
- red
- green
- blue
```

Loops compose naturally with the array and range functions. For example, `for n in range(1, 4)` iterates over `[1, 2, 3]`, and the `rangeDay`/`rangeMonth`/… functions iterate over time periods. See the [function reference](functions.md).

## Capture

A `capture` block evaluates its body and stores the rendered result in a new variable instead of emitting it. This is useful for building up a string from a block of template logic and reusing it later.

```
{{- capture greeting -}}
Hello, {{ "Ada" }}!
{{- /capture -}}
{{ greeting }}
{{ greeting }}
```
```
Output:
Hello, Ada!
Hello, Ada!
```

The captured value is the rendered text of the body, so a capture can contain any directives — including loops, conditionals, and `include` — and the variable it produces is an ordinary string.

## Composition: `include`

`include` renders another template inline by name, which lets you factor shared pieces (headers, footers, partials) into their own templates and compose them.

```
{{ include header }}
<main>...</main>
{{ include footer }}
```

Includes are resolved by the host through an `ITemplateResolver` that maps a name (`header`, `footer`, …) to template source. If the interpreter was created without a resolver, `include` is unavailable. The included template is rendered with access to the same data and functions as its host, and **circular includes are detected and reported** as an error rather than looping forever. See [Embedding Powwow in a .NET host](embedding.md#template-composition) for wiring up a resolver.
