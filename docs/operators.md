# Operators

Powwow has a small set of built-in operators that mirror the conventions of most C-family languages. They fall into four groups: arithmetic operators, which combine numbers; comparison operators, which test two values and yield a boolean; logical operators, which combine booleans; and the unary operator, which negates a boolean.

Operators are not polymorphic. Each operator expects operands of a specific type, and passing an operand of the wrong type raises an evaluation error rather than coercing. In particular, `+` does not concatenate strings (use the `concat` function for that), and there is no `%` operator (use the `mod` function for modulo).

The following sections describe each group.

## Arithmetic operators

The four arithmetic operators `+`, `-`, `*`, and `/` operate on numbers and produce numbers. They have infix notation and standard PMDAS precedence: multiplication and division bind tighter than addition and subtraction, and expressions can be grouped with parentheses to override the default order.

```
{{- let a = 5 -}}
{{- let b = 3 -}}
{{- let c = 2 -}}
{{- let d = 1 -}}
{{ a + b }}
{{ a - b }}
{{ a * b }}
{{ a / c }}
{{ (c + a * b - d) / c }}
```
```
Output:
8
2
15
2.5
8
```

The `-` symbol also appears as part of a numeric literal (for example `-3.1`), where it denotes a negative number rather than the subtraction operator. There is no standalone unary minus expression: to negate the value of an existing variable, subtract it from zero or multiply it by `-1`.

```
{{- let x = 7 -}}
{{ 0 - x }}
{{ x * -1 }}
```
```
Output:
-7
-7
```

Division by zero is an evaluation error rather than producing infinity or `NaN`.

```
{{- let x = 5 -}}
{{ x / 0 }}
```
```
Output:
(evaluation error: Cannot divide by zero)
```

Both operands of every arithmetic operator must be numbers. Attempting to apply an arithmetic operator to a value of any other type raises an evaluation error.

```
{{- let a = 1 -}}
{{- let b = "two" -}}
{{ a + b }}
```
```
Output:
(evaluation error: Expected value of type Number but found ...)
```

There is no `%` operator. The `mod` built-in function performs integer modulo on two numbers.

```
{{ mod(10, 3) }}
{{ mod(20, 7) }}
```
```
Output:
1
6
```

## Comparison operators

The comparison operators `<`, `<=`, `>`, `>=`, `==`, and `!=` test a relationship between two values and yield a boolean. They sit between the arithmetic and logical operators in precedence: arithmetic binds tighter than comparison, and comparison binds tighter than `&&` and `||`. This means an expression like `a + 1 < b * 2 && c == d` parses as `((a + 1) < (b * 2)) && (c == d)` without any explicit grouping.

```
{{- let a = 5 -}}
{{- let b = 3 -}}
{{ a > b }}
{{ a < b }}
{{ a >= 5 }}
{{ a <= 4 }}
{{ a == 5 }}
{{ a != b }}
```
```
Output:
true
false
true
false
true
true
```

The ordering operators `<`, `<=`, `>`, and `>=` are defined only on numbers. Applying them to operands of any other type, including booleans, strings, and DateTimes, raises an evaluation error.

The equality operators `==` and `!=` are more permissive: they accept operands of any type, but both operands must be of the **same** type. Comparing two values of different types is itself an evaluation error rather than yielding `false`. This is intentional, and helps surface mismatched-type bugs that would otherwise pass silently.

```
{{- let a = 1 -}}
{{- let b = "1" -}}
{{ a == b }}
```
```
Output:
(evaluation error: Expected similar types but found Number and String)
```

For most types, `==` checks equality of the underlying value: numbers, strings, and booleans compare by value, and DateTimes compare by the instant they represent (so two DateTimes parsed from differently-formatted strings are equal as long as they name the same moment in time). Arrays and objects, on the other hand, are reference types, so `==` on them tests reference identity rather than the equality of their contents. See the [Array](datatypes.md#array) and [Object](datatypes.md#object) sections of the data types reference for examples.

The result of any comparison operator can be combined with the logical operators, assigned to a variable, or used directly as the condition of an `if` directive.

```
{{- let age = 18 -}}
{{- if age >= 18 -}}
adult
{{- else -}}
minor
{{- /if -}}
```
```
Output:
adult
```

## Logical operators

The binary logical operators `&&` (and) and `||` (or) combine two booleans into a single boolean. Both operands must be booleans. Passing a value of any other type raises an evaluation error.

```
{{- let a = true -}}
{{- let b = false -}}
{{ a && b }}
{{ a || b }}
{{ a && !b }}
```
```
Output:
false
true
true
```

`&&` binds tighter than `||`, so an expression like `a || b && c` parses as `a || (b && c)`. Add explicit parentheses when the intended grouping is anything else.

```
{{- let a = false -}}
{{- let b = true -}}
{{- let c = true -}}
{{ a || b && c }}
{{ (a || b) && c }}
```
```
Output:
true
true
```

### Short-circuit evaluation

Both `&&` and `||` evaluate their operands lazily, from left to right.

`&&` evaluates its left operand first. If the left operand is `false`, the right operand is not evaluated, and the result is `false`. Only when the left operand is `true` is the right operand evaluated to determine the final result.

`||` works symmetrically. It evaluates its left operand first, and if the left operand is `true`, the right operand is not evaluated and the result is `true`. Only when the left operand is `false` is the right operand evaluated.

This short-circuit behavior makes it safe to guard an expression that would otherwise raise an evaluation error with a preceding test. In the example below, the right operand of `&&` is only evaluated when `contains(user, "age")` returns `true`, so the field access `user.age` is never attempted on an object that lacks the field.

```
{{- let user = obj(name: "Ada") -}}
{{ contains(user, "age") && user.age >= 18 }}
```
```
Output:
false
```

Without short-circuit evaluation, the same expression would raise an evaluation error because `user.age` does not exist. The same idiom applies to `||` for default-fallback patterns.

```
{{- let user = obj(name: "Ada", admin: true) -}}
{{ user.admin || contains(user, "owner") && user.owner }}
```
```
Output:
true
```

Because the operands are only evaluated as needed, any side effect on the right-hand side (for example, a function call that mutates state) will be skipped when the left operand decides the result. Avoid relying on the evaluation of a right-hand operand for anything other than producing its boolean value.

## Unary operator

The single unary operator `!` negates a boolean. It must be applied to an expression that evaluates to a boolean; applying it to a value of any other type raises an evaluation error.

```
{{- let a = true -}}
{{- let b = false -}}
{{ !a }}
{{ !b }}
{{ !(a && b) }}
```
```
Output:
false
true
true
```

`!` binds tighter than every binary operator, so `!a && b` parses as `(!a) && b`. Multiple `!` can be stacked, which is occasionally useful as a no-op assertion that a value really is a boolean.

```
{{- let a = false -}}
{{ !!a }}
{{ !!!a }}
```
```
Output:
false
true
```

There is no unary `+` or unary `-`. As noted in the [arithmetic operators](#arithmetic-operators) section, a leading `-` is part of a numeric literal, not an operator that can be applied to an arbitrary expression.

## Precedence summary

The full precedence of expression operators, from tightest binding to loosest, is:

| Precedence | Operators | Associativity |
|---|---|---|
| 1 (tightest) | `!` (unary) | right |
| 2 | `*`, `/` | left |
| 3 | `+`, `-` | left |
| 4 | `<`, `<=`, `>`, `>=`, `==`, `!=` | left |
| 5 | `&&` | left |
| 6 (loosest) | `\|\|` | left |

Parentheses can be used at any point to override the default precedence.
