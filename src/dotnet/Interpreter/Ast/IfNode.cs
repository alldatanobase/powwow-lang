using PowwowLang.Env;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Ast
{
    public class IfNode : AstNode
    {
        private readonly List<IfBranch> _conditionalBranches;
        private readonly AstNode _elseBranch;

        public List<IfBranch> ConditionalBranches { get { return _conditionalBranches; } }

        public AstNode ElseBranch { get { return _elseBranch; } }

        public class IfBranch
        {
            public AstNode Condition { get; private set; }
            public AstNode Body { get; private set; }

            public IfBranch(AstNode condition, AstNode body)
            {
                Condition = condition;
                Body = body;
            }
        }

        public IfNode(List<IfBranch> conditionalBranches, AstNode elseBranch, SourceLocation location) : base(location)
        {
            _conditionalBranches = conditionalBranches;
            _elseBranch = elseBranch;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            foreach (var branch in _conditionalBranches)
            {
                var evaluated = branch.Condition.Evaluate(context);
                if (TypeHelper.UnboxBoolean(evaluated, context, this))
                {
                    var currentContext = new ExecutionContext(
                        context.GetData(),
                        context.GetFunctionRegistry(),
                        context,
                        context.MaxDepth,
                        this);
                    return branch.Body.Evaluate(currentContext);
                }
            }

            if (_elseBranch != null)
            {
                var currentContext = new ExecutionContext(
                    context.GetData(),
                    context.GetFunctionRegistry(),
                    context,
                    context.MaxDepth,
                    this);
                return _elseBranch.Evaluate(currentContext);
            }

            return new Value(new StringValue(string.Empty));
        }

        public override string ToStackString()
        {
            return $"<if>";
        }

        public override string ToString()
        {
            var branchesStr = string.Join(", ", _conditionalBranches.Select(b =>
                $"{{condition={b.Condition.ToString()}, body={b.Body.ToString()}}}"
            ));

            string elseStr = _elseBranch != null ? _elseBranch.ToString() : "null";

            return $"IfNode(conditionalBranches=[{branchesStr}], elseBranch={elseStr})";
        }

        public override string Name()
        {
            return "<if>";
        }
    }
}
