using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Lib;
using PowwowLang.Parse;
using PowwowLang.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.UI.WebControls;

namespace PowwowLang.Runtime
{
    public class Interpreter
    {
        private readonly Lexer _lexer;
        private readonly Parser _parser;
        private readonly FunctionRegistry _functionRegistry;
        private readonly ITemplateResolver _templateResolver;
        private readonly IDataverseService _dataverseService;
        private readonly int _maxRecursionDepth;

        public Lexer Lexer { get { return _lexer; } }
        public Parser Parser { get { return _parser; } }

        public Interpreter(ITemplateResolver templateResolver = null, IDataverseService dataverseService = null, int maxRecursionDepth = 1000)
        {
            _functionRegistry = new FunctionRegistry();
            _lexer = new Lexer();
            _parser = new Parser(_functionRegistry);
            _templateResolver = templateResolver;
            _dataverseService = dataverseService;
            _maxRecursionDepth = maxRecursionDepth;

            RegisterDataverseFunctions();
        }

        public void RegisterFunction(string name, List<ParameterDefinition> parameterTypes, Func<ExecutionContext, AstNode, List<Value>, Value> implementation)
        {
            _functionRegistry.Register(name, parameterTypes, implementation);
        }

        private void RegisterDataverseFunctions()
        {
            _functionRegistry.Register("fetch",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    if (_dataverseService == null)
                    {
                        throw new TemplateEvaluationException(
                            "Dataverse service not configured. The fetch function requires a DataverseService to be provided to the Interpreter.",
                            context,
                            callSite);
                    }

                    var fetchXml = (args[0].ValueOf() as StringValue).Value();

                    if (string.IsNullOrEmpty(fetchXml))
                    {
                        throw new TemplateEvaluationException(
                            "fetch function requires a non-empty FetchXML string",
                            context,
                            callSite);
                    }

                    return _dataverseService.RetrieveMultiple(fetchXml);
                });
        }

        public string Interpret(string template, IDictionary<string, dynamic> data)
        {
            var tokens = _lexer.Tokenize(template);
            var ast = _parser.Parse(tokens);

            // If we have a template resolver, process includes
            if (_templateResolver != null)
            {
                ast = ProcessIncludes(ast);
            }

            return ast.Evaluate(new ExecutionContext(
                data != null ? ValueFactory.Create(data) : new Value(new ObjectValue(new Dictionary<string, Value>())),
                _functionRegistry,
                null,
                _maxRecursionDepth,
                ast)).ToString();
        }

        private AstNode ProcessIncludes(AstNode node, HashSet<string> descendantIncludes = null)
        {
            descendantIncludes = descendantIncludes ?? new HashSet<string>();

            // Handle IncludeNode
            if (node is IncludeNode includeNode)
            {
                if (!descendantIncludes.Add(includeNode.TemplateName))
                {
                    throw new TemplateParsingException(
                        $"Circular template reference detected: '{includeNode.TemplateName}'",
                        node.Location);
                }

                try
                {
                    var templateContent = _templateResolver.ResolveTemplate(includeNode.TemplateName);
                    var tokens = _lexer.Tokenize(templateContent);
                    var includedAst = _parser.Parse(tokens);

                    // Process includes in the included template
                    includedAst = ProcessIncludes(includedAst, descendantIncludes);

                    // Set the processed template
                    includeNode.SetIncludedTemplate(includedAst);
                    return includeNode;
                }
                finally
                {
                    descendantIncludes.Remove(includeNode.TemplateName);
                }
            }

            // Handle TemplateNode
            if (node is TemplateNode templateNode)
            {
                var processedChildren = templateNode.Children.Select(child => ProcessIncludes(child, descendantIncludes)).ToList();
                return new TemplateNode(processedChildren, node.Location);
            }

            // Handle IfNode
            if (node is IfNode ifNode)
            {
                var processedBranches = ifNode.ConditionalBranches.Select(branch =>
                    new IfNode.IfBranch(branch.Condition, ProcessIncludes(branch.Body, descendantIncludes))).ToList();
                var processedElse = ifNode.ElseBranch != null ? ProcessIncludes(ifNode.ElseBranch, descendantIncludes) : null;
                return new IfNode(processedBranches, processedElse, node.Location);
            }

            // Handle ForNode
            if (node is ForNode forNode)
            {
                var processedBody = ProcessIncludes(forNode.Body, descendantIncludes);
                return new ForNode(forNode.IteratorName, forNode.Collection, processedBody, node.Location);
            }

            if (node is CaptureNode captureNode)
            {
                var processedBody = ProcessIncludes(captureNode.Body, descendantIncludes);
                return new CaptureNode(captureNode.VariableName, processedBody, node.Location);
            }

            // For all other node types, return as is
            return node;
        }
    }
}
