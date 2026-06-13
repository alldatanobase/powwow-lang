# Built-in functions

Powwow ships with a library of built-in functions covering type inspection, conversion, numbers, strings, arrays, objects, dates, JSON, and web encoding. Functions are called with C-style syntax inside a directive:

```
{{ functionName(arg1, arg2) }}
```

A few things apply to every built-in:

* **Overloading by type.** Many names (`length`, `concat`, `contains`, `order`, `round`, `string`) have several overloads. Powwow picks the overload that best matches the types of the arguments you pass. Passing a type no overload accepts is an evaluation error.
* **Optional arguments.** Some functions take trailing optional arguments with a default value (shown as `arg = default` below). They may be omitted.
* **Errors, not nulls.** When a built-in cannot do its job — an out-of-bounds index, a missing field, an unparseable string — it raises an evaluation error rather than returning a null or empty value. Powwow has no null type. Where a result is uncertain, guard first (for example with `contains` before `get`).
* **Immutability.** Functions return new values; they never mutate their arguments. (The `mut` directive is the only way to change a binding — see [data types](datatypes.md).)

Sections:

* [Type inspection](#type-inspection)
* [Conversion](#conversion)
* [Number functions](#number-functions)
* [String functions](#string-functions)
* [Array functions](#array-functions)
* [Object functions](#object-functions)
* [DateTime functions](#datetime-functions)
* [JSON functions](#json-functions)
* [Web and encoding functions](#web-and-encoding-functions)
* [Control-flow functions](#control-flow-functions)
* [Dataverse functions](#dataverse-functions)

---

## Type inspection

### `typeof(value)`

Returns the type of any value as a type literal (`Number`, `String`, `Boolean`, `Array`, `Object`, `Function`, or `DateTime`). Compare the result against a type literal to test a value's type.

```
{{- let a = 42 -}}
{{- let b = "blue" -}}
{{ typeof(a) }}
{{ typeof(a) == Number }}
{{ typeof(b) == Number }}
```
```
Output:
type<Number>
true
false
```

---

## Conversion

### `string(value)`

Converts a `Number` or `Boolean` to its string form. Booleans become the lowercase `"true"` or `"false"`.

```
{{ string(42) }}
{{ string(true) }}
```
```
Output:
42
true
```

### `number(value)`

Parses a `String` into a `Number`. Raises an error if the string is empty or not numeric — test first with `numeric` if the input is uncertain.

```
{{ number("3.14") }}
```
```
Output:
3.14
```

### `numeric(value)`

Tests whether a `String` can be parsed as a number, returning a `Boolean`. An empty string is not numeric.

```
{{ numeric("42") }}
{{ numeric("blue") }}
```
```
Output:
true
false
```

---

## Number functions

### `range(start, end, step = 1)`

Builds an `Array` of numbers from `start` (inclusive) to `end` (exclusive), stepping by `step`. The step may be negative to count down, but must not be zero.

```
{{ range(0, 5) }}
{{ range(1, 10, 2) }}
{{ range(5, 0, -1) }}
```
```
Output:
[0, 1, 2, 3, 4]
[1, 3, 5, 7, 9]
[5, 4, 3, 2, 1]
```

### `mod(a, b)`

Returns the integer remainder of `a` divided by `b`. Both operands are truncated to integers. The divisor must not be zero.

```
{{ mod(7, 3) }}
{{ mod(10, 2) }}
```
```
Output:
1
0
```

### `floor(n)` · `ceil(n)`

Round a `Number` down (`floor`) or up (`ceil`) to the nearest whole number.

```
{{ floor(3.7) }}
{{ ceil(3.2) }}
```
```
Output:
3
4
```

### `round(n, decimals = 0)`

Rounds a `Number` to the given number of decimal places (zero by default). `decimals` may not be negative.

```
{{ round(3.14159) }}
{{ round(3.14159, 2) }}
```
```
Output:
3
3.14
```

---

## String functions

### `length(s)`

Returns the number of characters in a `String`. (`length` is also defined for arrays — see [Array functions](#array-functions).)

```
{{ length("hello") }}
```
```
Output:
5
```

### `empty(s)`

Returns `true` when a `String` is empty (or unset).

```
{{ empty("") }}
{{ empty("x") }}
```
```
Output:
true
false
```

### `concat(a, b)`

Joins two `String`s into one. (`concat` is also defined for arrays — see [Array functions](#array-functions).)

```
{{ concat("foo", "bar") }}
```
```
Output:
foobar
```

### `contains(s, search)` · `startsWith(s, search)` · `endsWith(s, search)`

Test, respectively, whether a `String` contains, begins with, or ends with another string. Each returns a `Boolean`. (`contains` is also defined for objects — see [Object functions](#object-functions).)

```
{{ contains("hello world", "o w") }}
{{ startsWith("hello", "he") }}
{{ endsWith("hello", "lo") }}
```
```
Output:
true
true
true
```

### `indexOf(s, search)` · `lastIndexOf(s, search)`

Return the zero-based index of the first (`indexOf`) or last (`lastIndexOf`) occurrence of `search` within `s`, or `-1` if it does not occur.

```
{{ indexOf("banana", "a") }}
{{ lastIndexOf("banana", "a") }}
{{ indexOf("banana", "z") }}
```
```
Output:
1
5
-1
```

### `substring(s, start, end = -1)`

Returns the portion of `s` from index `start` (inclusive) up to `end` (exclusive). If `end` is omitted, the substring runs to the end of the string.

```
{{ substring("hello world", 6) }}
{{ substring("hello world", 0, 5) }}
```
```
Output:
world
hello
```

### `toUpper(s)` · `toLower(s)` · `trim(s)`

Return the string converted to upper case, to lower case, or with leading and trailing whitespace removed.

```
{{ toUpper("hello") }}
{{ toLower("HELLO") }}
{{ trim("  hello  ") }}
```
```
Output:
HELLO
hello
hello
```

### `explode(s, delimiter)`

Splits a `String` into an `Array` of strings on every occurrence of `delimiter`. The inverse of `join`.

```
{{ explode("a,b,c", ",") }}
```
```
Output:
[a, b, c]
```

---

## Array functions

### `length(xs)`

Returns the number of elements in an `Array`.

```
{{ length([10, 20, 30]) }}
```
```
Output:
3
```

### `at(xs, index)`

Returns the element at the zero-based `index`. Equivalent to subscript syntax, `xs[index]` (see [Array indexing](datatypes.md#array)). An out-of-bounds index raises an error.

```
{{ at(["a", "b", "c"], 1) }}
```
```
Output:
b
```

### `first(xs)` · `last(xs)` · `rest(xs)`

`first` and `last` return the head and tail element; `rest` returns a new array of everything after the first element. `first` and `last` raise an error on an empty array; `rest` of an empty array is an empty array.

```
{{- let xs = [10, 20, 30, 40] -}}
{{ first(xs) }}
{{ last(xs) }}
{{ rest(xs) }}
```
```
Output:
10
40
[20, 30, 40]
```

### `any(xs)`

Returns `true` when an `Array` has at least one element.

```
{{ any([1]) }}
{{ any([]) }}
```
```
Output:
true
false
```

### `concat(a, b)`

Returns a new `Array` with the elements of `b` appended to those of `a`. (`concat` is also defined for strings — see [String functions](#string-functions).)

```
{{ concat([1, 2], [3, 4]) }}
```
```
Output:
[1, 2, 3, 4]
```

### `take(xs, n)` · `skip(xs, n)`

`take` returns the first `n` elements; `skip` returns everything after the first `n`. Counts past the ends of the array are handled gracefully (you simply get fewer, or zero, elements).

```
{{- let xs = [1, 2, 3, 4, 5] -}}
{{ take(xs, 2) }}
{{ skip(xs, 2) }}
```
```
Output:
[1, 2]
[3, 4, 5]
```

### `map(xs, lambda)`

Returns a new `Array` formed by applying a one-argument `lambda` to each element.

```
{{ map([1, 2, 3], (x) => x * x) }}
```
```
Output:
[1, 4, 9]
```

### `filter(xs, predicate)`

Returns a new `Array` of the elements for which the one-argument `predicate` lambda returns `true`. The predicate must return a `Boolean`.

```
{{ filter([1, 2, 3, 4, 5], (x) => x > 2) }}
```
```
Output:
[3, 4, 5]
```

### `reduce(xs, reducer, initial)`

Folds an `Array` into a single value. The two-argument `reducer` lambda receives the running accumulator and the current element, and returns the next accumulator. `initial` is the starting accumulator.

```
{{ reduce([1, 2, 3, 4], (acc, x) => acc + x, 0) }}
```
```
Output:
10
```

### `order(xs)` · `order(xs, ascending)` · `order(xs, comparer)`

Returns a sorted copy of an `Array`.

* `order(xs)` sorts ascending by natural order.
* `order(xs, ascending)` takes a `Boolean` — `true` for ascending, `false` for descending.
* `order(xs, comparer)` takes a two-argument `lambda` that returns a negative number, zero, or a positive number when its first argument sorts before, equal to, or after its second — for custom orderings.

```
{{ order([3, 1, 2]) }}
{{ order([3, 1, 2], false) }}
{{ order([3, 1, 2], (a, b) => b - a) }}
```
```
Output:
[1, 2, 3]
[3, 2, 1]
[3, 2, 1]
```

### `group(xs, field)`

Groups an `Array` of objects into an `Object` keyed by the value of a `String` `field`. Every element must be an object that contains `field`, and that field's value must be a string. Each value in the result is the array of elements sharing that key.

```
{{- let people = [
  obj(name: "Ada", team: "blue"),
  obj(name: "Bo", team: "red"),
  obj(name: "Cy", team: "blue")
] -}}
{{ keys(group(people, "team")) }}
```
```
Output:
[blue, red]
```

### `join(xs, delimiter)`

Concatenates the rendered elements of an `Array` into a single `String`, separated by `delimiter`. The inverse of `explode`.

```
{{ join(["a", "b", "c"], ", ") }}
```
```
Output:
a, b, c
```

---

## Object functions

### `get(obj, key)`

Reads a field by a `String` `key` — useful when the key is computed. Equivalent to subscript syntax, `obj[key]` (see [Object indexing](datatypes.md#object)). Raises an error if the field is absent (guard with `contains`). For a literal key, dot access (`obj.field`) is usually clearer.

```
{{- let user = obj(name: "Ada", age: 36) -}}
{{ get(user, "name") }}
```
```
Output:
Ada
```

### `contains(obj, key)`

Returns `true` when an `Object` has a field named `key`. (`contains` is also defined for strings — see [String functions](#string-functions).)

```
{{- let user = obj(name: "Ada") -}}
{{ contains(user, "name") }}
{{ contains(user, "email") }}
```
```
Output:
true
false
```

### `keys(obj)`

Returns an `Array` of an object's field names, as strings.

```
{{ keys(obj(name: "Ada", age: 36)) }}
```
```
Output:
[name, age]
```

---

## DateTime functions

DateTime values have no literal syntax; they are produced by these functions. See the [DateTime data type](datatypes.md) for an overview.

### `datetime(s)`

Parses a `String` into a `DateTime`. Raises an error if the string cannot be parsed as a date.

```
{{ datetime("2024-01-08 10:10:10") }}
```
```
Output:
2024-01-08T10:10:10.0000000
```

### `now()` · `utcNow()`

Return the current local time (`now`) or current UTC time (`utcNow`) as a `DateTime`. Neither takes arguments.

```
{{ format(now(), "yyyy-MM-dd") }}
```

### `format(d, formatString)`

Renders a `DateTime` as a `String` using a .NET-style format string.

```
{{- let d = datetime("2024-01-08 10:10:10") -}}
{{ format(d, "yyyy-MM-dd") }}
{{ format(d, "MMM d, yyyy") }}
```
```
Output:
2024-01-08
Jan 8, 2024
```

### `addYears(d, n)` · `addMonths(d, n)` · `addDays(d, n)` · `addHours(d, n)` · `addMinutes(d, n)` · `addSeconds(d, n)`

Return a new `DateTime` shifted by `n` of the named unit. `n` is truncated to an integer and may be negative to shift backward. The original value is unchanged.

```
{{- let d = datetime("2024-01-08 10:10:10") -}}
{{ format(addDays(d, 30), "yyyy-MM-dd") }}
{{ format(addMonths(d, -1), "yyyy-MM-dd") }}
```
```
Output:
2024-02-07
2023-12-08
```

### `rangeYear(start, end, step = 1)` · `rangeMonth` · `rangeDay` · `rangeHour` · `rangeMinute` · `rangeSecond`

Each returns an `Array` of `DateTime` values from `start` (inclusive) to `end` (exclusive), stepping by `step` of the named unit. The step is truncated to an integer and must be positive. A `start` at or after `end` yields an empty array. These pair naturally with a `for` directive.

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

---

## JSON functions

### `fromJson(s)`

Parses a JSON `String` into Powwow values — objects become objects, arrays become arrays, and primitives become numbers, strings, or booleans. Raises an error on invalid JSON.

```
{{- let data = fromJson("{\"name\": \"Ada\", \"age\": 36}") -}}
{{ data.name }}
{{ data.age }}
```
```
Output:
Ada
36
```

### `toJson(value, formatted = false)`

Serializes any value to a JSON `String`. Pass `true` as the second argument to produce indented, multi-line output.

```
{{ toJson(obj(name: "Ada", age: 36)) }}
```
```
Output:
{"name":"Ada","age":36}
```

---

## Web and encoding functions

### `htmlEncode(s)` · `htmlDecode(s)`

Encode a `String` for safe inclusion in HTML (`htmlEncode`), or decode HTML entities back to text (`htmlDecode`). `htmlEncode` is the one you want when interpolating untrusted text into an HTML template.

```
{{ htmlEncode("<b>Hi & bye</b>") }}
```
```
Output:
&lt;b&gt;Hi &amp; bye&lt;/b&gt;
```

### `urlEncode(s)` · `urlDecode(s)`

Encode a `String` for use in a URL (`urlEncode`), or decode a percent-encoded string (`urlDecode`).

```
{{ urlEncode("a b&c") }}
```
```
Output:
a+b%26c
```

### `uri(s)`

Parses a URL `String` into an `Object` exposing its components. Fields include `Scheme`, `Host`, `Port`, `AbsolutePath`, `PathAndQuery`, `Query`, `Fragment`, `Segments` (an array), and several booleans such as `IsAbsoluteUri` and `IsFile`.

```
{{- let u = uri("https://example.com:8080/path?q=1#top") -}}
{{ u.Scheme }}
{{ u.Host }}
{{ u.Port }}
{{ u.Query }}
```
```
Output:
https
example.com
8080
?q=1
```

---

## Control-flow functions

### `if(condition, then, else)`

A functional `if` *expression* — distinct from the [`{{ if }}` block directive](../grammar.md). It evaluates `condition` (which must be a `Boolean`) and returns either `then` or `else`. Both branches are evaluated lazily, so only the selected branch runs. This is convenient for inline choices within a larger expression.

```
{{- let n = 7 -}}
{{ if(n > 5, "big", "small") }}
```
```
Output:
big
```

---

## Dataverse functions

### `fetch(fetchXml)`

Runs a [FetchXML](https://learn.microsoft.com/power-apps/developer/data-platform/fetchxml/overview) query against Microsoft Dataverse and returns the results. This function is only available when the interpreter is constructed with a Dataverse service; without one, calling `fetch` raises an error. See [Embedding Powwow in a .NET host](embedding.md#dataverse-integration) for wiring it up.

```
{{ fetch("<fetch><entity name='account'><attribute name='name' /></entity></fetch>") }}
```
