using PowwowLang.Ast;
using PowwowLang.Exceptions;
using PowwowLang.Types;
using System.Collections.Generic;
using System.Linq;

namespace PowwowLang.Env
{
    public class LambdaExecutionContext : ExecutionContext
    {
        private readonly Dictionary<string, Value> _parameters;
        private readonly ExecutionContext _definitionContext;

        public LambdaExecutionContext(
            ExecutionContext parentContext,
            ExecutionContext definitionContext,
            List<string> parameterNames,
            List<Value> parameterValues,
            AstNode node)
            : base(parentContext.GetData(), parentContext.GetFunctionRegistry(), parentContext, parentContext.MaxDepth, node)
        {
            _parameters = new Dictionary<string, Value>();
            _variables = new Dictionary<string, Value>();
            _definitionContext = definitionContext;

            if (parameterNames.Count > parameterValues.Count)
            {
                var missingCount = parameterNames.Count - parameterValues.Count;
                var missingParams = string.Join(", ", parameterNames.Skip(parameterValues.Count).Take(missingCount));
                throw new InnerEvaluationException($"Not enough parameter values provided. Missing values for: {missingParams}");
            }

            // Map parameter names to values
            for (int i = 0; i < parameterNames.Count; i++)
            {
                _parameters[parameterNames[i]] = parameterValues[i];
            }
        }

        public bool HasParameter(string name)
        {
            return _parameters.ContainsKey(name);
        }

        public dynamic GetParameter(string name)
        {
            return _parameters[name];
        }

        public override void DefineVariable(string name, Value value)
        {
            // Check if already defined as a variable
            if (_variables.ContainsKey(name) || _parentContext.GetFunctionRegistry().HasFunction(name))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable, field, or function");
            }

            // Check if defined as a parameter
            if (_parameters.ContainsKey(name))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with a parameter name");
            }

            if (_parentContext.TryResolveNonShadowableValue(name, out _))
            {
                throw new InnerEvaluationException(
                    $"Cannot define variable '{name}' because it conflicts with an existing variable, field, or function");
            }

            _variables[name] = value;
        }

        public bool TryGetParameterFromAnyContext(string name, out Value value)
        {
            value = null;

            // Check parameters in current context
            if (_parameters.TryGetValue(name, out value))
            {
                return true;
            }

            // Check variables in current context
            if (_variables.TryGetValue(name, out value))
            {
                return true;
            }

            // Check definition context (for closure variables)
            if (_definitionContext is LambdaExecutionContext defLambdaContext &&
                defLambdaContext.TryGetParameterFromAnyContext(name, out value))
            {
                return true;
            }

            // Check caller context (for recursive call stack)
            if (_parentContext is LambdaExecutionContext callerLambdaContext &&
                callerLambdaContext.TryGetParameterFromAnyContext(name, out value))
            {
                return true;
            }

            return false;
        }

        public override bool TryResolveNonShadowableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
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

        public override bool TryResolveMutableValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            // Check if the first part is an iterator
            if (_iteratorValues.ContainsKey(parts[0]))
            {
                throw new InnerEvaluationException($"Iterator variable {path} is not mutable and cannot be reassigned");
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                throw new InnerEvaluationException($"Parameter {path} is not mutable and cannot be reassigned");
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
            else
            {
                Value descendentValue;

                if (_definitionContext.TryResolveMutableValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }
                else if (_parentContext.TryResolveMutableValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }

                return false;
            }

            value = current;
            return true;
        }

        public override bool TryResolveValue(string path, out Value value)
        {
            value = null;
            var parts = path.Split('.');
            Value current = null;

            if (_variables.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else if (_parameters.TryGetValue(parts[0], out current))
            {
                parts = parts.Skip(1).ToArray();
            }
            else
            {
                Value descendentValue;

                if (_definitionContext.TryResolveValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }
                else if (_parentContext.TryResolveValue(path, out descendentValue))
                {
                    value = descendentValue;
                    return true;
                }

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

        public override Value ResolveValue(string path)
        {
            var parts = path.Split('.');

            // First check if it's a parameter
            if (_parameters.ContainsKey(parts[0]))
            {
                Value current = _parameters[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
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
                        current = currentObject[parts[i]];
                    }
                    catch
                    {
                        throw new InnerEvaluationException($"Unknown identifier: {path}");
                    }
                }

                return current;
            }
            else if (_variables.ContainsKey(parts[0]))
            {
                Value current = _variables[parts[0]];

                // Handle nested property access for parameters
                for (int i = 1; i < parts.Length; i++)
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
                        current = currentObject[parts[i]];
                    }
                    catch
                    {
                        throw new InnerEvaluationException($"Unknown identifier: {path}");
                    }
                }

                return current;
            }

            try
            {
                // try closure context first
                return _definitionContext.ResolveValue(path);
            }
            catch
            {
                // If not found in parameters, delegate to parent context
                return _parentContext.ResolveValue(path);
            }
        }
    }
}
