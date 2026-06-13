using System.Linq;
using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class IndexNode : AstNode
    {
        private readonly AstNode _target;
        private readonly AstNode _index;

        public IndexNode(AstNode target, AstNode index, SourceLocation location) : base(location)
        {
            _target = target;
            _index = index;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var evaluatedIndexee = _target.Evaluate(context);
            var evaluatedIndex = _index.Evaluate(context);

            if (evaluatedIndexee == null)
            {
                throw new TemplateEvaluationException(
                    $"Cannot perform index on null value",
                    context,
                    _target);
            }

            if (evaluatedIndex == null)
            {
                throw new TemplateEvaluationException(
                    $"Cannot perform index using null value",
                    context,
                    _target);
            }

            if (evaluatedIndexee.ValueOf() is ArrayValue arrayValue)
            {
                if (evaluatedIndex.ValueOf() is NumberValue numberValue)
                {
                    var array = arrayValue.Value().ToArray();
                    var index = numberValue.Value();

                    if (index % 1 != 0)
                    {
                        throw new TemplateEvaluationException(
                            $"Expected a whole number while indexing array but found {index}",
                            context,
                            _index);
                    }

                    if (index < 0 || index >= array.Length)
                    {
                        throw new TemplateEvaluationException(
                            $"Index {index} is out of bounds for array of length {array.Length}",
                            context,
                            _index);
                    }

                    return array[(int)index];
                }
                else
                {
                    throw new TemplateEvaluationException(
                        $"Expected a whole number while indexing array but found {evaluatedIndex.TypeOf()}",
                        context,
                        _index);
                }
            }

            if (evaluatedIndexee.ValueOf() is ObjectValue objectValue)
            {
                if (evaluatedIndex.ValueOf() is StringValue stringValue)
                {
                    var obj = objectValue.Value();
                    var fieldName = stringValue.Value();
                    if (!obj.ContainsKey(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            $"Object does not contain field '{fieldName}'",
                            context,
                            _target);
                    }
                    return obj[fieldName];
                }
                else
                {
                    throw new TemplateEvaluationException(
                        $"Expected a string while indexing object but found {evaluatedIndex.TypeOf()}",
                        context,
                        _index);
                }
            }

            throw new TemplateEvaluationException(
                $"Indexing operation requires an array or an object",
                context,
                _target);
        }

        public override string Name()
        {
            return "<index>";
        }

        public override string ToStackString()
        {
            return $"{_target.ToStackString()}[{_index}]";
        }

        public override string ToString()
        {
            return $"IndexAccessNode(target={_target.ToString()}, index=\"{_index}\")";
        }
    }
}
