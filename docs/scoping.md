# Scoping and mutability

This document describes how Powwow resolves variable names, which constructs introduce a new scope, which bindings can and cannot be reassigned, and how lambdas capture their surrounding scope to form closures.

Powwow uses **lexical scoping**: a name is resolved by looking outward through the scopes that textually enclose its use, ending at the global scope. There is no dynamic scoping of ordinary variables — a lambda sees the variables where it was *defined*, not where it was *called* (see [Closures](#closures)).

## The global scope

Every template runs inside a single outermost scope, the **global scope**. It contains three kinds of names:

* **Host data.** The data object passed in by the host application. Its properties are referenced as top-level names (and nested properties with dot notation). These are read-only — see [Mutable and immutable bindings](#mutable-and-immutable-bindings).
* **Registered functions.** All built-in functions (`length`, `map`, `format`, …) and any custom functions the host registers. See [Embedding](embedding.md) for registration.
* **Top-level variables.** Anything declared with `let` directly in the template body.

## Name resolution

When a name is used, Powwow searches scopes from the innermost outward and the **first match wins**:

1. The **current scope** — for a lambda call, its parameters and any locals declared with `let`; for a loop iteration, the loop variable and any locals; for an `if` branch or `capture` body, its locals; for the template body, the top-level variables.
2. **Enclosing scopes**, following the chain outward. For a lambda this means its *definition* (closure) scope, then the scope of its *caller*.
3. The **global scope** — host data, then registered functions.

If no scope contains the name, evaluation fails:

```
{{ doesNotExist }}
```
```
Output:
(evaluation error: Unknown identifier: doesNotExist)
```

A lambda can freely read names from the scope that encloses it:

```
{{- let x = 2 -}}
{{ ((a) => a * x)(3) }}
```
```
Output:
6
```

## What creates a new scope

Every construct with a body runs that body in a fresh scope. A new scope is created by:

* **Each call to a lambda** — holds the parameters and any locals the body declares.
* **Each iteration of a `for` loop** — holds the loop variable and any locals declared in the body.
* **Each branch of an `if`** — the `if`, `elseif`, or `else` body that runs does so in its own scope.
* **The body of a `capture`** — runs in its own scope; only the capture variable itself is published to the enclosing scope (that variable is the block's single, explicit output).
* **An included template** — the body pulled in by `include` runs in its own scope. Unlike `capture`, an `include` publishes *nothing* back to its caller; it contributes only rendered text. Its top-level `let`s are local to it and are not visible to the including template, though it can still read the names in scope at the include site along with host data and registered functions.

Only the root template body runs directly in the [global scope](#the-global-scope).

All of these scopes behave the same way:

* A `let` inside the body is **local** and is discarded when the body finishes. It does not leak out, and (for loops) does not carry across iterations.
* The body can still **read and reassign** names from enclosing scopes. A reassignment reaches the outer binding and persists after the block ends.
* A `let` inside the body may **not reuse a name** that is already visible further out. Powwow's block scopes prevent leakage but do **not** allow shadowing: spelling an inner `let` with an outer name is an error, not a new inner variable. (Lambda parameters are the sole exception — a parameter may reuse an outer name and takes precedence inside the lambda.)

So a `let` declared inside an `if` branch is not visible once the block ends:

```
{{- let x = 1 -}}
{{- if x == 1 -}}{{- let z = 2 -}}{{- /if -}}
{{ z }}
```
```
Output:
(evaluation error: Unknown identifier: z)
```

Reassigning a variable from an enclosing scope, on the other hand, works and persists, because assignment resolves outward along the scope chain. Both the `if` and the `for` below update a variable declared outside them:

```
{{- let x = 1 -}}
{{- if x == 1 -}}{{- x = 2 -}}{{- /if -}}
{{ x }}
```
```
Output:
2
```

```
{{- let total = 0 -}}
{{- for n in [1, 2, 3] -}}
{{- total = total + n -}}
{{- /for -}}
{{ total }}
```
```
Output:
6
```

## Setting a variable conditionally

Powwow has no "declare without value" form: every `let` must bind a value immediately. Combined with block scoping, this means you cannot declare a variable inside an `if` branch and read it after the block. The pattern is to **declare the variable with a sensible default in the enclosing scope, then reassign it inside the branch** — the reassignment reaches the outer binding:

```
{{- let plan = obj(premium: true) -}}
{{- let label = "Free" -}}
{{- if plan.premium -}}{{- label = "Premium" -}}{{- /if -}}
{{ label }}
```
```
Output:
Premium
```

For a straightforward either/or value, the [`if(cond, a, b)` function](functions.md#control-flow-functions) is more direct, because it is an expression and can be bound in a single `let`:

```
{{- let plan = obj(premium: true) -}}
{{- let label = if(plan.premium, "Premium", "Free") -}}
{{ label }}
```
```
Output:
Premium
```

When the value you want is rendered text, `capture` is the idiomatic choice, since its variable is published to the enclosing scope by design:

```
{{- let plan = obj(premium: true) -}}
{{- capture label -}}{{- if plan.premium -}}Premium{{- else -}}Free{{- /if -}}{{- /capture -}}
{{ label }}
```
```
Output:
Premium
```

## Declaring and reassigning variables

`let` **declares** a new variable; it binds a name for the first time. A plain assignment (`name = expr`, optionally written `mut name = expr`) **reassigns** a name that already exists. See [Variables](directives.md#variables-let-and-assignment) for the directive syntax.

A name may be declared only once per scope, and `let` cannot reuse a name that is already visible — there is **no shadowing**. Declaring a name that already exists as a variable, a loop variable, a host data property, or a registered function is an error:

```
{{- let x = 1 -}}
{{- let x = 2 -}}
```
```
Output:
(evaluation error: Cannot define variable 'x' because it conflicts with an existing variable, field, or function)
```

The restriction runs both ways between variables and loop variables: a loop variable may not reuse the name of a variable in scope, and a `let` inside a loop may not reuse the loop variable's name.

```
{{- let item = 5 -}}
{{- for item in [1, 2, 3] -}}{{ item }}{{ /for -}}
```
```
Output:
(evaluation error: Iterator name 'item' conflicts with an existing variable, field, or function)
```

To change a value, declare it once with `let` and then reassign it — provided the binding is mutable, as described next.

## Mutable and immutable bindings

Only some kinds of bindings can be reassigned. Attempting to reassign anything else is an evaluation error.

### Mutable

* **Variables declared with `let`**, whether at the top level or inside a lambda body.

  ```
  {{- let count = 1 -}}
  {{- count = count + 1 -}}
  {{ count }}
  ```
  ```
  Output:
  2
  ```

* **Fields of an object** held in a `let` variable, addressed with dot notation. This updates an existing field; it does not add new fields.

  ```
  {{- let user = obj(name: "Ada", age: 36) -}}
  {{- user.age = 37 -}}
  {{ user.age }}
  ```
  ```
  Output:
  37
  ```

### Immutable

The following can be read but never reassigned:

* **Loop variables.** The variable bound by `for ... in` is fixed for the duration of each iteration.

  ```
  {{- for n in [1, 2, 3] -}}{{- n = n + 1 -}}{{- /for -}}
  ```
  ```
  Output:
  (evaluation error: Iterator variable n is not mutable and cannot be reassigned)
  ```

* **Lambda parameters.** A parameter is bound to its argument for the call and cannot be reassigned (declare a local `let` if you need a mutable copy).

  ```
  {{ ((y) => let x = 1, y = 2, x * y)(1) }}
  ```
  ```
  Output:
  (evaluation error: Parameter y is not mutable and cannot be reassigned)
  ```

* **Host data (global variables).** Properties supplied by the host are read-only.

  ```
  {{* assuming the host provided a data field named "name" *}}
  {{ name = "Bob" }}
  ```
  ```
  Output:
  (evaluation error: Global variable name is not mutable and cannot be reassigned)
  ```

* **Registered functions.** A built-in or host-registered function name cannot be reassigned, nor declared as a variable.

  ```
  {{ length = 3 }}
  ```
  ```
  Output:
  (evaluation error: Cannot mutate variable 'length' because it is an existing function)
  ```

### Value copies versus shared references

Reassigning a variable that holds a `Number`, `String`, `Boolean`, or type literal replaces that variable's own value and affects nothing else, because these values are copied when bound. Arrays, objects, and functions are reference types: binding one to a second variable makes both names refer to the same underlying value, so a field update through one name is visible through the other. This distinction is covered in detail in the [data types](datatypes.md#array) reference and matters whenever a value is shared across scopes (including by a closure).

## Closures

A lambda is a **closure**: when its literal is evaluated, it captures the scope in which it was defined. Calling it later creates a fresh call scope whose enclosing scope is that captured definition scope. Free names in the body are resolved against the definition scope first, then against the caller's scope, and finally the global scope.

Because capture is by reference to the binding (not a snapshot of the value), a closure sees later changes to a captured variable, and several closures sharing a binding share its state:

```
{{- let x = 2 -}}
{{- let getX = () => x -}}
{{- x = 5 -}}
{{ getX() }}
```
```
Output:
5
```

This makes it possible to build stateful objects whose methods mutate a shared, captured field. Here each call to `increment` updates the same `self.current`:

```
{{- let counter = (x) =>
   let self = (() => obj(
     current: x,
     increment: () => self.current = self.current + 1, self.current
   ))(),
   self
-}}
{{- let c = counter(0) -}}
{{- let _ = 0 -}}
{{- for i in range(0, 10) -}}
  {{- _ = c.increment() -}}
{{- /for -}}
{{ c.current }}
```
```
Output:
10
```

Because a lambda resolves its own name through its definition scope at call time, a lambda can call itself recursively, and two lambdas defined next to each other can call each other (mutual recursion) even though each name is only declared after the other:

```
{{- let fact = (n) => if(n <= 1, 1, n * fact(n - 1)) -}}
{{ fact(5) }}
```
```
Output:
120
```

```
{{- let isEven = (n) => if(n == 0, true, isOdd(n - 1)) -}}
{{- let isOdd = (n) => if(n == 0, false, isEven(n - 1)) -}}
{{ isEven(10) }}
```
```
Output:
true
```

Recursion is bounded by a maximum call-stack depth (1000 by default, configurable by the host); exceeding it raises an evaluation error rather than overflowing the host stack.

## Also see

* [Template syntax and directives](directives.md) — the `let`, assignment, `for`, and `capture` directives
* [Data types](datatypes.md) — value-copy versus reference semantics for each type
* [Built-in functions](functions.md) — the registered functions that occupy the global scope
* [Embedding](embedding.md) — supplying host data and registering custom functions
