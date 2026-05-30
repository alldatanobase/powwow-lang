/**
 * Evaluator + Interpreter — ports the per-node Evaluate() logic from the .NET
 * AST and the top-level Interpreter (lex -> parse -> resolve includes -> evaluate).
 * Output parity with .NET is the goal; see docs/js-port-parity.md.
 */

import { Lexer } from "./lexer.ts";
import { Parser } from "./parser.ts";
import type { AstNode } from "./ast.ts";
import {
  PowwowValue,
  NumberBox,
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
  object,
  typeLiteral,
  type Box,
} from "./values.ts";
import { PowwowNumber } from "./number.ts";
import { ExecutionContext, LambdaExecutionContext } from "./context.ts";
import { FunctionRegistry } from "./registry.ts";
import { InnerEvaluationError, TemplateEvaluationError } from "./errors.ts";

const EMPTY = (): PowwowValue => str("");

function unboxBoolean(value: PowwowValue): boolean {
  const box = value.getBox();
  if (box instanceof BooleanBox) return box.value;
  throw new TemplateEvaluationError(`Expected value of type boolean but found ${value.typeOf()}`);
}

function unboxNumber(value: PowwowValue): PowwowNumber {
  const box = value.getBox();
  if (box instanceof NumberBox) return box.value;
  throw new TemplateEvaluationError(`Expected value of type Number but found ${value.typeOf()}`);
}

/** Equality assuming both sides share a type; mirrors .NET's Equals(Unbox) rules. */
function areEqual(left: PowwowValue, right: PowwowValue): boolean {
  const lb = left.getBox();
  const rb = right.getBox();
  if (lb instanceof NumberBox && rb instanceof NumberBox) return lb.value.equals(rb.value);
  if (lb instanceof BooleanBox && rb instanceof BooleanBox) return lb.value === rb.value;
  if (lb instanceof TypeBox && rb instanceof TypeBox) return lb.value === rb.value;
  if (lb instanceof FunctionReferenceBox && rb instanceof FunctionReferenceBox) return lb.name === rb.name;
  if (typeof (lb as { value?: unknown }).value === "string" && typeof (rb as { value?: unknown }).value === "string") {
    return (lb as unknown as { value: string }).value === (rb as unknown as { value: string }).value;
  }
  // Arrays, objects, lambdas: reference identity (same box instance).
  return lb === rb;
}

