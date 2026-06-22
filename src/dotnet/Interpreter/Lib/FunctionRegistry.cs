using PowwowLang.Ast;
using PowwowLang.Env;
using PowwowLang.Exceptions;
using PowwowLang.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Web.Script.Serialization;

namespace PowwowLang.Lib
{
    public class FunctionRegistry
    {
        private readonly Dictionary<string, List<FunctionDefinition>> _functions;

        public FunctionRegistry()
        {
            _functions = new Dictionary<string, List<FunctionDefinition>>();
            RegisterBuiltInFunctions();
        }

        public bool HasFunction(string name)
        {
            return _functions.ContainsKey(name);
        }

        private void RegisterBuiltInFunctions()
        {
            Register("typeof",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(Box))
                },
                (context, callSite, args) =>
                {
                    var value = args[0];
                    return new Value(new TypeValue(value.TypeOf()));
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var enumerable = (args[0].ValueOf() as ArrayValue).Value();
                    if (enumerable == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires an array argument",
                            context,
                            callSite);
                    }
                    return new Value(new NumberValue(enumerable.Count()));
                });

            Register("length",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value();
                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires a string argument",
                            context,
                            callSite);
                    }
                    return new Value(new NumberValue(str.Length));
                });

            Register("empty",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value();
                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "length function requires a string argument",
                            context,
                            callSite);
                    }
                    return new Value(new BooleanValue(string.IsNullOrEmpty(str)));
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var first = (args[0].ValueOf() as ArrayValue).Value();
                    var second = (args[1].ValueOf() as ArrayValue).Value();

                    if (first == null || second == null)
                    {
                        throw new TemplateEvaluationException(
                            "concat function requires both arguments to be arrays",
                            context,
                            callSite);
                    }

                    // Combine both enumerables into a single list
                    return new Value(new ArrayValue(first.Concat(second)));
                });

            Register("concat",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str1 = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var str2 = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new StringValue(str1 + str2));
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new BooleanValue(str.Contains(searchStr)));
                });

            Register("contains",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    if (args[0].ValueOf() == null)
                    {
                        return new Value(new BooleanValue(false));
                    }

                    var obj = (args[0].ValueOf() as ObjectValue).Value();
                    var propertyName = (args[1].ValueOf() as StringValue).Value();

                    if (string.IsNullOrEmpty(propertyName))
                    {
                        return new Value(new BooleanValue(true));
                    }

                    return new Value(new BooleanValue(obj.ContainsKey(propertyName)));
                });

            Register("startsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new BooleanValue(str.StartsWith(searchStr)));
                });

            Register("endsWith",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new BooleanValue(str.EndsWith(searchStr)));
                });

            Register("toUpper",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new StringValue(str.ToUpper()));
                });

            Register("toLower",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new StringValue(str.ToLower()));
                });

            Register("trim",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new StringValue(str.Trim()));
                });

            Register("indexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new NumberValue(str.IndexOf(searchStr)));
                });

            Register("lastIndexOf",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var searchStr = (args[1].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    return new Value(new NumberValue(str.LastIndexOf(searchStr)));
                });

            Register("substring",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(-1))) // Optional end index
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value()?.ToString() ?? "";
                    var startIndex = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());
                    var endIndex = Convert.ToInt32((args[2].ValueOf() as NumberValue).Value());

                    // If end index is provided, use it; otherwise substring to the end
                    if (endIndex >= 0)
                    {
                        var length = endIndex - startIndex;
                        return new Value(new StringValue(str.Substring(startIndex, length)));
                    }

                    return new Value(new StringValue(str.Substring(startIndex)));
                });

            Register("range",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as NumberValue).Value();
                    var end = (args[1].ValueOf() as NumberValue).Value();
                    var step = (args[2].ValueOf() as NumberValue).Value();

                    if (step == 0)
                    {
                        throw new TemplateEvaluationException(
                            "range function requires a non-zero step value",
                            context,
                            callSite);
                    }

                    var result = new List<Value>();

                    // Handle both positive and negative step values
                    if (step > 0)
                    {
                        for (var value = start + step - step; value < end; value += step)
                        {
                            result.Add(new Value(new NumberValue(value)));
                        }
                    }
                    else
                    {
                        for (var value = start + step - step; value > end; value += step)
                        {
                            result.Add(new Value(new NumberValue(value)));
                        }
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeYear",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeYear function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeYear function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));
                        current = current.AddYears(step);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeMonth",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMonth function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMonth function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;
                    var originalDay = current.Day;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));

                        // Use AddMonths to get the next month
                        var nextMonth = current.AddMonths(step);

                        // Check if original day exists in the new month
                        var daysInNextMonth = DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month);
                        var targetDay = Math.Min(originalDay, daysInNextMonth);

                        // Create a new DateTime with the correct day
                        current = new DateTime(nextMonth.Year, nextMonth.Month, targetDay,
                                              current.Hour, current.Minute, current.Second,
                                              current.Millisecond);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeDay",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeDay function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeDay function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));
                        current = current.AddDays(step);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeHour",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeHour function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeHour function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));
                        current = current.AddHours(step);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeMinute",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMinute function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeMinute function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));
                        current = current.AddMinutes(step);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("rangeSecond",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue), true, new Value(new NumberValue(1)))
                },
                (context, callSite, args) =>
                {
                    var start = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var end = (args[1].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var stepDecimal = (args[2].ValueOf() as NumberValue).Value();
                    var step = (int)Math.Floor(stepDecimal);

                    if (!start.HasValue || !end.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "rangeSecond function requires valid DateTime parameters",
                            context,
                            callSite);
                    }

                    if (step <= 0)
                    {
                        throw new TemplateEvaluationException(
                            "rangeSecond function requires a positive step value",
                            context,
                            callSite);
                    }

                    if (start >= end)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    var result = new List<Value>();
                    var current = start.Value;

                    while (current < end)
                    {
                        result.Add(new Value(new DateTimeValue(new DateTime(current.Ticks))));
                        current = current.AddSeconds(step);
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("filter",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var collection = (args[0].ValueOf() as ArrayValue).Value();
                    var predicate = (args[1].ValueOf() as LambdaValue).Value();

                    if (collection == null || predicate == null)
                    {
                        throw new TemplateEvaluationException(
                            "filter function requires an array and a lambda function",
                            context,
                            callSite);
                    }

                    var result = new List<Value>();
                    foreach (var item in collection)
                    {
                        var predicateResult = predicate(context, callSite, new List<Value> { item });
                        if (predicateResult.ValueOf() is BooleanValue boolResult)
                        {
                            if (boolResult.Value())
                            {
                                result.Add(item);
                            }
                        }
                        else
                        {
                            throw new TemplateEvaluationException(
                                $"Filter predicate should evaluate to a boolean value but found {predicateResult.GetType()}",
                                context,
                                callSite);
                        }
                    }

                    return new Value(new ArrayValue(result));
                });

            Register("at",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var index = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "at function requires an array as first argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (index < 0 || index >= list.Count)
                    {
                        throw new TemplateEvaluationException(
                            $"Index {index} is out of bounds for array of length {list.Count}",
                            context,
                            callSite);
                    }

                    return list[index];
                });

            Register("first",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "first function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot get first element of empty array",
                            context,
                            callSite);
                    }

                    return list[0];
                });

            Register("rest",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "rest function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    return new Value(new ArrayValue(list.Skip(1)));
                });

            Register("last",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "last function requires an array argument",
                            context,
                            callSite);
                    }

                    var list = array.Cast<Value>().ToList();
                    if (list.Count == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot get last element of empty array",
                            context,
                            callSite);
                    }

                    return list[list.Count - 1];
                });

            Register("any",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "any function requires an array argument",
                            context,
                            callSite);
                    }

                    return new Value(new BooleanValue(array.Any()));
                });

            Register("if",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(LazyValue)),
                    new ParameterDefinition(typeof(LazyValue)),
                    new ParameterDefinition(typeof(LazyValue))
                },
                (context, callSite, args) =>
                {
                    var condition = args[0].ValueOf() as LazyValue;
                    var trueBranch = args[1].ValueOf() as LazyValue;
                    var falseBranch = args[2].ValueOf() as LazyValue;

                    var conditionResult = condition.Evaluate();
                    try
                    {
                        conditionResult.ExpectType(Types.ValueType.Boolean);
                    }
                    catch (InnerEvaluationException ex)
                    {
                        throw new TemplateEvaluationException(ex.Message, context, callSite);
                    }

                    return (conditionResult.ValueOf() as BooleanValue).Value() ?
                        trueBranch.Evaluate() :
                        falseBranch.Evaluate();
                },
                isLazilyEvaluated: true);

            Register("join",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var delimiter = (args[1].ValueOf() as StringValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "join function requires an array as first argument",
                            context,
                            callSite);
                    }
                    if (delimiter == null)
                    {
                        throw new TemplateEvaluationException(
                            "join function requires a string as second argument",
                            context,
                            callSite);
                    }

                    return new Value(new StringValue(string.Join(delimiter, array.Select(x => x.ValueOf().Output()))));
                });

            Register("explode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value();
                    var delimiter = (args[1].ValueOf() as StringValue).Value();

                    if (str == null)
                    {
                        throw new TemplateEvaluationException(
                            "explode function requires a string as first argument",
                            context,
                            callSite);
                    }
                    if (delimiter == null)
                    {
                        throw new TemplateEvaluationException(
                            "explode function requires a string as second argument",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(str
                        .Split(new[] { delimiter }, StringSplitOptions.None)
                        .Select(s => new Value(new StringValue(s))).ToList()));
                });

            Register("map",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var mapper = (args[1].ValueOf() as LambdaValue).Value();

                    if (array == null || mapper == null)
                    {
                        throw new TemplateEvaluationException(
                            "map function requires an array and a function",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(array.Select(item => mapper(context, callSite, new List<Value> { item })).ToList()));
                });

            Register("reduce",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue)),
                    new ParameterDefinition(typeof(Box))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var reducer = (args[1].ValueOf() as LambdaValue).Value();
                    var initialValue = args[2];

                    if (array == null || reducer == null)
                    {
                        throw new TemplateEvaluationException(
                            "reduce function requires an array and a function",
                            context,
                            callSite);
                    }

                    return array.Aggregate(initialValue, (acc, curr) =>
                            reducer(context, callSite, new List<Value> { acc, curr }));
                });

            Register("take",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    int count = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "take function requires an array as first argument",
                            context,
                            callSite);
                    }

                    if (count <= 0)
                    {
                        return new Value(new ArrayValue(new List<Value>()));
                    }

                    return new Value(new ArrayValue(array.Take(count)));
                });

            Register("skip",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    int count = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "skip function requires an array as first argument",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(array.Skip(count)));
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array argument",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(array.OrderBy(x => x.ValueOf().Unbox()).ToList()));
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(BooleanValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var ascending = (args[1].ValueOf() as BooleanValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array as first argument",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(ascending ?
                        array.OrderBy(x => x.ValueOf().Unbox()).ToList() :
                        array.OrderByDescending(x => x.ValueOf().Unbox()).ToList()));
                });

            Register("order",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(LambdaValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var comparer = (args[1].ValueOf() as LambdaValue).Value();

                    if (array == null || comparer == null)
                    {
                        throw new TemplateEvaluationException(
                            "order function requires an array and a comparison function",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new ArrayValue(array.OrderBy(x => x, new ValueComparer(context, callSite, comparer)).ToList()));
                    }
                    catch (InnerEvaluationException ex)
                    {
                        throw new TemplateEvaluationException(ex.Message, context, callSite);
                    }
                });

            Register("group",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ArrayValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var array = (args[0].ValueOf() as ArrayValue).Value();
                    var fieldName = (args[1].ValueOf() as StringValue).Value();

                    if (array == null)
                    {
                        throw new TemplateEvaluationException(
                            "group function requires an array as first argument",
                            context,
                            callSite);
                    }
                    if (string.IsNullOrEmpty(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            "group function requires a non-empty string as second argument",
                            context,
                            callSite);
                    }

                    var result = new Dictionary<string, Value>();

                    foreach (var item in array)
                    {
                        // Skip null items
                        if (item == null) continue;

                        // Get the group key from the item
                        string key;
                        if (item.ValueOf().Unbox() is IDictionary<string, Value> dict)
                        {
                            if (!dict.ContainsKey(fieldName))
                            {
                                throw new TemplateEvaluationException(
                                    $"Object does not contain field '{fieldName}'",
                                    context,
                                    callSite);
                            }
                            else
                            {
                                var value = dict[fieldName];
                                if (value.ValueOf() is StringValue str)
                                {
                                    key = str.Value();
                                }
                                else
                                {
                                    throw new TemplateEvaluationException(
                                        $"Cannot group by value of type '{value.GetType()}'",
                                        context,
                                        callSite);
                                }
                            }
                        }
                        else
                        {
                            throw new TemplateEvaluationException(
                                $"group function requires an array of objects to group",
                                context,
                                callSite);
                        }

                        if (key == null)
                        {
                            throw new TemplateEvaluationException(
                                $"Field '{fieldName}' must be present",
                                context,
                                callSite);
                        }

                        // Add item to the appropriate group
                        if (!result.ContainsKey(key))
                        {
                            result[key] = new Value(new ArrayValue(new List<Value>()));
                        }
                        result[key].ValueOf().Unbox().Add(item);
                    }

                    return new Value(new ObjectValue(result));
                });

            Register("get",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var obj = (args[0].ValueOf() as ObjectValue).Value();
                    var fieldName = (args[1].ValueOf() as StringValue).Value();

                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            "get function requires an object as first argument",
                            context,
                            callSite);
                    }
                    if (string.IsNullOrEmpty(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            "get function requires a non-empty string as second argument",
                            context,
                            callSite);
                    }
                    if (!obj.ContainsKey(fieldName))
                    {
                        throw new TemplateEvaluationException(
                            $"Object does not contain field '{fieldName}'",
                            context,
                            callSite);
                    }

                    return obj[fieldName];
                });

            Register("keys",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(ObjectValue))
                },
                (context, callSite, args) =>
                {
                    var obj = (args[0].ValueOf() as ObjectValue).Value();
                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            "keys function requires an object argument",
                            context,
                            callSite);
                    }

                    return new Value(new ArrayValue(obj.Keys.Select(key => new Value(new StringValue(key))).ToList()));
                });

            Register("mod",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number1 = Convert.ToInt32((args[0].ValueOf() as NumberValue).Value());
                    var number2 = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (number2 == 0)
                    {
                        throw new TemplateEvaluationException(
                            "Cannot perform modulo with zero as divisor",
                            context,
                            callSite);
                    }

                    return new Value(new NumberValue(number1 % number2));
                });

            Register("floor",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0].ValueOf() as NumberValue).Value();
                    return new Value(new NumberValue(Math.Floor(number)));
                });

            Register("ceil",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0].ValueOf() as NumberValue).Value();
                    return new Value(new NumberValue(Math.Ceiling(number)));
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0].ValueOf() as NumberValue).Value();
                    return new Value(new NumberValue(Math.Round(number, 0, MidpointRounding.AwayFromZero)));
                });

            Register("round",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0].ValueOf() as NumberValue).Value();
                    var decimals = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (decimals < 0)
                    {
                        throw new TemplateEvaluationException(
                            "Number of decimal places cannot be negative",
                            context,
                            callSite);
                    }

                    return new Value(new NumberValue(Math.Round(number, decimals, MidpointRounding.AwayFromZero)));
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var number = (args[0].ValueOf() as NumberValue).Value();
                    return new Value(new StringValue(number.ToString()));
                });

            Register("string",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(BooleanValue))
                },
                (context, callSite, args) =>
                {
                    var boolean = (args[0].ValueOf() as BooleanValue).Value();
                    return new Value(new StringValue(boolean.ToString().ToLower())); // returning "true" or "false" in lowercase
                });

            Register("number",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value();

                    if (string.IsNullOrEmpty(str))
                    {
                        throw new TemplateEvaluationException(
                            "Cannot convert empty or null string to number",
                            context,
                            callSite);
                    }

                    if (!decimal.TryParse(str, out decimal result))
                    {
                        throw new TemplateEvaluationException(
                            $"Cannot convert string '{str}' to number",
                            context,
                            callSite);
                    }

                    return new Value(new NumberValue(result));
                });

            Register("numeric",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var str = (args[0].ValueOf() as StringValue).Value();

                    if (string.IsNullOrEmpty(str))
                    {
                        return new Value(new BooleanValue(false));
                    }

                    return new Value(new BooleanValue(decimal.TryParse(str, out _)));
                });

            Register("datetime",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var dateStr = (args[0].ValueOf() as StringValue).Value();
                    if (string.IsNullOrEmpty(dateStr))
                    {
                        throw new TemplateEvaluationException(
                            "datetime function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(DateTime.Parse(dateStr)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse date string '{dateStr}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("format",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var format = (args[1].ValueOf() as StringValue).Value();

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "format function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    if (string.IsNullOrEmpty(format))
                    {
                        throw new TemplateEvaluationException(
                            "format function requires a non-empty format string as second argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new StringValue(date.Value.ToString(format)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to format date with format string '{format}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addYears",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var years = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addYears function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddYears(years)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {years} years to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addMonths",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var months = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addMonths function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddMonths(months)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {months} months to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addDays",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var days = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addDays function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddDays(days)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {days} days to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addHours",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var hours = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addHours function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddHours(hours)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {hours} hours to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addMinutes",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var minutes = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addMinutes function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddMinutes(minutes)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {minutes} minutes to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("addSeconds",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(DateTimeValue)),
                    new ParameterDefinition(typeof(NumberValue))
                },
                (context, callSite, args) =>
                {
                    var date = (args[0].ValueOf() as DateTimeValue).Value() as DateTime?;
                    var seconds = Convert.ToInt32((args[1].ValueOf() as NumberValue).Value());

                    if (!date.HasValue)
                    {
                        throw new TemplateEvaluationException(
                            "addSeconds function requires a valid DateTime as first argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        return new Value(new DateTimeValue(date.Value.AddSeconds(seconds)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to add {seconds} seconds to date: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("now",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return new Value(new DateTimeValue(DateTime.Now));
                });

            Register("utcNow",
                new List<ParameterDefinition>(),
                (context, callSite, args) =>
                {
                    return new Value(new DateTimeValue(DateTime.UtcNow));
                });

            Register("uri",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var uriString = (args[0].ValueOf() as StringValue).Value();
                    if (string.IsNullOrEmpty(uriString))
                    {
                        throw new TemplateEvaluationException(
                            "uri function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        var uri = new Uri(uriString);
                        var dict = new Dictionary<string, Value>();
                        dict["AbsolutePath"] = new Value(new StringValue(uri.AbsolutePath));
                        dict["AbsoluteUri"] = new Value(new StringValue(uri.AbsoluteUri));
                        dict["DnsSafeHost"] = new Value(new StringValue(uri.DnsSafeHost));
                        dict["Fragment"] = new Value(new StringValue(uri.Fragment));
                        dict["Host"] = new Value(new StringValue(uri.Host));
                        dict["HostNameType"] = new Value(new StringValue(uri.HostNameType.ToString()));
                        dict["IdnHost"] = new Value(new StringValue(uri.IdnHost));
                        dict["IsAbsoluteUri"] = new Value(new BooleanValue(uri.IsAbsoluteUri));
                        dict["IsDefaultPort"] = new Value(new BooleanValue(uri.IsDefaultPort));
                        dict["IsFile"] = new Value(new BooleanValue(uri.IsFile));
                        dict["IsLoopback"] = new Value(new BooleanValue(uri.IsLoopback));
                        dict["IsUnc"] = new Value(new BooleanValue(uri.IsUnc));
                        dict["LocalPath"] = new Value(new StringValue(uri.LocalPath));
                        dict["OriginalString"] = new Value(new StringValue(uri.OriginalString));
                        dict["PathAndQuery"] = new Value(new StringValue(uri.PathAndQuery));
                        dict["Port"] = new Value(new NumberValue(uri.Port));
                        dict["Query"] = new Value(new StringValue(uri.Query));
                        dict["Scheme"] = new Value(new StringValue(uri.Scheme));
                        dict["Segments"] = new Value(new ArrayValue(uri.Segments.Select(s => new Value(new StringValue(s))).ToList()));
                        dict["UserEscaped"] = new Value(new BooleanValue(uri.UserEscaped));
                        dict["UserInfo"] = new Value(new StringValue(uri.UserInfo));
                        return new Value(new ObjectValue(dict));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse uri string '{uriString}': {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("htmlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var html = (args[0].ValueOf() as StringValue).Value();

                    try
                    {
                        return new Value(new StringValue(WebUtility.HtmlEncode(html)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to encode html: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("htmlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var html = (args[0].ValueOf() as StringValue).Value();

                    try
                    {
                        return new Value(new StringValue(WebUtility.HtmlDecode(html)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to decode html: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("urlEncode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var url = (args[0].ValueOf() as StringValue).Value();

                    try
                    {
                        return new Value(new StringValue(WebUtility.UrlEncode(url)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to encode url: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("urlDecode",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var url = (args[0].ValueOf() as StringValue).Value();

                    try
                    {
                        return new Value(new StringValue(WebUtility.UrlDecode(url)));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to decode url: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("fromJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(StringValue))
                },
                (context, callSite, args) =>
                {
                    var jsonString = (args[0].ValueOf() as StringValue).Value();
                    if (string.IsNullOrEmpty(jsonString))
                    {
                        throw new TemplateEvaluationException("fromJson function requires a non-empty string argument",
                            context,
                            callSite);
                    }

                    try
                    {
                        var parsed = ParseToObject(jsonString);
                        if (parsed == null)
                        {
                            throw new TemplateEvaluationException(
                                $"Failed to deserialize object to JSON: Object must have a value",
                                context,
                                callSite);
                        }
                        return parsed;
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to parse JSON string: {ex.Message}",
                            context,
                            callSite);
                    }
                });

            Register("toJson",
                new List<ParameterDefinition> {
                    new ParameterDefinition(typeof(Box)),
                    new ParameterDefinition(typeof(BooleanValue), true, new Value(new BooleanValue(false)))
                },
                (context, callSite, args) =>
                {
                    var obj = args[0];
                    var formatted = (args[1].ValueOf() as BooleanValue).Value();

                    if (obj == null)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to serialize object to JSON: Must have a value to serialize",
                            context,
                            callSite);
                    }

                    try
                    {
                        var json = obj.JsonSerialize();

                        if (formatted)
                        {
                            return new Value(new StringValue(FormatJson(json)));
                        }

                        return new Value(new StringValue(json));
                    }
                    catch (Exception ex)
                    {
                        throw new TemplateEvaluationException(
                            $"Failed to serialize object to JSON: {ex.Message}",
                            context,
                            callSite);
                    }
                });
        }

        private Value ParseToObject(string jsonString)
        {
            var serializer = new JavaScriptSerializer();
            var deserializedObject = serializer.Deserialize<object>(jsonString);
            return ConvertToDictionary(deserializedObject);
        }

        private Value ConvertToDictionary(object obj)
        {
            if (obj == null)
            {
                return null;
            }

            // Handle dictionary (objects in JSON)
            if (obj is Dictionary<string, object> dict)
            {
                var newDict = new Dictionary<string, Value>();
                foreach (var kvp in dict)
                {
                    var convertedValue = ConvertToDictionary(kvp.Value);
                    if (convertedValue != null)  // Skip null values
                    {
                        newDict[kvp.Key] = convertedValue;
                    }
                }
                return new Value(new ObjectValue(newDict));
            }

            // Handle array
            if (obj is System.Collections.ArrayList arrayList)
            {
                return new Value(new ArrayValue(arrayList.Cast<object>()
                               .Select(item => ConvertToDictionary(item))
                               .Where(item => item != null)
                               .ToList()));  // Filter out null values
            }

            if (obj is object[] array)
            {
                return new Value(new ArrayValue(array.Cast<object>()
                               .Select(item => ConvertToDictionary(item))
                               .Where(item => item != null)
                               .ToList()));  // Filter out null values
            }

            // Handle numbers - convert to decimal where possible
            if (TypeHelper.IsConvertibleToDecimal(obj))
            {
                return new Value(new NumberValue(Convert.ToDecimal(obj)));
            }

            if (obj is string str)
            {
                return new Value(new StringValue(str));
            }

            if (obj is bool boolean)
            {
                return new Value(new BooleanValue(boolean));
            }

            if (obj is DateTime dateTime)
            {
                return new Value(new DateTimeValue(dateTime));
            }

            // Return other primitives as-is (string, bool)
            throw new Exception($"Unable to convert value to known datatype: {obj}");
        }

        private string FormatJson(string json)
        {
            var indent = 0;
            var quoted = false;
            var sb = new StringBuilder();

            for (var i = 0; i < json.Length; i++)
            {
                var ch = json[i];
                switch (ch)
                {
                    case '{':
                    case '[':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent++;
                            sb.Append(new string(' ', indent * 4));
                        }
                        break;
                    case '}':
                    case ']':
                        if (!quoted)
                        {
                            sb.AppendLine();
                            indent--;
                            sb.Append(new string(' ', indent * 4));
                        }
                        sb.Append(ch);
                        break;
                    case '"':
                        sb.Append(ch);
                        bool escaped = false;
                        var index = i;
                        while (index > 0 && json[--index] == '\\')
                            escaped = !escaped;
                        if (!escaped)
                            quoted = !quoted;
                        break;
                    case ',':
                        sb.Append(ch);
                        if (!quoted)
                        {
                            sb.AppendLine();
                            sb.Append(new string(' ', indent * 4));
                        }
                        break;
                    case ':':
                        sb.Append(ch);
                        if (!quoted)
                            sb.Append(" ");
                        break;
                    default:
                        sb.Append(ch);
                        break;
                }
            }

            return sb.ToString();
        }

        public void Register(
            string name,
            List<ParameterDefinition> parameters,
            Func<ExecutionContext, AstNode, List<Value>, Value> implementation,
            bool isLazilyEvaluated = false)
        {
            var definition = new FunctionDefinition(name, parameters, implementation, isLazilyEvaluated);

            if (!_functions.ContainsKey(name))
            {
                _functions[name] = new List<FunctionDefinition>();
            }

            // Check if an identical overload already exists
            var existingOverload = _functions[name].FirstOrDefault(f =>
                f.Parameters.Count == parameters.Count &&
                f.Parameters.Zip(parameters, (a, b) => a.Type == b.Type && a.IsOptional == b.IsOptional).All(x => x));

            if (existingOverload != null)
            {
                throw new InitializationException($"Function '{name}' is already registered with the same parameter types");
            }

            _functions[name].Add(definition);
        }

        public bool LazyFunctionExists(
            string name,
            int argumentCount)
        {
            if (!_functions.TryGetValue(name, out var overloads))
            {
                return false;
            }

            var candidates = overloads.Where(f =>
                argumentCount >= f.RequiredParameterCount &&
                argumentCount <= f.TotalParameterCount &&
                f.IsLazilyEvaluated
            ).ToList();

            if (!candidates.Any())
            {
                return false;
            }

            return true;
        }

        public bool TryGetFunction(
            string name,
            List<Value> arguments,
            out FunctionDefinition matchingFunction,
            out List<Value> effectiveArguments)
        {
            matchingFunction = null;
            effectiveArguments = null;

            if (!_functions.TryGetValue(name, out var overloads))
            {
                return false;
            }

            // Find all overloads with the correct number of parameters
            var candidateOverloads = overloads.Where(f =>
                arguments.Count >= f.RequiredParameterCount &&
                arguments.Count <= f.TotalParameterCount
            ).ToList();

            if (!candidateOverloads.Any())
            {
                return false;
            }

            // Score each overload based on type compatibility
            var scoredOverloads = candidateOverloads.Select(overload => new
            {
                Function = overload,
                Score = ScoreTypeMatch(overload.Parameters, arguments),
                EffectiveArgs = CreateEffectiveArguments(overload.Parameters, arguments)
            })
            .Where(x => x.Score >= 0) // Filter out incompatible matches
            .OrderByDescending(x => x.Score)
            .ToList();

            if (!scoredOverloads.Any())
            {
                return false;
            }

            // If we have multiple matches with the same best score, it's ambiguous
            if (scoredOverloads.Count > 1 && scoredOverloads[0].Score == scoredOverloads[1].Score)
            {
                throw new InnerEvaluationException(
                    $"Ambiguous function call to '{name}'. Multiple overloads match the provided arguments.");
            }

            var bestMatch = scoredOverloads.First();
            matchingFunction = bestMatch.Function;
            effectiveArguments = bestMatch.EffectiveArgs;
            return true;
        }

        private List<Value> CreateEffectiveArguments(
            List<ParameterDefinition> parameters,
            List<Value> providedArgs)
        {
            var effectiveArgs = new List<Value>();

            for (int i = 0; i < parameters.Count; i++)
            {
                if (i < providedArgs.Count)
                {
                    effectiveArgs.Add(providedArgs[i]);
                }
                else if (parameters[i].IsOptional)
                {
                    effectiveArgs.Add(parameters[i].DefaultValue);
                }
                else
                {
                    // This shouldn't happen due to earlier checks, but just in case
                    throw new InnerEvaluationException("Function missing required argument");
                }
            }

            return effectiveArgs;
        }

        private int ScoreTypeMatch(List<ParameterDefinition> parameters, List<Value> arguments)
        {
            if (arguments.Count < parameters.Count(p => !p.IsOptional) ||
                arguments.Count > parameters.Count)
            {
                return -1;
            }

            int totalScore = 0;

            for (int i = 0; i < arguments.Count; i++)
            {
                var arg = arguments[i];
                var paramType = parameters[i].Type;

                // Handle null arguments
                if (arg == null)
                {
                    if (!paramType.IsClass) // Value types can't be null
                    {
                        return -1;
                    }
                    totalScore += 1;
                    continue;
                }

                var argType = arg.ValueOf().GetType();

                // Exact type match
                if (paramType == argType)
                {
                    totalScore += 3;
                    continue;
                }

                // Special handling for IEnumerable
                if (paramType == typeof(ArrayValue))
                {
                    if (arg.ValueOf() is ArrayValue)
                    {
                        totalScore += 2;
                        continue;
                    }
                    return -1;
                }

                // Assignable type match (inheritance)
                if (paramType.IsAssignableFrom(argType))
                {
                    totalScore += 2;
                    continue;
                }

                return -1; // No valid conversion possible
            }

            return totalScore;
        }

        public void ValidateArguments(FunctionDefinition function, List<Value> arguments)
        {
            if (arguments.Count != function.Parameters.Count)
            {
                throw new InnerEvaluationException(
                    $"Function '{function.Name}' expects {function.Parameters.Count} arguments, but got {arguments.Count}");
            }

            for (int i = 0; i < arguments.Count; i++)
            {
                var argument = arguments[i];
                var parameter = function.Parameters[i];

                // Special handling for LazyValue
                if (parameter.Type == typeof(LazyValue) && argument.ValueOf() is LazyValue)
                {
                    continue;
                }

                // Handle null arguments
                if (argument == null)
                {
                    if (!parameter.Type.IsClass)
                    {
                        throw new InnerEvaluationException($"Argument {i + 1} of function '{function.Name}' cannot be null");
                    }
                    continue;
                }

                var argumentType = argument.ValueOf().GetType();

                // Special handling for IEnumerable parameter type
                if (parameter.Type == typeof(ArrayValue))
                {
                    if (!(argument.ValueOf() is ArrayValue))
                    {
                        throw new InnerEvaluationException($"Argument {i + 1} of function '{function.Name}' must be an array");
                    }
                    continue;
                }

                // Check if the argument can be converted to the expected type
                if (!parameter.Type.IsAssignableFrom(argumentType))
                {
                    throw new InnerEvaluationException(
                        $"Argument {i + 1} of function '{function.Name}' must be of type {parameter.Type.Name}");
                }
            }
        }
    }
}
