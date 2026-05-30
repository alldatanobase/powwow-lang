/**
 * Lexer — a faithful port of src/dotnet/Interpreter/Lex/Lexer.cs.
 *
 * Intentionally preserves the .NET tokenizer's behavior (including its keyword
 * prefix-matching and ordering) so the JS port produces the same token stream.
 * Output parity depends on this. See docs/js-port-parity.md.
 */

import { Token, SourceLocation, TemplateParsingError, type TokenType } from "./token.ts";

interface PositionState {
  position: number;
  line: number;
  column: number;
}

// .NET char.IsWhiteSpace includes newlines; IsWhitespace() in the lexer excludes them.
function isWhiteSpaceChar(ch: string): boolean {
  return /\s/.test(ch);
}
function isLetter(ch: string): boolean {
  return /\p{L}/u.test(ch);
}
function isDigit(ch: string): boolean {
  return ch >= "0" && ch <= "9";
}
function isLetterOrDigit(ch: string): boolean {
  return isLetter(ch) || isDigit(ch);
}

export class Lexer {
  private input = "";
  private position = 0;
  private line = 1;
  private column = 1;
  private tokens: Token[] = [];
  private sourceName: string | null = null;

  tokenize(input: string, sourceName: string | null = null): Token[] {
    this.input = input;
    this.position = 0;
    this.line = 1;
    this.column = 1;
    this.tokens = [];
    if (sourceName !== null) this.sourceName = sourceName;

    while (this.position < this.input.length) {
      if (this.tryMatch("{{-")) {
        this.addToken("DirectiveStart", "{{-");
        this.updatePositionAndTracking(3);
        this.tokenizeDirective();
      } else if (this.tryMatch("{{")) {
        this.addToken("DirectiveStart", "{{");
        this.updatePositionAndTracking(2);
        this.tokenizeDirective();
      } else if (this.isNewline(this.position)) {
        this.tokenizeNewline();
      } else if (this.isWhitespace(this.position)) {
        this.tokenizeWhitespace();
      } else {
        this.tokenizeText();
      }
    }

    return this.tokens;
  }

  private tokenizeComment(): void {
    while (this.position < this.input.length) {
      if (this.tryMatch("*")) {
        const saved = this.savePosition();
        this.updatePositionAndTracking(1); // skip past "*"

        while (this.position < this.input.length && isWhiteSpaceChar(this.input[this.position]!)) {
          this.updatePositionAndTracking(1);
        }

        if (this.tryMatch("}}")) {
          this.addTokenAt("CommentEnd", "*", saved);
          this.addToken("DirectiveEnd", "}}");
          this.updatePositionAndTracking(2);
          return;
        } else if (this.tryMatch("-}}")) {
          this.addTokenAt("CommentEnd", "*", saved);
          this.addToken("DirectiveEnd", "-}}");
          this.updatePositionAndTracking(3);
          return;
        }
      }

      this.updatePositionAndTracking(1);
    }

    this.throwLexerError("Unterminated comment");
  }

