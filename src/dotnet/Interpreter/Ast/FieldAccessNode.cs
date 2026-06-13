using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Lex;
using PowwowLang.Types;

namespace PowwowLang.Ast
{
    public class FieldAccessNode : AstNode
    {
        private readonly AstNode _object;
        private readonly string _fieldName;

        public FieldAccessNode(AstNode obj, string fieldName, SourceLocation location) : base(location)
        {
            _object = obj;
            _fieldName = fieldName;
        }

        public override Value Evaluate(ExecutionContext context)
        {
            var evaluated = _object.Evaluate(context);
            if (evaluated == null)
            {
                throw new TemplateEvaluationException(
                    $"Cannot access field '{_fieldName}' on null object",
                    context,
                    _object);
            }

            if (evaluated.ValueOf() is ObjectValue obj)
            {
                var value = obj.Value();
                if (!value.ContainsKey(_fieldName))
                {
                    throw new TemplateEvaluationException(
                        $"Object does not contain field '{_fieldName}'",
                        context,
                        _object);
                }
                return value[_fieldName];
            }

            throw new TemplateEvaluationException(
                $"Expected type of object but found '{evaluated.TypeOf()}'",
                context,
                _object);
        }

        public override string Name()
        {
            return "<field access>";
        }

        public override string ToStackString()
        {
            return $"{_object.ToStackString()}.{_fieldName}";
        }

        public override string ToString()
        {
            return $"FieldAccessNode(object={_object.ToString()}, fieldName=\"{_fieldName}\")";
        }
    }
}
