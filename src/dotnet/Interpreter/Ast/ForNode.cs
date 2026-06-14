using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;
using System.Text;

namespace PowwowLang.Ast
{
    public class ForNode : AstNode
    {
        private readonly string _iteratorName;
        private readonly AstNode _collection;
        private readonly AstNode _body;

        public string IteratorName { get { return _iteratorName; } }

        public AstNode Collection { get { return _collection; } }

        public AstNode Body { get { return _body; } }

        public ForNode(string iteratorName, AstNode collection, AstNode body, SourceLocation location) : base(location)
        {
            _iteratorName = iteratorName;
            _collection = collection;
            _body = body;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            // Check if iterator name conflicts with existing variable
            if (context.GetFunctionRegistry().HasFunction(_iteratorName) || context.TryResolveValue(_iteratorName, out _))
            {
                throw new TemplateEvaluationException(
                    $"Iterator name '{_iteratorName}' conflicts with an existing variable, field, or function",
                    context,
                    this);
            }

            var collection = _collection.Evaluate(context);
            if (collection.ValueOf() is ArrayValue array)
            {
                var result = new StringBuilder();
                foreach (var item in array.Value())
                {
                    var iterationContext = context.CreateIteratorContext(_iteratorName, item, this);
                    result.Append(_body.Evaluate(iterationContext));
                }
                return new Value(new StringValue(result.ToString()));
            }
            else
            {
                throw new TemplateEvaluationException(
                    "Each statement requires an array",
                    context,
                    _collection);
            }
        }

        public override string ToStackString()
        {
            return $"<iteration {_iteratorName} in {_collection.ToStackString()}>";
        }

        public override string ToString()
        {
            return $"ForNode(iteratorName=\"{_iteratorName}\", collection={_collection.ToString()}, body={_body.ToString()})";
        }

        public override string Name()
        {
            return "<for>";
        }
    }
}
