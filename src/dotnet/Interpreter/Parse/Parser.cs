using PowwowLang.Ast;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Lib;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Parse
{
    public class Parser
    {
        private IReadOnlyList<Token> _tokens;
        private int _position;
        private readonly FunctionRegistry _functionRegistry;

        public Parser(FunctionRegistry functionRegistry)
        {
            _functionRegistry = functionRegistry;
        }

        public AstNode Parse(IReadOnlyList<Token> tokens)
        {
            _tokens = tokens;
            _position = 0;
            return ParseTemplate();
        }

        private AstNode ParseTemplate()
        {
            var nodes = new List<AstNode>();
            var startLocation = _tokens[0]?.Location;
            TemplateParsingException parentException = new TemplateParsingException();

            while (_position < _tokens.Count)
            {
                try
                {
                    var token = Current();

                    if (token.Type == TokenType.Text)
                    {
                        nodes.Add(new TextNode(token.Value, token.Location));
                        Advance();
                    }
                    else if (token.Type == TokenType.Whitespace)
                    {
                        if (CheckSkipWhitespace())
                        {
                            Advance();
                        }
                        else
                        {
                            nodes.Add(new WhitespaceNode(token.Value, token.Location));
                            Advance();
                        }
                    }
                    else if (token.Type == TokenType.Newline)
                    {
                        if (CheckSkipNewline())
                        {
                            Advance();
                        }
                        else
                        {
                            nodes.Add(new NewlineNode(token.Value, token.Location));
                            Advance();
                        }
                    }
                    else if (token.Type == TokenType.DirectiveStart)
                    {
                        // Look at the next token to determine what kind of directive we're dealing with
                        var nextToken = _tokens[_position + 1];

                        if (nextToken.Type == TokenType.CommentStart)
                        {
                            ParseComment();
                        }
                        else if (nextToken.Type == TokenType.Let)
                        {
                            nodes.Add(ParseLetStatement());
                        }
                        else if (nextToken.Type == TokenType.Mutation)
                        {
                            nodes.Add(ParseMutationStatement());
                        }
                        else if (nextToken.Type == TokenType.Variable && IsMutationAhead(_position + 1))
                        {
                            nodes.Add(ParseImplicitMutation());
                        }
                        else if (nextToken.Type == TokenType.Capture)
                        {
                            nodes.Add(ParseCaptureStatement());
                        }
                        else if (nextToken.Type == TokenType.Literal)
                        {
                            nodes.Add(ParseLiteralStatement());
                        }
                        else if (nextToken.Type == TokenType.Include)
                        {
                            nodes.Add(ParseIncludeStatement());
                        }
                        else if (nextToken.Type == TokenType.If)
                        {
                            nodes.Add(ParseIfStatement());
                        }
                        else if (nextToken.Type == TokenType.For)
                        {
                            nodes.Add(ParseForStatement());
                        }
                        else if (nextToken.Type == TokenType.ElseIf ||
                                 nextToken.Type == TokenType.Else ||
                                 nextToken.Type == TokenType.EndIf ||
                                 nextToken.Type == TokenType.EndFor ||
                                 nextToken.Type == TokenType.EndCapture)
                        {
                            if (_position == 0)
                            {
                                throw new TemplateParsingException($"Unexpected token: {token.Type}", token.Location);
                            }
                            // We've hit a closing directive - return control to the parent parser
                            break;
                        }
                        else
                        {
                            nodes.Add(ParseExpressionStatement());
                        }
                    }
                    else
                    {
                        throw new TemplateParsingException($"Unexpected token: {token.Type}", token.Location);
                    }
                }
                catch (TemplateParsingException e)
                {
                    parentException.InnerExceptions.Add(e);
                    do
                    {
                        Advance();
                    }
                    while (_position < _tokens.Count && Current().Type != TokenType.DirectiveStart);
                }
            }

            if (parentException.InnerExceptions.Any())
            {
                throw parentException;
            }

            return new TemplateNode(nodes, startLocation ?? null);
        }

        private void ParseComment()
        {
            Advance(); // Skip {{
            Advance(); // Skip *

            Expect(TokenType.CommentEnd);
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance();
        }

        private AstNode ParseLetStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip let

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.Assignment);
            Advance();

            var expression = ParseExpression();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new LetNode(variableName, expression, token.Location);
        }

        private AstNode ParseMutationStatement()
        {
            // Leaving support in for the mut keyword for backwards compatibility
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip mut

            return ParseMutationBase(token);
        }

        private AstNode ParseImplicitMutation()
        {
            Advance(); // Skip {{
            var token = Current();

            return ParseMutationBase(token);
        }

        private AstNode ParseMutationBase(Token token)
        {
            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            // Handle any fields accessed after a dot
            while (_position < _tokens.Count && Current().Type == TokenType.Dot)
            {
                Advance(); // Skip the dot
                var fieldToken = Current();
                if (fieldToken.Type != TokenType.Field && fieldToken.Type != TokenType.Variable)
                {
                    throw new TemplateParsingException($"Expected field name but got {fieldToken.Type}", fieldToken.Location);
                }
                variableName = $"{variableName}.{fieldToken.Value}";
                Advance();
            }

            Expect(TokenType.Assignment);
            Advance();

            var expression = ParseExpression();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new MutationNode(variableName, expression, token.Location);
        }

        private AstNode ParseCaptureStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip capture

            var variableName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            var body = ParseTemplate();

            // Parse the closing capture tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndCapture);
            Advance(); // Skip /capture
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new CaptureNode(variableName, body, token.Location);
        }

        private AstNode ParseLiteralStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip literal

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            // The next token should be the raw content
            Expect(TokenType.Text);
            var content = Current().Value;
            Advance();

            // Parse the closing capture tag
            Expect(TokenType.DirectiveStart);
            Advance(); // Skip {{
            Expect(TokenType.EndLiteral);
            Advance(); // Skip /capture
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new LiteralNode(content, token.Location);
        }

        private AstNode ParseIncludeStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip include

            var templateName = Expect(TokenType.Variable).Value;
            Advance();

            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}

            return new IncludeNode(templateName, token.Location);
        }

        private AstNode ParseExpressionStatement()
        {
            Advance(); // Skip {{
            var expression = ParseExpression();
            Expect(TokenType.DirectiveEnd);
            Advance(); // Skip }}
            return expression;
        }

        private AstNode ParseGroupExpression()
        {
            Advance(); // Skip (
            var expression = ParseExpression();
            Expect(TokenType.RightParen);
            Advance(); // Skip )
            return expression;
        }

        private AstNode ParseInvocation(AstNode callable)
        {
            var token = Current();

            Advance(); // Skip (
            var arguments = new List<AstNode>();

            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    arguments.Add(ParseExpression());

                    if (Current().Type == TokenType.RightParen)
                        break;

                    try
                    {
                        Expect(TokenType.Comma);
                        Advance();
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Expected comma between function arguments or a closing parenthesis: {ex.Descriptor}", Current().Location);
                    }
                }
            }

            try
            {
                Expect(TokenType.RightParen);
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                new TemplateParsingException($"Unclosed functional call: {ex.Descriptor}", Current().Location);
            }

            return new InvocationNode(callable, arguments, token.Location);
        }

        private AstNode ParseLambda()
        {
            Expect(TokenType.LeftParen);
            var token = Current();
            Advance(); // Skip (

            var parameters = new List<string>();
            var statements = new List<KeyValuePair<string, System.Tuple<AstNode, LambdaNode.StatementType>>>();

            // Parse parameters
            if (Current().Type != TokenType.RightParen)
            {
                while (true)
                {
                    if (Current().Type != TokenType.Variable && Current().Type != TokenType.Parameter)
                    {
                        throw new TemplateParsingException($"Expected parameter name but got {Current().Type}", Current().Location);
                    }

                    if (parameters.Contains(Current().Value))
                    {
                        throw new TemplateParsingException(
                            $"Duplicate parameter name '{Current().Value}' in lambda definition",
                            Current().Location
                        );
                    }

                    parameters.Add(Current().Value);
                    Advance();

                    if (Current().Type == TokenType.RightParen)
                        break;

                    try
                    {
                        Expect(TokenType.Comma);
                        Advance(); // Skip comma
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException(
                            $"Expected comma between lambda parameters: {ex.Descriptor}",
                            Current().Location
                        );
                    }
                }
            }

            try
            {
                Expect(TokenType.RightParen);
                Advance(); // Skip )
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException(
                    $"Expected closing parenthesis after lambda parameters: {ex.Descriptor}",
                    Current().Location
                );
            }

            try
            {
                Expect(TokenType.Arrow);
                Advance(); // Skip =>
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException(
                    $"Expected '=>' after lambda parameters: {ex.Descriptor}",
                    Current().Location
                );
            }

            // Parse statement list
            while (true)
            {
                if (Current().Type != TokenType.Let && Current().Type != TokenType.Mutation && !IsMutationAhead(_position))
                {
                    // If next expression is not a variable declaration or mutation then must be return statement
                    var finalExpression = ParseExpression();
                    return new LambdaNode(parameters, statements, finalExpression, _functionRegistry, token.Location);
                }

                var statementType = Current().Type == TokenType.Let ? LambdaNode.StatementType.Declaration : LambdaNode.StatementType.Mutation;
                if (Current().Type == TokenType.Let || Current().Type == TokenType.Mutation)
                {
                    Advance(); // skip let or mut if it's not an implicit mutation
                }

                string variableName = null;
                try
                {
                    Expect(TokenType.Variable);
                    variableName = Current().Value;
                    Advance();
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected variable name in lambda: {ex.Descriptor}",
                        Current().Location
                    );
                }

                // Handle any fields accessed after a dot
                while (_position < _tokens.Count && Current().Type == TokenType.Dot)
                {
                    Advance(); // Skip the dot
                    var fieldToken = Current();
                    if (fieldToken.Type != TokenType.Field && fieldToken.Type != TokenType.Variable)
                    {
                        throw new TemplateParsingException($"Expected field name but got {fieldToken.Type}", fieldToken.Location);
                    }
                    variableName = $"{variableName}.{fieldToken.Value}";
                    Advance();
                }

                try
                {
                    Expect(TokenType.Assignment);
                    Advance();
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected assignment operator '=' after variable in lambda: {ex.Descriptor}",
                        Current().Location
                    );
                }

                var expression = ParseExpression();
                statements.Add(new KeyValuePair<string, System.Tuple<AstNode, LambdaNode.StatementType>>(
                    variableName,
                    System.Tuple.Create(expression, statementType)));

                try
                {
                    Expect(TokenType.Comma);
                    Advance(); // Skip comma
                }
                catch (TemplateParsingException ex)
                {
                    throw new TemplateParsingException(
                        $"Expected comma after statement in lambda: {ex.Descriptor}",
                        Current().Location
                    );
                }
            }
        }

        private AstNode ParseObjectCreation()
        {
            var token = Current();
            Advance(); // Skip obj(

            var fields = new List<KeyValuePair<string, AstNode>>();

            while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
            {
                // Parse field name
                if (Current().Type != TokenType.Variable)
                {
                    throw new TemplateParsingException($"Expected field name but got {Current().Type}", Current().Location);
                }

                var fieldName = Current().Value;
                if (fields.Any(f => f.Key == fieldName))
                {
                    throw new TemplateParsingException($"Duplicate field name '{fieldName}' defined in object", Current().Location);
                }
                Advance();

                // Parse colon
                if (Current().Type != TokenType.Colon)
                {
                    throw new TemplateParsingException($"Expected ':' but got {Current().Type}", Current().Location);
                }
                Advance();

                // Parse field value expression
                var fieldValue = ParseExpression();

                fields.Add(new KeyValuePair<string, AstNode>(fieldName, fieldValue));

                // If there's a comma, skip it and continue
                if (Current().Type == TokenType.Comma)
                {
                    Advance();
                }
                // If there's a right paren, we're done
                else if (Current().Type == TokenType.RightParen)
                {
                    break;
                }
                else
                {
                    throw new TemplateParsingException($"Unclosed object literal: expected ',' or ')' but got {Current().Type}", Current().Location);
                }
            }

            Expect(TokenType.RightParen);
            Advance(); // Skip )

            return new ObjectCreationNode(fields, token.Location);
        }

        private AstNode ParseArrayCreation()
        {
            var token = Current();
            Advance(); // Skip [

            var elements = new List<AstNode>();

            // Handle empty array case
            if (Current().Type == TokenType.RightBracket)
            {
                Advance(); // Skip ]
                return new ArrayNode(elements, token.Location);
            }

            // Parse array elements
            while (true)
            {
                elements.Add(ParseExpression());

                if (Current().Type == TokenType.RightBracket)
                {
                    Advance(); // Skip ]
                    break;
                }

                if (Current().Type != TokenType.Comma)
                {
                    throw new TemplateParsingException($"Expected ',' or ']' but got {Current().Type}", Current().Location);
                }

                Advance(); // Skip comma
            }

            return new ArrayNode(elements, token.Location);
        }

        private AstNode ParseIfStatement()
        {
            var conditionalBranches = new List<IfNode.IfBranch>();
            AstNode elseBranch = null;
            bool foundClosingTag = false;

            // Parse initial if
            Advance(); // Skip {{
            var ifToken = Current();
            Advance(); // Skip if
            var condition = ParseExpression();

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed if directive: {ex.Descriptor}", ifToken.Location);
            }

            var body = ParseTemplate();
            conditionalBranches.Add(new IfNode.IfBranch(condition, body));

            // Parse any elseif/else clauses
            while (_position < _tokens.Count && Current().Type == TokenType.DirectiveStart)
            {
                var token = _tokens[_position + 1]; // Look at the directive type

                if (token.Type == TokenType.ElseIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip elseif
                    condition = ParseExpression();

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed elseif directive: {ex.Descriptor}", token.Location);
                    }

                    body = ParseTemplate();
                    conditionalBranches.Add(new IfNode.IfBranch(condition, body));
                }
                else if (token.Type == TokenType.Else)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip else

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed else directive: {ex.Descriptor}", token.Location);
                    }
                    elseBranch = ParseTemplate();
                }

                else if (token.Type == TokenType.EndIf)
                {
                    Advance(); // Skip {{
                    Advance(); // Skip /if

                    try
                    {
                        Expect(TokenType.DirectiveEnd);
                        Advance(); // Skip }}
                    }
                    catch (TemplateParsingException ex)
                    {
                        throw new TemplateParsingException($"Unclosed /if directive: {ex.Descriptor}", token.Location);
                    }

                    foundClosingTag = true;
                    break;
                }
                else
                {
                    // This is not an if-related token, so it must be the start of
                    // nested content - let ParseTemplate handle it
                    break;
                }
            }

            // Check if we found a closing tag
            if (!foundClosingTag)
            {
                try
                {
                    var token = Current();
                    throw new TemplateParsingException("Unclosed if statement: Missing {{/if}} directive", Current().Location);
                }
                catch (TemplateParsingException)
                {
                    var lastToken = _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
                    var location = lastToken?.Location ?? new SourceLocation(0, 0, 0);
                    throw new TemplateParsingException("Unclosed if statement: Missing {{/if}} directive", location);
                }
            }

            return new IfNode(conditionalBranches, elseBranch, ifToken.Location);
        }

        private AstNode ParseForStatement()
        {
            Advance(); // Skip {{
            var token = Current();
            Advance(); // Skip for

            string iteratorName;

            try
            {
                iteratorName = Expect(TokenType.Variable).Value;
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected iterator variable name: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.In);
                Advance();
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected 'in' keyword after iterator name: {ex.Descriptor}", Current().Location);
            }

            var collection = ParseExpression();

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed for directive: {ex.Descriptor}", Current().Location);
            }

            var body = ParseTemplate();

            // Handle the closing for tag
            try
            {
                Expect(TokenType.DirectiveStart);
                Advance(); // Skip {{
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected closing /for directive: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.EndFor);
                Advance(); // Skip /for
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Expected /for in closing directive: {ex.Descriptor}", Current().Location);
            }

            try
            {
                Expect(TokenType.DirectiveEnd);
                Advance(); // Skip }}
            }
            catch (TemplateParsingException ex)
            {
                throw new TemplateParsingException($"Unclosed /for directive: {ex.Descriptor}", Current().Location);
            }

            return new ForNode(iteratorName, collection, body, token.Location);
        }

        private AstNode ParseExpression()
        {
            return ParseOr();
        }

        private AstNode ParseOr()
        {
            var left = ParseAnd();

            while (_position < _tokens.Count && Current().Type == TokenType.Or)
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseAnd();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseAnd()
        {
            var left = ParseComparison();

            while (_position < _tokens.Count && Current().Type == TokenType.And)
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseComparison();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseComparison()
        {
            var left = ParseAdditive();

            while (_position < _tokens.Count && IsComparisonOperator(Current().Type))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseAdditive();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseAdditive()
        {
            var left = ParseMultiplicative();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Plus || Current().Type == TokenType.Minus))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseMultiplicative();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseMultiplicative()
        {
            var left = ParseUnary();

            while (_position < _tokens.Count &&
                   (Current().Type == TokenType.Multiply || Current().Type == TokenType.Divide))
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var right = ParseUnary();
                left = new BinaryNode(op, left, right, token.Location);
            }

            return left;
        }

        private AstNode ParseUnary()
        {
            if (Current().Type == TokenType.Not)
            {
                var token = Current();
                var op = token.Type;
                Advance();
                var expression = ParseUnary();
                return new UnaryNode(op, expression, token.Location);
            }

            return ParsePrimary();
        }

        private AstNode ParsePrimary()
        {
            var token = Current();
            AstNode expr = null;

            switch (token.Type)
            {
                case TokenType.LeftBracket:
                    expr = ParseArrayCreation();
                    break;

                case TokenType.ObjectStart:
                    expr = ParseObjectCreation();
                    break;

                case TokenType.LeftParen:
                    if (IsLambdaAhead())
                    {
                        expr = ParseLambda();
                    }
                    else
                    {
                        expr = ParseGroupExpression();
                    }
                    break;

                case TokenType.Function:
                    expr = new FunctionReferenceNode(token.Value, token.Location);
                    Advance();
                    break;

                case TokenType.Variable:
                    Advance();
                    expr = new VariableNode(token.Value, token.Location);
                    break;

                case TokenType.String:
                    Advance();
                    expr = new StringNode(token.Value, token.Location);
                    break;

                case TokenType.Number:
                    Advance();
                    expr = new NumberNode(token.Value, token.Location);
                    break;

                case TokenType.True:
                    Advance();
                    expr = new BooleanNode(true, token.Location);
                    break;

                case TokenType.False:
                    Advance();
                    expr = new BooleanNode(false, token.Location);
                    break;

                case TokenType.Type:
                    expr = ParseType();
                    break;

                default:
                    throw new TemplateParsingException($"Expected an expression but found {token.Type}", token.Location);
            }

            // postfix look to check for invocations, field access, or indexing following the primary expr
            while (_position < _tokens.Count)
            {
                // Handle any invocations that follow the primary expression
                if (Current().Type == TokenType.LeftParen)
                {
                    expr = ParseInvocation(expr);
                }
                else if (Current().Type == TokenType.Dot)
                {
                    Advance(); // Skip the dot
                    var fieldToken = Current();
                    if (fieldToken.Type != TokenType.Field && fieldToken.Type != TokenType.Variable)
                    {
                        throw new TemplateParsingException($"Expected field name but got {fieldToken.Type}", fieldToken.Location);
                    }
                    expr = new FieldAccessNode(expr, fieldToken.Value, fieldToken.Location);
                    Advance();
                }
                else if (Current().Type == TokenType.LeftBracket)
                {
                    Advance(); // Skip the [
                    var index = ParseExpression();
                    Expect(TokenType.RightBracket);
                    Advance(); // Skip the ]
                    expr = new IndexNode(expr, index, index.Location);
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private AstNode ParseType()
        {
            var token = Current();
            Advance();
            ValueType type = ValueType.Type;
            switch (token.Value)
            {
                case "String":
                    type = ValueType.String;
                    break;
                case "Number":
                    type = ValueType.Number;
                    break;
                case "Boolean":
                    type = ValueType.Boolean;
                    break;
                case "Array":
                    type = ValueType.Array;
                    break;
                case "Object":
                    type = ValueType.Object;
                    break;
                case "Function":
                    type = ValueType.Function;
                    break;
                case "DateTime":
                    type = ValueType.DateTime;
                    break;
                default:
                    throw new TemplateParsingException($"Unable to parse unknown type {token.Value}", token.Location);
            }
            return new TypeNode(type, token.Location);
        }

        private bool IsMutationAhead(int pos)
        {
            if (pos >= _tokens.Count || _tokens[pos].Type != TokenType.Variable)
            {
                return false;
            }

            pos++; // Skip Variable

            while (pos < _tokens.Count && _tokens[pos].Type == TokenType.Dot)
            {
                pos++; // Skip Dot
                if (pos >= _tokens.Count || 
                    (_tokens[pos].Type != TokenType.Field && _tokens[pos].Type != TokenType.Variable))
                {
                    return false;
                }
                pos++; // Skip Field
            }

            return pos < _tokens.Count && _tokens[pos].Type == TokenType.Assignment;
        }

        private bool IsLambdaAhead()
        {
            var pos = _position;
            try
            {
                Advance();
                bool firstParam = true;
                while (_position < _tokens.Count && Current().Type != TokenType.RightParen)
                {
                    if (firstParam)
                    {
                        if (Current().Type != TokenType.Variable)
                        {
                            return false;
                        }
                        firstParam = false;
                    }
                    else
                    {
                        if (Current().Type == TokenType.Comma)
                        {
                            Advance();
                            if (Current().Type != TokenType.Variable)
                            {
                                return false;
                            }
                        }
                        else
                        {
                            return false;
                        }
                    }
                    Advance();
                }

                if (Current().Type != TokenType.RightParen)
                {
                    return false;
                }

                Advance();

                return Current().Type == TokenType.Arrow;
            }
            finally
            {
                _position = pos;
            }
        }

        private bool CheckSkipNewline()
        {
            var token = Current();

            if (token.Type == TokenType.Newline &&
                (_tokens.Count > _position + 1 &&
                 _tokens[_position + 1].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 1].Value == "{{-") ||
                (_tokens.Count > _position + 2 &&
                 _tokens[_position + 1].Type == TokenType.Whitespace &&
                 _tokens[_position + 2].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 2].Value == "{{-") ||
                (_position > 0 &&
                 _tokens[_position - 1].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 1].Value == "-}}") ||
                (_position > 1 &&
                 _tokens[_position - 1].Type == TokenType.Whitespace &&
                 _tokens[_position - 2].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 2].Value == "-}}"))
            {
                return true;
            }

            return false;
        }

        private bool CheckSkipWhitespace()
        {
            var token = Current();

            if (token.Type == TokenType.Whitespace &&
                (_tokens.Count > _position + 1 &&
                 _tokens[_position + 1].Type == TokenType.DirectiveStart &&
                 _tokens[_position + 1].Value == "{{-") ||
                (_position > 0 &&
                 _tokens[_position - 1].Type == TokenType.DirectiveEnd &&
                 _tokens[_position - 1].Value == "-}}"))
            {
                return true;
            }

            return false;
        }

        private bool IsComparisonOperator(TokenType type)
        {
            return type == TokenType.Equal ||
                   type == TokenType.NotEqual ||
                   type == TokenType.LessThan ||
                   type == TokenType.LessThanEqual ||
                   type == TokenType.GreaterThan ||
                   type == TokenType.GreaterThanEqual;
        }

        private Token Current()
        {
            if (_position >= _tokens.Count)
            {
                var lastToken = _tokens.Count > 0 ? _tokens[_tokens.Count - 1] : null;
                var location = lastToken?.Location ?? new SourceLocation(0, 0, 0);

                throw new TemplateParsingException("Unexpected end of template: the template is incomplete or contains a syntax error", location);
            }
            return _tokens[_position];
        }

        private void Advance()
        {
            _position++;
        }

        private Token Expect(TokenType type)
        {
            var token = Current();
            if (token.Type != type)
            {
                throw new TemplateParsingException(
                    $"Expected <{type.ToString().ToLower()}> but got <{token.Type.ToString().ToLower()}>",
                    token.Location);
            }
            return token;
        }
    }
}
