/**
 * FunctionRegistry — ported from src/dotnet/Interpreter/Lib/FunctionRegistry.cs.
 *
 * Holds built-in (and host-registered) functions, with overload resolution by
 * argument type. This milestone ports a substantial starter set of built-ins;
 * the DateTime family, JSON, encoding, uri, order/group remain (see README).
 */

import {
  PowwowValue,
  NumberBox,
  StringBox,
  BooleanBox,
  ArrayBox,
  ObjectBox,
  LambdaBox,
  FunctionReferenceBox,
  TypeBox,
  LazyBox,
  num,
  str,
  bool,
  array,
  typeLiteral,
  type Box,
} from "./values.ts";
import { PowwowNumber } from "./number.ts";
import { InnerEvaluationError, TemplateEvaluationError, InitializationError } from "./errors.ts";
import type { ExecutionContext } from "./context.ts";
import type { AstNode } from "./ast.ts";

export type ParamType =
  | "Number"
  | "String"
  | "Boolean"
  | "Array"
  | "Object"
  | "DateTime"
  | "Lambda"
  | "FunctionRef"
  | "Type"
  | "Lazy"
  | "Any";

export interface ParameterDefinition {
  type: ParamType;
  optional?: boolean;
  default?: PowwowValue;
}

export type FunctionImpl = (context: ExecutionContext, callSite: AstNode | null, args: PowwowValue[]) => PowwowValue;

interface FunctionDefinition {
  name: string;
  params: ParameterDefinition[];
  impl: FunctionImpl;
  lazy: boolean;
}

function requiredCount(params: ParameterDefinition[]): number {
  return params.filter((p) => !p.optional).length;
}

function paramTagOf(box: Box): ParamType {
  if (box instanceof NumberBox) return "Number";
  if (box instanceof StringBox) return "String";
  if (box instanceof BooleanBox) return "Boolean";
  if (box instanceof ArrayBox) return "Array";
  if (box instanceof ObjectBox) return "Object";
  if (box instanceof LambdaBox) return "Lambda";
  if (box instanceof FunctionReferenceBox) return "FunctionRef";
  if (box instanceof TypeBox) return "Type";
  if (box instanceof LazyBox) return "Lazy";
  return "Any";
}

export class FunctionRegistry {
  private functions = new Map<string, FunctionDefinition[]>();

  constructor() {
    registerBuiltins(this);
  }

  hasFunction(name: string): boolean {
    return this.functions.has(name);
  }

  register(name: string, params: ParameterDefinition[], impl: FunctionImpl, lazy = false): void {
    const definition: FunctionDefinition = { name, params, impl, lazy };
    let overloads = this.functions.get(name);
    if (!overloads) {
      overloads = [];
      this.functions.set(name, overloads);
    }
    const clash = overloads.find(
      (f) =>
        f.params.length === params.length &&
        f.params.every((p, i) => p.type === params[i]!.type && !!p.optional === !!params[i]!.optional),
    );
    if (clash) {
      throw new InitializationError(`Function '${name}' is already registered with the same parameter types`);
    }
    overloads.push(definition);
  }

  lazyFunctionExists(name: string, argumentCount: number): boolean {
    const overloads = this.functions.get(name);
    if (!overloads) return false;
    return overloads.some(
      (f) => argumentCount >= requiredCount(f.params) && argumentCount <= f.params.length && f.lazy,
    );
  }

  /** Resolve the best-matching overload; returns the definition and the arguments
   *  padded with defaults, or null when no overload matches. */
  tryGetFunction(
    name: string,
    args: PowwowValue[],
  ): { definition: FunctionDefinition; effectiveArgs: PowwowValue[] } | null {
    const overloads = this.functions.get(name);
    if (!overloads) return null;

    const candidates = overloads.filter(
      (f) => args.length >= requiredCount(f.params) && args.length <= f.params.length,
    );
    if (candidates.length === 0) return null;

    const scored = candidates
      .map((f) => ({ f, score: this.scoreTypeMatch(f.params, args) }))
      .filter((x) => x.score >= 0)
      .sort((a, b) => b.score - a.score);

    if (scored.length === 0) return null;
    if (scored.length > 1 && scored[0]!.score === scored[1]!.score) {
      throw new InnerEvaluationError(
        `Ambiguous function call to '${name}'. Multiple overloads match the provided arguments.`,
      );
    }

    const best = scored[0]!.f;
    return { definition: best, effectiveArgs: this.createEffectiveArguments(best.params, args) };
  }

  validateArguments(): void {
    // Overload scoring already enforced compatibility; kept for call-shape parity.
  }

  callImpl(definition: FunctionDefinition, context: ExecutionContext, callSite: AstNode | null, args: PowwowValue[]): PowwowValue {
    return definition.impl(context, callSite, args);
  }

  private createEffectiveArguments(params: ParameterDefinition[], provided: PowwowValue[]): PowwowValue[] {
    const result: PowwowValue[] = [];
    for (let i = 0; i < params.length; i++) {
      if (i < provided.length) {
        result.push(provided[i]!);
      } else if (params[i]!.optional && params[i]!.default !== undefined) {
        result.push(params[i]!.default!);
      } else {
        throw new InnerEvaluationError("Function missing required argument");
      }
    }
    return result;
  }

