import { test } from "node:test";
import assert from "node:assert/strict";
import { Interpreter, type TemplateResolver } from "./evaluator.ts";

const render = (template: string, data: unknown = null) => new Interpreter().interpret(template, data);

test("interpolation with data", () => {
  assert.equal(render("Hello, {{name}}!", { name: "Ada" }), "Hello, Ada!");
});

test("decimal scale parity in rendered arithmetic", () => {
  assert.equal(render("{{ 2.5 + 1.5 }}"), "4.0");
  assert.equal(render("{{ 10 / 4 }}"), "2.5");
  assert.equal(render("{{ 2 + 3 }}"), "5");
});

test("ComplexArithmetic test parity (renders 9.0)", () => {
  assert.equal(render("Result: {{((5 * (var2 + 1.5)) / 2) - 1}}", { var2: 2.5 }), "Result: 9.0");
});

test("datatypes.md PMDAS example renders 8", () => {
  assert.equal(render("{{ (c + a * b - d) / c }}", { a: 5, b: 3, c: 2, d: 1 }), "8");
});

test("whitespace control around a let", () => {
  assert.equal(render('{{- let name = "Ada" -}}\nHello, {{ name }}!'), "Hello, Ada!");
});

test("for loop with trimming (datatypes.md parity)", () => {
  const template = '{{- let xs = ["red", "green", "blue"] -}}\n{{- for x in xs -}}\n- {{x}}\n{{ /for -}}';
  assert.equal(render(template), "- red\n- green\n- blue\n");
});

test("if / elseif / else", () => {
  const t =
    "{{- if score >= 90 -}}A{{- elseif score >= 80 -}}B{{- elseif score >= 70 -}}C{{- else -}}F{{- /if -}}";
  assert.equal(render(t, { score: 72 }), "C");
});

test("inline if() builtin", () => {
  const t = 'Hi {{ user.name }}, {{ if(user.premium, "thanks for being premium!", "enjoy your free account!") }}';
  assert.equal(render(t, { user: { name: "Ada", premium: true } }), "Hi Ada, thanks for being premium!");
});

test("typeof renders type<Name> and compares to a type literal", () => {
  assert.equal(render("{{ typeof(a) }}", { a: 1 }), "type<Number>");
  assert.equal(render("{{ typeof(a) == Number }}", { a: 1 }), "true");
  assert.equal(render('{{ typeof(b) == Number }}', { b: "blue" }), "false");
});

test("higher-order array functions", () => {
  assert.equal(render("{{ map([1, 2, 3, 4], (x) => x * x) }}"), "[1, 4, 9, 16]");
  assert.equal(render("{{ filter([1, 2, 3, 4, 5], (x) => x > 2) }}"), "[3, 4, 5]");
  assert.equal(render("{{ reduce([1, 2, 3, 4], (acc, x) => acc + x, 0) }}"), "10");
});

test("object literal output and field access", () => {
  assert.equal(render("{{ obj(x: 1, y: 2) }}"), "{x: 1, y: 2}");
  assert.equal(render("{{ user.name }} is {{ user.age }}", { user: { name: "Ada", age: 36 } }), "Ada is 36");
});

test("contains guard", () => {
  const t = "{{ if contains(user, \"email\") }}{{ user.email }}{{ else }}no email{{ /if }}";
  assert.equal(render(t, { user: { name: "Ada" } }), "no email");
});

test("closures capture their defining scope", () => {
  assert.equal(render("{{- let factor = 10 -}}{{- let scale = (x) => x * factor -}}{{ scale(5) }}"), "50");
});

test("recursion via a named lambda + lazy if", () => {
  const t = "{{- let fact = (n) => if(n <= 1, 1, n * fact(n - 1)) -}}{{ fact(5) }}";
  assert.equal(render(t), "120");
});

test("lambda local statements", () => {
  const t = "{{- let f = (a, b) => let a2 = a * a, let b2 = b * b, a2 + b2 -}}{{ f(3, 4) }}";
  assert.equal(render(t), "25");
});

test("reference semantics: objects are shared, scalars copied", () => {
  assert.equal(render("{{- let x = obj(a: 1) -}}{{- let y = x -}}{{- mut y.a = 3 -}}{{ x.a }}/{{ y.a }}"), "3/3");
  assert.equal(render("{{- let a = 1 -}}{{- let b = a -}}{{- mut b = 2 -}}{{ a }}/{{ b }}"), "1/2");
});

test("capture stores rendered text", () => {
  assert.equal(render('{{- capture g -}}Hi {{ "Ada" }}{{- /capture -}}{{ g }}/{{ g }}'), "Hi Ada/Hi Ada");
});

test("literal block is verbatim", () => {
  assert.equal(render("{{ literal }}raw {{ x }} text{{ /literal }}"), "raw {{ x }} text");
});

test("comments emit nothing", () => {
  assert.equal(render("a{{* hidden *}}b"), "ab");
});

test("equality is reference identity for arrays/objects", () => {
  assert.equal(render("{{- let a = [1, 2, 3] -}}{{- let b = [1, 2, 3] -}}{{ a == b }}"), "false");
  assert.equal(render("{{- let a = [1, 2, 3] -}}{{- let c = a -}}{{ a == c }}"), "true");
});

test("string and number builtins", () => {
  assert.equal(render('{{ toUpper("hi") }}'), "HI");
  assert.equal(render("{{ length([1, 2, 3]) }}"), "3");
  assert.equal(render('{{ join(["a", "b", "c"], ", ") }}'), "a, b, c");
  assert.equal(render("{{ round(3.14159, 2) }}"), "3.14");
});

test("template composition via include", () => {
  const templates: Record<string, string> = {
    header: "<h1>{{title}}</h1>",
    footer: "<footer>{{copyright}}</footer>",
  };
  const resolver: TemplateResolver = (name) => {
    const t = templates[name];
    if (t === undefined) throw new Error(`unknown template ${name}`);
    return t;
  };
  const interp = new Interpreter({ resolver });
  const out = interp.interpret("{{include header}}<main>{{content}}</main>{{include footer}}", {
    title: "My Page",
    content: "Hello World",
    copyright: "© 2025",
  });
  assert.equal(out, "<h1>My Page</h1><main>Hello World</main><footer>© 2025</footer>");
});
