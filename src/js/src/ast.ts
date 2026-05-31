/**
 * AST node definitions, ported from src/dotnet/Interpreter/Ast.
 *
 * In .NET each node carries its own Evaluate(); here the nodes are pure data
 * (discriminated by `kind`) and evaluation lives in the evaluator (next milestone).
 * This keeps the parser a faithful structural port without pulling in runtime concerns.
 *
 * Note: fields are declared explicitly (not via constructor parameter properties),
 * since Node's strip-only TypeScript mode does not support parameter properties.
 */

import type { SourceLocation, TokenType } from "./token.ts";
import type { ValueType } from "./values.ts";

export type AstNode =
  | TemplateNode
  | TextNode
  | WhitespaceNode
  | NewlineNode
  | LiteralNode
  | LetNode
  | MutationNode
  | CaptureNode
  | IncludeNode
  | IfNode
  | ForNode
  | BinaryNode
  | UnaryNode
  | VariableNode
  | StringNode
  | NumberNode
  | BooleanNode
  | TypeNode
  | ArrayNode
  | ObjectCreationNode
  | LambdaNode
  | FunctionReferenceNode
  | FieldAccessNode
  | InvocationNode;

export class TemplateNode {
  readonly kind = "Template";
  readonly children: AstNode[];
  readonly location: SourceLocation | null;
  constructor(children: AstNode[], location: SourceLocation | null) {
    this.children = children;
    this.location = location;
  }
}

export class TextNode {
  readonly kind = "Text";
  readonly value: string;
  readonly location: SourceLocation;
  constructor(value: string, location: SourceLocation) {
    this.value = value;
    this.location = location;
  }
}

export class WhitespaceNode {
  readonly kind = "Whitespace";
  readonly value: string;
  readonly location: SourceLocation;
  constructor(value: string, location: SourceLocation) {
    this.value = value;
    this.location = location;
  }
}

export class NewlineNode {
  readonly kind = "Newline";
  readonly value: string;
  readonly location: SourceLocation;
  constructor(value: string, location: SourceLocation) {
    this.value = value;
    this.location = location;
  }
}

export class LiteralNode {
  readonly kind = "Literal";
  readonly content: string;
  readonly location: SourceLocation;
  constructor(content: string, location: SourceLocation) {
    this.content = content;
    this.location = location;
  }
}

export class LetNode {
  readonly kind = "Let";
  readonly variableName: string;
  readonly expression: AstNode;
  readonly location: SourceLocation;
  constructor(variableName: string, expression: AstNode, location: SourceLocation) {
    this.variableName = variableName;
    this.expression = expression;
    this.location = location;
  }
}

export class MutationNode {
  readonly kind = "Mutation";
  /** `variableName` may be dotted (e.g. "user.age") for field mutation. */
  readonly variableName: string;
  readonly expression: AstNode;
  readonly location: SourceLocation;
  constructor(variableName: string, expression: AstNode, location: SourceLocation) {
    this.variableName = variableName;
    this.expression = expression;
    this.location = location;
  }
}

export class CaptureNode {
  readonly kind = "Capture";
  readonly variableName: string;
  readonly body: AstNode;
  readonly location: SourceLocation;
  constructor(variableName: string, body: AstNode, location: SourceLocation) {
    this.variableName = variableName;
    this.body = body;
    this.location = location;
  }
}

export class IncludeNode {
  readonly kind = "Include";
  readonly templateName: string;
  readonly location: SourceLocation;
  /** Set during the include-resolution pass. */
  includedTemplate: AstNode | null = null;
  constructor(templateName: string, location: SourceLocation) {
    this.templateName = templateName;
    this.location = location;
  }
}

export interface IfBranch {
  condition: AstNode;
  body: AstNode;
}

export class IfNode {
  readonly kind = "If";
  readonly branches: IfBranch[];
  readonly elseBranch: AstNode | null;
  readonly location: SourceLocation;
  constructor(branches: IfBranch[], elseBranch: AstNode | null, location: SourceLocation) {
    this.branches = branches;
    this.elseBranch = elseBranch;
    this.location = location;
  }
}