  private tokenizeDirective(): void {
    this.skipWhitespace();

    if (this.tryMatch("*")) {
      this.addToken("CommentStart", "*");
      this.updatePositionAndTracking(1);
      this.tokenizeComment();
      return;
    }

    if (this.tryMatch("literal")) {
      this.addToken("Literal", "literal");
      this.updatePositionAndTracking(7);

      this.skipWhitespace();

      if (!this.tryMatch("}}") && !this.tryMatch("-}}")) {
        this.throwLexerError("Unterminated literal directive");
      }

      if (this.tryMatch("}}")) {
        this.addToken("DirectiveEnd", "}}");
        this.updatePositionAndTracking(2);
      } else {
        this.addToken("DirectiveEnd", "-}}");
        this.updatePositionAndTracking(3);
      }

      const startPosition = this.savePosition();
      let literalStackCount = 0;

      while (this.position < this.input.length) {
        const originalPosition = this.position;
        let savedPosition = this.savePosition();

        if (this.tryMatch("{{") || this.tryMatch("{{-")) {
          let directiveStartToken: Token;

          if (this.tryMatch("{{")) {
            directiveStartToken = new Token("DirectiveStart", "{{", this.createLocationAt(savedPosition));
            savedPosition = this.updatePositionAndTrackingOnState(2 + this.whitespaceCount(savedPosition.position + 2), savedPosition);
          } else {
            directiveStartToken = new Token("DirectiveStart", "{{-", this.createLocationAt(savedPosition));
            savedPosition = this.updatePositionAndTrackingOnState(3 + this.whitespaceCount(savedPosition.position + 3), savedPosition);
          }

          if (this.tryMatchAt("literal", savedPosition.position)) {
            savedPosition = this.updatePositionAndTrackingOnState(7 + this.whitespaceCount(savedPosition.position + 7), savedPosition);

            if (this.tryMatchAt("}}", savedPosition.position)) {
              savedPosition = this.updatePositionAndTrackingOnState(2, savedPosition);
              this.updatePositionAndTracking(savedPosition.position - originalPosition);
              literalStackCount++;
              continue;
            }

            if (this.tryMatchAt("-}}", savedPosition.position)) {
              savedPosition = this.updatePositionAndTrackingOnState(3, savedPosition);
              this.updatePositionAndTracking(savedPosition.position - originalPosition);
              literalStackCount++;
              continue;
            }
          }

          if (this.tryMatchAt("/literal", savedPosition.position)) {
            const endLiteralToken = new Token("EndLiteral", "/literal", this.createLocationAt(savedPosition));
            savedPosition = this.updatePositionAndTrackingOnState(8 + this.whitespaceCount(savedPosition.position + 8), savedPosition);

            if (this.tryMatchAt("}}", savedPosition.position) || this.tryMatchAt("-}}", savedPosition.position)) {
              let directiveEndToken: Token;

              if (this.tryMatchAt("}}", savedPosition.position)) {
                directiveEndToken = new Token("DirectiveEnd", "}}", this.createLocationAt(savedPosition));
                savedPosition = this.updatePositionAndTrackingOnState(2, savedPosition);
              } else {
                directiveEndToken = new Token("DirectiveEnd", "-}}", this.createLocationAt(savedPosition));
                savedPosition = this.updatePositionAndTrackingOnState(3, savedPosition);
              }

              if (literalStackCount > 0) {
                literalStackCount--;
              } else {
                // Raw literal body is everything from just after the opening directive to here.
                const content = this.input.substring(startPosition.position, this.position);
                this.addTokenAt("Text", content, startPosition);
                this.tokens.push(directiveStartToken);
                this.tokens.push(endLiteralToken);
                this.tokens.push(directiveEndToken);
                this.updatePositionAndTracking(savedPosition.position - originalPosition);
                return;
              }
            }
          }
        }

        this.updatePositionAndTracking(1);
      }

      this.throwLexerError("Unterminated literal directive");
    }

    while (this.position < this.input.length) {
      this.skipWhitespace();

      if (this.position >= this.input.length) {
        continue;
      }

      if (this.tryMatch("}}")) {
        this.addToken("DirectiveEnd", "}}");
        this.updatePositionAndTracking(2);
        return;
      }
      if (this.tryMatch("-}}")) {
        this.addToken("DirectiveEnd", "-}}");
        this.updatePositionAndTracking(3);
        return;
      }
      if (this.tryMatch(",")) {
        this.addToken("Comma", ",");
        this.updatePositionAndTracking(1);
        continue;
      }
      if (this.tryMatch("=>")) {
        this.addToken("Arrow", "=>");
        this.updatePositionAndTracking(2);
        continue;
      }
      if (this.tryMatch("obj(")) {
        this.addToken("ObjectStart", "obj(");
        this.updatePositionAndTracking(4);
        continue;
      }
      if (this.tryMatch(":")) {
        this.addToken("Colon", ":");
        this.updatePositionAndTracking(1);
        continue;
      }
      if (this.tryMatch(".")) {
        this.addToken("Dot", ".");
        this.updatePositionAndTracking(1);
        continue;
      }
      if (this.tryMatch("[")) {
        this.addToken("LeftBracket", "[");
        this.updatePositionAndTracking(1);
        continue;
      }
      if (this.tryMatch("]")) {
        this.addToken("RightBracket", "]");
        this.updatePositionAndTracking(1);
        continue;
      }

      // After a dot, treat identifiers as field names
      if (
        this.position > 0 &&
        this.tokens.length > 0 &&
        this.tokens[this.tokens.length - 1]!.type === "Dot" &&
        this.position < this.input.length &&
        (isLetter(this.input[this.position]!) || this.input[this.position] === "_")
      ) {
        const savedState = this.savePosition();
        while (
          this.position < this.input.length &&
          (isLetterOrDigit(this.input[this.position]!) || this.input[this.position] === "_")
        ) {
          this.updatePositionAndTracking(1);
        }
        const fieldName = this.input.substring(savedState.position, this.position);
        this.addTokenAt("Field", fieldName, savedState);
        continue;
      }

      // Match function calls before other operations
      if (isLetter(this.input[this.position]!)) {
        const savedPosition = this.savePosition();
        while (this.position < this.input.length && isLetter(this.input[this.position]!)) {
          this.updatePositionAndTracking(1);
        }
        const value = this.input.substring(savedPosition.position, this.position);

        this.skipWhitespace();

        if (this.position < this.input.length && this.input[this.position] === "(") {
          this.addTokenAt("Function", value, savedPosition);
          continue;
        } else {
          this.restorePosition(savedPosition);
        }
      }

      if (this.tryMatch("let")) {
        this.addToken("Let", "let");
        this.updatePositionAndTracking(3);
        continue;
      } else if (this.tryMatch("mut")) {
        this.addToken("Mutation", "mut");
        this.updatePositionAndTracking(3);
        continue;
      } else if (this.tryMatch("capture")) {
        this.addToken("Capture", "capture");
        this.updatePositionAndTracking(7);
        continue;
      } else if (this.tryMatch("/capture")) {
        this.addToken("EndCapture", "/capture");
        this.updatePositionAndTracking(8);
        continue;
      } else if (this.tryMatch("for")) {
        this.addToken("For", "for");
        this.updatePositionAndTracking(3);
        continue;
      } else if (this.tryMatch("include")) {
        this.addToken("Include", "include");
        this.updatePositionAndTracking(7);
        continue;
      } else if (this.tryMatch("if")) {
        this.addToken("If", "if");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("elseif")) {
        this.addToken("ElseIf", "elseif");
        this.updatePositionAndTracking(6);
        continue;
      } else if (this.tryMatch("else")) {
        this.addToken("Else", "else");
        this.updatePositionAndTracking(4);
        continue;
      } else if (this.tryMatch("/for")) {
        this.addToken("EndFor", "/for");
        this.updatePositionAndTracking(4);
        continue;
      } else if (this.tryMatch("/if")) {
        this.addToken("EndIf", "/if");
        this.updatePositionAndTracking(3);
        continue;
      } else if (this.tryMatch("String")) {
        this.addToken("Type", "String");
        this.updatePositionAndTracking(6);
        continue;
      } else if (this.tryMatch("Number")) {
        this.addToken("Type", "Number");
        this.updatePositionAndTracking(6);
        continue;
      } else if (this.tryMatch("Boolean")) {
        this.addToken("Type", "Boolean");
        this.updatePositionAndTracking(7);
        continue;
      } else if (this.tryMatch("Array")) {
        this.addToken("Type", "Array");
        this.updatePositionAndTracking(5);
        continue;
      } else if (this.tryMatch("Object")) {
        this.addToken("Type", "Object");
        this.updatePositionAndTracking(6);
        continue;
      } else if (this.tryMatch("Function")) {
        this.addToken("Type", "Function");
        this.updatePositionAndTracking(8);
        continue;
      } else if (this.tryMatch("DateTime")) {
        this.addToken("Type", "DateTime");
        this.updatePositionAndTracking(8);
        continue;
      } else if (this.tryMatch(">=")) {
        this.addToken("GreaterThanEqual", ">=");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("<=")) {
        this.addToken("LessThanEqual", "<=");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("==")) {
        this.addToken("Equal", "==");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("=")) {
        this.addToken("Assignment", "=");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("!=")) {
        this.addToken("NotEqual", "!=");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("&&")) {
        this.addToken("And", "&&");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch("||")) {
        this.addToken("Or", "||");
        this.updatePositionAndTracking(2);
        continue;
      } else if (this.tryMatch(">")) {
        this.addToken("GreaterThan", ">");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("<")) {
        this.addToken("LessThan", "<");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("!")) {
        this.addToken("Not", "!");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("+")) {
        this.addToken("Plus", "+");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("*")) {
        this.addToken("Multiply", "*");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("/")) {
        this.addToken("Divide", "/");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch("(")) {
        this.addToken("LeftParen", "(");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch(")")) {
        this.addToken("RightParen", ")");
        this.updatePositionAndTracking(1);
        continue;
      } else if (this.tryMatch('"')) {
        this.tokenizeString();
        continue;
      } else if (isDigit(this.input[this.position]!) || (this.input[this.position] === "-" && isDigit(this.peekNext()))) {
        this.tokenizeNumber();
        continue;
      } else if (this.tryMatch("-")) {
        this.addToken("Minus", "-");
        this.updatePositionAndTracking(1);
        continue;
      } else if (isLetter(this.input[this.position]!) || this.input[this.position] === "_") {
        this.tokenizeIdentifier();
        continue;
      } else {
        this.throwLexerError(`Unexpected character '${this.input[this.position]}'`);
      }
    }
  }

