/** Ported from src/dotnet/Interpreter/Lex/{TokenType,Token,SourceLocation}.cs */

export type TokenType =
  | "Text"
  | "Whitespace"
  | "Newline"
  | "DirectiveStart" // {{ or {{-
  | "DirectiveEnd" // }} or -}}
  | "Variable"
  | "String"
  | "Number"
  | "True"
  | "False"
  | "Not"
  | "Equal"
  | "NotEqual"
  | "LessThan"
  | "LessThanEqual"
  | "GreaterThan"
  | "GreaterThanEqual"
  | "And"
  | "Or"
  | "Plus"
  | "Minus"
  | "Multiply"
  | "Divide"
  | "LeftParen"
  | "RightParen"
  | "For"
  | "In"
  | "If"
  | "ElseIf"
  | "Else"
  | "EndFor"
  | "EndIf"
  | "Let"
  | "Assignment"
  | "Function"
  | "Comma"
  | "Arrow"
  | "Parameter"
  | "ObjectStart"
  | "Colon"
  | "Dot"
  | "Field"
  | "LeftBracket"
  | "RightBracket"
  | "Include"
  | "Literal"
  | "EndLiteral"
  | "Capture"
  | "EndCapture"
  | "CommentStart"
  | "CommentEnd"
  | "Type"
  | "Mutation";

export class SourceLocation {
  readonly line: number;
  readonly column: number;
  readonly position: number;
  readonly source: string | null;

  constructor(line: number, column: number, position: number, source: string | null = null) {
    this.line = line;
    this.column = column;
    this.position = position;
    this.source = source;
  }

  toString(): string {
    return this.source !== null
      ? `line ${this.line}, column ${this.column} in ${this.source}`
      : `line ${this.line}, column ${this.column}`;
  }
}

export class Token {
  readonly type: TokenType;
  readonly value: string;
  readonly location: SourceLocation;

  constructor(type: TokenType, value: string, location: SourceLocation) {
    this.type = type;
    this.value = value;
    this.location = location;
  }
}

export class TemplateParsingError extends Error {
  readonly location: SourceLocation | null;
  /** Aggregated child errors (the parser collects multiple problems before failing). */
  readonly innerErrors: TemplateParsingError[];
  constructor(message: string, location: SourceLocation | null = null, innerErrors: TemplateParsingError[] = []) {
    super(location !== null ? `${message} (${location.toString()})` : message);
    this.name = "TemplateParsingError";
    this.location = location;
    this.innerErrors = innerErrors;
  }
}
