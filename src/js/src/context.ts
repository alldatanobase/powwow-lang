/**
 * ExecutionContext + LambdaExecutionContext — ported from
 * src/dotnet/Interpreter/Env. Holds scope (data, variables, iterators), the
 * function registry, parent chain, and recursion depth. The resolve methods'
 * quirks (e.g. nested lookups falling back to data properties) are preserved.
 */

import { PowwowValue, ObjectBox, FunctionReferenceBox, type Box } from "./values.ts";
import { InnerEvaluationError, TemplateEvaluationError } from "./errors.ts";
import type { FunctionRegistry } from "./registry.ts";

export class ExecutionContext {
  protected data: PowwowValue | null;
  protected iteratorValues: Map<string, PowwowValue>;
  protected variables: Map<string, PowwowValue>;
  private functionRegistry: FunctionRegistry;
  protected parentContext: ExecutionContext | null;
  protected maxDepth: number;
  private currentDepth: number;

  constructor(
    data: PowwowValue | null,
    functionRegistry: FunctionRegistry,
    parentContext: ExecutionContext | null,
    maxDepth: number,
  ) {
    this.data = data;
    this.iteratorValues = new Map();
    this.variables = new Map();
    this.functionRegistry = functionRegistry;
    this.parentContext = parentContext;
    this.maxDepth = maxDepth;
    this.currentDepth = parentContext !== null ? parentContext.currentDepth + 1 : 0;
    this.checkStackDepth();
  }

  get depthLimit(): number {
    return this.maxDepth;
  }

  checkStackDepth(): void {
    if (this.currentDepth >= this.maxDepth) {
      throw new TemplateEvaluationError(`Maximum call stack depth ${this.maxDepth} has been exceeded.`);
    }
  }

  defineVariable(name: string, value: PowwowValue): void {
    if (this.variables.has(name) || this.iteratorValues.has(name) || this.tryResolveValue(name) !== null) {
      throw new InnerEvaluationError(
        `Cannot define variable '${name}' because it conflicts with an existing variable or field`,
      );
    }
    this.variables.set(name, value);
  }

  redefineVariable(name: string, value: PowwowValue): void {
    const variable = this.tryResolveMutableValue(name);
    if (variable === null) {
      throw new InnerEvaluationError(`Cannot mutate variable '${name}' because it has not been defined`);
    }
    variable.mutate(value.getBox());
  }

  createIteratorContext(iteratorName: string, value: PowwowValue): ExecutionContext {
    const newContext = new ExecutionContext(this.data, this.functionRegistry, this, this.maxDepth);
    for (const [key, v] of this.variables) newContext.variables.set(key, v);
    newContext.iteratorValues.set(iteratorName, value);
    for (const [key, v] of this.iteratorValues) newContext.iteratorValues.set(key, v);
    return newContext;
  }

  getFunctionRegistry(): FunctionRegistry {
    return this.functionRegistry;
  }

  getData(): PowwowValue | null {
    return this.data;
  }

  protected tryGetDataProperty(propertyName: string): PowwowValue | null {
    if (this.data === null || propertyName === "") return null;
    const box: Box = this.data.getBox();
    if (box instanceof ObjectBox && box.fields.has(propertyName)) {
      return box.fields.get(propertyName)!;
    }
    return null;
  }

  /** Walk object fields for the trailing path parts; null if any part is missing. */
  protected descendObjectFields(start: PowwowValue, parts: string[]): PowwowValue | null {
    let current = start;
    for (const part of parts) {
      const box = current.getBox();
      if (box instanceof ObjectBox && box.fields.has(part)) {
        current = box.fields.get(part)!;
      } else {
        return null;
      }
    }
    return current;
  }

  tryResolveNonShadowableValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    let current: PowwowValue;
    if (this.iteratorValues.has(parts[0]!)) {
      current = this.iteratorValues.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.variables.has(parts[0]!)) {
      current = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.tryGetDataProperty(parts[0]!) !== null) {
      current = this.data!;
    } else {
      return this.parentContext !== null ? this.parentContext.tryResolveNonShadowableValue(path) : null;
    }
    return this.descendObjectFields(current, parts);
  }

  tryResolveMutableValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    if (this.iteratorValues.has(parts[0]!)) {
      throw new InnerEvaluationError(`Iterator variable ${path} is not mutable and cannot be reassigned`);
    } else if (this.variables.has(parts[0]!)) {
      const start = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
      return this.descendObjectFields(start, parts);
    } else if (this.tryGetDataProperty(parts[0]!) !== null) {
      throw new InnerEvaluationError(`Global variable ${path} is not mutable and cannot be reassigned`);
    } else if (this.parentContext !== null) {
      return this.parentContext.tryResolveMutableValue(path);
    }
    return null;
  }

  tryResolveValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    let current: PowwowValue;
    if (this.iteratorValues.has(parts[0]!)) {
      current = this.iteratorValues.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.variables.has(parts[0]!)) {
      current = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.tryGetDataProperty(parts[0]!) !== null) {
      current = this.data!; // keep full parts; the loop below re-reads from data
    } else if (this.parentContext !== null) {
      return this.parentContext.tryResolveValue(path);
    } else {
      return null;
    }
    // NOTE: faithful to .NET — trailing parts are looked up as data properties.
    let result: PowwowValue = current;
    for (const part of parts) {
      const next = this.tryGetDataProperty(part);
      if (next === null) return null;
      result = next;
    }
    return result;
  }

  resolveValue(path: string): PowwowValue {
    const value = this.tryResolveValue(path);
    if (value !== null) return value;
    if (this.functionRegistry.hasFunction(path)) {
      return new PowwowValue(new FunctionReferenceBox(path));
    }
    throw new InnerEvaluationError(`Unknown identifier: ${path}`);
  }
}

