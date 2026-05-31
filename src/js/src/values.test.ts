import { test } from "node:test";
import assert from "node:assert/strict";
import { PowwowNumber } from "./number.ts";
import { num, str, bool, array, object, typeLiteral, PowwowValue } from "./values.ts";

const N = (s: string) => num(PowwowNumber.fromString(s));
const obj = (entries: [string, PowwowValue][]) => object(new Map(entries));

test("scalar output forms", () => {
  assert.equal(N("2.1").output(), "2.1");
  assert.equal(str("hello").output(), "hello");
  assert.equal(bool(true).output(), "true");
  assert.equal(bool(false).output(), "false");
});

test("array output: comma-space joined, strings unquoted, nesting (datatypes.md parity)", () => {
  assert.equal(array([N("1"), N("2"), N("3")]).output(), "[1, 2, 3]");
  assert.equal(
    array([N("1"), str("two"), bool(false), array([N("4"), N("5")])]).output(),
    "[1, two, false, [4, 5]]",
  );
  assert.equal(array([]).output(), "[]");
});

test("object output: unquoted keys, insertion order, nesting (datatypes.md parity)", () => {
  assert.equal(obj([["x", N("1")], ["y", N("2")]]).output(), "{x: 1, y: 2}");
  assert.equal(
    obj([["name", str("Ada")], ["age", N("36")], ["tags", array([str("admin"), str("engineer")])]]).output(),
    "{name: Ada, age: 36, tags: [admin, engineer]}",
  );
  assert.equal(obj([["outer", obj([["inner", str("hello")]])]]).output(), "{outer: {inner: hello}}");
});

test("typeof renders as type<Name> (NOT bare Name)", () => {
  assert.equal(typeLiteral("Number").output(), "type<Number>");
  assert.equal(typeLiteral("String").output(), "type<String>");
  assert.equal(typeLiteral("Function").output(), "type<Function>");
});

test("JSON serialization forms", () => {
  assert.equal(N("4.0").jsonSerialize(), "4.0");
  assert.equal(str("hi").jsonSerialize(), '"hi"');
  assert.equal(str('a"b').jsonSerialize(), '"a\\"b"');
  assert.equal(bool(true).jsonSerialize(), "true");
  assert.equal(
    obj([["name", str("Ada")], ["age", N("36")]]).jsonSerialize(),
    '{"name":"Ada","age":36}',
  );
  assert.equal(array([N("1"), str("two")]).jsonSerialize(), '[1,"two"]');
});

test("reference semantics: shared box vs swapped cell", () => {
  const x = obj([["a", N("1")]]);
  const y = x; // same cell -> same box
  assert.equal(x.getBox() === y.getBox(), true);

  // Simulating `let z = x` (new cell, same box) then `mut z = ...` (swap z's box only).
  const z = new PowwowValue(x.getBox());
  assert.equal(z.getBox() === x.getBox(), true);
  z.mutate(N("99").getBox());
  assert.equal(x.output(), "{a: 1}"); // x unaffected by z's reassignment
  assert.equal(z.output(), "99");
});