export function evaluate(node: AstNode, context: ExecutionContext): PowwowValue {
  switch (node.kind) {
    case "Template": {
      let result = "";
      for (const child of node.children) result += evaluate(child, context).output();
      return str(result);
    }
    case "Text":
    case "Whitespace":
    case "Newline":
      return str(node.value);
    case "Literal":
      return str(node.content);
    case "Number":
      return num(PowwowNumber.fromString(node.raw));
    case "String":
      return str(node.value);
    case "Boolean":
      return bool(node.value);
    case "Type":
      return typeLiteral(node.type);

    case "Variable":
      try {
        return context.resolveValue(node.name);
      } catch (e) {
        if (e instanceof InnerEvaluationError) throw new TemplateEvaluationError(e.message);
        throw e;
      }

    case "FieldAccess": {
      const target = evaluate(node.target, context);
      const box = target.getBox();
      if (box instanceof ObjectBox) {
        if (!box.fields.has(node.fieldName)) {
          throw new TemplateEvaluationError(`Object does not contain field '${node.fieldName}'`);
        }
        return box.fields.get(node.fieldName)!;
      }
      throw new TemplateEvaluationError(`Object does not contain field '${node.fieldName}'`);
    }

    case "Array":
      return array(node.elements.map((el) => evaluate(el, context)));

    case "ObjectCreation": {
      const fields = new Map<string, PowwowValue>();
      for (const field of node.fields) fields.set(field.key, evaluate(field.value, context));
      return object(fields);
    }

    case "Unary": {
      const value = evaluate(node.operand, context);
      if (node.operator === "Not") return bool(!unboxBoolean(value));
      throw new TemplateEvaluationError(`Unknown unary operator: ${node.operator}`);
    }

    case "Binary":
      return evaluateBinary(node.operator, node.left, node.right, context);

    case "Lambda": {
      const definitionContext = context;
      const lambdaBox = new LambdaBox(node.parameters, (callerContext, _callSite, args) => {
        try {
          const lambdaContext = new LambdaExecutionContext(
            callerContext as ExecutionContext,
            definitionContext,
            node.parameters,
            args,
          );
          for (const stmt of node.statements) {
            const value = evaluate(stmt.expression, lambdaContext);
            try {
              if (stmt.statementType === "Declaration") lambdaContext.defineVariable(stmt.variableName, value);
              else lambdaContext.redefineVariable(stmt.variableName, value);
            } catch (ex) {
              if (ex instanceof InnerEvaluationError) throw new TemplateEvaluationError(ex.message);
              throw ex;
            }
          }
          return evaluate(node.body, lambdaContext);
        } catch (ex) {
          if (ex instanceof InnerEvaluationError) throw new TemplateEvaluationError(ex.message);
          throw ex;
        }
      });
      return new PowwowValue(lambdaBox);
    }

    case "FunctionReference":
      return new PowwowValue(new FunctionReferenceBox(node.name));

    case "Invocation":
      return evaluateInvocation(node, context);

    case "Let": {
      const value = evaluate(node.expression, context);
      const t = value.typeOf();
      // Scalars are copied by value (a fresh cell over the immutable box); references are shared.
      const toStore =
        t === "Number" || t === "String" || t === "Boolean" || t === "Type"
          ? new PowwowValue(value.getBox())
          : value;
      try {
        context.defineVariable(node.variableName, toStore);
      } catch (e) {
        if (e instanceof InnerEvaluationError) throw new TemplateEvaluationError(e.message);
        throw e;
      }
      return EMPTY();
    }

    case "Mutation": {
      const value = evaluate(node.expression, context);
      try {
        context.redefineVariable(node.variableName, value);
      } catch (e) {
        if (e instanceof InnerEvaluationError) throw new TemplateEvaluationError(e.message);
        throw e;
      }
      return EMPTY();
    }

    case "Capture": {
      const result = evaluate(node.body, context);
      try {
        context.defineVariable(node.variableName, result);
      } catch (e) {
        if (e instanceof InnerEvaluationError) throw new TemplateEvaluationError(e.message);
        throw e;
      }
      return EMPTY();
    }

    case "If": {
      for (const branch of node.branches) {
        if (unboxBoolean(evaluate(branch.condition, context))) {
          return evaluate(branch.body, context);
        }
      }
      return node.elseBranch !== null ? evaluate(node.elseBranch, context) : EMPTY();
    }

    case "For": {
      if (context.tryResolveValue(node.iteratorName) !== null) {
        throw new TemplateEvaluationError(
          `Iterator name '${node.iteratorName}' conflicts with an existing variable or field`,
        );
      }
      const collection = evaluate(node.collection, context);
      const box = collection.getBox();
      if (!(box instanceof ArrayBox)) {
        throw new TemplateEvaluationError("Each statement requires an array");
      }
      let result = "";
      for (const item of box.items) {
        const iterationContext = context.createIteratorContext(node.iteratorName, item);
        result += evaluate(node.body, iterationContext).output();
      }
      return str(result);
    }

    case "Include": {
      if (node.includedTemplate === null) {
        throw new TemplateEvaluationError(`Template '${node.templateName}' could not be resolved`);
      }
      const childContext = new ExecutionContext(
        context.getData(),
        context.getFunctionRegistry(),
        context,
        context.depthLimit,
      );
      return evaluate(node.includedTemplate, childContext);
    }

    default: {
      const exhaustive: never = node;
      throw new TemplateEvaluationError(`Unknown node: ${(exhaustive as { kind: string }).kind}`);
    }
  }
}

function evaluateBinary(operator: string, leftNode: AstNode, rightNode: AstNode, context: ExecutionContext): PowwowValue {
  if (operator === "And") {
    return bool(unboxBoolean(evaluate(leftNode, context)) && unboxBoolean(evaluate(rightNode, context)));
  }
  if (operator === "Or") {
    return bool(unboxBoolean(evaluate(leftNode, context)) || unboxBoolean(evaluate(rightNode, context)));
  }

  const left = evaluate(leftNode, context);
  const right = evaluate(rightNode, context);

  switch (operator) {
    case "Plus":
      return num(unboxNumber(left).add(unboxNumber(right)));
    case "Minus":
      return num(unboxNumber(left).subtract(unboxNumber(right)));
    case "Multiply":
      return num(unboxNumber(left).multiply(unboxNumber(right)));
    case "Divide": {
      const r = right.getBox();
      if (r instanceof NumberBox && r.value.isZero()) throw new TemplateEvaluationError("Cannot divide by zero");
      return num(unboxNumber(left).divide(unboxNumber(right)));
    }
    case "LessThan":
      return bool(unboxNumber(left).compareTo(unboxNumber(right)) < 0);
    case "LessThanEqual":
      return bool(unboxNumber(left).compareTo(unboxNumber(right)) <= 0);
    case "GreaterThan":
      return bool(unboxNumber(left).compareTo(unboxNumber(right)) > 0);
    case "GreaterThanEqual":
      return bool(unboxNumber(left).compareTo(unboxNumber(right)) >= 0);
    case "Equal":
    case "NotEqual": {
      if (left.typeOf() !== right.typeOf()) {
        throw new TemplateEvaluationError(`Expected similar types but found ${left.typeOf()} and ${right.typeOf()}`);
      }
      const equal = areEqual(left, right);
      return bool(operator === "Equal" ? equal : !equal);
    }
    default:
      throw new TemplateEvaluationError(`Unknown binary operator: ${operator}`);
  }
}

