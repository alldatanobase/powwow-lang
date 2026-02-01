# Data types

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
* [Built-in functions for numbers](#)

## String

## Boolean

## Array

## Object

## DateTime