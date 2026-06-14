using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Lib;
using PowwowLang.Types;
using System;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Ast
{
    public class LambdaNode : AstNode
    {
        private readonly List<string> _parameters;
        private readonly List<KeyValuePair<string, Tuple<AstNode, StatementType>>> _statements;
        private readonly AstNode _finalExpression;

        public enum StatementType
        {
            Declaration,
            Mutation
        }

        public LambdaNode(
            List<string> parameters,
            List<KeyValuePair<string, Tuple<AstNode, StatementType>>> statements,
            AstNode finalExpression,
            FunctionRegistry functionRegistry,
            SourceLocation location) : base(location)
        {
            // Validate parameter names against function registry
            var seenParams = new HashSet<string>();
            foreach (var param in parameters)
            {
                if (functionRegistry.HasFunction(param))
                {
                    throw new TemplateParsingException(
                        $"Parameter name '{param}' conflicts with an existing function name",
                        location);
                }
                if (!seenParams.Add(param))
                {
                    throw new TemplateParsingException(
                        $"Parameter name '{param}' is already defined",
                        location);
                }
            }

            // Validate statement variable names don't conflict with parameters, each other, or functions in registry
            var seenVariables = new HashSet<string>();

            foreach (var statement in statements)
            {
                if (statement.Value.Item2 == StatementType.Declaration)
                {
                    if (parameters.Contains(statement.Key) || 
                        functionRegistry.HasFunction(statement.Key) ||
                        !seenVariables.Add(statement.Key))
                    {
                        throw new TemplateParsingException(
                            $"Cannot define variable '{statement.Key}' because it conflicts with an existing variable, field, or function",
                            location);
                    }
                }
            }

            _parameters = parameters;
            _statements = statements;
            _finalExpression = finalExpression;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var definitionContext = context;

            // Return a delegate that can be called later with parameters
            // Context is captured here, during evaluation, not during parsing
            return new Value(new LambdaValue(new Func<ExecutionContext, AstNode, List<Value>, Value>((callerContext, callSite, args) =>
            {
                try
                {
                    // Create a new context that includes both captured context and new parameters
                    var lambdaContext = new LambdaExecutionContext(callerContext, definitionContext, _parameters, args, callSite);

                    // Execute each statement in order
                    foreach (var statement in _statements)
                    {
                        var value = statement.Value.Item1.Evaluate(lambdaContext);
                        try
                        {
                            if (statement.Value.Item2 == StatementType.Declaration)
                            {
                                lambdaContext.DefineVariable(statement.Key, value);
                            }
                            else
                            {
                                lambdaContext.RedefineVariable(statement.Key, value);
                            }
                        }
                        catch (InnerEvaluationException ex)
                        {
                            throw new TemplateEvaluationException(ex.Message, lambdaContext, callSite);
                        }
                    }

                    return _finalExpression.Evaluate(lambdaContext);
                }
                catch (InnerEvaluationException ex)
                {
                    throw new TemplateEvaluationException(ex.Message, context, this);
                }
            }), _parameters));
        }

        public override string ToStackString()
        {
            return $"<lambda {string.Join(", ", _parameters)}>";
        }

        public override string ToString()
        {
            var paramsStr = string.Join(", ", _parameters.Select(p => $"\"{p}\""));
            var statementsStr = string.Join(", ",
                _statements.Select(st => $"{{key=\"{st.Key}\", value={st.Value.Item1.ToString()}, type={st.Value.Item2.ToString()}}}"));

            return $"LambdaNode(parameters=[{paramsStr}], statements=[{statementsStr}], finalExpression={_finalExpression.ToString()})";
        }

        public override string Name()
        {
            return "<lambda>";
        }
    }
}
