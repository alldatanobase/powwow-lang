using PowwowLang.Ast;
using PowwowLang.Exceptions;
using PowwowLang.Lib;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Env
{
    public class ExecutionContext
    {
        protected readonly Value _data;
        protected readonly Dictionary<string, Value> _iteratorValues;
        protected Dictionary<string, Value> _variables;
        private readonly FunctionRegistry _functionRegistry;
        protected readonly ExecutionContext _parentContext;
        protected readonly int _maxDepth;
        private readonly int _currentDepth;
        private readonly AstNode _callSite;

        public int MaxDepth { get { return _maxDepth; } }
        public int CurrentDepth { get { return _currentDepth; } }
        public ExecutionContext Parent { get { return _parentContext; } }
        public AstNode CallSite { get { return _callSite; } }

        public ExecutionContext(
            Value data,
            FunctionRegistry functionRegistry,
            ExecutionContext parentContext,
            int maxDepth,
            AstNode callSite)
        {
            _data = data;
            _iteratorValues = new Dictionary<string, Value>();
            _variables = new Dictionary<string, Value>();
            _functionRegistry = functionRegistry;
            _parentContext = parentContext;
            _maxDepth = maxDepth;
            _callSite = callSite;
            _currentDepth = _parentContext != null ? _parentContext.CurrentDepth + 1 : 0;
            CheckStackDepth();
        }

        public void CheckStackDepth()
        {
            if (_currentDepth >= _maxDepth)
            {
                throw new TemplateEvaluationException(
                    $"Maximum call stack depth {_maxDepth} has been exceeded.",
                    this,
                    _callSite);
            }
        }

        public virtual void DefineVariable(string name, Value value)
        {
            // Check if already defined as a variable, an iterator variable, function registry function, or defined in the data context
            if (_variables.ContainsKey(name) ||
                _iteratorValues.ContainsKey(name) ||
                _functionRegistry.HasFunction(name) ||
                TryResolveValue(name, out _))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable, field, or function");
            }

            // If we get here, the name is safe to use
            _variables[name] = value;
        }

        public virtual void RedefineVariable(string name, Value value)
        {
            var firstPart = name.Split('.')[0];
            if (_functionRegistry.HasFunction(firstPart))
            {
                throw new InnerEvaluationException(
                    $"Cannot mutate variable '{name}' because it is an existing function");
            }

            bool result = TryResolveMutableValue(name, out Value variable);
            if (!result)
            {
                throw new InnerEvaluationException(
                    $"Cannot mutate variable '{name}' because it has not been defined");
            }

            // If we get here, the name is safe to use
            variable.Mutate(value.ValueOf());
        }

        public ExecutionContext CreateIteratorContext(string iteratorName, Value value, AstNode callSite)
        {
            var newContext = new ExecutionContext(_data, _functionRegistry, this, MaxDepth, callSite);

            // Copy variables to new context
            foreach (var variable in _variables)
            {
                newContext._variables.Add(variable.Key, variable.Value);
            }

            // Copy iterators to new context
            newContext._iteratorValues.Add(iteratorName, value);
            foreach (var key in _iteratorValues.Keys)
            {
                newContext._iteratorValues.Add(key, _iteratorValues[key]);
            }

            return newContext;
        }

        public FunctionRegistry GetFunctionRegistry()
        {
            return _functionRegistry;
        }

        public Value GetData()
        {
            return _data;
        }

        protected bool TryGetDataProperty(string propertyName, out Value value)
        {
            value = null;

            if (_data == null || string.IsNullOrEmpty(propertyName))
            {
                return false;
            }

            return _data.ValueOf().Unbox().TryGetValue(propertyName, out value);
        }

        public virtual bool TryResolveNonShadowableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            if (_iteratorValues.ContainsKey(parts[0]))
            {
                current = _iteratorValues[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                current = _data;
            }
            else
            {
                if (_parentContext != null)
                {
                    return _parentContext.TryResolveNonShadowableValue(path, out value);
                }
                return false;
            }

            foreach (var part in parts)
            {
                try
                {
                    IDictionary<string, Value> currentObject = null;
                    if (current.ValueOf() is ObjectValue obj)
                    {
                        currentObject = obj.Value();
                    }
                    else if (current is IDictionary<string, Value> dict)
                    {
                        currentObject = dict;
                    }
                    current = currentObject[part];
                }
                catch
                {
                    return false;
                }
            }

            value = current;
            return true;
        }

        public virtual bool TryResolveMutableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                throw new InnerEvaluationException($"Iterator variable {path} is not mutable and cannot be reassigned");
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                current = _variables[parts[0]];
                parts = parts.Skip(1).ToArray();

                foreach (var part in parts)
                {
                    try
                    {
                        IDictionary<string, Value> currentObject = null;
                        if (current.ValueOf() is ObjectValue obj)
                        {
                            currentObject = obj.Value();
                        }
                        else if (current is IDictionary<string, Value> dict)
                        {
                            currentObject = dict;
                        }
                        current = currentObject[part];
                    }
                    catch
                    {
                        return false;
                    }
                }
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                throw new InnerEvaluationException($"Global variable {path} is not mutable and cannot be reassigned");
            }
            else if (_parentContext != null)
            {
                return _parentContext.TryResolveMutableValue(path, out value);
            }
            else
            {
                return false;
            }

            value = current;
            return true;
        }

        public virtual bool TryResolveValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                current = _iteratorValues[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                current = _variables[parts[0]];
                parts = parts.Skip(1).ToArray();
            }
            else if (TryGetDataProperty(parts[0], out current))
            {
                current = _data;
            }
            else if (_parentContext != null)
            {
                return _parentContext.TryResolveValue(path, out value);
            }
            else
            {
                return false;
            }

            foreach (var part in parts)
            {
                if (!TryGetDataProperty(part, out current))
                {
                    return false;
                }
            }

            value = current;
            return true;
        }

        public virtual Value ResolveValue(string path)
        {
            if (TryResolveValue(path, out Value value))
            {
                return value;
            }

            if (GetFunctionRegistry().HasFunction(path))
            {
                return new Value(new FunctionReferenceValue(path));
            }

            throw new InnerEvaluationException($"Unknown identifier: {path}");
        }
    }
}
