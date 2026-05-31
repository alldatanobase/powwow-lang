/**
 * Parser — a faithful port of src/dotnet/Interpreter/Parse/Parser.cs.
 *
 * Recursive-descent over the token stream, producing the AST in ast.ts. The
 * expression grammar (or -> and -> comparison -> additive -> multiplicative ->
 * unary -> primary) and the whitespace-trimming skip checks mirror .NET exactly.
 */

import { Token, SourceLocation, TemplateParsingError, type TokenType } from "./token.ts";
import type { ValueType } from "./values.ts";
import {
  type AstNode,
  type IfBranch,
  type LambdaStatement,
  type ObjectField,
  type StatementType,
  TemplateNode,
  TextNode,
  WhitespaceNode,
  NewlineNode,
  LiteralNode,
  LetNode,
  MutationNode,
  CaptureNode,
  IncludeNode,
  IfNode,
  ForNode,
  BinaryNode,
  UnaryNode,
  VariableNode,
  StringNode,
  NumberNode,
  BooleanNode,
  TypeNode,
  ArrayNode,
  ObjectCreationNode,
  LambdaNode,
  FunctionReferenceNode,
  FieldAccessNode,
  InvocationNode,
} from "./ast.ts";

export class Parser {
  private tokens: Token[] = [];
  private position = 0;

  parse(tokens: Token[]): AstNode {
    this.tokens = tokens;
    this.position = 0;
    return this.parseTemplate();
  }

  private parseTemplate(): AstNode {
    const nodes: AstNode[] = [];
    const startLocation = this.tokens.length > 0 ? this.tokens[0]!.location : null;
    const innerErrors: TemplateParsingError[] = [];

    while (this.position < this.tokens.length) {
      try {
        const token = this.current();

        if (token.type === "Text") {
          nodes.push(new TextNode(token.value, token.location));
          this.advance();
        } else if (token.type === "Whitespace") {
          if (this.checkSkipWhitespace()) {
            this.advance();
          } else {
            nodes.push(new WhitespaceNode(token.value, token.location));
            this.advance();
          }
        } else if (token.type === "Newline") {
          if (this.checkSkipNewline()) {
            this.advance();
          } else {
            nodes.push(new NewlineNode(token.value, token.location));
            this.advance();
          }
        } else if (token.type === "DirectiveStart") {
          const nextToken = this.tokens[this.position + 1]!;

          if (nextToken.type === "CommentStart") {
            this.parseComment();
          } else if (nextToken.type === "Let") {
            nodes.push(this.parseLetStatement());
          } else if (nextToken.type === "Mutation") {
            nodes.push(this.parseMutationStatement());
          } else if (nextToken.type === "Capture") {
            nodes.push(this.parseCaptureStatement());
          } else if (nextToken.type === "Literal") {
            nodes.push(this.parseLiteralStatement());
          } else if (nextToken.type === "Include") {
            nodes.push(this.parseIncludeStatement());
          } else if (nextToken.type === "If") {
            nodes.push(this.parseIfStatement());
          } else if (nextToken.type === "For") {
            nodes.push(this.parseForStatement());
          } else if (
            nextToken.type === "ElseIf" ||
            nextToken.type === "Else" ||
            nextToken.type === "EndIf" ||
            nextToken.type === "EndFor" ||
            nextToken.type === "EndCapture"
          ) {
            if (this.position === 0) {
              throw new TemplateParsingError(`Unexpected token: ${token.type}`, token.location);
            }
            break; // closing directive — return control to the parent parser
          } else {
            nodes.push(this.parseExpressionStatement());
          }
        } else {
          throw new TemplateParsingError(`Unexpected token: ${token.type}`, token.location);
        }
      } catch (e) {
        if (!(e instanceof TemplateParsingError)) throw e;
        innerErrors.push(e);
        do {
          this.advance();
        } while (this.position < this.tokens.length && this.tokens[this.position]!.type !== "DirectiveStart");
      }
    }

    if (innerErrors.length > 0) {
      throw new TemplateParsingError("Template parsing failed", null, innerErrors);
    }

    return new TemplateNode(nodes, startLocation);
  }

  private parseComment(): void {
    this.advance(); // {{
    this.advance(); // *
    this.expect("CommentEnd");
    this.advance();
    this.expect("DirectiveEnd");
    this.advance();
  }

