import { test } from "node:test";
import assert from "node:assert/strict";
import { Lexer } from "./lexer.ts";
import { Parser } from "./parser.ts";
import {
  type AstNode,
  TemplateNode,
  BinaryNode,
  UnaryNode,
  LetNode,
  MutationNode,
  ForNode,
  IfNode,
  CaptureNode,
  IncludeNode,
  LambdaNode,
  ObjectCreationNode,
  ArrayNode,
  FieldAccessNode,
  InvocationNode,
  VariableNode,
  NumberNode,
  TypeNode,
} from "./ast.ts";

function parse(input: string): TemplateNode {
  const tokens = new Lexer().tokenize(input);
  const ast = new Parser().parse(tokens);
  assert.ok(ast instanceof TemplateNode);
  return ast;
}
const childKinds = (input: string) => parse(input).children.map((c) => c.kind);

test("text and an interpolation produce text + expression nodes", () => {
  const ast = parse("Hi {{ name }}!");
  assert.deepEqual(ast.children.map((c) => c.kind), ["Text", "Whitespace", "Variable", "Text"]);
});

test("let / mut (with dotted field) statements", () => {
  const letAst = parse("{{ let a = 1 }}");
  const letNode = letAst.children[0]!;
  assert.ok(letNode instanceof LetNode);
  assert.equal(letNode.variableName, "a");
  assert.ok(letNode.expression instanceof NumberNode);

  const mutNode = parse("{{ mut user.age = 37 }}").children[0]!;
  assert.ok(mutNode instanceof MutationNode);
  assert.equal(mutNode.variableName, "user.age");
});

test("operator precedence: 1 + 2 * 3 -> (+ 1 (* 2 3))", () => {
  const expr = parse("{{ 1 + 2 * 3 }}").children[0]! as BinaryNode;
  assert.ok(expr instanceof BinaryNode);
  assert.equal(expr.operator, "Plus");
  assert.ok(expr.left instanceof NumberNode);
  const right = expr.right as BinaryNode;
  assert.ok(right instanceof BinaryNode);
  assert.equal(right.operator, "Multiply");
});

test("boolean precedence: a || b && c -> (|| a (&& b c))", () => {
  const expr = parse("{{ a || b && c }}").children[0]! as BinaryNode;
  assert.equal(expr.operator, "Or");
  assert.ok(expr.left instanceof VariableNode);
  const right = expr.right as BinaryNode;
  assert.equal(right.operator, "And");
});

test("unary not", () => {
  const expr = parse("{{ !ready }}").children[0]!;
  assert.ok(expr instanceof UnaryNode);
  assert.equal(expr.operator, "Not");
});

test("field access and invocation chains", () => {
  const expr = parse("{{ user.greet(name) }}").children[0]!;
  assert.ok(expr instanceof InvocationNode);
  assert.ok(expr.callable instanceof FieldAccessNode);
  assert.equal((expr.callable as FieldAccessNode).fieldName, "greet");
});

test("array and object construction", () => {
  const arr = parse("{{ [1, 2, 3] }}").children[0]!;
  assert.ok(arr instanceof ArrayNode);
  assert.equal(arr.elements.length, 3);

  const obj = parse("{{ obj(x: 1, y: 2) }}").children[0]! as ObjectCreationNode;
  assert.ok(obj instanceof ObjectCreationNode);
  assert.deepEqual(obj.fields.map((f) => f.key), ["x", "y"]);
});

test("lambda with parameters", () => {
  const lam = parse("{{ let f = (a, b) => a + b }}").children[0]! as LetNode;
  const lambda = lam.expression as LambdaNode;
  assert.ok(lambda instanceof LambdaNode);
  assert.deepEqual(lambda.parameters, ["a", "b"]);
  assert.ok(lambda.body instanceof BinaryNode);
});

test("lambda with local statements before the result expression", () => {
  const lam = parse("{{ let f = (x) => let y = x, y + 1 }}").children[0]! as LetNode;
  const lambda = lam.expression as LambdaNode;
  assert.equal(lambda.statements.length, 1);
  assert.equal(lambda.statements[0]!.variableName, "y");
  assert.equal(lambda.statements[0]!.statementType, "Declaration");
});

test("grouping vs lambda disambiguation: (1 + 2) is a group, not a lambda", () => {
  const expr = parse("{{ (1 + 2) * 3 }}").children[0]! as BinaryNode;
  assert.ok(expr instanceof BinaryNode);
  assert.equal(expr.operator, "Multiply");
  assert.ok(expr.left instanceof BinaryNode); // the grouped (1 + 2)
});

test("if / elseif / else / endif", () => {
  const node = parse("{{ if a }}A{{ elseif b }}B{{ else }}C{{ /if }}").children[0]! as IfNode;
  assert.ok(node instanceof IfNode);
  assert.equal(node.branches.length, 2);
  assert.ok(node.elseBranch !== null);
});

test("for loop body parses as a template", () => {
  const node = parse("{{ for x in xs }}- {{x}}{{ /for }}").children[0]! as ForNode;
  assert.ok(node instanceof ForNode);
  assert.equal(node.iteratorName, "x");
  assert.ok(node.body instanceof TemplateNode);
});

test("capture and include", () => {
  const cap = parse("{{ capture g }}hi{{ /capture }}").children[0]!;
  assert.ok(cap instanceof CaptureNode);
  const inc = parse("{{ include header }}").children[0]! as IncludeNode;
  assert.ok(inc instanceof IncludeNode);
  assert.equal(inc.templateName, "header");
});

test("type literal", () => {
  const cmp = parse("{{ typeof(a) == Number }}").children[0]! as BinaryNode;
  assert.equal(cmp.operator, "Equal");
  assert.ok(cmp.right instanceof TypeNode);
  assert.equal((cmp.right as TypeNode).type, "Number");
});

test("whitespace control trims adjacent newline tokens", () => {
  // The newline after `a` and the newline before `b` should be trimmed by {{- and -}}.
  assert.deepEqual(childKinds("a\n{{- x -}}\nb"), ["Text", "Variable", "Text"]);
  // Without markers, the newlines survive as nodes.
  assert.deepEqual(childKinds("a\n{{ x }}\nb"), ["Text", "Newline", "Variable", "Newline", "Text"]);
});

test("comments produce no node", () => {
  assert.deepEqual(childKinds("{{* nothing *}}"), []);
});

test("unclosed if reports a parsing error", () => {
  assert.throws(() => parse("{{ if a }}body"), /Template parsing failed|Unclosed if/);
});