  private tokenizeText(): void {
    const savedPosition = this.savePosition();
    while (
      this.position < this.input.length &&
      !this.tryMatch("{{") &&
      !this.isNewline(this.position) &&
      !this.isWhitespace(this.position)
    ) {
      this.updatePositionAndTracking(1);
    }
    if (this.position > savedPosition.position) {
      this.addTokenAt("Text", this.input.substring(savedPosition.position, this.position), savedPosition);
    }
  }

  private tokenizeWhitespace(): void {
    const savedPosition = this.savePosition();
    while (this.position < this.input.length && this.isWhitespace(this.position)) {
      this.updatePositionAndTracking(1);
    }
    if (this.position > savedPosition.position) {
      this.addTokenAt("Whitespace", this.input.substring(savedPosition.position, this.position), savedPosition);
    }
  }

  private tokenizeNewline(): void {
    const savedPosition = this.savePosition();
    let newlineValue: string;
    if (this.input[this.position] === "\r" && this.position + 1 < this.input.length && this.input[this.position + 1] === "\n") {
      newlineValue = "\r\n";
      this.updatePositionAndTracking(2);
    } else {
      newlineValue = this.input[this.position] === "\r" ? "\r" : "\n";
      this.updatePositionAndTracking(1);
    }
    this.addTokenAt("Newline", newlineValue, savedPosition);
  }

