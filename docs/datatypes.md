# Data types

Powwow is a dynamically typed language. Variables are not declared with a type, and the same name may be rebound to a value of a different type over the course of a template. The type of a value is determined at the moment it is evaluated, and operators and built-in functions check the types of their operands at evaluation time. Passing a value of the wrong type raises an evaluation error rather than silently coercing.

The language deliberately has no concept of `null` or `undefined`. Every variable that exists is bound to a real value of one of the types described in this document, and every field of an object holds a real value. Referencing a name that has not been declared, or accessing a field that does not exist on an object, is an evaluation error rather than a way to obtain an "empty" value. When the possible absence of something needs to be represented, the template author is expected to model it explicitly (for example, with a boolean flag, a sentinel value, or by testing for a field's presence with `contains` before reading it).

The following sections describe each of the built-in data types.

## Number

Numbers are expressed as literals such as 42 and -2.71828. The primitive data type for a number is a floating point value. There are no integer or unsigned-specific data types for numbers.

```
{{- let a = 0 -}}
{{- let b = 1 -}}
{{- let c = 2.1 -}}
{{- let d = -3.1 -}}
{{- let e = -0.000000004 -}}
{{a}} {{b}} {{c}} {{d}} {{e}}
```
```
Output:
0 1 2.1 -3.1 -0.000000004
```

Numbers can be added, subtracted, multiplied, and divided, and operations can be grouped with parentheses. Operations have infix ordering and standard PMDAS order of operations.

```
{{- let a = 5 -}}
{{- let b = 3 -}}
{{- let c = 2 -}}
{{- let d = 1 -}}
{{ (c + a * b - d) / c }}
```
```
Output:
8
```

You can check if a value is a number by comparing with the `Number` type literal.

```
{{- let a = 1 -}}
{{- let b = "blue" -}}
{{ typeof(a) == Number }}
{{ typeof(b) == Number }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions for numbers](functions.md#number-functions)

## String

Strings are expressed as literal sequences of characters inside of double quotes such as "foo". Unicode is supported.

```
{{- let myFirstString = "Hello world! 😀" -}}{{ myFirstString }}
```

```
Output:
Hello world! 😀
```

The backslash is used as an escape modifier for certain characters.

|Escape sequence|Outputs|
|---|---|
|```\"```|```"```|
|```\\```|```\```|
|```\r```|Carriage return|
|```\n```|Line feed|
|```\t```|Tab|

```
Note: Unicode escapes are not currently supported.
```

You can check if a value is a string by comparing with the `String` type literal.

```
{{- let a = 1 -}}
{{- let b = "blue" -}}
{{ typeof(a) == String }}
{{ typeof(b) == String }}
```

```
Output:
false
true
```

Also see:
* [Built-in functions for strings](functions.md#string-functions)

## Boolean

Booleans are expressed using the literals `true` and `false`.

```
{{- let truthy = true -}}
{{- let falsey = false -}}
{{truthy}} {{falsey}}
```
```
Output:
true false
```

Booleans can be combined with the logical operators `&&` (and), `||` (or), and `!` (not). Both `&&` and `||` use short-circuit evaluation: the right-hand operand is only evaluated when the left-hand operand cannot already decide the result.

```
{{- let a = true -}}
{{- let b = false -}}
{{ a && b }}
{{ a || b }}
{{ !a }}
{{ !a || (a && !b) }}
```
```
Output:
false
true
false
true
```

Booleans can be tested for equality and inequality with `==` and `!=`. Boolean values do not support the ordering operators (`<`, `<=`, `>`, `>=`), which are reserved for numbers.

```
{{- let a = true -}}
{{- let b = false -}}
{{ a == b }}
{{ a != b }}
{{ a == !b }}
```
```
Output:
false
true
true
```

Conditional directives expect a boolean expression:

```
{{- let isReady = true -}}
{{- if isReady -}}
Ready!
{{- else -}}
Not ready.
{{- /if -}}
```
```
Output:
Ready!
```

You can check if a value is a boolean by comparing with the `Boolean` type literal.

```
{{- let a = true -}}
{{- let b = "blue" -}}
{{ typeof(a) == Boolean }}
{{ typeof(b) == Boolean }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions for booleans](functions.md#control-flow-functions)

## Array

Arrays are expressed as comma-separated lists of expressions surrounded by square brackets. Elements may be of any type, including mixed types, and may themselves be arrays or objects.

```
{{- let numbers = [1, 2, 3] -}}
{{- let mixed = [1, "two", false, [4, 5]] -}}
{{- let empty = [] -}}
{{numbers}}
{{mixed}}
{{empty}}
```
```
Output:
[1, 2, 3]
[1, two, false, [4, 5]]
[]
```

Elements are accessed by zero-based index. Powwow supports both subscript syntax, `xs[i]`, and the built-in `at` function, `at(xs, i)`; the two are equivalent. The index may be any expression that evaluates to a whole number. A fractional, negative, or out-of-bounds index raises an evaluation error.

```
{{- let xs = ["a", "b", "c"] -}}
{{ xs[0] }}
{{ xs[2] }}
{{ at(xs, 1) }}
```
```
Output:
a
c
b
```

Subscripts can be chained, which is convenient for reaching into nested arrays.

```
{{- let grid = [[1, 2, 3], [4, 5, 6]] -}}
{{ grid[1][2] }}
```
```
Output:
6
```

The `length` function returns the number of elements, and `first`, `last`, and `rest` give convenient access to the head, tail element, and remainder.

```
{{- let xs = [10, 20, 30, 40] -}}
{{ length(xs) }}
{{ first(xs) }}
{{ last(xs) }}
{{ rest(xs) }}
```
```
Output:
4
10
40
[20, 30, 40]
```

Arrays can be iterated with the `for` directive.

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

Arrays can be transformed with higher-order functions like `map`, `filter`, and `reduce`, which accept lambdas.

```
{{- let xs = [1, 2, 3, 4, 5] -}}
{{ map(xs, (x) => x * x) }}
{{ filter(xs, (x) => x > 2) }}
{{ reduce(xs, (acc, x) => acc + x, 0) }}
```
```
Output:
[1, 4, 9, 16, 25]
[3, 4, 5]
15
```

Note that arrays are reference types. Comparing two arrays with `==` checks reference identity, not the equality of their elements, so two arrays with the same contents are not considered equal unless they refer to the same underlying value.

```
{{- let a = [1, 2, 3] -}}
{{- let b = [1, 2, 3] -}}
{{- let c = a -}}
{{ a == b }}
{{ a == c }}
```
```
Output:
false
true
```

You can check if a value is an array by comparing with the `Array` type literal.

```
{{- let a = [1, 2, 3] -}}
{{- let b = "blue" -}}
{{ typeof(a) == Array }}
{{ typeof(b) == Array }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions for arrays](functions.md#array-functions)

## Object

Objects are created with the `obj(...)` constructor, listing fields as comma-separated `key: expression` pairs. Field names are bare identifiers (not quoted), and field values may be of any type, including other objects, arrays, lambdas, or function references.

```
{{- let point = obj(x: 1, y: 2) -}}
{{- let user = obj(name: "Ada", age: 36, tags: ["admin", "engineer"]) -}}
{{- let nested = obj(outer: obj(inner: "hello")) -}}
{{point}}
{{user}}
{{nested}}
```
```
Output:
{x: 1, y: 2}
{name: Ada, age: 36, tags: [admin, engineer]}
{outer: {inner: hello}}
```

Fields are read with dot notation. Accessing a field that does not exist on the object raises an evaluation error; use the `contains` function first if a field's presence is uncertain.

```
{{- let user = obj(name: "Ada", age: 36) -}}
{{ user.name }}
{{ user.age }}
{{ contains(user, "email") }}
```
```
Output:
Ada
36
false
```

A field can also be read with subscript syntax using a string key, `user["name"]`. This is equivalent to the `get` function and is especially useful when the key is computed at runtime. As with dot access, indexing a field that does not exist raises an evaluation error. The index must be a string; indexing an object with a number is an error.

```
{{- let user = obj(name: "Ada", age: 36) -}}
{{- let field = "age" -}}
{{ user["name"] }}
{{ user[field] }}
```
```
Output:
Ada
36
```

The fields of an existing object can be updated with `mut`, using dot notation on the left-hand side. Note that `mut` reassigns or updates an existing binding. It does not add new fields to an object.

```
{{- let user = obj(name: "Ada", age: 36) -}}
{{- mut user.age = 37 -}}
{{user.age}}
```
```
Output:
37
```

The `get` function reads a field by a string key (useful when the key is computed), `keys` returns the list of field names, and `contains` tests for the presence of a field.

```
{{- let user = obj(name: "Ada", age: 36) -}}
{{ get(user, "name") }}
{{ keys(user) }}
{{ contains(user, "age") }}
```
```
Output:
Ada
[name, age]
true
```

Functions can be stored as fields and invoked through dot access, which makes objects useful for grouping related behavior with data.

```
{{- let counter = obj(value: 10, double: (x) => x * 2) -}}
{{ counter.double(counter.value) }}
```
```
Output:
20
```

Like arrays, objects are reference types. Assigning an object to a new variable does not copy it. Both names refer to the same underlying value, so mutations through one are visible through the other.

```
{{- let x = obj(a: 1, b: 2) -}}
{{- let y = x -}}
{{- mut y.a = 3 -}}
{{ y.a }} {{ x.a }}
```
```
Output:
3 3
```

For the same reason, `==` on two objects compares reference identity rather than the equality of their fields.

You can check if a value is an object by comparing with the `Object` type literal.

```
{{- let a = obj(x: 1) -}}
{{- let b = "blue" -}}
{{ typeof(a) == Object }}
{{ typeof(b) == Object }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions for objects](functions.md#object-functions)

## Function

Functions are first-class values. The literal form is a lambda: a parenthesized parameter list, an arrow (`=>`), and a single result expression. A lambda may take zero or more parameters.

```
{{- let square = (x) => x * x -}}
{{- let add = (a, b) => a + b -}}
{{ square(5) }}
{{ add(3, 4) }}
```
```
Output:
25
7
```

Because functions are values, they can be assigned to variables, stored in object fields, passed as arguments, and returned from other functions. The higher-order array functions such as `map`, `filter`, `reduce`, and `order` take a lambda as an argument.

```
{{- let nums = [1, 2, 3, 4] -}}
{{ map(nums, (x) => x * x) }}
```
```
Output:
[1, 4, 9, 16]
```

A lambda closes over the variables in scope where it is defined, capturing them for later use.

```
{{- let factor = 10 -}}
{{- let scale = (x) => x * factor -}}
{{ scale(5) }}
```
```
Output:
50
```

A lambda body may declare local `let` or `mut` statements before its result expression, each followed by a comma.

```
{{- let hypotenuseSquared = (a, b) => let a2 = a * a, let b2 = b * b, a2 + b2 -}}
{{ hypotenuseSquared(3, 4) }}
```
```
Output:
25
```

When a function value is rendered directly, it is printed as `lambda(...)` listing its parameter names. Built-in functions are also of type `Function`.

```
{{- let square = (x) => x * x -}}
{{ square }}
```
```
Output:
lambda(x)
```

You can check if a value is a function by comparing with the `Function` type literal.

```
{{- let a = (x) => x -}}
{{- let b = "blue" -}}
{{ typeof(a) == Function }}
{{ typeof(b) == Function }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions](functions.md)

## DateTime

DateTime values represent an instant in time. They have no literal syntax; instead, they are produced by built-in functions. The most common is `datetime(...)`, which parses a string, but `now()` and `utcNow()` are also available for the current local and UTC time.

```
{{- let d1 = datetime("2024-01-08 10:10:10") -}}
{{- let d2 = datetime("2024-12-25") -}}
{{d1}}
{{d2}}
```
```
Output:
2024-01-08T10:10:10.0000000
2024-12-25T00:00:00.0000000
```

By default a DateTime is rendered in ISO 8601 format. Use the `format` function with a .NET-style format string to render in any other form.

```
{{- let d = datetime("2024-01-08 10:10:10") -}}
{{ format(d, "yyyy-MM-dd") }}
{{ format(d, "yyyy-MM-dd HH:mm:ss") }}
{{ format(d, "MMM d, yyyy") }}
```
```
Output:
2024-01-08
2024-01-08 10:10:10
Jan 8, 2024
```

DateTimes are shifted forward or backward in time with the `addYears`, `addMonths`, `addDays`, `addHours`, `addMinutes`, and `addSeconds` functions. Each takes a DateTime and an integer offset (negative values shift backward) and returns a new DateTime. The original is not modified.

```
{{- let d = datetime("2024-01-08 10:10:10") -}}
{{ format(addYears(d, 1), "yyyy-MM-dd") }}
{{ format(addMonths(d, 6), "yyyy-MM-dd") }}
{{ format(addDays(d, -3), "yyyy-MM-dd") }}
```
```
Output:
2025-01-08
2024-07-08
2024-01-05
```

DateTimes can be tested for equality and inequality with `==` and `!=`. Equality is based on the underlying instant in time, not on the way the value was constructed, so two DateTimes that name the same instant are equal even if they were parsed from differently-formatted strings.

```
{{- let a = datetime("2024-01-08 10:10:10") -}}
{{- let b = datetime("2024-01-08 10:10:10") -}}
{{- let c = datetime("2025-01-01") -}}
{{ a == b }}
{{ a != c }}
```
```
Output:
true
true
```

A set of `rangeX` functions produce arrays of DateTimes between a start (inclusive) and an end (exclusive), stepping by year, month, day, hour, minute, or second. These are convenient for iterating over time periods with a `for` directive.

```
{{- let days = rangeDay(datetime("2024-01-01"), datetime("2024-01-04")) -}}
{{- for d in days -}}
{{ format(d, "yyyy-MM-dd") }}
{{ /for -}}
```
```
Output:
2024-01-01
2024-01-02
2024-01-03
```

You can check if a value is a DateTime by comparing with the `DateTime` type literal. Note that a string that happens to look like a date is still a string until it is passed through `datetime`.

```
{{- let a = datetime("2024-01-08") -}}
{{- let b = "2024-01-08" -}}
{{ typeof(a) == DateTime }}
{{ typeof(b) == DateTime }}
```

```
Output:
true
false
```

Also see:
* [Built-in functions for datetimes](functions.md#datetime-functions)