  private parseLetStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // let

    const variableName = this.expect("Variable").value;
    this.advance();

    this.expect("Assignment");
    this.advance();

    const expression = this.parseExpression();

    this.expect("DirectiveEnd");
    this.advance();

    return new LetNode(variableName, expression, token.location);
  }

  private parseMutationStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // mut

    let variableName = this.expect("Variable").value;
    this.advance();

    while (this.position < this.tokens.length && this.current().type === "Dot") {
      this.advance(); // dot
      const fieldToken = this.current();
      if (fieldToken.type !== "Field" && fieldToken.type !== "Variable") {
        throw new TemplateParsingError(`Expected field name but got ${fieldToken.type}`, fieldToken.location);
      }
      variableName = `${variableName}.${fieldToken.value}`;
      this.advance();
    }

    this.expect("Assignment");
    this.advance();

    const expression = this.parseExpression();

    this.expect("DirectiveEnd");
    this.advance();

    return new MutationNode(variableName, expression, token.location);
  }

  private parseCaptureStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // capture

    const variableName = this.expect("Variable").value;
    this.advance();

    this.expect("DirectiveEnd");
    this.advance();

    const body = this.parseTemplate();

    this.expect("DirectiveStart");
    this.advance();
    this.expect("EndCapture");
    this.advance();
    this.expect("DirectiveEnd");
    this.advance();

    return new CaptureNode(variableName, body, token.location);
  }

  private parseLiteralStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // literal

    this.expect("DirectiveEnd");
    this.advance();

    this.expect("Text");
    const content = this.current().value;
    this.advance();

    this.expect("DirectiveStart");
    this.advance();
    this.expect("EndLiteral");
    this.advance();
    this.expect("DirectiveEnd");
    this.advance();

    return new LiteralNode(content, token.location);
  }

  private parseIncludeStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // include

    const templateName = this.expect("Variable").value;
    this.advance();

    this.expect("DirectiveEnd");
    this.advance();

    return new IncludeNode(templateName, token.location);
  }

  private parseExpressionStatement(): AstNode {
    this.advance(); // {{
    const expression = this.parseExpression();
    this.expect("DirectiveEnd");
    this.advance();
    return expression;
  }

  private parseGroupExpression(): AstNode {
    this.advance(); // (
    const expression = this.parseExpression();
    this.expect("RightParen");
    this.advance();
    return expression;
  }

  private parseInvocation(callable: AstNode): AstNode {
    const token = this.current();
    this.advance(); // (
    const args: AstNode[] = [];

    if (this.current().type !== "RightParen") {
      while (true) {
        args.push(this.parseExpression());
        if (this.current().type === "RightParen") break;
        try {
          this.expect("Comma");
          this.advance();
        } catch (ex) {
          throw new TemplateParsingError(
            `Expected comma between function arguments or a closing parenthesis: ${(ex as Error).message}`,
            this.current().location,
          );
        }
      }
    }

    this.expect("RightParen");
    this.advance();

    return new InvocationNode(callable, args, token.location);
  }

  private parseLambda(): AstNode {
    this.expect("LeftParen");
    const token = this.current();
    this.advance(); // (

    const parameters: string[] = [];
    const statements: LambdaStatement[] = [];

    if (this.current().type !== "RightParen") {
      while (true) {
        if (this.current().type !== "Variable" && this.current().type !== "Parameter") {
          throw new TemplateParsingError(`Expected parameter name but got ${this.current().type}`, this.current().location);
        }
        if (parameters.includes(this.current().value)) {
          throw new TemplateParsingError(
            `Duplicate parameter name '${this.current().value}' in lambda definition`,
            this.current().location,
          );
        }
        parameters.push(this.current().value);
        this.advance();

        if (this.current().type === "RightParen") break;

        try {
          this.expect("Comma");
          this.advance();
        } catch (ex) {
          throw new TemplateParsingError(`Expected comma between lambda parameters: ${(ex as Error).message}`, this.current().location);
        }
      }
    }

    this.expect("RightParen");
    this.advance(); // )

    this.expect("Arrow");
    this.advance(); // =>

    while (true) {
      if (this.current().type !== "Let" && this.current().type !== "Mutation") {
        const finalExpression = this.parseExpression();
        return new LambdaNode(parameters, statements, finalExpression, token.location);
      }

      const statementType: StatementType = this.current().type === "Let" ? "Declaration" : "Mutation";
      this.advance(); // let or mut

      this.expect("Variable");
      let variableName = this.current().value;
      this.advance();

      while (this.position < this.tokens.length && this.current().type === "Dot") {
        this.advance(); // dot
        const fieldToken = this.current();
        if (fieldToken.type !== "Field" && fieldToken.type !== "Variable") {
          throw new TemplateParsingError(`Expected field name but got ${fieldToken.type}`, fieldToken.location);
        }
        variableName = `${variableName}.${fieldToken.value}`;
        this.advance();
      }

      this.expect("Assignment");
      this.advance();

      const expression = this.parseExpression();
      statements.push({ variableName, expression, statementType });

      this.expect("Comma");
      this.advance();
    }
  }

  private parseObjectCreation(): AstNode {
    const token = this.current();
    this.advance(); // obj(

    const fields: ObjectField[] = [];

    while (this.position < this.tokens.length && this.current().type !== "RightParen") {
      if (this.current().type !== "Variable") {
        throw new TemplateParsingError(`Expected field name but got ${this.current().type}`, this.current().location);
      }
      const fieldName = this.current().value;
      if (fields.some((f) => f.key === fieldName)) {
        throw new TemplateParsingError(`Duplicate field name '${fieldName}' defined in object`, this.current().location);
      }
      this.advance();

      if (this.current().type !== "Colon") {
        throw new TemplateParsingError(`Expected ':' but got ${this.current().type}`, this.current().location);
      }
      this.advance();

      const fieldValue = this.parseExpression();
      fields.push({ key: fieldName, value: fieldValue });

      if (this.current().type === "Comma") {
        this.advance();
      } else if (this.current().type === "RightParen") {
        break;
      } else {
        throw new TemplateParsingError(
          `Unclosed object literal: expected ',' or ')' but got ${this.current().type}`,
          this.current().location,
        );
      }
    }

    this.expect("RightParen");
    this.advance();

    return new ObjectCreationNode(fields, token.location);
  }

  private parseArrayCreation(): AstNode {
    const token = this.current();
    this.advance(); // [

    const elements: AstNode[] = [];

    if (this.current().type === "RightBracket") {
      this.advance();
      return new ArrayNode(elements, token.location);
    }

    while (true) {
      elements.push(this.parseExpression());

      if (this.current().type === "RightBracket") {
        this.advance();
        break;
      }
      if (this.current().type !== "Comma") {
        throw new TemplateParsingError(`Expected ',' or ']' but got ${this.current().type}`, this.current().location);
      }
      this.advance();
    }

    return new ArrayNode(elements, token.location);
  }

  private parseIfStatement(): AstNode {
    const branches: IfBranch[] = [];
    let elseBranch: AstNode | null = null;
    let foundClosingTag = false;

    this.advance(); // {{
    const ifToken = this.current();
    this.advance(); // if
    let condition = this.parseExpression();

    this.expect("DirectiveEnd");
    this.advance();

    let body = this.parseTemplate();
    branches.push({ condition, body });

    while (this.position < this.tokens.length && this.current().type === "DirectiveStart") {
      const token = this.tokens[this.position + 1]!;

      if (token.type === "ElseIf") {
        this.advance(); // {{
        this.advance(); // elseif
        condition = this.parseExpression();
        this.expect("DirectiveEnd");
        this.advance();
        body = this.parseTemplate();
        branches.push({ condition, body });
      } else if (token.type === "Else") {
        this.advance(); // {{
        this.advance(); // else
        this.expect("DirectiveEnd");
        this.advance();
        elseBranch = this.parseTemplate();
      } else if (token.type === "EndIf") {
        this.advance(); // {{
        this.advance(); // /if
        this.expect("DirectiveEnd");
        this.advance();
        foundClosingTag = true;
        break;
      } else {
        break;
      }
    }

    if (!foundClosingTag) {
      const lastToken = this.tokens.length > 0 ? this.tokens[this.tokens.length - 1] : null;
      const location = lastToken ? lastToken.location : new SourceLocation(0, 0, 0);
      throw new TemplateParsingError("Unclosed if statement: Missing {{/if}} directive", location);
    }

    return new IfNode(branches, elseBranch, ifToken.location);
  }

  private parseForStatement(): AstNode {
    this.advance(); // {{
    const token = this.current();
    this.advance(); // for

    const iteratorName = this.expect("Variable").value;
    this.advance();

    this.expect("In");
    this.advance();

    const collection = this.parseExpression();

    this.expect("DirectiveEnd");
    this.advance();

    const body = this.parseTemplate();

    this.expect("DirectiveStart");
    this.advance();
    this.expect("EndFor");
    this.advance();
    this.expect("DirectiveEnd");
    this.advance();

    return new ForNode(iteratorName, collection, body, token.location);
  }

  private parseExpression(): AstNode {
    return this.parseOr();
  }

  private parseOr(): AstNode {
    let left = this.parseAnd();
    while (this.position < this.tokens.length && this.current().type === "Or") {
      const token = this.current();
      this.advance();
      const right = this.parseAnd();
      left = new BinaryNode(token.type, left, right, token.location);
    }
    return left;
  }

  private parseAnd(): AstNode {
    let left = this.parseComparison();
    while (this.position < this.tokens.length && this.current().type === "And") {
      const token = this.current();
      this.advance();
      const right = this.parseComparison();
      left = new BinaryNode(token.type, left, right, token.location);
    }
    return left;
  }

  private parseComparison(): AstNode {
    let left = this.parseAdditive();
    while (this.position < this.tokens.length && this.isComparisonOperator(this.current().type)) {
      const token = this.current();
      this.advance();
      const right = this.parseAdditive();
      left = new BinaryNode(token.type, left, right, token.location);
    }
    return left;
  }

  private parseAdditive(): AstNode {
    let left = this.parseMultiplicative();
    while (this.position < this.tokens.length && (this.current().type === "Plus" || this.current().type === "Minus")) {
      const token = this.current();
      this.advance();
      const right = this.parseMultiplicative();
      left = new BinaryNode(token.type, left, right, token.location);
    }
    return left;
  }

  private parseMultiplicative(): AstNode {
    let left = this.parseUnary();
    while (this.position < this.tokens.length && (this.current().type === "Multiply" || this.current().type === "Divide")) {
      const token = this.current();
      this.advance();
      const right = this.parseUnary();
      left = new BinaryNode(token.type, left, right, token.location);
    }
    return left;
  }

  private parseUnary(): AstNode {
    if (this.current().type === "Not") {
      const token = this.current();
      this.advance();
      const expression = this.parseUnary();
      return new UnaryNode(token.type, expression, token.location);
    }
    return this.parsePrimary();
  }

  private parsePrimary(): AstNode {
    const token = this.current();
    let expr: AstNode;

    switch (token.type) {
      case "LeftBracket":
        expr = this.parseArrayCreation();
        break;
      case "ObjectStart":
        expr = this.parseObjectCreation();
        break;
      case "LeftParen":
        expr = this.isLambdaAhead() ? this.parseLambda() : this.parseGroupExpression();
        break;
      case "Function":
        expr = new FunctionReferenceNode(token.value, token.location);
        this.advance();
        break;
      case "Variable":
        this.advance();
        expr = new VariableNode(token.value, token.location);
        break;
      case "String":
        this.advance();
        expr = new StringNode(token.value, token.location);
        break;
      case "Number":
        this.advance();
        expr = new NumberNode(token.value, token.location);
        break;
      case "True":
        this.advance();
        expr = new BooleanNode(true, token.location);
        break;
      case "False":
        this.advance();
        expr = new BooleanNode(false, token.location);
        break;
      case "Type":
        expr = this.parseType();
        break;
      default:
        throw new TemplateParsingError(`Expected an expression but found ${token.type}`, token.location);
    }

    // Invocations directly following the primary expression.
    while (this.position < this.tokens.length && this.current().type === "LeftParen") {
      expr = this.parseInvocation(expr);
    }

    // Field access, with possible chained invocations.
    while (this.position < this.tokens.length && this.current().type === "Dot") {
      this.advance(); // dot
      const fieldToken = this.current();
      if (fieldToken.type !== "Field" && fieldToken.type !== "Variable") {
        throw new TemplateParsingError(`Expected field name but got ${fieldToken.type}`, fieldToken.location);
      }
      expr = new FieldAccessNode(expr, fieldToken.value, fieldToken.location);
      this.advance();

      while (this.position < this.tokens.length && this.current().type === "LeftParen") {
        expr = this.parseInvocation(expr);
      }
    }

    return expr;
  }

  private parseType(): AstNode {
    const token = this.current();
    this.advance();
    let type: ValueType;
    switch (token.value) {
      case "String": type = "String"; break;
      case "Number": type = "Number"; break;
      case "Boolean": type = "Boolean"; break;
      case "Array": type = "Array"; break;
      case "Object": type = "Object"; break;
      case "Function": type = "Function"; break;
      case "DateTime": type = "DateTime"; break;
      default:
        throw new TemplateParsingError(`Unable to parse unknown type ${token.value}`, token.location);
    }
    return new TypeNode(type, token.location);
  }

  private isLambdaAhead(): boolean {
    const pos = this.position;
    try {
      this.advance();
      let firstParam = true;
      while (this.position < this.tokens.length && this.current().type !== "RightParen") {
        if (firstParam) {
          if (this.current().type !== "Variable") return false;
          firstParam = false;
        } else {
          if (this.current().type === "Comma") {
            this.advance();
            if (this.current().type !== "Variable") return false;
          } else {
            return false;
          }
        }
        this.advance();
      }
      if (this.current().type !== "RightParen") return false;
      this.advance();
      return this.current().type === "Arrow";
    } catch {
      return false;
    } finally {
      this.position = pos;
    }
  }

  private checkSkipNewline(): boolean {
    const token = this.current();
    if (
      (token.type === "Newline" &&
        this.tokens.length > this.position + 1 &&
        this.tokens[this.position + 1]!.type === "DirectiveStart" &&
        this.tokens[this.position + 1]!.value === "{{-") ||
      (this.tokens.length > this.position + 2 &&
        this.tokens[this.position + 1]!.type === "Whitespace" &&
        this.tokens[this.position + 2]!.type === "DirectiveStart" &&
        this.tokens[this.position + 2]!.value === "{{-") ||
      (this.position > 0 &&
        this.tokens[this.position - 1]!.type === "DirectiveEnd" &&
        this.tokens[this.position - 1]!.value === "-}}") ||
      (this.position > 1 &&
        this.tokens[this.position - 1]!.type === "Whitespace" &&
        this.tokens[this.position - 2]!.type === "DirectiveEnd" &&
        this.tokens[this.position - 2]!.value === "-}}")
    ) {
      return true;
    }
    return false;
  }

  private checkSkipWhitespace(): boolean {
    const token = this.current();
    if (
      (token.type === "Whitespace" &&
        this.tokens.length > this.position + 1 &&
        this.tokens[this.position + 1]!.type === "DirectiveStart" &&
        this.tokens[this.position + 1]!.value === "{{-") ||
      (this.position > 0 &&
        this.tokens[this.position - 1]!.type === "DirectiveEnd" &&
        this.tokens[this.position - 1]!.value === "-}}")
    ) {
      return true;
    }
    return false;
  }

  private isComparisonOperator(type: TokenType): boolean {
    return (
      type === "Equal" ||
      type === "NotEqual" ||
      type === "LessThan" ||
      type === "LessThanEqual" ||
      type === "GreaterThan" ||
      type === "GreaterThanEqual"
    );
  }

  private current(): Token {
    if (this.position >= this.tokens.length) {
      const lastToken = this.tokens.length > 0 ? this.tokens[this.tokens.length - 1] : null;
      const location = lastToken ? lastToken.location : new SourceLocation(0, 0, 0);
      throw new TemplateParsingError(
        "Unexpected end of template: the template is incomplete or contains a syntax error",
        location,
      );
    }
    return this.tokens[this.position]!;
  }

  private advance(): void {
    this.position++;
  }

  private expect(type: TokenType): Token {
    const token = this.current();
    if (token.type !== type) {
      throw new TemplateParsingError(
        `Expected <${type.toLowerCase()}> but got <${token.type.toLowerCase()}>`,
        token.location,
      );
    }
    return token;
  }
}
