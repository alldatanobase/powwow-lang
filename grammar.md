# Powwow Lang Grammar

## Template

```
template ::= {directive | whitespace | newline | text} ;
```

## Whitespace, newlines, and text handling

```
whitespace ::= (" " | "\t") {whitespace} ;
```

```
newline ::= "\r\n" | "\r" | "\n" ;
```

```
text ::= ? any character sequence not parsed as a directive, whitespace, or a newline ? ;
```

## Directive

```
directive ::= comment
            | literal
            | assignment
            | mutation
            | capture
            | for
            | if
            | include
            | expression ;
directive_start ::= ("{{" | "{{-") {whitespace} ;
directive_end ::= {whitespace} ("}}" | "-}}") ;
```

## Comments

```
comment ::= directive_start "*" comment_body "*" directive_end ;
comment_body ::= ? any character sequence ? ;
```

### Example

```
{{* this is a comment *}}
{{-* so * is * this *-}}
{{ * and
    this
    as
    well
* }}
```

## Literals

```
literal ::= literal_open literal_body literal_close ;
literal_open ::= directive_start "literal" directive_end ;
literal_body ::= ? any character sequence ? ;
literal_close ::= directive_start "/literal" directive_end ;
```

### Example

```
{{ literal }}
text is rendered exactly {{ as is }} and all {{ directives }}
are {{* ignored *}}
{{ /literal }}
```

## Variable assignment, mutation, and assignment through capture

```
assignment ::= directive_start "let" identifier "=" expression directive_end ;
mutation ::= directive_start "mut" identifier ["." identifier] "=" expression directive_end ;
```

```
capture ::= capture_open template capture_close ;
capture_open ::= directive_start "capture" identifier directive_end ;
capture_close ::= directive_start "/capture" directive_end ;
```

### Example

```
{{ let x = 1 }}
{{ mut y = 2 }}
{{ mut z.a = 3 }}
{{ capture foo }}the capture body is evaluated {{let x = 1}}{{x}} and the result is stored into the newly declared variable foo{{ /capture}}
```

## Looping

```
for ::= for_open template for_close ;
for_open ::= directive_start "for" identifier "in" expression directive_end ;
for_close ::= directive_start "/for" directive_end ;
```

### Example

```
{{ for x in xs }}
  <li>{{x}}<li>
{{ /for}}
```

## Conditionals

```
if ::= if_open template {elseif template} [else template] if_close ;
if_open ::= directive_start "if" expression directive_close ;
elseif ::= directive_start "elseif" expression directive_close ;
else ::= directive_start "else" directive_close ;
if_close ::= directive_start "/if" directive_close ;
```

### Example

```
{{ if x > 10 }}
  x is big
{{ elseif x > 5 }}
  x is not small
{{ elseif x > 1 }}
  x is small
{{ else }}
  there is no x
{{ /if }}
```

## Inclusions

```
include ::= directive_start "include" identifier directive_end ;
```

### Example

```
{{ include foo }}
```

## Expressions

```
expression ::= or ;
or ::= and {"||" and} ;
and ::= comparison {"&&" comparison} ;
comparison ::= term {("<" | "<=" | ">" | ">=" | "==" | "!=") term} ;
term ::= factor {("+" | "-") factor} ;
factor ::= unary {("*" | "/") unary} ;
unary ::= {"!"} primary ;
grouping ::= "(" expression ")" ;
```

### Example

{{ (1 < 7 || 7 <= 1) && 4 > 3 && 4 >= 4 || 2 * 7 == 14 && 3 + 1 != 4 || !false }}

## Primary Expressions

```
primary ::= array
          | object
          | lambda
          | grouping
          | function_call
          | field_access
          | string
          | number
          | boolean
          | type_literal
          | identifier ;
```

## Arrays

```
array ::= "[" [expression {"," expression}] "]" ;
```

### Example

```
{{ let array = [1, 2, "abc", false] }}
```

## Objects 

```
object ::= "obj(" [identifier ":" expression {"," identifier ":" expression}] ")" ;
```

### Example

```
{{ obj(x: 1, y: 2, z: obj(foo: "hello", bar: "world")) }}
```

## Lambdas and functions

```
lambda ::= "(" [identifier {"," identifier}] ")" "=>" [statement_list] expression ;
statement_list ::= (assignment | mutation) {"," (assignment | mutation)} "," ;

function_call ::= expression "(" [expression {"," expression}] ")" ;
```

### Example

```
{{ let add = (x, y) => x * y }}
{{ add(1, 2) }}
{{ myObj.func("foo") }}
```

## Field access and identifiers

```
field_access ::= expression "." identifier ;

identifier ::= ["_" | [A-Z] | [a-z]] {"_" | [A-Z] | [a-z]} ;
```

### Example

```
{{ foo.bar.baz }}
{{ let _my_var1 = 1 }}
```

## Strings

```
string ::= '"' {char | escape_sequence} '"' ;
char ::= ? any character except " or \ ? ;
escape_sequence ::= "\" ('"' | "\" | "n" | "r" | "t") ;
```

### Example

```
{{ let myString = "this is a string\nwith several\n\tlines and a tab" }}
```

## Numbers

```
number ::= ["-"] digit {digit} ["." {digit}] ;
digit ::= "0" | "1" | "2" | "3" | "4" | "5" | "6" | "7" | "8" | "9" ;
```

### Example

```
{{ [1, -2, -.3, -0.4, 5.01, .6, 0.7 ]}}
```

## Boolean values

```
boolean ::= "true" | "false" ;
```

### Example

```
{{ let truthy = true }}
{{ let falsey = false }}
```

## Type literals

```
type_literal ::= "String" | "Number" | "Boolean" | "Array" | "Object" | "Function" | "DateTime"
```

### Examples

```
{{ let stringType = String }}
{{ stringType == String }}
```