  private isNewline(pos: number): boolean {
    if (pos >= this.input.length) return false;
    return this.input[pos] === "\r" || this.input[pos] === "\n";
  }

  private isWhitespace(pos: number): boolean {
    if (pos >= this.input.length) return false;
    return isWhiteSpaceChar(this.input[pos]!) && !this.isNewline(pos);
  }

  private tokenizeString(): void {
    this.updatePositionAndTracking(1); // skip opening quote
    let result = "";
    const savedPosition = this.savePosition();

    while (this.position < this.input.length && this.input[this.position] !== '"') {
      if (this.input[this.position] === "\\" && this.position + 1 < this.input.length) {
        const nextChar = this.input[this.position + 1];
        switch (nextChar) {
          case '"': result += '"'; break;
          case "\\": result += "\\"; break;
          case "n": result += "\n"; break;
          case "r": result += "\r"; break;
          case "t": result += "\t"; break;
          default:
            this.throwLexerError(`Invalid escape sequence '\\${nextChar}'`);
        }
        this.updatePositionAndTracking(2);
      } else {
        result += this.input[this.position];
        this.updatePositionAndTracking(1);
      }
    }

    if (this.position >= this.input.length) {
      this.throwLexerError("Unterminated string literal");
    }

    this.addTokenAt("String", result, savedPosition);
    this.updatePositionAndTracking(1); // skip closing quote
  }