function evaluateInvocation(
  node: Extract<AstNode, { kind: "Invocation" }>,
  context: ExecutionContext,
): PowwowValue {
  const currentContext = new ExecutionContext(
    context.getData(),
    context.getFunctionRegistry(),
    context,
    context.depthLimit,
  );
  currentContext.checkStackDepth();

  const callable = evaluate(node.callable, currentContext);
  const registry = context.getFunctionRegistry();
  const callableBox: Box = callable.getBox();

  const lazy =
    callableBox instanceof FunctionReferenceBox &&
    registry.lazyFunctionExists(callableBox.name, node.args.length);

  const args = lazy
    ? node.args.map((arg) => new PowwowValue(new LazyBox(() => evaluate(arg, currentContext))))
    : node.args.map((arg) => evaluate(arg, currentContext));

  if (callableBox instanceof LambdaBox) {
    return callableBox.invoke(context, node, args);
  }

  if (callableBox instanceof FunctionReferenceBox) {
    const name = callableBox.name;

    if (context instanceof LambdaExecutionContext) {
      const paramValue = context.tryGetParameterFromAnyContext(name);
      if (paramValue !== null) {
        const pbox = paramValue.getBox();
        if (pbox instanceof LambdaBox) return pbox.invoke(context, node, args);
        if (pbox instanceof FunctionReferenceBox) return callRegistry(registry, pbox.name, currentContext, node, args, name);
      }
    }

    const variableValue = context.tryResolveValue(name);
    if (variableValue !== null) {
      const vbox = variableValue.getBox();
      if (vbox instanceof LambdaBox) return vbox.invoke(context, node, args);
      if (vbox instanceof FunctionReferenceBox) return callRegistry(registry, vbox.name, currentContext, node, args, name);
    }

    return callRegistry(registry, name, currentContext, node, args, name);
  }

  throw new TemplateEvaluationError(`Expression is not callable`);
}

function callRegistry(
  registry: FunctionRegistry,
  name: string,
  context: ExecutionContext,
  callSite: AstNode,
  args: PowwowValue[],
  reportName: string,
): PowwowValue {
  try {
    const match = registry.tryGetFunction(name, args);
    if (!match) {
      throw new TemplateEvaluationError(
        `No matching overload found for function '${reportName}' with the provided arguments`,
      );
    }
    registry.validateArguments();
    return registry.callImpl(match.definition, context, callSite, match.effectiveArgs);
  } catch (e) {
    if (e instanceof InnerEvaluationError) throw new TemplateEvaluationError(e.message);
    throw e;
  }
}

// ---- data binding (ValueFactory port) ----------------------------------

export function valueFromData(value: unknown): PowwowValue {
  if (Array.isArray(value)) return array(value.map(valueFromData));
  if (typeof value === "string") return str(value);
  if (typeof value === "number") return num(PowwowNumber.fromString(String(value)));
  if (typeof value === "bigint") return num(PowwowNumber.fromString(value.toString()));
  if (typeof value === "boolean") return bool(value);
  if (value !== null && typeof value === "object") {
    const fields = new Map<string, PowwowValue>();
    for (const [key, v] of Object.entries(value as Record<string, unknown>)) {
      fields.set(key, valueFromData(v));
    }
    return object(fields);
  }
  throw new TemplateEvaluationError(`Unable to bind data value of type ${typeof value}`);
}

// ---- include resolution -------------------------------------------------

export type TemplateResolver = (templateName: string) => string;

function resolveIncludes(node: AstNode, resolver: TemplateResolver, visited: Set<string>): void {
  switch (node.kind) {
    case "Include": {
      if (visited.has(node.templateName)) {
        throw new TemplateEvaluationError(`Circular template reference detected: '${node.templateName}'`);
      }
      visited.add(node.templateName);
      const content = resolver(node.templateName);
      const sub = new Parser().parse(new Lexer().tokenize(content));
      resolveIncludes(sub, resolver, visited);
      node.includedTemplate = sub;
      visited.delete(node.templateName);
      break;
    }
    case "Template":
      for (const child of node.children) resolveIncludes(child, resolver, visited);
      break;
    case "If":
      for (const branch of node.branches) resolveIncludes(branch.body, resolver, visited);
      if (node.elseBranch !== null) resolveIncludes(node.elseBranch, resolver, visited);
      break;
    case "For":
      resolveIncludes(node.body, resolver, visited);
      break;
    case "Capture":
      resolveIncludes(node.body, resolver, visited);
      break;
    default:
      break;
  }
}

// ---- top-level interpreter ---------------------------------------------

export class Interpreter {
  private registry = new FunctionRegistry();
  private resolver: TemplateResolver | null;
  private maxDepth: number;

  constructor(options: { resolver?: TemplateResolver; maxDepth?: number } = {}) {
    this.resolver = options.resolver ?? null;
    this.maxDepth = options.maxDepth ?? 1000;
  }

  getRegistry(): FunctionRegistry {
    return this.registry;
  }

  interpret(template: string, data: unknown = null): string {
    const tokens = new Lexer().tokenize(template);
    const ast = new Parser().parse(tokens);
    if (this.resolver !== null) resolveIncludes(ast, this.resolver, new Set());
    const rootData = data !== null && data !== undefined ? valueFromData(data) : object(new Map());
    const context = new ExecutionContext(rootData, this.registry, null, this.maxDepth);
    return evaluate(ast, context).output();
  }
}
