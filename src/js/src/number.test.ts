import { test } from "node:test";
import assert from "node:assert/strict";
import { PowwowNumber } from "./number.ts";

const n = (s: string) => PowwowNumber.fromString(s);

test("literal rendering preserves scale and avoids exponential notation", () => {
  assert.equal(n("0").toString(), "0");
  assert.equal(n("1").toString(), "1");
  assert.equal(n("2.1").toString(), "2.1");
  assert.equal(n("-3.1").toString(), "-3.1");
  assert.equal(n("-0.000000004").toString(), "-0.000000004");
  assert.equal(n("42").toString(), "42");
  assert.equal(n("-.3").toString(), "-0.3");
});

test("addition takes the larger scale (2.5 + 1.5 = 4.0)", () => {
  assert.equal(n("2.5").add(n("1.5")).toString(), "4.0");
  assert.equal(n("2").add(n("3")).toString(), "5");
  assert.equal(n("10.0").subtract(n("1")).toString(), "9.0");
});

test("multiplication adds scales", () => {
  assert.equal(n("2.5").multiply(n("2")).toString(), "5.0");
  assert.equal(n("5").multiply(n("4.0")).toString(), "20.0");
});

test("division: preferred scale, exactness, and trailing-zero handling", () => {
  assert.equal(n("10").divide(n("4")).toString(), "2.5"); // not exact at scale 0 -> extends
  assert.equal(n("16").divide(n("2")).toString(), "8"); // exact, preferred scale 0
  assert.equal(n("20.0").divide(n("2")).toString(), "10.0"); // preferred scale 1
});

test("divide by zero throws", () => {
  assert.throws(() => n("1").divide(n("0")), /divide by zero/i);
});

test("ComplexArithmetic test parity: ((5 * (2.5 + 1.5)) / 2) - 1 = 9.0", () => {
  const result = n("5").multiply(n("2.5").add(n("1.5"))).divide(n("2")).subtract(n("1"));
  assert.equal(result.toString(), "9.0");
});

test("datatypes.md parity: (c + a*b - d)/c with a=5 b=3 c=2 d=1 = 8", () => {
  const a = n("5"), b = n("3"), c = n("2"), d = n("1");
  const result = c.add(a.multiply(b)).subtract(d).divide(c);
  assert.equal(result.toString(), "8");
});

test("round uses banker's rounding (half to even)", () => {
  assert.equal(n("2.5").round(0).toString(), "2");
  assert.equal(n("3.5").round(0).toString(), "4");
  assert.equal(n("0.5").round(0).toString(), "0");
  assert.equal(n("1.5").round(0).toString(), "2");
  assert.equal(n("3.14159").round(2).toString(), "3.14");
  assert.equal(n("3.14159").round(0).toString(), "3");
});

test("toIntValue (Convert.ToInt32) is also half-even", () => {
  assert.equal(n("2.5").toIntValue(), 2);
  assert.equal(n("3.5").toIntValue(), 4);
  assert.equal(n("7.9").toIntValue(), 8);
});

test("floor and ceil", () => {
  assert.equal(n("3.7").floor().toString(), "3");
  assert.equal(n("3.2").ceil().toString(), "4");
  assert.equal(n("-3.2").floor().toString(), "-4");
  assert.equal(n("-3.7").ceil().toString(), "-3");
});

test("equality is scale-insensitive like .NET decimal ==", () => {
  assert.equal(n("4.0").equals(n("4")), true);
  assert.equal(n("2.50").equals(n("2.5")), true);
  assert.equal(n("2.5").equals(n("2.6")), false);
});
