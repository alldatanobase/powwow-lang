import { test } from "node:test";
import assert from "node:assert/strict";
import { Lexer } from "./lexer.ts";
import type { TokenType } from "./token.ts";

const lex = (input: string) => new Lexer().tokenize(input);
/** Compact [type, value] view of the token stream. */
const shape = (input: string): [TokenType, string][] =>
  lex(input).map((t) => [t.type, t.value]);

test("plain text and a simple interpolation", () => {
  assert.deepEqual(shape("Hi {{ name }}!"), [
    ["Text", "Hi"],
    ["Whitespace", " "],
    ["DirectiveStart", "{{"],
    ["Variable", "name"],
    ["DirectiveEnd", "}}"],
    ["Text", "!"],
  ]);
});

test("whitespace-control markers are distinct directive delimiters", () => {
  assert.deepEqual(shape("{{- x -}}"), [
    ["DirectiveStart", "{{-"],
    ["Variable", "x"],
    ["DirectiveEnd", "-}}"],
  ]);
});

test("function call vs bare variable (lookahead for '(')", () => {
  assert.deepEqual(shape("{{ toUpper(name) }}"), [
    ["DirectiveStart", "{{"],
    ["Function", "toUpper"],
    ["LeftParen", "("],
    ["Variable", "name"],
    ["RightParen", ")"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("field access after a dot is a Field token", () => {
  assert.deepEqual(shape("{{ user.name }}"), [
    ["DirectiveStart", "{{"],
    ["Variable", "user"],
    ["Dot", "."],
    ["Field", "name"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("let assignment with a number literal", () => {
  assert.deepEqual(shape("{{ let a = -2.5 }}"), [
    ["DirectiveStart", "{{"],
    ["Let", "let"],
    ["Variable", "a"],
    ["Assignment", "="],
    ["Number", "-2.5"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("string literal with escapes is unescaped into the token value", () => {
  const tokens = lex('{{ "a\\nb\\"c" }}');
  const strTok = tokens.find((t) => t.type === "String");
  assert.equal(strTok?.value, 'a\nb"c');
});

test("operators and booleans", () => {
  assert.deepEqual(shape("{{ a >= 1 && !b || true }}"), [
    ["DirectiveStart", "{{"],
    ["Variable", "a"],
    ["GreaterThanEqual", ">="],
    ["Number", "1"],
    ["And", "&&"],
    ["Not", "!"],
    ["Variable", "b"],
    ["Or", "||"],
    ["True", "true"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("object construction and arrays", () => {
  assert.deepEqual(shape("{{ obj(x: [1, 2]) }}"), [
    ["DirectiveStart", "{{"],
    ["ObjectStart", "obj("],
    ["Variable", "x"],
    ["Colon", ":"],
    ["LeftBracket", "["],
    ["Number", "1"],
    ["Comma", ","],
    ["Number", "2"],
    ["RightBracket", "]"],
    ["RightParen", ")"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("type literals tokenize as Type", () => {
  assert.deepEqual(shape("{{ typeof(a) == Number }}"), [
    ["DirectiveStart", "{{"],
    ["Function", "typeof"],
    ["LeftParen", "("],
    ["Variable", "a"],
    ["RightParen", ")"],
    ["Equal", "=="],
    ["Type", "Number"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("comments produce comment tokens and no inner tokens", () => {
  assert.deepEqual(shape("{{* hello *}}"), [
    ["DirectiveStart", "{{"],
    ["CommentStart", "*"],
    ["CommentEnd", "*"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("literal block captures its body verbatim as Text", () => {
  const tokens = lex("{{ literal }}raw {{ x }} text{{ /literal }}");
  const types = tokens.map((t) => t.type);
  assert.deepEqual(types, [
    "DirectiveStart",
    "Literal",
    "DirectiveEnd",
    "Text",
    "DirectiveStart",
    "EndLiteral",
    "DirectiveEnd",
  ]);
  const text = tokens.find((t) => t.type === "Text");
  assert.equal(text?.value, "raw {{ x }} text");
});

test("for/in/endfor keywords", () => {
  assert.deepEqual(shape("{{ for x in xs }}{{ /for }}"), [
    ["DirectiveStart", "{{"],
    ["For", "for"],
    ["Variable", "x"],
    ["In", "in"],
    ["Variable", "xs"],
    ["DirectiveEnd", "}}"],
    ["DirectiveStart", "{{"],
    ["EndFor", "/for"],
    ["DirectiveEnd", "}}"],
  ]);
});

test("newlines are their own tokens (whitespace control depends on it)", () => {
  const types = lex("a\nb").map((t) => t.type);
  assert.deepEqual(types, ["Text", "Newline", "Text"]);
});
