# Embedding Powwow in a .NET host

Powwow is designed to be embedded in a host application. The host compiles the interpreter source into its own assembly, binds data into templates, and can extend the language with custom functions, template composition, and a Dataverse data source. This guide covers that host-facing API.

The core interpreter has **no third-party dependencies** and targets .NET Framework, so it can be compiled directly into existing assemblies — including Microsoft Dataverse plugins, the original use case. (Only the optional Dataverse integration pulls in `Microsoft.Xrm.Sdk`, which a plugin host already references.)

Relevant namespaces:

```csharp
using PowwowLang.Runtime;   // Interpreter
using PowwowLang.Lib;       // ParameterDefinition, TemplateRegistry, ITemplateResolver, IDataverseService, DataverseService
using PowwowLang.Types;     // Value, StringValue, NumberValue, BooleanValue, ArrayValue, ObjectValue, DateTimeValue, Box
using PowwowLang.Env;       // ExecutionContext
using PowwowLang.Ast;       // AstNode
```

## Rendering a template

Construct an `Interpreter` and call `Interpret(template, data)`. The `data` argument is the root object whose members become the template's top-level variables.

```csharp
var interpreter = new Interpreter();

var data = new ExpandoObject();
((IDictionary<string, object>)data)["name"] = "Ada";

string output = interpreter.Interpret("Hello, {{name}}!", data);
// output: "Hello, Ada!"
```

A single `Interpreter` instance is reusable across many `Interpret` calls; built-in and custom function registrations live on the instance.

## Binding data

The `data` object is converted into Powwow values by a factory that accepts a range of shapes, so you can pass whatever is convenient:

* **`ExpandoObject` / `IDictionary<string, object>`** — keys become variables. This is the most common choice for ad-hoc data.
* **Plain CLR objects (POCOs)** — public properties are read by reflection and become object fields.
* **Collections** (`IEnumerable<...>` of primitives, strings, dates, or dictionaries) — become arrays.
* **Primitives** — strings, the numeric types (mapped to Powwow's decimal `Number`), `bool`, and `DateTime`.

Conversion is recursive, so nested dictionaries, lists, and objects map onto Powwow's nested objects and arrays. Inside the template these follow the normal [data type](datatypes.md) rules.

```csharp
var data = new ExpandoObject();
var dict = (IDictionary<string, object>)data;
dict["user"] = new { name = "Ada", roles = new[] { "admin", "engineer" } };
dict["count"] = 3;

// Template: "{{user.name}} has {{length(user.roles)}} roles"
// Output:   "Ada has 3 roles"
```

## Interpreter options

The constructor takes three optional arguments:

```csharp
public Interpreter(
    ITemplateResolver templateResolver = null,
    IDataverseService dataverseService = null,
    int maxRecursionDepth = 1000)
```

* **`templateResolver`** — enables the `{{ include }}` directive (see [Template composition](#template-composition)).
* **`dataverseService`** — enables the `fetch` function (see [Dataverse integration](#dataverse-integration)).
* **`maxRecursionDepth`** — guards against runaway recursion; the default is 1000.

## Custom functions

Register host functions with `RegisterFunction(name, parameters, implementation)`. They are then callable from templates exactly like the built-ins, including overloading and the same argument-type matching.

```csharp
interpreter.RegisterFunction(
    "greet",
    new List<ParameterDefinition> { new ParameterDefinition(typeof(StringValue)) },
    (context, callSite, args) =>
        new Value(new StringValue($"Hello, {(args[0].ValueOf() as StringValue).Value()}!")));

// Template: {{ greet("World") }}
// Output:   Hello, World!
```

A few points on the signature:

* **Parameter types.** Each `ParameterDefinition` names the expected value type — `typeof(StringValue)`, `typeof(NumberValue)`, `typeof(BooleanValue)`, `typeof(ArrayValue)`, `typeof(ObjectValue)`, `typeof(DateTimeValue)`, or `typeof(LambdaValue)`. Use `typeof(Box)` to accept a value of any type.
* **Optional parameters.** `new ParameterDefinition(typeof(NumberValue), isOptional: true, defaultValue: new Value(new NumberValue(1)))` declares a trailing optional argument with a default.
* **Reading arguments.** `args[i].ValueOf()` returns the underlying boxed value; cast it to the expected type and call `.Value()` to get the CLR value (for example `(args[0].ValueOf() as NumberValue).Value()` yields a `decimal`).
* **Returning a value.** Wrap a value box in a `Value`, e.g. `new Value(new StringValue(...))` or `new Value(new ArrayValue(list))`.
* **Overloading.** Call `RegisterFunction` multiple times with the same name and different parameter lists to provide overloads; registering two identical signatures throws an `InitializationException`.
* **`context` and `callSite`.** These carry execution state and source location; pass them through when constructing a `TemplateEvaluationException` so errors report a useful location.

## Template composition

The `{{ include name }}` directive renders another template inline. To enable it, supply an `ITemplateResolver` that maps a name to template source:

```csharp
public interface ITemplateResolver
{
    string ResolveTemplate(string templateName);
}
```

A ready-made in-memory implementation, `TemplateRegistry`, is included:

```csharp
var registry = new TemplateRegistry();
registry.RegisterTemplate("header", "<h1>{{title}}</h1>");
registry.RegisterTemplate("footer", "<footer>{{copyright}}</footer>");

var interpreter = new Interpreter(registry);

var data = new ExpandoObject();
var dict = (IDictionary<string, object>)data;
dict["title"] = "My Page";
dict["content"] = "Hello World";
dict["copyright"] = "© 2025";

string output = interpreter.Interpret(
    "{{include header}}<main>{{content}}</main>{{include footer}}",
    data);
// <h1>My Page</h1><main>Hello World</main><footer>© 2025</footer>
```

Included templates render with access to the same data and functions as the template that includes them, and they may include further templates. **Circular includes are detected** and raise a parsing error rather than looping forever. To resolve templates from elsewhere — a database, the file system, Dataverse — implement `ITemplateResolver` yourself; the only requirement is to return template source for a name (or throw if it is unknown).

## Dataverse integration

Supplying an `IDataverseService` enables the `fetch` function, which runs a FetchXML query and returns the results as Powwow values:

```csharp
public interface IDataverseService
{
    Value RetrieveMultiple(string fetchXml);
}
```

The bundled `DataverseService` adapts a standard `IOrganizationService`:

```csharp
var dataverseService = new DataverseService(organizationService); // IOrganizationService from the plugin context
var interpreter = new Interpreter(dataverseService: dataverseService);

// Template: {{ fetch("<fetch><entity name='account'><attribute name='name' /></entity></fetch>") }}
```

`RetrieveMultiple` returns an array of objects — one per entity — whose fields are the queried attributes. Special attribute types such as `EntityReference` and `OptionSetValue` are converted to objects with their constituent parts. Without a Dataverse service configured, calling `fetch` raises an error.

## Error handling

The interpreter signals problems with exceptions, all under `PowwowLang.Exceptions`:

* **`TemplateParsingException`** — the template is syntactically invalid, or an include cannot be resolved or is circular. Carries the source location.
* **`TemplateEvaluationException`** — an error during evaluation (a type mismatch, a missing object field, an out-of-bounds index, a custom-function failure). Carries execution context and the offending call site for diagnostics.
* **`InitializationException`** — a setup problem, such as registering two functions with identical signatures or resolving an unknown template from `TemplateRegistry`.

Wrap `Interpret` in a `try`/`catch` to surface these to your host's logging or error UI.

## See also

* [Template syntax and directives](directives.md) — the language features these APIs drive
* [Built-in functions](functions.md) — the function library custom functions sit alongside
* [Data types](datatypes.md) — how host data appears inside a template