  private scoreTypeMatch(params: ParameterDefinition[], args: PowwowValue[]): number {
    if (args.length < requiredCount(params) || args.length > params.length) return -1;
    let total = 0;
    for (let i = 0; i < args.length; i++) {
      const wanted = params[i]!.type;
      const tag = paramTagOf(args[i]!.getBox());
      if (wanted === tag) total += 3;
      else if (wanted === "Any") total += 2;
      else return -1;
    }
    return total;
  }
}

// ---- argument helpers ---------------------------------------------------

const asNumber = (v: PowwowValue): PowwowNumber => (v.getBox() as NumberBox).value;
const asString = (v: PowwowValue): string => (v.getBox() as StringBox).value;
const asBoolean = (v: PowwowValue): boolean => (v.getBox() as BooleanBox).value;
const asArray = (v: PowwowValue): PowwowValue[] => (v.getBox() as ArrayBox).items;
const asObject = (v: PowwowValue): Map<string, PowwowValue> => (v.getBox() as ObjectBox).fields;
const asLambda = (v: PowwowValue): LambdaBox => v.getBox() as LambdaBox;
const intOf = (v: PowwowValue): number => asNumber(v).toIntValue();

const P = (type: ParamType): ParameterDefinition => ({ type });
const POpt = (type: ParamType, def: PowwowValue): ParameterDefinition => ({ type, optional: true, default: def });