  private tokenizeNumber(): void {
    const savedPosition = this.savePosition();
    let hasDecimal = false;

    if (this.input[this.position] === "-") {
      this.updatePositionAndTracking(1);
    }

    while (
      this.position < this.input.length &&
      (isDigit(this.input[this.position]!) || (!hasDecimal && this.input[this.position] === "."))
    ) {
      if (this.input[this.position] === ".") hasDecimal = true;
      this.updatePositionAndTracking(1);
    }

    const value = this.input.substring(savedPosition.position, this.position);
    this.addTokenAt("Number", value, savedPosition);
  }

  private tokenizeIdentifier(): void {
    const savedPosition = this.savePosition();
    while (
      this.position < this.input.length &&
      (isLetterOrDigit(this.input[this.position]!) || this.input[this.position] === "_")
    ) {
      this.updatePositionAndTracking(1);
    }
    const value = this.input.substring(savedPosition.position, this.position);

    switch (value) {
      case "true": this.addTokenAt("True", value, savedPosition); break;
      case "false": this.addTokenAt("False", value, savedPosition); break;
      case "in": this.addTokenAt("In", value, savedPosition); break;
      default: this.addTokenAt("Variable", value, savedPosition); break;
    }
  }

  private skipWhitespace(): void {
    while (this.position < this.input.length && isWhiteSpaceChar(this.input[this.position]!)) {
      this.updatePositionAndTracking(1);
    }
  }

  private whitespaceCount(position: number): number {
    let current = position;
    while (current < this.input.length && isWhiteSpaceChar(this.input[current]!)) {
      current++;
    }
    return current - position;
  }

  private updatePositionAndTrackingOnState(distance: number, state: PositionState): PositionState {
    for (let i = 0; i < distance && state.position < this.input.length; i++) {
      if (this.input[state.position] === "\n") {
        state.line++;
        state.column = 1;
      } else if (this.input[state.position] === "\r") {
        if (state.position + 1 < this.input.length && this.input[state.position + 1] === "\n") {
          i++;
          state.position++;
        }
        state.line++;
        state.column = 1;
      } else {
        state.column++;
      }
      state.position++;
    }
    return state;
  }

  private updatePositionAndTracking(distance: number): void {
    for (let i = 0; i < distance && this.position < this.input.length; i++) {
      if (this.input[this.position] === "\n") {
        this.line++;
        this.column = 1;
      } else if (this.input[this.position] === "\r") {
        if (this.position + 1 < this.input.length && this.input[this.position + 1] === "\n") {
          i++;
          this.position++;
        }
        this.line++;
        this.column = 1;
      } else {
        this.column++;
      }
      this.position++;
    }
  }

  private tryMatch(pattern: string): boolean {
    if (this.position + pattern.length > this.input.length) return false;
    return this.input.substring(this.position, this.position + pattern.length) === pattern;
  }

  private tryMatchAt(pattern: string, position: number): boolean {
    if (position + pattern.length > this.input.length) return false;
    return this.input.substring(position, position + pattern.length) === pattern;
  }

  private peekNext(): string {
    return this.position + 1 < this.input.length ? this.input[this.position + 1]! : "\0";
  }

  private savePosition(): PositionState {
    return { position: this.position, line: this.line, column: this.column };
  }

  private restorePosition(state: PositionState): void {
    this.position = state.position;
    this.line = state.line;
    this.column = state.column;
  }

  private createLocation(): SourceLocation {
    return new SourceLocation(this.line, this.column, this.position, this.sourceName);
  }

  private createLocationAt(state: PositionState): SourceLocation {
    return new SourceLocation(state.line, state.column, state.position, this.sourceName);
  }

  private addToken(type: TokenType, value: string): void {
    this.tokens.push(new Token(type, value, this.createLocation()));
  }

  private addTokenAt(type: TokenType, value: string, state: PositionState): void {
    this.tokens.push(new Token(type, value, this.createLocationAt(state)));
  }

  private throwLexerError(message: string): never {
    throw new TemplateParsingError(message, new SourceLocation(this.line, this.column, this.position, this.sourceName));
  }
}
