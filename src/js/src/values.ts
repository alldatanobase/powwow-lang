/**
 * The Powwow value model, ported from src/dotnet/Interpreter/Types.
 *
 * .NET splits this into `Box` (the typed value, with Output/JsonSerialize) and
 * `Value` (a mutable cell holding a Box, reassignable via `Mutate`). We mirror that:
 *
 *   - `Box` subclasses carry the actual data and the rendering logic.
 *   - `PowwowValue` is the cell. `let y = x` makes a new cell pointing at the SAME box,
 *     so arrays/objects are reference types (mutations to their box are shared) while a
 *     `mut y = ...` only swaps cell y's box. Reference identity == is box identity.
 *
 * Output() forms must match .NET byte-for-byte. See js-port-parity.md.
 */

import { PowwowNumber } from "./number.ts";

export type ValueType =
  | "Object"
  | "Array"
  | "String"
  | "Number"
  | "Boolean"
  | "DateTime"
  | "Lazy"
  | "Function"
  | "Type";

/** JSON string escaping. NOTE: .NET's JavaScriptSerializer also escapes <, >, & as \u00xx;
 *  that refinement is deferred (see js-port-parity.md, JSON section). */
function jsonEscapeString(s: string): string {
  let out = '"';
  for (const ch of s) {
    const code = ch.codePointAt(0)!;
    switch (ch) {
      case '"': out += '\\"'; break;
      case "\\": out += "\\\\"; break;
      case "\b": out += "\\b"; break;
      case "\f": out += "\\f"; break;
      case "\n": out += "\\n"; break;
      case "\r": out += "\\r"; break;
      case "\t": out += "\\t"; break;
      default:
        if (code < 0x20) {
          out += "\\u" + code.toString(16).padStart(4, "0");
        } else {
          out += ch;
        }
    }
  }
  return out + '"';
}

export abstract class Box {
  abstract typeOf(): ValueType;
  abstract output(): string;
  abstract jsonSerialize(): string;
}

export class NumberBox extends Box {
  readonly value: PowwowNumber;
  constructor(value: PowwowNumber) {
    super();
    this.value = value;
  }
  typeOf(): ValueType { return "Number"; }
  output(): string { return this.value.toString(); }
  jsonSerialize(): string { return this.value.toString(); }
}

export class StringBox extends Box {
  readonly value: string;
  constructor(value: string) {
    super();
    this.value = value;
  }
  typeOf(): ValueType { return "String"; }
  output(): string { return this.value; }
  jsonSerialize(): string { return jsonEscapeString(this.value); }
}

export class BooleanBox extends Box {
  readonly value: boolean;
  constructor(value: boolean) {
    super();
    this.value = value;
  }
  typeOf(): ValueType { return "Boolean"; }
  output(): string { return this.value ? "true" : "false"; }
  jsonSerialize(): string { return this.value ? "true" : "false"; }
}

export class ArrayBox extends Box {
  readonly items: PowwowValue[];
  constructor(items: PowwowValue[]) {
    super();
    this.items = items;
  }
  typeOf(): ValueType { return "Array"; }
  output(): string {
    return "[" + this.items.map((v) => v.output()).join(", ") + "]";
  }
  jsonSerialize(): string {
    return "[" + this.items.map((v) => v.jsonSerialize()).join(",") + "]";
  }
}

export class ObjectBox extends Box {
  /** Insertion order is significant for Output() parity; Map preserves it. */
  readonly fields: Map<string, PowwowValue>;
  constructor(fields: Map<string, PowwowValue>) {
    super();
    this.fields = fields;
  }
  typeOf(): ValueType { return "Object"; }
  output(): string {
    const parts: string[] = [];
    for (const [key, value] of this.fields) {
      parts.push(key + ": " + value.output());
    }
    return "{" + parts.join(", ") + "}";
  }
  jsonSerialize(): string {
    const parts: string[] = [];
    for (const [key, value] of this.fields) {
      parts.push('"' + key + '":' + value.jsonSerialize());
    }
    return "{" + parts.join(",") + "}";
  }
}

/** A lambda's invocation closure. `callerContext`/`callSite` are typed loosely
 *  (as unknown) so values.ts stays free of context/AST imports. */
export type LambdaInvoke = (callerContext: unknown, callSite: unknown, args: PowwowValue[]) => PowwowValue;

export class LambdaBox extends Box {
  readonly parameterNames: string[];
  readonly invoke: LambdaInvoke;
  constructor(parameterNames: string[], invoke: LambdaInvoke) {
    super();
    this.parameterNames = parameterNames;
    this.invoke = invoke;
  }
  typeOf(): ValueType { return "Function"; }
  output(): string { return `lambda(${this.parameterNames.join(", ")})`; }
  jsonSerialize(): string { return `"func<lambda(${this.parameterNames.join(", ")})>"`; }
}

/** A deferred expression — used for the lazily-evaluated `if` builtin. */
export class LazyBox extends Box {
  readonly evaluate: () => PowwowValue;
  constructor(evaluate: () => PowwowValue) {
    super();
    this.evaluate = evaluate;
  }
  typeOf(): ValueType { return "Lazy"; }
  output(): string { return this.evaluate().output(); }
  jsonSerialize(): string { return this.evaluate().jsonSerialize(); }
}

export class FunctionReferenceBox extends Box {
  readonly name: string;
  constructor(name: string) {
    super();
    this.name = name;
  }
  typeOf(): ValueType { return "Function"; }
  output(): string { return `func<${this.name}>`; }
  jsonSerialize(): string { return `"func<${this.name}>"`; }
}

export class TypeBox extends Box {
  readonly value: ValueType;
  constructor(value: ValueType) {
    super();
    this.value = value;
  }
  typeOf(): ValueType { return "Type"; }
  output(): string { return `type<${this.value}>`; }
  jsonSerialize(): string { return `"type<${this.value}>"`; }
}

/** A mutable cell holding a Box. See file header for the reference-semantics rationale. */
export class PowwowValue {
  private box: Box;
  constructor(box: Box) {
    this.box = box;
  }
  getBox(): Box { return this.box; }
  mutate(box: Box): void { this.box = box; }
  typeOf(): ValueType { return this.box.typeOf(); }
  output(): string { return this.box.output(); }
  jsonSerialize(): string { return this.box.jsonSerialize(); }
}

// Convenience constructors mirroring `new Value(new XBox(...))`.
export const num = (value: PowwowNumber): PowwowValue => new PowwowValue(new NumberBox(value));
export const str = (value: string): PowwowValue => new PowwowValue(new StringBox(value));
export const bool = (value: boolean): PowwowValue => new PowwowValue(new BooleanBox(value));
export const array = (items: PowwowValue[]): PowwowValue => new PowwowValue(new ArrayBox(items));
export const object = (fields: Map<string, PowwowValue>): PowwowValue => new PowwowValue(new ObjectBox(fields));
export const typeLiteral = (value: ValueType): PowwowValue => new PowwowValue(new TypeBox(value));