function registerBuiltins(r: FunctionRegistry): void {
  r.register("typeof", [P("Any")], (_c, _s, a) => typeLiteral(a[0]!.typeOf()));

  r.register("length", [P("Array")], (_c, _s, a) => num(PowwowNumber.fromInt(asArray(a[0]!).length)));
  r.register("length", [P("String")], (_c, _s, a) => num(PowwowNumber.fromInt(asString(a[0]!).length)));
  r.register("empty", [P("String")], (_c, _s, a) => bool(asString(a[0]!).length === 0));

  r.register("concat", [P("String"), P("String")], (_c, _s, a) => str(asString(a[0]!) + asString(a[1]!)));
  r.register("concat", [P("Array"), P("Array")], (_c, _s, a) => array([...asArray(a[0]!), ...asArray(a[1]!)]));

  r.register("contains", [P("String"), P("String")], (_c, _s, a) => bool(asString(a[0]!).includes(asString(a[1]!))));
  r.register("contains", [P("Object"), P("String")], (_c, _s, a) => bool(asObject(a[0]!).has(asString(a[1]!))));
  r.register("startsWith", [P("String"), P("String")], (_c, _s, a) => bool(asString(a[0]!).startsWith(asString(a[1]!))));
  r.register("endsWith", [P("String"), P("String")], (_c, _s, a) => bool(asString(a[0]!).endsWith(asString(a[1]!))));
  r.register("toUpper", [P("String")], (_c, _s, a) => str(asString(a[0]!).toUpperCase()));
  r.register("toLower", [P("String")], (_c, _s, a) => str(asString(a[0]!).toLowerCase()));
  r.register("trim", [P("String")], (_c, _s, a) => str(asString(a[0]!).trim()));
  r.register("indexOf", [P("String"), P("String")], (_c, _s, a) => num(PowwowNumber.fromInt(asString(a[0]!).indexOf(asString(a[1]!)))));
  r.register("lastIndexOf", [P("String"), P("String")], (_c, _s, a) => num(PowwowNumber.fromInt(asString(a[0]!).lastIndexOf(asString(a[1]!)))));
  r.register("substring", [P("String"), P("Number"), POpt("Number", num(PowwowNumber.fromInt(-1)))], (_c, _s, a) => {
    const s = asString(a[0]!);
    const start = intOf(a[1]!);
    const end = intOf(a[2]!);
    return str(end >= 0 ? s.substring(start, end) : s.substring(start));
  });
  r.register("explode", [P("String"), P("String")], (_c, _s, a) =>
    array(asString(a[0]!).split(asString(a[1]!)).map((part) => str(part))),
  );
  r.register("join", [P("Array"), P("String")], (_c, _s, a) =>
    str(asArray(a[0]!).map((x) => x.output()).join(asString(a[1]!))),
  );

  // Arrays
  r.register("at", [P("Array"), P("Number")], (c, s, a) => {
    const arr = asArray(a[0]!);
    const i = intOf(a[1]!);
    if (i < 0 || i >= arr.length) throw new TemplateEvaluationError(`Index ${i} is out of bounds for array of length ${arr.length}`);
    return arr[i]!;
  });
  r.register("first", [P("Array")], (_c, _s, a) => {
    const arr = asArray(a[0]!);
    if (arr.length === 0) throw new TemplateEvaluationError("Cannot get first element of empty array");
    return arr[0]!;
  });
  r.register("last", [P("Array")], (_c, _s, a) => {
    const arr = asArray(a[0]!);
    if (arr.length === 0) throw new TemplateEvaluationError("Cannot get last element of empty array");
    return arr[arr.length - 1]!;
  });
  r.register("rest", [P("Array")], (_c, _s, a) => array(asArray(a[0]!).slice(1)));
  r.register("any", [P("Array")], (_c, _s, a) => bool(asArray(a[0]!).length > 0));
  r.register("take", [P("Array"), P("Number")], (_c, _s, a) => {
    const n = intOf(a[1]!);
    return array(n <= 0 ? [] : asArray(a[0]!).slice(0, n));
  });
  r.register("skip", [P("Array"), P("Number")], (_c, _s, a) => array(asArray(a[0]!).slice(Math.max(0, intOf(a[1]!)))));

  r.register("map", [P("Array"), P("Lambda")], (c, s, a) => {
    const lambda = asLambda(a[1]!);
    return array(asArray(a[0]!).map((item) => lambda.invoke(c, s, [item])));
  });
  r.register("filter", [P("Array"), P("Lambda")], (c, s, a) => {
    const lambda = asLambda(a[1]!);
    const result: PowwowValue[] = [];
    for (const item of asArray(a[0]!)) {
      const keep = lambda.invoke(c, s, [item]);
      if (!(keep.getBox() instanceof BooleanBox)) {
        throw new TemplateEvaluationError(`Filter predicate should evaluate to a boolean value`);
      }
      if (asBoolean(keep)) result.push(item);
    }
    return array(result);
  });
  r.register("reduce", [P("Array"), P("Lambda"), P("Any")], (c, s, a) => {
    const lambda = asLambda(a[1]!);
    let acc = a[2]!;
    for (const item of asArray(a[0]!)) acc = lambda.invoke(c, s, [acc, item]);
    return acc;
  });

  // Objects
  r.register("get", [P("Object"), P("String")], (_c, _s, a) => {
    const obj = asObject(a[0]!);
    const key = asString(a[1]!);
    if (!obj.has(key)) throw new TemplateEvaluationError(`Object does not contain field '${key}'`);
    return obj.get(key)!;
  });
  r.register("keys", [P("Object")], (_c, _s, a) => array([...asObject(a[0]!).keys()].map((k) => str(k))));

  // Conversions
  r.register("string", [P("Number")], (_c, _s, a) => str(asNumber(a[0]!).toString()));
  r.register("string", [P("Boolean")], (_c, _s, a) => str(asBoolean(a[0]!) ? "true" : "false"));
  r.register("number", [P("String")], (_c, _s, a) => {
    const s = asString(a[0]!);
    if (s === "") throw new TemplateEvaluationError("Cannot convert empty or null string to number");
    try {
      return num(PowwowNumber.fromString(s));
    } catch {
      throw new TemplateEvaluationError(`Cannot convert string '${s}' to number`);
    }
  });
  r.register("numeric", [P("String")], (_c, _s, a) => {
    const s = asString(a[0]!);
    if (s === "") return bool(false);
    try {
      PowwowNumber.fromString(s);
      return bool(true);
    } catch {
      return bool(false);
    }
  });

  // Numbers
  r.register("mod", [P("Number"), P("Number")], (_c, _s, a) => {
    const n2 = intOf(a[1]!);
    if (n2 === 0) throw new TemplateEvaluationError("Cannot perform modulo with zero as divisor");
    return num(PowwowNumber.fromInt(intOf(a[0]!) % n2));
  });
  r.register("floor", [P("Number")], (_c, _s, a) => num(asNumber(a[0]!).floor()));
  r.register("ceil", [P("Number")], (_c, _s, a) => num(asNumber(a[0]!).ceil()));
  r.register("round", [P("Number")], (_c, _s, a) => num(asNumber(a[0]!).round(0)));
  r.register("round", [P("Number"), P("Number")], (_c, _s, a) => num(asNumber(a[0]!).round(intOf(a[1]!))));

  r.register("range", [P("Number"), P("Number"), POpt("Number", num(PowwowNumber.fromInt(1)))], (_c, _s, a) => {
    const start = asNumber(a[0]!);
    const end = asNumber(a[1]!);
    const step = asNumber(a[2]!);
    if (step.isZero()) throw new TemplateEvaluationError("range function requires a non-zero step value");
    const result: PowwowValue[] = [];
    const positive = step.compareTo(PowwowNumber.fromInt(0)) > 0;
    let value = start;
    while (positive ? value.compareTo(end) < 0 : value.compareTo(end) > 0) {
      result.push(num(value));
      value = value.add(step);
    }
    return array(result);
  });

  // Control flow (lazy)
  r.register(
    "if",
    [P("Lazy"), P("Lazy"), P("Lazy")],
    (_c, _s, a) => {
      const condition = (a[0]!.getBox() as LazyBox).evaluate();
      if (!(condition.getBox() instanceof BooleanBox)) {
        throw new TemplateEvaluationError(`Expected type Boolean but found ${condition.typeOf()}`);
      }
      const branch = asBoolean(condition) ? a[1]! : a[2]!;
      return (branch.getBox() as LazyBox).evaluate();
    },
    true,
  );
}