export class ForNode {
  readonly kind = "For";
  readonly iteratorName: string;
  readonly collection: AstNode;
  readonly body: AstNode;
  readonly location: SourceLocation;
  constructor(iteratorName: string, collection: AstNode, body: AstNode, location: SourceLocation) {
    this.iteratorName = iteratorName;
    this.collection = collection;
    this.body = body;
    this.location = location;
  }
}

export class BinaryNode {
  readonly kind = "Binary";
  readonly operator: TokenType;
  readonly left: AstNode;
  readonly right: AstNode;
  readonly location: SourceLocation;
  constructor(operator: TokenType, left: AstNode, right: AstNode, location: SourceLocation) {
    this.operator = operator;
    this.left = left;
    this.right = right;
    this.location = location;
  }
}

export class UnaryNode {
  readonly kind = "Unary";
  readonly operator: TokenType;
  readonly operand: AstNode;
  readonly location: SourceLocation;
  constructor(operator: TokenType, operand: AstNode, location: SourceLocation) {
    this.operator = operator;
    this.operand = operand;
    this.location = location;
  }
}

export class VariableNode {
  readonly kind = "Variable";
  readonly name: string;
  readonly location: SourceLocation;
  constructor(name: string, location: SourceLocation) {
    this.name = name;
    this.location = location;
  }
}

export class StringNode {
  readonly kind = "String";
  readonly value: string;
  readonly location: SourceLocation;
  constructor(value: string, location: SourceLocation) {
    this.value = value;
    this.location = location;
  }
}

export class NumberNode {
  readonly kind = "Number";
  /** The raw literal text (e.g. "-2.5"); parsed to a PowwowNumber at evaluation. */
  readonly raw: string;
  readonly location: SourceLocation;
  constructor(raw: string, location: SourceLocation) {
    this.raw = raw;
    this.location = location;
  }
}

export class BooleanNode {
  readonly kind = "Boolean";
  readonly value: boolean;
  readonly location: SourceLocation;
  constructor(value: boolean, location: SourceLocation) {
    this.value = value;
    this.location = location;
  }
}

export class TypeNode {
  readonly kind = "Type";
  readonly type: ValueType;
  readonly location: SourceLocation;
  constructor(type: ValueType, location: SourceLocation) {
    this.type = type;
    this.location = location;
  }
}

export class ArrayNode {
  readonly kind = "Array";
  readonly elements: AstNode[];
  readonly location: SourceLocation;
  constructor(elements: AstNode[], location: SourceLocation) {
    this.elements = elements;
    this.location = location;
  }
}

export interface ObjectField {
  key: string;
  value: AstNode;
}

export class ObjectCreationNode {
  readonly kind = "ObjectCreation";
  readonly fields: ObjectField[];
  readonly location: SourceLocation;
  constructor(fields: ObjectField[], location: SourceLocation) {
    this.fields = fields;
    this.location = location;
  }
}

export type StatementType = "Declaration" | "Mutation";

export interface LambdaStatement {
  variableName: string;
  expression: AstNode;
  statementType: StatementType;
}

export class LambdaNode {
  readonly kind = "Lambda";
  readonly parameters: string[];
  readonly statements: LambdaStatement[];
  readonly body: AstNode;
  readonly location: SourceLocation;
  constructor(parameters: string[], statements: LambdaStatement[], body: AstNode, location: SourceLocation) {
    this.parameters = parameters;
    this.statements = statements;
    this.body = body;
    this.location = location;
  }
}

export class FunctionReferenceNode {
  readonly kind = "FunctionReference";
  readonly name: string;
  readonly location: SourceLocation;
  constructor(name: string, location: SourceLocation) {
    this.name = name;
    this.location = location;
  }
}

export class FieldAccessNode {
  readonly kind = "FieldAccess";
  readonly target: AstNode;
  readonly fieldName: string;
  readonly location: SourceLocation;
  constructor(target: AstNode, fieldName: string, location: SourceLocation) {
    this.target = target;
    this.fieldName = fieldName;
    this.location = location;
  }
}

export class InvocationNode {
  readonly kind = "Invocation";
  readonly callable: AstNode;
  readonly args: AstNode[];
  readonly location: SourceLocation;
  constructor(callable: AstNode, args: AstNode[], location: SourceLocation) {
    this.callable = callable;
    this.args = args;
    this.location = location;
  }
}