export class LambdaExecutionContext extends ExecutionContext {
  private parameters: Map<string, PowwowValue>;
  private definitionContext: ExecutionContext;

  constructor(
    parentContext: ExecutionContext,
    definitionContext: ExecutionContext,
    parameterNames: string[],
    parameterValues: PowwowValue[],
  ) {
    super(parentContext.getData(), parentContext.getFunctionRegistry(), parentContext, parentContext.depthLimit);
    this.parameters = new Map();
    this.definitionContext = definitionContext;

    if (parameterNames.length > parameterValues.length) {
      const missing = parameterNames.slice(parameterValues.length).join(", ");
      throw new InnerEvaluationError(`Not enough parameter values provided. Missing values for: ${missing}`);
    }
    for (let i = 0; i < parameterNames.length; i++) {
      this.parameters.set(parameterNames[i]!, parameterValues[i]!);
    }
  }

  override defineVariable(name: string, value: PowwowValue): void {
    if (this.variables.has(name)) {
      throw new InnerEvaluationError(
        `Cannot define variable '${name}' because it conflicts with an existing variable or field`,
      );
    }
    if (this.parameters.has(name)) {
      throw new InnerEvaluationError(`Cannot define variable '${name}' because it conflicts with a parameter name`);
    }
    if (this.parentContext !== null && this.parentContext.tryResolveNonShadowableValue(name) !== null) {
      throw new InnerEvaluationError(
        `Cannot define variable '${name}' because it conflicts with an existing variable or field`,
      );
    }
    this.variables.set(name, value);
  }

  tryGetParameterFromAnyContext(name: string): PowwowValue | null {
    if (this.parameters.has(name)) return this.parameters.get(name)!;
    if (this.variables.has(name)) return this.variables.get(name)!;
    if (this.definitionContext instanceof LambdaExecutionContext) {
      const fromDef = this.definitionContext.tryGetParameterFromAnyContext(name);
      if (fromDef !== null) return fromDef;
    }
    if (this.parentContext instanceof LambdaExecutionContext) {
      const fromCaller = this.parentContext.tryGetParameterFromAnyContext(name);
      if (fromCaller !== null) return fromCaller;
    }
    return null;
  }

  override tryResolveNonShadowableValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    let current: PowwowValue;
    if (this.variables.has(parts[0]!)) {
      current = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.parameters.has(parts[0]!)) {
      current = this.parameters.get(parts[0]!)!;
      parts = parts.slice(1);
    } else {
      return this.parentContext !== null ? this.parentContext.tryResolveNonShadowableValue(path) : null;
    }
    return this.descendObjectFields(current, parts);
  }

  override tryResolveMutableValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    if (this.iteratorValues.has(parts[0]!)) {
      throw new InnerEvaluationError(`Iterator variable ${path} is not mutable and cannot be reassigned`);
    } else if (this.parameters.has(parts[0]!)) {
      throw new InnerEvaluationError(`Parameter ${path} is not mutable and cannot be reassigned`);
    } else if (this.variables.has(parts[0]!)) {
      const start = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
      return this.descendObjectFields(start, parts);
    } else if (this.tryGetDataProperty(parts[0]!) !== null) {
      throw new InnerEvaluationError(`Global variable ${path} is not mutable and cannot be reassigned`);
    } else {
      const fromDef = this.definitionContext.tryResolveMutableValue(path);
      if (fromDef !== null) return fromDef;
      return this.parentContext !== null ? this.parentContext.tryResolveMutableValue(path) : null;
    }
  }

  override tryResolveValue(path: string): PowwowValue | null {
    let parts = path.split(".");
    let current: PowwowValue;
    if (this.variables.has(parts[0]!)) {
      current = this.variables.get(parts[0]!)!;
      parts = parts.slice(1);
    } else if (this.parameters.has(parts[0]!)) {
      current = this.parameters.get(parts[0]!)!;
      parts = parts.slice(1);
    } else {
      const fromDef = this.definitionContext.tryResolveValue(path);
      if (fromDef !== null) return fromDef;
      return this.parentContext !== null ? this.parentContext.tryResolveValue(path) : null;
    }
    let result: PowwowValue = current;
    for (const part of parts) {
      const next = this.tryGetDataProperty(part);
      if (next === null) return null;
      result = next;
    }
    return result;
  }

  override resolveValue(path: string): PowwowValue {
    const parts = path.split(".");
    if (this.parameters.has(parts[0]!)) {
      const descended = this.descendObjectFields(this.parameters.get(parts[0]!)!, parts.slice(1));
      if (descended === null) throw new InnerEvaluationError(`Unknown identifier: ${path}`);
      return descended;
    }
    if (this.variables.has(parts[0]!)) {
      const descended = this.descendObjectFields(this.variables.get(parts[0]!)!, parts.slice(1));
      if (descended === null) throw new InnerEvaluationError(`Unknown identifier: ${path}`);
      return descended;
    }
    try {
      return this.definitionContext.resolveValue(path);
    } catch {
      return this.parentContext!.resolveValue(path);
    }
  }
}
