using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using NUnit.Framework;
using System.Web.Script.Serialization;
using Moq;
using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using System.Text.RegularExpressions;
using PowwowLang.Lib;
using PowwowLang.Runtime;
using PowwowLang.Lex;
using PowwowLang.Exceptions;
using PowwowLang.Types;

namespace PowwowLang.Tests
{
    [TestFixture]
    public class InterpreterTests
    {
        private Mock<IOrganizationService> _mockOrgService;
        private DataverseService _dataverseService;
        private Interpreter _interpreter;
        private Lexer _lexer;
        private ExpandoObject _emptyData;

        [SetUp]
        public void Setup()
        {
            _mockOrgService = new Mock<IOrganizationService>();
            _dataverseService = new DataverseService(_mockOrgService.Object);
            _interpreter = new Interpreter(dataverseService: _dataverseService);
            _lexer = new Lexer();
            _emptyData = new ExpandoObject();
        }

        [Test]
        public void ComplexArithmetic()
        {
            // Arrange
            var template = "Result: {{((5 * (var2 + 1.5)) / 2) - 1}}";
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var2", 2.5);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Result: 9.0"));
        }

        [Test]
        public void ComplexBooleanLogic()
        {
            // Arrange
            var template = "{{!(var1 == \"test\" && (var2 > 2 || var3.x < 0))}}";
            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", "test");
            dataDict.Add("var2", 2.5);
            var nested = new ExpandoObject();
            ((IDictionary<string, object>)nested).Add("x", 1.5);
            dataDict.Add("var3", nested);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("false"));
        }

        [Test]
        public void NestedIfStatements()
        {
            // Arrange
            var template = @"
            {{if var1 == ""test""}}
                Outer if true
                {{if var2 * 2 >= 5}}
                    Inner if also true
                {{else}}
                    Inner if false
                {{/if}}
            {{else}}
                Outer if false
            {{/if}}";
            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", "test");
            dataDict.Add("var2", 2.5);

            // Act
            var result = _interpreter.Interpret(template, data).Trim();

            // Assert
            StringAssert.Contains("Outer if true", result);
            StringAssert.Contains("Inner if also true", result);
        }

        [Test]
        public void EachStatementWithComplexCondition()
        {
            // Arrange
            var template = @"
            Users with high scores:
            {{for user in users}}
                {{if user.score > 80 && (user.age >= 18 || user.region == ""EU"")}}
                    {{user.name}} ({{user.score}})
                {{/if}}
            {{/for}}";

            var data = new ExpandoObject();
            var usersList = new List<ExpandoObject>();

            var user1 = new ExpandoObject();
            var user1Dict = (IDictionary<string, object>)user1;
            user1Dict.Add("name", "Alice");
            user1Dict.Add("score", 90);
            user1Dict.Add("age", 18);
            user1Dict.Add("region", "EU");
            usersList.Add(user1);

            var user2 = new ExpandoObject();
            var user2Dict = (IDictionary<string, object>)user2;
            user2Dict.Add("name", "Bob");
            user2Dict.Add("score", 85);
            user2Dict.Add("age", 25);
            user2Dict.Add("region", "EU");
            usersList.Add(user2);

            ((IDictionary<string, object>)data).Add("users", usersList);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("Alice (90)", result);
            StringAssert.Contains("Bob (85)", result);
        }

        [Test]
        public void ComplexMathAndLogicExpressions()
        {
            // Arrange
            var template = @"
            {{ (var1 * 2 + var2) / var3.x }}
            {{ ((var1 > var2) == (var3.x < 5)) && !(var2 == 2.5) }}";

            var data = new ExpandoObject();
            var dataDict = (IDictionary<string, object>)data;
            dataDict.Add("var1", 3);
            dataDict.Add("var2", 2.5);
            var nested = new ExpandoObject();
            ((IDictionary<string, object>)nested).Add("x", 2);
            dataDict.Add("var3", nested);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("4.25", result);
            StringAssert.Contains("false", result);
        }

        [Test]
        public void NestedEachStatementsWithConditionals()
        {
            // Arrange
            var template = @"
            {{for department in departments}}
            Department: {{department.name}}
                {{for employee in department.employees}}
                    {{if employee.salary > department.avgSalary && !employee.isTemp}}
                        {{employee.name}} (Senior)
                    {{elseif employee.salary > department.avgSalary * 0.8}}
                        {{employee.name}} (Mid-level)
                    {{else}}
                        {{employee.name}} (Junior)
                    {{/if}}
                {{/for}}
            {{/for}}";

            var data = new ExpandoObject();
            var deptList = new List<ExpandoObject>();

            var itDept = new ExpandoObject();
            var itDeptDict = (IDictionary<string, object>)itDept;
            itDeptDict.Add("name", "IT");
            itDeptDict.Add("avgSalary", 75000);

            var itEmployees = new List<ExpandoObject>();
            var emp1 = new ExpandoObject();
            ((IDictionary<string, object>)emp1).Add("name", "John");
            ((IDictionary<string, object>)emp1).Add("salary", 80000);
            ((IDictionary<string, object>)emp1).Add("isTemp", false);
            itEmployees.Add(emp1);

            var emp2 = new ExpandoObject();
            ((IDictionary<string, object>)emp2).Add("name", "Jane");
            ((IDictionary<string, object>)emp2).Add("salary", 65000);
            ((IDictionary<string, object>)emp2).Add("isTemp", false);
            itEmployees.Add(emp2);

            itDeptDict.Add("employees", itEmployees);
            deptList.Add(itDept);
            ((IDictionary<string, object>)data).Add("departments", deptList);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("Department: IT", result);
            StringAssert.Contains("John (Senior)", result);
            StringAssert.Contains("Jane (Mid-level)", result);
        }

        [Test]
        public void ArbitrarilyDeepVariablePathReference()
        {
            // Arrange
            var template = "{{var1.foo.bar.baz}}";
            var data = new ExpandoObject();
            var nested1 = new ExpandoObject();
            var nested2 = new ExpandoObject();
            var nested3 = new ExpandoObject();
            ((IDictionary<string, object>)nested3).Add("baz", "hello world");
            ((IDictionary<string, object>)nested2).Add("bar", nested3);
            ((IDictionary<string, object>)nested1).Add("foo", nested2);
            ((IDictionary<string, object>)data).Add("var1", nested1);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [Test]
        public void ArrayLength()
        {
            // Arrange
            var template = "{{length(myArray)}}";
            var data = new ExpandoObject();
            var employees = new List<ExpandoObject>();

            var emp1 = new ExpandoObject();
            ((IDictionary<string, object>)emp1).Add("name", "John");
            ((IDictionary<string, object>)emp1).Add("salary", 80000);
            employees.Add(emp1);

            var emp2 = new ExpandoObject();
            ((IDictionary<string, object>)emp2).Add("name", "Jane");
            ((IDictionary<string, object>)emp2).Add("salary", 65000);
            employees.Add(emp2);

            ((IDictionary<string, object>)data).Add("myArray", employees);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void NestedFunctionCalls()
        {
            // Arrange
            var template = "{{concat(\"hello\", concat(\" \", \"world\"))}}";
            var data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [Test]
        public void StringFunctions()
        {
            // Arrange
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var1", "Hello World");

            // Act & Assert
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"World\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{startsWith(\"Hello World\", \"Hello\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{endsWith(\"Hello World\", \"World\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{toUpper(\"Hello World\")}}", data), Is.EqualTo("HELLO WORLD"));
            Assert.That(_interpreter.Interpret("{{toLower(\"Hello World\")}}", data), Is.EqualTo("hello world"));
            Assert.That(_interpreter.Interpret("{{trim(\"  Hello World  \")}}", data), Is.EqualTo("Hello World"));
            Assert.That(_interpreter.Interpret("{{indexOf(\"Hello World\", \"World\")}}", data), Is.EqualTo("6"));
            Assert.That(_interpreter.Interpret("{{lastIndexOf(\"Hello World World\", \"World\")}}", data), Is.EqualTo("12"));
            Assert.That(_interpreter.Interpret("{{substring(\"Hello World\", 6)}}", data), Is.EqualTo("World"));
            Assert.That(_interpreter.Interpret("{{substring(\"Hello World\", 0, 5)}}", data), Is.EqualTo("Hello"));
            Assert.That(_interpreter.Interpret("{{substring(var1, indexOf(var1, \"W\"))}}", data), Is.EqualTo("World"));
        }

        [Test]
        public void LambdaFunction()
        {
            // Arrange
            var template = @"{{for user in filter(users, (x) => x.age > 17 && length(filter(x.loc, (x) => x.name == ""Atlanta"")) > 0)}}{{user.age}}{{for loc in user.loc}}{{loc.name}}{{/for}}{{/for}}";
            var data = new ExpandoObject();
            var users = new List<ExpandoObject>();

            var emp1 = new ExpandoObject();
            var locs1 = new List<ExpandoObject>();
            var loc1a = new ExpandoObject();
            var loc1b = new ExpandoObject();
            ((IDictionary<string, object>)loc1a).Add("name", "Atlanta");
            ((IDictionary<string, object>)loc1b).Add("name", "Denver");
            locs1.Add(loc1a);
            locs1.Add(loc1b);
            ((IDictionary<string, object>)emp1).Add("age", 17);
            ((IDictionary<string, object>)emp1).Add("loc", locs1);
            users.Add(emp1);

            var emp2 = new ExpandoObject();
            var locs2 = new List<ExpandoObject>();
            var loc2a = new ExpandoObject();
            var loc2b = new ExpandoObject();
            ((IDictionary<string, object>)loc2a).Add("name", "Atlanta");
            ((IDictionary<string, object>)loc2b).Add("name", "Decatur");
            locs2.Add(loc2a);
            locs2.Add(loc2b);
            ((IDictionary<string, object>)emp2).Add("age", 21);
            ((IDictionary<string, object>)emp2).Add("loc", locs2);
            users.Add(emp2);

            ((IDictionary<string, object>)data).Add("users", users);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("21AtlantaDecatur"));
        }

        [Test]
        public void ObjectOperations()
        {
            // Arrange & Act
            var result1 = _interpreter.Interpret("{{obj(name: \"John\", age: 30).name}}", new ExpandoObject());
            var result2 = _interpreter.Interpret("{{obj(person: obj(name: \"John\", age: 30), active: true).person.age}}", new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("John"));
            Assert.That(result2, Is.EqualTo("30"));
        }

        [Test]
        public void DateTimeFunctions()
        {
            // Arrange
            var template = @"
                {{ format(datetime(""2024-01-08 10:10:10""), ""yyyy-MM-dd HH:mm:ss"") }}
                {{ format(addYears(datetime(""2024-01-08 10:10:10""), 1), ""yyyy-MM-dd"") }}
                {{ format(addMonths(datetime(""2024-01-08 10:10:10""), 6), ""yyyy-MM-dd"") }}
                {{ format(addDays(datetime(""2024-01-08 10:10:10""), 10), ""yyyy-MM-dd"") }}";

            // Act
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            StringAssert.Contains("2024-01-08 10:10:10", result);
            StringAssert.Contains("2025-01-08", result);
            StringAssert.Contains("2024-07-08", result);
            StringAssert.Contains("2024-01-18", result);
        }

        [Test]
        public void HtmlEncodeAndDecode()
        {
            // Arrange
            var template1 = @"{{ htmlEncode(""<script>alert('XSS');</script>"") }}";
            var template2 = @"{{ htmlDecode(""&lt;script&gt;alert(&#39;XSS&#39;);&lt;/script&gt;"") }}";

            // Act
            var result1 = _interpreter.Interpret(template1, new ExpandoObject());
            var result2 = _interpreter.Interpret(template2, new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("&lt;script&gt;alert(&#39;XSS&#39;);&lt;/script&gt;"));
            Assert.That(result2, Is.EqualTo("<script>alert('XSS');</script>"));
        }

        [Test]
        public void UrlEncodeAndDecode()
        {
            // Arrange
            var template1 = @"{{ urlEncode(""https://www.example.com/search?query=C# programming&sort=recent"") }}";
            var template2 = @"{{ urlDecode(""https%3A%2F%2Fwww.example.com%2Fsearch%3Fquery%3DC%23+programming%26sort%3Drecent"") }}";

            // Act
            var result1 = _interpreter.Interpret(template1, new ExpandoObject());
            var result2 = _interpreter.Interpret(template2, new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("https%3A%2F%2Fwww.example.com%2Fsearch%3Fquery%3DC%23+programming%26sort%3Drecent"));
            Assert.That(result2, Is.EqualTo("https://www.example.com/search?query=C# programming&sort=recent"));
        }

        [Test]
        public void UriFunctions()
        {
            // Arrange
            var template = @"{{ let uriRes = uri(""https://user:password@www.site.com:80/Home/Index.htm?q1=v1&q2=v2#FragmentName"") }}{{uriRes.AbsolutePath}}
{{uriRes.AbsoluteUri}}
{{uriRes.DnsSafeHost}}
{{uriRes.Fragment}}
{{uriRes.Host}}
{{uriRes.IdnHost}}
{{uriRes.IsAbsoluteUri}}
{{uriRes.IsDefaultPort}}
{{uriRes.IsFile}}
{{uriRes.IsLoopback}}
{{uriRes.IsUnc}}
{{uriRes.LocalPath}}
{{uriRes.OriginalString}}
{{uriRes.PathAndQuery}}
{{uriRes.Port}}
{{uriRes.Query}}
{{uriRes.Scheme}}
{{join(uriRes.Segments, "", "")}}
{{uriRes.UserEscaped}}
{{uriRes.UserInfo}}";

            // Act
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            Assert.AreEqual(@"/Home/Index.htm
https://user:password@www.site.com:80/Home/Index.htm?q1=v1&q2=v2#FragmentName
www.site.com
#FragmentName
www.site.com
www.site.com
true
false
false
false
false
/Home/Index.htm
https://user:password@www.site.com:80/Home/Index.htm?q1=v1&q2=v2#FragmentName
/Home/Index.htm?q1=v1&q2=v2
80
?q1=v1&q2=v2
https
/, Home/, Index.htm
false
user:password", result);
        }

        [Test]
        public void ArrayOperations()
        {
            // Arrange
            var templates = new Dictionary<string, string>
            {
                {"emptyArray", "{{for x in []}}{{x}}, {{/for}}"},
                {"numericArray", "{{for x in [1.1, 2, 0.3]}}{{x}}, {{/for}}"},
                {"stringArray", "{{for x in [\"hello world\", \"foo bar\"]}}{{x}}, {{/for}}"},
                {"objectArray", "{{for x in [obj(name: \"Jeff\"), obj(name: \"Jim\")]}}{{x.name}}, {{/for}}"},
                {"mixedArray", "{{for x in [\"foo\", 2, \"bar\", false, obj(x: 1, y: 2)]}}{{x}}, {{/for}}"},
                {"nestedArray", "{{for x in [1, [2, 3], 4]}}{{x}}, {{/for}}"},
                {"objectPropertyArray", "{{for x in obj(arr: [1, 2, 3]).arr}}{{x}}, {{/for}}"}
            };

            // Act & Assert
            Assert.That(_interpreter.Interpret(templates["emptyArray"], new ExpandoObject()), Is.EqualTo(""));
            Assert.That(_interpreter.Interpret(templates["numericArray"], new ExpandoObject()), Is.EqualTo("1.1, 2, 0.3, "));
            Assert.That(_interpreter.Interpret(templates["stringArray"], new ExpandoObject()), Is.EqualTo("hello world, foo bar, "));
            Assert.That(_interpreter.Interpret(templates["objectArray"], new ExpandoObject()), Is.EqualTo("Jeff, Jim, "));
            Assert.That(_interpreter.Interpret(templates["mixedArray"], new ExpandoObject()), Is.EqualTo("foo, 2, bar, false, {x: 1, y: 2}, "));
            Assert.That(_interpreter.Interpret(templates["nestedArray"], new ExpandoObject()), Is.EqualTo("1, [2, 3], 4, "));
            Assert.That(_interpreter.Interpret(templates["objectPropertyArray"], new ExpandoObject()), Is.EqualTo("1, 2, 3, "));
        }

        [Test]
        public void ContainsFunction()
        {
            // Arrange
            var data = new ExpandoObject();
            var user = new { firstName = "John", lastName = "Doe" };
            dynamic person = new ExpandoObject();
            person.name = "John";
            var dict = new Dictionary<string, object> { ["key"] = "value" };

            ((IDictionary<string, object>)data).Add("user", user);
            ((IDictionary<string, object>)data).Add("person", person);
            ((IDictionary<string, object>)data).Add("dict", dict);

            // Act & Assert
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"World\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{contains(\"Hello World\", \"foo\")}}", data), Is.EqualTo("false"));
            Assert.That(_interpreter.Interpret("{{contains(user, \"firstName\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{contains(user, \"age\")}}", data), Is.EqualTo("false"));
            Assert.That(_interpreter.Interpret("{{contains(person, \"name\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{contains(person, \"age\")}}", data), Is.EqualTo("false"));
            Assert.That(_interpreter.Interpret("{{contains(dict, \"key\")}}", data), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{contains(dict, \"missing\")}}", data), Is.EqualTo("false"));
        }

        [Test]
        public void CollectionOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{at([1, 2, 3], 1)}}", new ExpandoObject()), Is.EqualTo("2"));
            Assert.That(_interpreter.Interpret("{{first([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("1"));
            Assert.That(_interpreter.Interpret("{{last([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{any([1, 2, 3])}}", new ExpandoObject()), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{any([])}}", new ExpandoObject()), Is.EqualTo("false"));
            Assert.That(_interpreter.Interpret("{{join([3.4, false, \"foo\"], \" | \")}}", new ExpandoObject()), Is.EqualTo("3.4 | false | foo"));
            Assert.That(_interpreter.Interpret("{{for x in explode(\"a,b,c\", \",\")}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("a b c "));
            Assert.That(_interpreter.Interpret("{{for x in map([1, 2, 3], (x) => x * 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("2 4 6 "));
            Assert.That(_interpreter.Interpret("{{reduce([1, 2, 3, 4], (acc, curr) => acc + curr, 0)}}", new ExpandoObject()), Is.EqualTo("10"));
            Assert.That(_interpreter.Interpret("{{for x in take([\"foo\", \"bar\", \"baz\"], 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("foo bar "));
            Assert.That(_interpreter.Interpret("{{for x in skip([\"foo\", \"bar\", \"baz\"], 2)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("baz "));
        }

        [Test]
        public void OrderingOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{for x in order([4, 7, 2])}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("2 4 7 "));
            Assert.That(_interpreter.Interpret("{{for x in order([4, 7, 2], false)}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("7 4 2 "));
            Assert.That(_interpreter.Interpret("{{for x in order([\"aaaa\", \"zz\", \"yyy\"], ((a, b) => length(a) - length(b)))}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("zz yyy aaaa "));
        }

        [Test]
        public void ObjectAndGroupOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{get(obj(name: \"gordon\", age: 22), \"name\")}}", new ExpandoObject()), Is.EqualTo("gordon"));
            Assert.That(_interpreter.Interpret("{{for x in keys(obj(name: \"John\", age: 30, city: \"Atlanta\"))}}{{x}} {{/for}}", new ExpandoObject()), Is.EqualTo("name age city "));

            var groupTemplate = "{{for key in keys(group([" +
                "obj(city: \"Atlanta\", name: \"Jeff\", age: 10), " +
                "obj(city: \"Atlanta\", name: \"Jim\", age: 44), " +
                "obj(city: \"Denver\", name: \"Cindy\", age: 23)], " +
                "\"city\"))}}{{key}} {{/for}}";
            Assert.That(_interpreter.Interpret(groupTemplate, new ExpandoObject()), Is.EqualTo("Atlanta Denver "));
        }

        [Test]
        public void NumericOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{mod(7, 3)}}", new ExpandoObject()), Is.EqualTo("1"));
            Assert.That(_interpreter.Interpret("{{floor(3.7)}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{ceil(3.2)}}", new ExpandoObject()), Is.EqualTo("4"));
            Assert.That(_interpreter.Interpret("{{round(3.45)}}", new ExpandoObject()), Is.EqualTo("3"));
            Assert.That(_interpreter.Interpret("{{round(3.45678, 2)}}", new ExpandoObject()), Is.EqualTo("3.46"));
        }

        [Test]
        public void ConversionOperations()
        {
            // Arrange & Act & Assert
            Assert.That(_interpreter.Interpret("{{string(123.45)}}", new ExpandoObject()), Is.EqualTo("123.45"));
            Assert.That(_interpreter.Interpret("{{string(true)}}", new ExpandoObject()), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{number(\"123.45\")}}", new ExpandoObject()), Is.EqualTo("123.45"));
            Assert.That(_interpreter.Interpret("{{numeric(\"123.45\")}}", new ExpandoObject()), Is.EqualTo("true"));
            Assert.That(_interpreter.Interpret("{{numeric(\"abc\")}}", new ExpandoObject()), Is.EqualTo("false"));
        }

        [Test]
        public void LambdaAndHigherOrderFunctions()
        {
            // Arrange
            var templates = new Dictionary<string, string>
            {
                {"square", "{{((x) => x * x)(5)}}"},
                {"regularFunction", "{{concat(\"Hello \", \"World\")}}"},
                {"simpleLambda", "{{((a, b) => a + b)(2, 3)}}"},
                {"higherOrder1", "{{((f) => f(2))((x) => x * 3)}}"},
                {"higherOrder2", "{{((f) => f(\"Hello\", \"World\"))((a, b) => concat(a, b))}}"},
                {"higherOrder3", "{{((f) => ((g) => f(g(\"hello\", \"world\"))))((a) => toUpper(a))((x, y) => concat(x, y))}}"}
            };

            // Act & Assert
            Assert.That(_interpreter.Interpret(templates["square"], new ExpandoObject()), Is.EqualTo("25"));
            Assert.That(_interpreter.Interpret(templates["regularFunction"], new ExpandoObject()), Is.EqualTo("Hello World"));
            Assert.That(_interpreter.Interpret(templates["simpleLambda"], new ExpandoObject()), Is.EqualTo("5"));
            Assert.That(_interpreter.Interpret(templates["higherOrder1"], new ExpandoObject()), Is.EqualTo("6"));
            Assert.That(_interpreter.Interpret(templates["higherOrder2"], new ExpandoObject()), Is.EqualTo("HelloWorld"));
            Assert.That(_interpreter.Interpret(templates["higherOrder3"], new ExpandoObject()), Is.EqualTo("HELLOWORLD"));
        }

        [Test]
        public void IncludeTemplateTest()
        {
            // Arrange
            var registry = new TemplateRegistry();
            registry.RegisterTemplate("header", "<h1>{{title}}</h1>");
            registry.RegisterTemplate("footer", "<footer>{{copyright}}</footer>");

            var interpreter = new Interpreter(registry);

            var template = "{{include header}}<main>{{content}}</main>{{include footer}}";

            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("title", "My Page");
            ((IDictionary<string, object>)data).Add("content", "Hello World");
            ((IDictionary<string, object>)data).Add("copyright", "© 2025");

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("<h1>My Page</h1><main>Hello World</main><footer>© 2025</footer>"));
        }

        [Test]
        public void NestedIncludesTest()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("outer", @"
<div>
    <h1>{{title}}</h1>
    {{include inner}}
</div>");

            registry.RegisterTemplate("inner", @"
<section>
    {{message}}
    {{include footer}}
</section>");

            registry.RegisterTemplate("footer", @"
<footer>
    {{copyright}}
</footer>");

            // Create test data using ExpandoObject
            dynamic data = new ExpandoObject();
            data.title = "Welcome";
            data.message = "Hello World";
            data.copyright = "© 2025";

            var template = "{{include outer}}";

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("<h1>Welcome</h1>", result);
            StringAssert.Contains("Hello World", result);
            StringAssert.Contains("© 2025", result);

            // Verify proper nesting
            Assert.That(result.IndexOf("<div>") < result.IndexOf("<section>"),
                "Outer div should contain section");
            Assert.That(result.IndexOf("<section>") < result.IndexOf("<footer>"),
                "Section should contain footer");
            Assert.That(result.IndexOf("</footer>") < result.IndexOf("</section>"),
                "Footer should close before section");
            Assert.That(result.IndexOf("</section>") < result.IndexOf("</div>"),
                "Section should close before div");
        }

        [Test]
        public void IncludeInsideEach()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("list", @"
<ul>
    {{for item in items}}
        {{include listitem}}
    {{/for}}
</ul>");

            registry.RegisterTemplate("listitem", @"
<li class=""item"">
    <span class=""name"">{{item.name}}</span>
    <span class=""value"">{{item.value}}</span>
</li>");

            // Create test data using ExpandoObject and List<dynamic>
            var items = new List<dynamic>();

            dynamic item1 = new ExpandoObject();
            item1.name = "First";
            item1.value = 100;
            items.Add(item1);

            dynamic item2 = new ExpandoObject();
            item2.name = "Second";
            item2.value = 200;
            items.Add(item2);

            dynamic data = new ExpandoObject();
            data.items = items;

            var template = "{{include list}}";

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            StringAssert.Contains("<span class=\"name\">First</span>", result);
            StringAssert.Contains("<span class=\"value\">100</span>", result);
            StringAssert.Contains("<span class=\"name\">Second</span>", result);
            StringAssert.Contains("<span class=\"value\">200</span>", result);

            // Verify order and nesting
            Assert.That(result.IndexOf("First") < result.IndexOf("Second"),
                "First item should appear before second item");
            Assert.That(result.IndexOf("<ul>") < result.IndexOf("<li"),
                "List items should be inside ul");
            Assert.That(result.IndexOf("</li>") < result.IndexOf("</ul>"),
                "List items should close before ul closes");
        }

        [Test]
        public void IncludeInsideIf_RendersCorrectly()
        {
            // Arrange
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            // Register templates
            registry.RegisterTemplate("profile", @"
<div class=""profile"">
    <h2>{{username}}</h2>
    {{if premium}}
        {{include premiumcontent}}
    {{else}}
        {{include basiccontent}}
    {{/if}}
</div>");

            registry.RegisterTemplate("premiumcontent", @"
<div class=""premium"">
    <span class=""badge"">Premium User</span>
    <p>{{specialMessage}}</p>
</div>");

            registry.RegisterTemplate("basiccontent", @"
<div class=""basic"">
    <p>Upgrade to premium!</p>
</div>");

            // Create test data using ExpandoObject
            dynamic premiumUser = new ExpandoObject();
            premiumUser.username = "JohnDoe";
            premiumUser.premium = true;
            premiumUser.specialMessage = "Welcome to premium!";

            dynamic basicUser = new ExpandoObject();
            basicUser.username = "JaneSmith";
            basicUser.premium = false;

            // Test with premium user
            var premiumResult = interpreter.Interpret("{{include profile}}", premiumUser);

            // Test with basic user
            var basicResult = interpreter.Interpret("{{include profile}}", basicUser);

            // Assert premium user result
            StringAssert.Contains("<h2>JohnDoe</h2>", premiumResult);
            StringAssert.Contains("<span class=\"badge\">Premium User</span>", premiumResult);
            StringAssert.Contains("Welcome to premium!", premiumResult);
            StringAssert.DoesNotContain("Upgrade to premium!", premiumResult);

            // Assert basic user result
            StringAssert.Contains("<h2>JaneSmith</h2>", basicResult);
            StringAssert.Contains("Upgrade to premium!", basicResult);
            StringAssert.DoesNotContain("Premium User", basicResult);
            StringAssert.DoesNotContain("Welcome to premium!", basicResult);

            // Verify proper nesting
            Assert.That(premiumResult.IndexOf("<div class=\"profile\">") < premiumResult.IndexOf("<div class=\"premium\">"),
                "Premium content should be nested inside profile div");
            Assert.That(basicResult.IndexOf("<div class=\"profile\">") < basicResult.IndexOf("<div class=\"basic\">"),
                "Basic content should be nested inside profile div");
        }

        [Test]
        public void IncludeUsingVariables()
        {
            var registry = new TemplateRegistry();
            var interpreter = new Interpreter(registry);

            registry.RegisterTemplate("inner", @"<div>{{outerVar}}</div>");
            var result = interpreter.Interpret(@"{{ let outerVar = 5 }}<div>{{ include inner }}</div>", _emptyData);

            Assert.That("<div><div>5</div></div>", Is.EqualTo(result));
        }

        [Test]
        public void BasicVariableAssignment()
        {
            // Template that assigns a value and then outputs it
            var template = "{{let x = 2}}{{x}}";

            // Create empty data context
            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void VariableExpressionAssignment()
        {
            // Template that uses a variable in an expression to assign to another variable
            var template = "{{let x = 2}}{{let y = x + 12}}{{y}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("14"));
        }

        [Test]
        public void LambdaFunctionAssignment()
        {
            // Template that defines a lambda function, assigns variables, and uses them
            var template = @"
                {{let f = (a, b) => a * b}}
                {{let x = 2}}
                {{let y = 14}}
                {{let z = f(x, y)}}
                {{z}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("28"));
        }

        [Test]
        public void VariableRedefinitionThrowsException()
        {
            var template = "{{let x = 2}}{{let x = 3}}";
            dynamic data = new ExpandoObject();

            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Should throw exception when trying to redefine variable");
        }

        [Test]
        public void VariableConflictWithDataContextThrowsException()
        {
            var template = "{{let existingField = 2}}";

            dynamic data = new ExpandoObject();
            var dict = (IDictionary<string, object>)data;
            dict["existingField"] = 1;

            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Should throw exception when variable name conflicts with data context");
        }

        [Test]
        public void VariableConflictWithIteratorThrowsException()
        {
            var template = @"
                {{for item in [1,2,3]}}
                    {{let item = 2}}
                {{/for}}";

            dynamic data = new ExpandoObject();

            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Should throw exception when variable name conflicts with iterator");
        }

        [Test]
        public void VariableScopeInNestedStructures()
        {
            var template = @"{{let x = 1}}{{for item in [1,2]}}{{let y = x + item}}{{y}}{{/for}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("23")); // Should output "2" then "3"
        }

        [Test]
        public void ComplexExpressionAssignment()
        {
            var template = @"
                {{let x = 10}}
                {{let y = 5}}
                {{let z = (x * y) + (x / y)}}
                {{z}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("52")); // (10 * 5) + (10 / 5) = 50 + 2 = 52
        }

        [Test]
        public void LambdaAccessToVariables()
        {
            // Template that defines a variable and then uses it within a lambda
            var template = @"{{let x = 2}}{{((a) => a * x)(3)}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("6")); // 3 * 2 = 6
        }

        [Test]
        public void LambdaClosures()
        {
            // Template that defines a variable and then uses it within a lambda
            var template = @"{{let x = 2}}{{let f = ((a) => (() => a * x))}}{{f(3)()}}";

            dynamic data = new ExpandoObject();

            var result = _interpreter.Interpret(template, data).Trim();
            Assert.That(result, Is.EqualTo("6")); // 3 * 2 = 6
        }

        [Test]
        public void IteratorVariableNameConflicts()
        {
            // Test case 1: Iterator conflicts with existing variable
            var template1 = @"
            {{let item = 5}}
            {{for item in [1,2,3]}}
                {{item}}
            {{/for}}";

            dynamic data = new ExpandoObject();

            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template1, data),
                "Should throw exception when iterator name conflicts with existing variable");

            // Test case 2: Variable conflicts with existing iterator
            var template2 = @"
            {{for item in [1,2,3]}}
                {{let item = 5}}
                {{item}}
            {{/for}}";

            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template2, data),
                "Should throw exception when variable name conflicts with existing iterator");

            // Test case 3: Verify proper nested loops with different iterator names work
            var template3 = @"{{for i in [1,2]}}{{for j in [3,4]}}{{i * j}}{{/for}}{{/for}}";

            var result = _interpreter.Interpret(template3, data).Trim();
            Assert.That(result, Is.EqualTo("3468")); // Should output: 3,4,6,8
        }

        [Test]
        public void SingleLineComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "Hello {{* This is a comment *}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(8));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.DirectiveStart));
            Assert.That(tokens[2].Value, Is.EqualTo("{{"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.CommentStart));
            Assert.That(tokens[3].Value, Is.EqualTo("*"));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.CommentEnd));
            Assert.That(tokens[4].Value, Is.EqualTo("*"));
            Assert.That(tokens[5].Type, Is.EqualTo(TokenType.DirectiveEnd));
            Assert.That(tokens[5].Value, Is.EqualTo("}}"));
            Assert.That(tokens[6].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[6].Value, Is.EqualTo(" "));
            Assert.That(tokens[7].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[7].Value, Is.EqualTo("World"));
        }

        [Test]
        public void MultiLineComment_ShouldBeIgnored()
        {
            // Arrange
            var template = @"Hello {{* 
                This is a
                multi-line comment
            *}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(8));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.DirectiveStart));
            Assert.That(tokens[2].Value, Is.EqualTo("{{"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.CommentStart));
            Assert.That(tokens[3].Value, Is.EqualTo("*"));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.CommentEnd));
            Assert.That(tokens[4].Value, Is.EqualTo("*"));
            Assert.That(tokens[5].Type, Is.EqualTo(TokenType.DirectiveEnd));
            Assert.That(tokens[5].Value, Is.EqualTo("}}"));
            Assert.That(tokens[6].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[6].Value, Is.EqualTo(" "));
            Assert.That(tokens[7].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[7].Value, Is.EqualTo("World"));
        }

        [Test]
        public void CommentBetweenDirectives_ShouldBeIgnored()
        {
            // Arrange
            var template = "{{ value1 }}{{* comment *}}{{ value2 }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(10));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.CommentStart,
                TokenType.CommentEnd,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void NestedDirectiveLikeSyntaxInComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "{{* This {{ should }} be ignored *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(7));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.CommentStart,
                TokenType.CommentEnd,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void UnterminatedComment_ShouldThrowException()
        {
            // Arrange
            var template = "Hello {{* This comment is not terminated";

            // Act & Assert
            var ex = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            Assert.That(ex.Message, Is.EqualTo("Error at line 1, column 41: Unterminated comment"));
        }

        [Test]
        public void MultipleConsecutiveComments_ShouldAllBeIgnored()
        {
            // Arrange
            var template = "{{* Comment 1 *}}{{* Comment 2 *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(11));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.CommentStart,
                TokenType.CommentEnd,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.CommentStart,
                TokenType.CommentEnd,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void CommentWithAsterisksInContent_ShouldBeParsedCorrectly()
        {
            // Arrange
            var template = "{{* This * has * asterisks * in * it *}}{{ value }}";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(7));
            Assert.That(tokens.Select(t => t.Type).ToList(), Is.EqualTo(new[] {
                TokenType.DirectiveStart,
                TokenType.CommentStart,
                TokenType.CommentEnd,
                TokenType.DirectiveEnd,
                TokenType.DirectiveStart,
                TokenType.Variable,
                TokenType.DirectiveEnd
            }));
        }

        [Test]
        public void EmptyComment_ShouldBeIgnored()
        {
            // Arrange
            var template = "Hello {{**}} World";

            // Act
            var tokens = _lexer.Tokenize(template);

            // Assert
            Assert.That(tokens.Count, Is.EqualTo(8));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.DirectiveStart));
            Assert.That(tokens[2].Value, Is.EqualTo("{{"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.CommentStart));
            Assert.That(tokens[3].Value, Is.EqualTo("*"));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.CommentEnd));
            Assert.That(tokens[4].Value, Is.EqualTo("*"));
            Assert.That(tokens[5].Type, Is.EqualTo(TokenType.DirectiveEnd));
            Assert.That(tokens[5].Value, Is.EqualTo("}}"));
            Assert.That(tokens[6].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[6].Value, Is.EqualTo(" "));
            Assert.That(tokens[7].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[7].Value, Is.EqualTo("World"));
        }

        [Test]
        public void StringQuotes_CanBeEscaped()
        {
            // Arrange
            var template = "{{let x = \"\\\"hello world\\\"\"}}{{x}}";

            // Act
            dynamic data = new ExpandoObject();

            // Assert
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("\"hello world\""));
        }

        [Test]
        public void SingleQuote()
        {
            var tokens = _lexer.Tokenize("{{\"Hello 'World'\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello 'World'"));
        }

        [Test]
        public void DoubleQuoteEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello \\\"World\\\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello \"World\""));
        }

        [Test]
        public void BackslashEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello \\\\World\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello \\World"));
        }

        [Test]
        public void NewlineEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\nWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\nWorld"));
        }

        [Test]
        public void CarriageReturnEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\rWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\rWorld"));
        }

        [Test]
        public void TabEscape()
        {
            var tokens = _lexer.Tokenize("{{\"Hello\\tWorld\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("Hello\tWorld"));
        }

        [Test]
        public void MultipleEscapeSequences()
        {
            var tokens = _lexer.Tokenize("{{\"\\\"Hello\\n\\tWorld\\\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("\"Hello\n\tWorld\""));
        }

        [Test]
        public void InvalidEscapeSequence()
        {
            Assert.Throws<TemplateParsingException>(() =>
                _lexer.Tokenize("{{\"Hello\\xWorld\"}}")
            );
        }

        [Test]
        public void UnterminatedString()
        {
            Assert.Throws<TemplateParsingException>(() =>
                _lexer.Tokenize("{{\"Hello World")
            );
        }

        [Test]
        public void EscapeAtEndOfString()
        {
            Assert.Throws<TemplateParsingException>(() =>
                _lexer.Tokenize("{{\"Hello World\\")
            );
        }

        [Test]
        public void EmptyString()
        {
            var tokens = _lexer.Tokenize("{{\"\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo(""));
        }

        [Test]
        public void StringWithOnlyEscapeSequence()
        {
            var tokens = _lexer.Tokenize("{{\"\\n\"}}");
            var stringToken = tokens.FirstOrDefault(t => t.Type == TokenType.String);

            Assert.That(stringToken, Is.Not.Null);
            Assert.That(stringToken.Value, Is.EqualTo("\n"));
        }

        [Test]
        public void Literal_BasicDirective_ShouldReturnExactContent()
        {
            var template = "{{ literal }}Hello World{{ /literal }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("Hello World"));
        }

        [Test]
        public void Literal_WithTemplateDirectives_ShouldReturnUnprocessedContent()
        {
            var template = "{{literal}}{{if x}}True{{else}}False{{/if}}{{/literal}}";
            dynamic data = new ExpandoObject();
            data.x = true;

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{{if x}}True{{else}}False{{/if}}"));
        }

        [Test]
        public void Literal_WithVariables_ShouldReturnUnprocessedVariables()
        {
            var template = "{{literal}}{{name}} is {{age}} years old{{/literal}}";
            dynamic data = new ExpandoObject();
            data.name = "John";
            data.age = 30;

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{{name}} is {{age}} years old"));
        }

        [Test]
        public void Literal_WithMixedContent_ShouldProcessOutsideAndPreserveInside()
        {
            var template = "Name: {{name}} {{literal}}Age: {{age}}{{/literal}} Location: {{location}}";
            dynamic data = new ExpandoObject();
            data.name = "John";
            data.age = 30;
            data.location = "New York";

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("Name: John Age: {{age}} Location: New York"));
        }

        [Test]
        public void Literal_WithNestedDirectives_ShouldPreserveAllContent()
        {
            var template = "{{literal}}{{for item in items}}{{item.name}}{{if item.active}}Active{{/if}}{{/for}}{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("{{for item in items}}{{item.name}}{{if item.active}}Active{{/if}}{{/for}}"));
        }

        [Test]
        public void Literal_WithSpecialCharacters_ShouldPreserveFormatting()
        {
            var template = "{{literal}}Line 1\nLine 2\tTabbed{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("Line 1\nLine 2\tTabbed"));
        }

        [Test]
        public void Literal_Empty_ShouldReturnEmptyString()
        {
            var template = "{{literal}}{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo(""));
        }

        [Test]
        public void Literal_WithExpressions_ShouldPreserveExpressions()
        {
            var template = "{{literal}}{{x + y * z}}{{/literal}}";
            dynamic data = new ExpandoObject();
            data.x = 1;
            data.y = 2;
            data.z = 3;

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{{x + y * z}}"));
        }

        [Test]
        public void Literal_WithMultipleDirectives_ShouldProcessCorrectly()
        {
            var template = "{{literal}}First{{/literal}} Middle {{literal}}Last{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("First Middle Last"));
        }

        [Test]
        public void Literal_WithNestedLiterals_ShouldHandleNestingCorrectly()
        {
            var template = "{{literal}}Outer {{literal}}Inner{{/literal}} Content{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("Outer {{literal}}Inner{{/literal}} Content"));
        }

        [Test]
        public void Literal_UnterminatedDirective_ShouldThrowException()
        {
            var template = "{{literal}}Unterminated content";
            Assert.Throws<TemplateParsingException>(() =>
                _interpreter.Interpret(template, new ExpandoObject())
            );
        }

        [Test]
        public void Literal_MismatchedDirectives_ShouldThrowException()
        {
            var template = "{{literal}}{{/if}}";
            Assert.Throws<TemplateParsingException>(() =>
                _interpreter.Interpret(template, new ExpandoObject())
            );
        }

        [Test]
        public void Literal_WithFunctionCalls_ShouldPreserveFunctionCalls()
        {
            var template = "{{literal}}{{length(items)}} {{uppercase(name)}}{{/literal}}";
            dynamic data = new ExpandoObject();
            data.items = new[] { 1, 2, 3 };
            data.name = "john";

            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{{length(items)}} {{uppercase(name)}}"));
        }

        [Test]
        public void Literal_WithMultipleLines_ShouldPreserveLineBreaks()
        {
            var template = @"{{literal}}
Line 1
Line 2
Line 3
{{/literal}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("\r\nLine 1\r\nLine 2\r\nLine 3\r\n"));
        }

        [Test]
        public void BasicCapture_SimpleText_CapturesCorrectly()
        {
            // Arrange
            var template = "{{capture x}}Hello World{{/capture}}Captured: {{x}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Captured: Hello World"));
        }

        [Test]
        public void Capture_WithExpression_CapturesEvaluatedResult()
        {
            // Arrange
            var template = "{{capture x}}{{2 + 3}}{{/capture}}Result: {{x}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Result: 5"));
        }

        [Test]
        public void Capture_WithForLoop_CapturesIterationResults()
        {
            // Arrange
            var template = "{{capture x}}{{for i in [1, 2, 3]}}{{i}},{{/for}}{{/capture}}Numbers: {{x}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Numbers: 1,2,3,"));
        }

        [Test]
        public void NestedCaptures_CaptureCorrectly()
        {
            // Arrange
            var template = @"{{capture outer}}{{capture inner}}nested content{{/capture}}Outer with {{inner}}{{/capture}}Result: {{outer}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data).Trim();

            // Assert
            Assert.That(result, Is.EqualTo("Result: Outer with nested content"));
        }

        [Test]
        public void Capture_WithConditional_CapturesCorrectBranch()
        {
            // Arrange
            var template = @"{{capture result}}{{if true}}true branch{{else}}false branch{{/if}}{{/capture}}Got: {{result}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data).Trim();

            // Assert
            Assert.That(result, Is.EqualTo("Got: true branch"));
        }

        [Test]
        public void Capture_WithData_CanAccessContextData()
        {
            // Arrange
            var template = "{{capture x}}Name: {{user.name}}{{/capture}}Captured: {{x}}";
            dynamic data = new ExpandoObject();
            dynamic name = new ExpandoObject();
            ((IDictionary<string, object>)name).Add("name", "John");
            ((IDictionary<string, object>)data).Add("user", name);

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Captured: Name: John"));
        }

        [Test]
        public void Capture_WithSameVariableName_ThrowsError()
        {
            // Arrange
            var template = @"
                {{capture x}}first{{/capture}}
                {{capture x}}second{{/capture}}
                Value: {{x}}";
            dynamic data = new ExpandoObject();

            // Act Assert
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data));
        }

        [Test]
        public void Capture_WithWhitespace_PreservesWhitespace()
        {
            // Arrange
            var template = "{{capture x}}  spaced  content  {{/capture}}[{{x}}]";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("[  spaced  content  ]"));
        }

        [Test]
        public void Capture_EmptyContent_CapturesEmptyString()
        {
            // Arrange
            var template = "{{capture x}}{{/capture}}Empty:[{{x}}]";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Empty:[]"));
        }

        [Test]
        public void MissingEndCapture_ThrowsException()
        {
            // Arrange
            var template = "{{capture x}}unclosed";
            dynamic data = new ExpandoObject();

            // Act & Assert
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, data));
        }

        [Test]
        public void Capture_WithoutVariableName_ThrowsException()
        {
            // Arrange
            var template = "{{capture}}content{{/capture}}";
            dynamic data = new ExpandoObject();

            // Act & Assert
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, data));
        }

        [Test]
        public void EndCaptureWithoutStart_ThrowsException()
        {
            // Arrange
            var template = "{{/for}}";
            dynamic data = new ExpandoObject();

            // Act & Assert
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, data));
        }

        [Test]
        public void Capture_VariableScope_RespectsScopingRules()
        {
            // Arrange
            var template = @"{{capture outer}}{{for i in [1]}}{{capture inner}}Inner Content{{/capture}}In loop: {{inner}}{{/for}}{{/capture}}After loop: {{outer}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data).Trim();

            // Assert
            Assert.That(result, Is.EqualTo("After loop: In loop: Inner Content"));
        }

        [Test]
        public void Capture_WithFunctionCall_CapturesFunctionResult()
        {
            // Arrange
            _interpreter.RegisterFunction(
                "greet",
                new List<ParameterDefinition> { new ParameterDefinition(typeof(StringValue)) },
                (context, callSite, args) => new Value(new StringValue($"Hello, {(args[0].ValueOf() as StringValue).Value()}!")));

            var template = "{{capture x}}{{greet(\"World\")}}{{/capture}}Message: {{x}}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("Message: Hello, World!"));
        }

        [Test]
        public void FromJson_SimpleObject_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let person = fromJson(""{\""name\"":\""John\"",\""age\"":30}"") }}
                {{ person.name }},{{ person.age }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("John,30"));
        }

        [Test]
        public void FromJson_NestedObject_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let data = fromJson(""{
                    \""person\"": {
                        \""name\"": \""John\"",
                        \""address\"": {
                            \""street\"": \""123 Main St\"",
                            \""city\"": \""Boston\""
                        }
                    }
                }"") }}
                {{ data.person.name }},{{ data.person.address.street }},{{ data.person.address.city }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("John,123 Main St,Boston"));
        }

        [Test]
        public void FromJson_Array_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let numbers = fromJson(""[1, 2, 3, 4, 5]"") }}
                {{ for num in numbers }}{{ num }}{{ if num != 5 }},{{ /if }}{{ /for }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("1,2,3,4,5"));
        }

        [Test]
        public void FromJson_ObjectWithNullValues_SkipsNullProperties()
        {
            // Arrange
            var template = @"
                {{ let person = fromJson(""{
                    \""name\"": \""John\"",
                    \""email\"": null,
                    \""age\"": 30,
                    \""address\"": null
                }"") }}
{{ if contains(person, ""name"") }}has_name{{ /if }}
{{ if contains(person, ""age"") }}has_age{{ /if }}
{{ if contains(person, ""email"") }}has_email{{ /if }}
{{ if contains(person, ""address"") }}has_address{{ /if }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("has_name\r\nhas_age"));
        }

        [Test]
        public void FromJson_ArrayWithNullValues_FiltersOutNulls()
        {
            // Arrange
            var template = @"
                {{ let numbers = fromJson(""[1, null, 2, null, 3]"") }}
                {{ length(numbers) }},{{ for num in numbers }}{{ num }}{{ if num != 3 }},{{ /if }}{{ /for }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("3,1,2,3"));
        }

        [Test]
        public void FromJson_ArrayOfObjects_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let people = fromJson(""[
                    {\""name\"": \""John\"", \""age\"": 30},
                    {\""name\"": \""Jane\"", \""age\"": 25}
                ]"") }}
                {{ for person in people }}{{ person.name }}:{{ person.age }}{{ if person != last(people) }},{{ /if }}{{ /for }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("John:30,Jane:25"));
        }

        [Test]
        public void FromJson_BooleanValues_ParseCorrectly()
        {
            // Arrange
            var template = @"
                {{ let flags = fromJson(""{
                    \""isActive\"": true,
                    \""isDeleted\"": false
                }"") }}
                {{ if flags.isActive }}active{{ /if }}
                {{ if flags.isDeleted }}deleted{{ /if }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("active"));
        }

        [Test]
        public void FromJson_NumericOperations_WorkWithParsedNumbers()
        {
            // Arrange
            var template = @"
                {{ let data = fromJson(""{
                    \""price\"": 10.5,
                    \""quantity\"": 3,
                    \""discount\"": 2.5
                }"") }}
                {{ floor(data.price * data.quantity - data.discount) }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("29"));
        }

        [Test]
        public void FromJson_EmptyObject_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let obj = fromJson(""{}"") }}
                {{ length(keys(obj)) }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("0"));
        }

        [Test]
        public void FromJson_EmptyArray_ParsesCorrectly()
        {
            // Arrange
            var template = @"
                {{ let arr = fromJson(""[]"") }}
                {{ length(arr) }}";

            // Act
            var result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result.Trim(), Is.EqualTo("0"));
        }

        [Test]
        public void FromJson_InvalidJson_ThrowsException()
        {
            // Arrange
            var template = @"{{ fromJson(""{invalid json}"") }}";

            // Act & Assert
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, _emptyData));
        }

        [Test]
        public void FromJson_NullInput_ThrowsException()
        {
            // Arrange
            var template = "{{ fromJson(null) }}";

            // Act & Assert
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, _emptyData));
        }

        [Test]
        public void ToJson_SimpleObject_ReturnsCorrectJson()
        {
            dynamic person = new ExpandoObject();
            person.name = "John";
            person.age = 30;

            dynamic data = new ExpandoObject();
            data.person = person;

            var template = "{{ toJson(person) }}";
            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
        }

        [Test]
        public void ToJson_Obj_ReturnsCorrectJson()
        {
            var template = "{{ toJson(obj(name: \"John\", age: 30)) }}";
            Assert.That(_interpreter.Interpret(template, new ExpandoObject()), Is.EqualTo("{\"name\":\"John\",\"age\":30}"));
        }

        [Test]
        public void ToJson_Func_ReturnsCorrectJson()
        {
            var template = "{{ toJson(obj(name: \"John\", age: 30, fn: (x, y) => 2 * x)) }}";
            Assert.That(_interpreter.Interpret(template, new ExpandoObject()), Is.EqualTo("{\"name\":\"John\",\"age\":30,\"fn\":\"func<lambda(x, y)>\"}"));
        }

        [Test]
        public void ToJson_Array_ReturnsCorrectJson()
        {
            var template = "{{ toJson([1, 2, 3]) }}";
            var result = _interpreter.Interpret(template, null);
            Assert.That(result, Is.EqualTo("[1,2,3]"));
        }

        [Test]
        public void ToJson_NestedObject_ReturnsCorrectJson()
        {
            dynamic address = new ExpandoObject();
            address.city = "New York";
            address.zip = "10001";

            dynamic person = new ExpandoObject();
            person.name = "John";
            person.address = address;

            dynamic data = new ExpandoObject();
            data.person = person;

            var template = "{{ toJson(person) }}";
            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("{\"name\":\"John\",\"address\":{\"city\":\"New York\",\"zip\":\"10001\"}}"));
        }

        [Test]
        public void ToJson_ArrayOfObjects_ReturnsCorrectJson()
        {
            dynamic person1 = new ExpandoObject();
            person1.name = "John";

            dynamic person2 = new ExpandoObject();
            person2.name = "Jane";

            var people = new[] { person1, person2 };

            dynamic data = new ExpandoObject();
            data.people = people;

            var template = "{{ toJson(people) }}";
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("[{\"name\":\"John\"},{\"name\":\"Jane\"}]"));
        }

        [Test]
        public void ToJson_DateTimeValue_ReturnsIsoFormattedString()
        {
            var date = new DateTime(2023, 12, 25, 12, 0, 0, DateTimeKind.Utc);
            var template = "{{ toJson(date) }}";
            dynamic data = new ExpandoObject();
            data.date = date;
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("\"2023-12-25T12:00:00.0000000Z\""));
        }

        [Test]
        public void ToJson_UriValue_ReturnsCorrectJson()
        {
            var uri = new Uri("https://example.com");
            var template = "{{ toJson(uri.AbsoluteUri) }}";
            dynamic data = new ExpandoObject();
            data.uri = uri;
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("\"https://example.com/\""));
        }

        [Test]
        public void ToJson_DecimalValue_ReturnsCorrectJson()
        {
            decimal value = 123.45m;
            var template = "{{ toJson(value) }}";
            dynamic data = new ExpandoObject();
            data.value = value;
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("123.45"));
        }

        [Test]
        public void ToJson_BooleanValue_ReturnsCorrectJson()
        {
            var template = "{{ toJson(true) }}";
            var result = _interpreter.Interpret(template, _emptyData);
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void ToJson_EmptyArray_ReturnsCorrectJson()
        {
            var template = "{{ toJson([]) }}";
            var result = _interpreter.Interpret(template, null);
            Assert.That(result, Is.EqualTo("[]"));
        }

        [Test]
        public void ToJson_EmptyObject_ReturnsCorrectJson()
        {
            dynamic empty = new ExpandoObject();
            var template = "{{ toJson(empty) }}";
            dynamic data = new ExpandoObject();
            data.empty = empty;
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{}"));
        }

        [Test]
        public void ToJson_WithFormatting_ReturnsFormattedJson()
        {
            dynamic address = new ExpandoObject();
            address.city = "New York";
            address.zip = "10001";

            dynamic person = new ExpandoObject();
            person.name = "John";
            person.address = address;

            dynamic data = new ExpandoObject();
            data.person = person;

            var template = "{{ toJson(person, true) }}";
            var result = _interpreter.Interpret(template, data);

            var expected = @"{
    ""name"": ""John"",
    ""address"": {
        ""city"": ""New York"",
        ""zip"": ""10001""
    }
}";

            Assert.That(result, Is.EqualTo(expected));
        }

        [Test]
        public void ToJson_WithStringEscaping_ReturnsCorrectJson()
        {
            dynamic obj = new ExpandoObject();
            obj.text = "Hello \"World\"\nNew Line";
            var template = "{{ toJson(obj) }}";
            dynamic data = new ExpandoObject();
            data.obj = obj;
            var result = _interpreter.Interpret(template, data);
            Assert.That(result, Is.EqualTo("{\"text\":\"Hello \\\"World\\\"\\nNew Line\"}"));
        }

        [Test]
        public void ToJson_ComplexNestedStructure_ReturnsCorrectJson()
        {
            dynamic address1 = new ExpandoObject();
            address1.street = "123 Main St";
            address1.city = "New York";

            dynamic address2 = new ExpandoObject();
            address2.street = "456 Oak Ave";
            address2.city = "Boston";

            dynamic person1 = new ExpandoObject();
            person1.name = "John";
            person1.age = 30;
            person1.address = address1;
            person1.hobbies = new[] { "reading", "gaming" };

            dynamic person2 = new ExpandoObject();
            person2.name = "Jane";
            person2.age = 28;
            person2.address = address2;
            person2.hobbies = new[] { "painting", "music" };

            dynamic org = new ExpandoObject();
            org.people = new[] { person1, person2 };
            org.company = "Acme Corp";
            org.active = true;

            dynamic data = new ExpandoObject();
            data.org = org;

            var template = "{{ toJson(org, true) }}";
            var result = _interpreter.Interpret(template, data);

            // Verify structure using deserialization
            var serializer = new JavaScriptSerializer();
            var deserialized = serializer.Deserialize<Dictionary<string, object>>(result);

            Assert.Multiple(() =>
            {
                Assert.That(deserialized["company"], Is.EqualTo("Acme Corp"));
                Assert.That(deserialized["active"], Is.EqualTo(true));

                var people = (System.Collections.ArrayList)deserialized["people"];
                Assert.That(people.Count, Is.EqualTo(2));

                var firstPerson = (Dictionary<string, object>)people[0];
                Assert.That(firstPerson["name"], Is.EqualTo("John"));
                Assert.That(firstPerson["age"], Is.EqualTo(30));

                var firstAddress = (Dictionary<string, object>)firstPerson["address"];
                Assert.That(firstAddress["city"], Is.EqualTo("New York"));

                var firstHobbies = (System.Collections.ArrayList)firstPerson["hobbies"];
                Assert.That(firstHobbies[0], Is.EqualTo("reading"));
            });
        }

        [Test]
        public void ToJson_WithSpecialCharacters_ReturnsCorrectJson()
        {
            dynamic specialChars = new ExpandoObject();
            specialChars.text = "Special chars: 你好, Pößen, ñ, 🌟";
            var template = "{{ toJson(specialChars) }}";
            dynamic data = new ExpandoObject();
            data.specialChars = specialChars;
            var result = _interpreter.Interpret(template, data);

            // Verify the result contains the special characters correctly
            Assert.That(result.Contains("你好"), Is.True);
            Assert.That(result.Contains("Pößen"), Is.True);
            Assert.That(result.Contains("ñ"), Is.True);
            Assert.That(result.Contains("🌟"), Is.True);
        }

        [Test]
        public void Fetch_SimpleQuery_ReturnsCorrectExpandoObjects()
        {
            // Arrange
            var fetchXml = @"<fetch top='2'>
                <entity name='account'>
                    <attribute name='name' />
                    <attribute name='accountid' />
                </entity>
            </fetch>";

            var entityCollection = new EntityCollection(new List<Entity>
            {
                new Entity("account")
                {
                    ["name"] = "Test Account 1",
                    ["accountid"] = Guid.NewGuid()
                },
                new Entity("account")
                {
                    ["name"] = "Test Account 2",
                    ["accountid"] = Guid.NewGuid()
                }
            });

            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Returns(entityCollection);

            // Act
            var template = "{{ let accounts = fetch(\"" + fetchXml + "\") }}{{ for account in accounts }}{{ account.name }}|{{ /for }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            Assert.That(result, Is.EqualTo("Test Account 1|Test Account 2|"));
        }

        [Test]
        public void Fetch_WithNullValues_SkipsNullAttributes()
        {
            // Arrange
            var fetchXml = "<fetch><entity name='contact'><attribute name='firstname' /><attribute name='lastname' /></entity></fetch>";
            var accountId = Guid.NewGuid();

            var entityCollection = new EntityCollection(new List<Entity>
            {
                new Entity("contact")
                {
                    ["firstname"] = "John",
                    ["lastname"] = null // This should be skipped
                }
            });

            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Returns(entityCollection);

            // Act
            var template = "{{ let contact = first(fetch(\"" + fetchXml + "\")) }}{{ if contains(contact, \"firstname\") }}{{ contact.firstname }}{{ /if }}{{ if contains(contact, \"lastname\") }}{{ contact.lastname }}{{ /if }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            Assert.That(result, Is.EqualTo("John")); // Only firstname should be present
        }

        [Test]
        public void Fetch_WithSpecialDataTypes_ConvertsCorrectly()
        {
            // Arrange
            var fetchXml = "<fetch><entity name='account'><attribute name='primarycontactid' /><attribute name='accountcategorycode' /><attribute name='revenue' /></entity></fetch>";
            var contactId = Guid.NewGuid();

            Entity entity = new Entity("account")
            {
                ["primarycontactid"] = new EntityReference("contact", contactId),
                ["accountcategorycode"] = new OptionSetValue(1),
                ["revenue"] = new Money(1000.50m)
            };

            entity.FormattedValues["accountcategorycode"] = "First";

            var entityCollection = new EntityCollection(new List<Entity>
            {
                entity
            });

            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Returns(entityCollection);

            // Act
            var template = @"{{ let accounts = fetch(""" + fetchXml + @""") }}
                           Contact: {{ first(accounts).primarycontactid }}
                           Category: {{ first(accounts).accountcategorycode }}
                           Category Value: {{ first(accounts).accountcategorycode.value }}
                           Category Label: {{ first(accounts).accountcategorycode.label }}
                           Revenue: {{ first(accounts).revenue }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            StringAssert.Contains(contactId.ToString(), result);
            StringAssert.Contains("Category: {value: 1, label: First}", result); // OptionSetValue
            StringAssert.Contains("Category Value: 1", result); // OptionSetValue
            StringAssert.Contains("Category Label: First", result); // OptionSetValue
            StringAssert.Contains("Revenue: 1000.50", result); // Money value
        }

        [Test]
        public void Fetch_WithAliasedValues_ConvertsCorrectly()
        {
            // Arrange
            var fetchXml = "<fetch><entity name='account'><attribute name='contact_name' /></entity></fetch>";

            var entityCollection = new EntityCollection(new List<Entity>
            {
                new Entity("account")
                {
                    ["contact_name"] = new AliasedValue("contact", "fullname", "John Doe")
                }
            });

            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Returns(entityCollection);

            // Act
            var template = "{{ let accounts = fetch(\"" + fetchXml + "\") }}{{ first(accounts).contact_name }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            Assert.That(result, Is.EqualTo("John Doe"));
        }

        [Test]
        public void Fetch_WithAliasedDotValues_ConvertsCorrectly()
        {
            // Arrange
            var fetchXml = "<fetch><entity name='account'><attribute name='contact_name' /></entity></fetch>";

            var entityCollection = new EntityCollection(new List<Entity>
            {
                new Entity("account")
                {
                    ["contact.name"] = new AliasedValue("contact", "fullname", "John Doe")
                }
            });

            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Returns(entityCollection);

            // Act
            var template = "{{ let accounts = fetch(\"" + fetchXml + "\") }}{{ get(first(accounts), \"contact.name\") }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());

            // Assert
            Assert.That(result, Is.EqualTo("John Doe"));
        }

        [Test]
        public void Fetch_WithNoDataverseService_ThrowsException()
        {
            // Arrange
            var interpreter = new Interpreter(); // No DataverseService provided
            var fetchXml = "<fetch><entity name='account'><attribute name='name' /></entity></fetch>";
            var template = "{{ fetch(\"" + fetchXml + "\") }}";

            // Act & Assert
            var ex = Assert.Throws<TemplateEvaluationException>(() => interpreter.Interpret(template, new ExpandoObject()));
            Assert.That(ex.Message, Does.Contain("Dataverse service not configured"));
        }

        [Test]
        public void Fetch_WithEmptyFetchXml_ThrowsException()
        {
            // Arrange
            var template = "{{ fetch(\"\") }}";

            // Act & Assert
            var ex = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, new ExpandoObject()));
            Assert.That(ex.Message, Does.Contain("requires a non-empty FetchXML string"));
        }

        [Test]
        public void DataverseService_WithNullOrganizationService_ThrowsException()
        {
            // Act & Assert
            Assert.Throws<ArgumentNullException>(() => new DataverseService(null));
        }

        [Test]
        public void Fetch_WithInvalidFetchXml_PropagatesException()
        {
            // Arrange
            var invalidFetchXml = "not valid fetch xml";
            _mockOrgService
                .Setup(x => x.RetrieveMultiple(It.IsAny<FetchExpression>()))
                .Throws(new Exception("Invalid FetchXML"));

            var template = "{{ fetch(\"" + invalidFetchXml + "\") }}";

            // Act & Assert
            Assert.Throws<Exception>(() => _interpreter.Interpret(template, new ExpandoObject()));
        }

        [Test]
        public void TokenizeBasicWhitespace_SingleSpace()
        {
            var tokens = _lexer.Tokenize("Hello World");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeBasicWhitespace_MultipleSpaces()
        {
            var tokens = _lexer.Tokenize("Hello    World");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo("    "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeBasicWhitespace_Tabs()
        {
            var tokens = _lexer.Tokenize("Hello\tWorld");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo("\t"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeBasicWhitespace_MixedWhitespace()
        {
            var tokens = _lexer.Tokenize("Hello \t  World");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" \t  "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeNewlines_SingleUnixNewline()
        {
            var tokens = _lexer.Tokenize("Hello\nWorld");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\n"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeNewlines_SingleWindowsNewline()
        {
            var tokens = _lexer.Tokenize("Hello\r\nWorld");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\r\n"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeNewlines_SingleMacNewline()
        {
            var tokens = _lexer.Tokenize("Hello\rWorld");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\r"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[2].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeNewlines_MultipleNewlines()
        {
            var tokens = _lexer.Tokenize("Hello\n\nWorld");

            Assert.That(tokens.Count, Is.EqualTo(4));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\n"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[2].Value, Is.EqualTo("\n"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[3].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeMixedWhitespaceAndNewlines()
        {
            var tokens = _lexer.Tokenize("Hello  \n  World");

            Assert.That(tokens.Count, Is.EqualTo(5));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo("  "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[2].Value, Is.EqualTo("\n"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[3].Value, Is.EqualTo("  "));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[4].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeDirectiveWithWhitespace()
        {
            var tokens = _lexer.Tokenize("Hello {{ name }}World");

            Assert.That(tokens.Count, Is.EqualTo(6));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[1].Value, Is.EqualTo(" "));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.DirectiveStart));
            Assert.That(tokens[2].Value, Is.EqualTo("{{"));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.Variable));
            Assert.That(tokens[3].Value, Is.EqualTo("name"));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.DirectiveEnd));
            Assert.That(tokens[4].Value, Is.EqualTo("}}"));
            Assert.That(tokens[5].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[5].Value, Is.EqualTo("World"));
        }

        [Test]
        public void TokenizeEmptyInput()
        {
            var tokens = _lexer.Tokenize("");
            Assert.That(tokens.Count, Is.EqualTo(0));
        }

        [Test]
        public void TokenizeOnlyWhitespace()
        {
            var tokens = _lexer.Tokenize("   \t  ");

            Assert.That(tokens.Count, Is.EqualTo(1));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[0].Value, Is.EqualTo("   \t  "));
        }

        [Test]
        public void TokenizeOnlyNewlines()
        {
            var tokens = _lexer.Tokenize("\n\r\n\n");

            Assert.That(tokens.Count, Is.EqualTo(3));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[0].Value, Is.EqualTo("\n"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\r\n"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[2].Value, Is.EqualTo("\n"));
        }

        [Test]
        public void TokenizeComplexMixedContent()
        {
            var input = "Hello,\n" +
                       "  {{name}}  \r\n" +
                       "\tWelcome!";

            var tokens = _lexer.Tokenize(input);

            Assert.That(tokens.Count, Is.EqualTo(10));
            Assert.That(tokens[0].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[0].Value, Is.EqualTo("Hello,"));
            Assert.That(tokens[1].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[1].Value, Is.EqualTo("\n"));
            Assert.That(tokens[2].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[2].Value, Is.EqualTo("  "));
            Assert.That(tokens[3].Type, Is.EqualTo(TokenType.DirectiveStart));
            Assert.That(tokens[3].Value, Is.EqualTo("{{"));
            Assert.That(tokens[4].Type, Is.EqualTo(TokenType.Variable));
            Assert.That(tokens[4].Value, Is.EqualTo("name"));
            Assert.That(tokens[5].Type, Is.EqualTo(TokenType.DirectiveEnd));
            Assert.That(tokens[5].Value, Is.EqualTo("}}"));
            Assert.That(tokens[6].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[6].Value, Is.EqualTo("  "));
            Assert.That(tokens[7].Type, Is.EqualTo(TokenType.Newline));
            Assert.That(tokens[7].Value, Is.EqualTo("\r\n"));
            Assert.That(tokens[8].Type, Is.EqualTo(TokenType.Whitespace));
            Assert.That(tokens[8].Value, Is.EqualTo("\t"));
            Assert.That(tokens[9].Type, Is.EqualTo(TokenType.Text));
            Assert.That(tokens[9].Value, Is.EqualTo("Welcome!"));
        }

        [Test]
        public void TokenizePositionTracking()
        {
            var input = "Hello\n  World";
            var tokens = _lexer.Tokenize(input);

            Assert.That(tokens.Count, Is.EqualTo(4));
            Assert.That(tokens[0].Location.Position, Is.EqualTo(0)); // "Hello"
            Assert.That(tokens[1].Location.Position, Is.EqualTo(5)); // "\n"
            Assert.That(tokens[2].Location.Position, Is.EqualTo(6)); // "  "
            Assert.That(tokens[3].Location.Position, Is.EqualTo(8)); // "World"
        }

        [Test]
        public void ParserHandlesWhitespaceAndNewlinesAsText()
        {
            var input = "Hello\n  World";

            // Evaluate with empty context to get the result
            var result = _interpreter.Interpret(input, new ExpandoObject());

            // The result should preserve all whitespace and newlines
            Assert.That(result, Is.EqualTo("Hello\n  World"));
        }

        [Test]
        public void ConditionalWithNestedContent()
        {
            // Arrange
            var input = "{{let x = 4}}{{if x == 4}}{{x}} {{for y in [1, 2, 3]}}{{y}} {{/for}}{{else}}{{x}}foo{{/if}}hello world";

            // Act
            var result = _interpreter.Interpret(input, new ExpandoObject());

            // Assert
            Assert.That(result, Is.EqualTo("4 1 2 3 hello world"));
        }

        [Test]
        public void NoTrimming_PreservesWhitespace()
        {
            var template = "Hello  {{ name }}  World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Hello  Test  World"));
        }

        [Test]
        public void LeftTrimming_RemovesLeadingWhitespace()
        {
            var template = "Hello  {{- name }} World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("HelloTest World"));
        }

        [Test]
        public void RightTrimming_RemovesTrailingWhitespace()
        {
            var template = "Hello {{ name -}}  World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Hello TestWorld"));
        }

        [Test]
        public void BothTrimming_RemovesBothWhitespaces()
        {
            var template = "Hello  {{- name -}}  World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("HelloTestWorld"));
        }

        [Test]
        public void LeftTrimming_RemovesLeadingNewlineAndWhitespace()
        {
            var template = "Hello\n  {{- name }} World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("HelloTest World"));
        }

        [Test]
        public void RightTrimming_RemovesTrailingNewlineAndWhitespace()
        {
            var template = "Hello {{ name -}}  \nWorld";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Hello TestWorld"));
        }

        [Test]
        public void BothTrimming_RemovesBothNewlinesAndWhitespaces()
        {
            var template = "Hello\n  {{- name -}}  \nWorld";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("HelloTestWorld"));
        }

        [Test]
        public void MultipleDirectives_HandlesTrimmingCorrectly()
        {
            var template = "Hello\n  {{- first -}}  \n  {{- second -}}  \nWorld";
            dynamic data = new ExpandoObject();
            data.first = "Test1";
            data.second = "Test2";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("HelloTest1Test2World"));
        }

        [Test]
        public void IfStatement_HandlesTrimmingCorrectly()
        {
            var template = "Start\n  {{- if true -}}\n    Content\n  {{- /if -}}  \nEnd";

            var result = _interpreter.Interpret(template, new ExpandoObject());

            Assert.That(result, Is.EqualTo("Start    ContentEnd"));
        }

        [Test]
        public void ForLoop_HandlesTrimmingCorrectly()
        {
            var template = "Start\n  {{- for item in items -}}\n    {{ item -}}  \n  {{- /for -}}  \nEnd";
            dynamic data = new ExpandoObject();
            data.items = new List<decimal>() { 1, 2, 3 };

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Start    1    2    3End"));
        }

        [Test]
        public void Comments_HandlesTrimmingCorrectly()
        {
            var template = "Start\n  {{-* Comment *-}}  \nEnd";

            var result = _interpreter.Interpret(template, new ExpandoObject());

            Assert.That(result, Is.EqualTo("StartEnd"));
        }

        [Test]
        public void MixedContent_HandlesTrimmingCorrectly()
        {
            var template = @"Hello
  {{- if true -}}
    {{ name -}}
  {{- /if -}}
World";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Hello    TestWorld"));
        }

        [Test]
        public void OnlyWhitespace_HandlesTrimmingCorrectly()
        {
            var template = "  {{- name -}}  ";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Test"));
        }

        [Test]
        public void MultipleNewlines_HandlesTrimmingCorrectly()
        {
            var template = "Hello\n\n  {{- name -}}  \n\nWorld";
            dynamic data = new ExpandoObject();
            data.name = "Test";

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Hello\nTest\nWorld"));
        }

        [Test]
        public void ComplexNesting_HandlesTrimmingCorrectly()
        {
            var template = @"Start
  {{- if true -}}
    {{- for item in items -}}
      {{ item -}}
    {{- /for -}}
  {{- /if -}}
End";
            dynamic data = new ExpandoObject();
            data.items = new List<decimal>() { 1, 2, 3 };

            var result = _interpreter.Interpret(template, data);

            Assert.That(result, Is.EqualTo("Start      1      2      3End"));
        }

        [Test]
        public void ObjectFunctionCreation()
        {
            // Arrange & Act
            var result1 = _interpreter.Interpret("{{let o = obj(fn: (a) => a * 5, n: 5)}}{{o.fn(o.n)}}", new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("25"));
        }

        [Test]
        public void ArrayFunctionCreation()
        {
            // Arrange & Act
            var result1 = _interpreter.Interpret("{{let fns = [(a) => a * 2, (a) => a * 5]}}{{for fn in fns}}{{fn(5)}} {{/for}}", new ExpandoObject());

            // Assert
            Assert.That(result1, Is.EqualTo("10 25 "));
        }

        [Test]
        public void ObjectPropertyNameDefinedMultipleTimes()
        {
            var template = "{{let x = obj(a: 1, a: 2)}}{{x.a}}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Duplicate field name 'a' defined at position 21");
        }

        [Test]
        public void BasicStatementList_Works()
        {
            var template = "{{ ((a) => let x = 1, let y = 2, x + a * y)(3) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("7"));
        }

        [Test]
        public void SingleStatement_Works()
        {
            var template = "{{ ((a) => let x = a * 2, x)(3) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("6"));
        }

        [Test]
        public void MultipleStatements_WithWhitespaceAndNewlines_Works()
        {
            var template = @"{{ 
                let fn = (n) =>
                    let count = length(n),
                    let squared = count * count,
                    squared + 1
            }}{{ fn(""hello"") }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("26")); // length is 5, squared is 25, plus 1 is 26
        }

        [Test]
        public void ParameterNameConflict_ThrowsException()
        {
            var template = "{{ (a) => let a = 1, a }}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Variable name 'a' conflicts with parameter name");
        }

        [Test]
        public void ExternalVariableConflict_ThrowsException()
        {
            var template = "{{ let x = 2 }}{{ (() => let x = 3, x)() }}";
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Cannot define variable 'x' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void IteratorConflict_ThrowsException()
        {
            var template = "{{ for x in [1,2,3] }}{{ (() => let x = 3, x)() }}{{ /for }}";
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Cannot define variable 'x' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void DataFieldConflict_ThrowsException()
        {
            var data = new ExpandoObject() as IDictionary<string, object>;
            data["x"] = "hello world";

            var template = "{{ ((a) => let x = [\"foo\"], concat(a, x))([1]) }}";
            var exception = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Cannot define variable 'x' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void DataFieldConflictWithGlobal_ThrowsException()
        { 
            var data = new ExpandoObject() as IDictionary<string, object>;
            data["x"] = "hello world";

            var template = "{{ let x = 3 }}";
            var exception = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Cannot define variable 'x' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void DataFieldConflictWithGlobalInIterator_ThrowsException()
        { 
            var data = new ExpandoObject() as IDictionary<string, object>;
            data["x"] = "hello world";

            var template = "{{ for i in [1, 2] }}{{ let x = 3 }}{{ /for }}";
            var exception = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data),
                "Cannot define variable 'x' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void Interpret_LambdaWithMissingParameterValues_ThrowsExceptionWithCorrectMessage()
        {
            // Arrange
            // Template that defines a lambda function with 4 parameters but only calls it with 2 arguments
            string template = @"
{{ let myFunc = (param1, param2, param3, param4) => param1 + param2 + param3 + param4 }}
{{ myFunc(1, 2) }}";

            // Act & Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, _emptyData));
            
            // Verify the exception message contains information about missing parameters
            Assert.That(exception.Message, Does.Contain("Missing values for:"));
            Assert.That(exception.Message, Does.Contain("param3"));
            Assert.That(exception.Message, Does.Contain("param4"));
            Assert.That(exception.Message, Does.Not.Contain("param1")); // These params have values
            Assert.That(exception.Message, Does.Not.Contain("param2"));
        }

        [Test]
        public void FunctionNameConflict_ThrowsException()
        {
            var template = "{{ (a) => let length = 3, a * length }}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Cannot define variable 'length' because it conflicts with an existing variable, field, or function");
        }

        [Test]
        public void VariableReassignment_ThrowsException()
        {
            var template = "{{ () => let x = 1, let x = 2, x }}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Cannot reassign variable 'x' in lambda function");
        }

        [Test]
        public void NestedLambdas_WithStatements_Works()
        {
            var template = @"{{ 
                let fn = (x) => 
                    let mult = x * 2,
                    (y) => 
                        let sum = mult + y,
                        sum * 2
            }}{{ fn(3)(4) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("20")); // (3 * 2 + 4) * 2 = 20
        }

        [Test]
        public void LambdaWithoutStatements_OnlyExpression_Works()
        {
            var template = "{{ ((x) => x * 2)(5) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("10"));
        }

        [Test]
        public void StatementWithoutComma_ThrowsException()
        {
            var template = "{{ () => x = 1 y = 2, x + y }}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Expected comma after statement");
        }

        [Test]
        public void StatementWithoutAssignment_ThrowsException()
        {
            var template = "{{ () => x, y = 2, x + y }}";
            Assert.Throws<TemplateParsingException>(() => _interpreter.Interpret(template, new ExpandoObject()),
                "Expected assignment after variable name");
        }

        [Test]
        public void ComplexExpressionInStatements_Works()
        {
            var template = @"{{ let fn =
                (arr) => 
                    let filtered = filter(arr, (x) => x > 2),
                    let sum = reduce(filtered, (acc, x) => acc + x, 0),
                    sum / length(filtered)
            }}{{ fn([1,2,3,4,5]) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("4")); // Average of [3,4,5] is 4
        }

        [Test]
        public void StatementVariablesInScope_ForLaterStatements_Works()
        {
            var template = "{{ (() => let x = 10, let y = x * 2, let z = y + 5, z)() }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("25")); // 10 * 2 + 5 = 25
        }

        [Test]
        public void ClosureCapturesVariablesAndParams()
        {
            var template = "{{ let x = ((v) => let a = 5, (b) => b * a * v)(5) }}{{ x(5) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.That(result, Is.EqualTo("125"));
        }

        [Test]
        public void Concat_ListAndString_ReturnsNewListWithItem()
        {
            // Arrange
            var template = @"{{ let result = concat(list, [item]) }}{{ join(result, "", "") }}";
            dynamic data = new ExpandoObject();
            data.list = new List<string> { "a", "b", "c" };
            data.item = "d";

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("a, b, c, d"));
        }

        [Test]
        public void Concat_ListAndList_ReturnsNewCombinedList()
        {
            // Arrange
            var template = @"{{ let result = concat(list1, list2) }}{{ join(result, "", "") }}";
            dynamic data = new ExpandoObject();
            data.list1 = new List<string> { "a", "b" };
            data.list2 = new List<string> { "c", "d" };

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("a, b, c, d"));
        }

        [Test]
        public void Concat_EmptyLists_ReturnsEmptyString()
        {
            // Arrange
            var template = @"{{ let result = concat(list1, list2) }}{{ join(result, "", "") }}";
            dynamic data = new ExpandoObject();
            data.list1 = new List<string>();
            data.list2 = new List<string>();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.Empty);
        }

        [Test]
        public void Concat_ArrayAndList_ConcatenatesCorrectly()
        {
            // Arrange
            var template = @"{{ let result = concat(array, list) }}{{ join(result, "", "") }}";
            dynamic data = new ExpandoObject();
            data.array = new[] { 1, 2, 3 };
            data.list = new List<int> { 4, 5, 6 };

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("1, 2, 3, 4, 5, 6"));
        }

        [Test]
        public void Concat_ListLength_ValidatesResultLength()
        {
            // Arrange
            var template = @"{{ let result = concat(list1, list2) }}{{ length(result) }}";
            dynamic data = new ExpandoObject();
            data.list1 = new List<string> { "a", "b" };
            data.list2 = new List<string> { "c", "d" };

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("4"));
        }

        [Test]
        public void IfFuncDoesNotConflictWithIfDirective()
        {
            // Arrange
            var template = @"{{ if(1 > 2, 2, 3) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("3"));
        }

        [Test]
        public void FactorialWorks()
        {
            var template = @"{{ let fact = (n) => if(n <= 1, 1, n * fact(n - 1)) }}{{ fact(5) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.AreEqual("120", result);
        }

        // Mutual recursion example:
        [Test]
        public void MutualRecursionWorks()
        {
            var template = @"{{- let isEven = (n) => if(n == 0, true, isOdd(n - 1)) -}}
    {{- let isOdd = (n) => if(n == 0, false, isEven(n - 1)) -}}
    {{- isEven(10) -}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.AreEqual("true", result);
        }

        // Short-circuit evaluation example:
        [Test]
        public void ShortCircuitEvaluationWorks()
        {
            var template = @"{{ let dangerous = (n) => 1/0 }}{{ false && dangerous(0) }}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.AreEqual("false", result);
        }

        // Fibonacci example with memoization:
        [Test]
        public void RecursiveFibonacciWorks()
        {
            var template = @"
    {{- let fib = (n) => if(n <= 1, n, fib(n - 1) + fib(n - 2)) -}}
    {{- fib(10) -}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.AreEqual("55", result);
        }

        // Y-combinator for anonymous recursion:
        [Test]
        public void YCombinatorWorks()
        {
            var template = @"
    {{- let Y = (f) => ((x) => f((y) => x(x)(y)))((x) => f((y) => x(x)(y))) -}}
    {{- let factorialF = (fact) => (n) => if(n <= 1, 1, n * fact(n - 1)) -}}
    {{- let factorial = Y(factorialF) -}}
    {{- factorial(5) -}}";
            var result = _interpreter.Interpret(template, new ExpandoObject());
            Assert.AreEqual("120", result);
        }

        [Test]
        public void CallingObjectFunctionWorks()
        {
            var template = @"{{ let o = (x) => let self = (() => obj(value: () => x, y: 2, timesY: () => o(self.y * x)))(), self }}{{ let o1 = o(2) }}{{ o1.timesY().value() }}";
            Assert.AreEqual("4", _interpreter.Interpret(template, new ExpandoObject()));
        }

        [Test]
        public void TestIdentityMonad()
        {
            const string template = @"
{{- let Identity = (x) => 
   let self = (() => obj(
     value: () => x,
     map: (f) => Identity(f(x)),
     bind: (f) => f(x),
     join: () => x.value(),
     chain: (f1, f2) => self.map(f1).map(f2)
   ))(), 
   self 
-}}

{{-* Basic value wrapping and extraction *-}}
{{- let id1 = Identity(5) -}}
Value 1: {{ id1.value() }}

{{-* Map operation *-}}
{{- let id2 = id1.map((x) => x * 2) -}}
Mapped value: {{ id2.value() }}

{{-* Bind operation with a monad-returning function *-}}
{{- let multiplyAndWrap = (x) => Identity(x * 3) -}}
{{- let id3 = id1.bind(multiplyAndWrap) -}}
Bound value: {{ id3.value() }}

{{-* Chaining multiple operations *-}}
{{- let id4 = Identity(1).map((x) => x + 1).map((x) => x * 2) -}}
Chained operations: {{ id4.value() }}

{{-* Using bind for composition *-}}
{{- let add10 = (x) => Identity(x + 10) -}}
{{- let double = (x) => Identity(x * 2) -}}
{{- let id5 = Identity(5).bind(add10).bind(double) -}}
Composed functions: {{ id5.value() }}

{{-* Implementing left identity law: return a >>= f ≡ f a *-}}
{{- let leftIdValue = Identity(7).bind(add10).value() -}}
{{- let directFValue = add10(7).value() -}}
Left identity (should be equal): {{ leftIdValue }} and {{ directFValue }}

{{-* Implementing right identity law: m >>= return ≡ m *-}}
{{- let monad = Identity(42) -}}
{{- let rightIdValue = monad.bind(Identity).value() -}}
Right identity (should be same as 42): {{ rightIdValue }}

{{-* Implementing associativity law: (m >>= f) >>= g ≡ m >>= (\x -> f x >>= g) *-}}
{{- let triple = (x) => Identity(x * 3) -}}
{{- let left = Identity(3).bind(add10).bind(triple).value() -}}
{{- let right = Identity(3).bind((x) => add10(x).bind(triple)).value() -}}
Associativity (should be equal): {{ left }} and {{ right }}

{{-* Using the chain helper function *-}}
{{- let combined = Identity(5).chain((x) => x + 5, (x) => x * 2).value() -}}
Chain helper result: {{ combined }}

{{-* Handling nested monads *-}}
{{- let nestedMonad = Identity(Identity(10)) -}}
{{- let flattened = nestedMonad.join() -}}
Joined nested monad: {{ flattened }}
";

            dynamic data = new ExpandoObject();
            string result = _interpreter.Interpret(template, data);

            // Verify basic identity monad operations
            Assert.IsTrue(result.Contains("Value 1: 5"));
            Assert.IsTrue(result.Contains("Mapped value: 10"));
            Assert.IsTrue(result.Contains("Bound value: 15"));
            Assert.IsTrue(result.Contains("Chained operations: 4"));
            Assert.IsTrue(result.Contains("Composed functions: 30"));

            // Verify monad laws
            Assert.IsTrue(result.Contains("Left identity (should be equal): 17 and 17"));
            Assert.IsTrue(result.Contains("Right identity (should be same as 42): 42"));
            Assert.IsTrue(result.Contains("Associativity (should be equal): 39 and 39"));

            // Verify helper functions
            Assert.IsTrue(result.Contains("Chain helper result: 20"));
            Assert.IsTrue(result.Contains("Joined nested monad: 10"));
        }

        [Test]
        public void TestWhiteSpaceHandlingAtEnd()
        {
            var template = @"{{ 1 -}}   
";
            Assert.That("1", Is.EqualTo(_interpreter.Interpret(template, new ExpandoObject())));
        }

        [Test]
        public void TestConcatWithDynamicArrayDeclaration()
        {
            var template = @"{{ let a = 4 }}{{ let b = concat([1, 2, 3], [a]) }}{{ for x in b }}{{ x }}{{ /for }}";
            Assert.That("1234", Is.EqualTo(_interpreter.Interpret(template, new ExpandoObject())));
        }

        [Test]
        public void Range_BasicUsage_ReturnsExpectedString()
        {
            // Arrange
            var template = "{{ range(1, 5) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[1, 2, 3, 4]"));
        }

        [Test]
        public void Range_WithStep_ReturnsExpectedString()
        {
            // Arrange
            var template = "{{ range(0, 10, 2) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[0, 2, 4, 6, 8]"));
        }

        [Test]
        public void Range_WithDecimalStep_ReturnsExpectedString()
        {
            // Arrange
            var template = "{{ range(0, 3, 0.5) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[0.0, 0.5, 1.0, 1.5, 2.0, 2.5]"));
        }

        [Test]
        public void Range_WithNegativeStep_ReturnsExpectedString()
        {
            // Arrange
            var template = "{{ range(5, 0, -1) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[5, 4, 3, 2, 1]"));
        }

        [Test]
        public void Range_WithDecimalNegativeStep_ReturnsExpectedString()
        {
            // Arrange
            var template = "{{ range(2, 0, -0.5) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[2.0, 1.5, 1.0, 0.5]"));
        }

        [Test]
        public void Range_StartEqualsEnd_ReturnsEmptyArray()
        {
            // Arrange
            var template = "{{ range(5, 5) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[]"));
        }

        [Test]
        public void Range_StartGreaterThanEndWithPositiveStep_ReturnsEmptyArray()
        {
            // Arrange
            var template = "{{ range(10, 5) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[]"));
        }

        [Test]
        public void Range_StartLessThanEndWithNegativeStep_ReturnsEmptyArray()
        {
            // Arrange
            var template = "{{ range(5, 10, -1) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[]"));
        }

        [Test]
        public void Range_ZeroStep_ThrowsException()
        {
            // Arrange
            var template = "{{ range(0, 5, 0) }}";
            dynamic data = new ExpandoObject();

            // Act & Assert
            var ex = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, data));
            Assert.That(ex.Message, Contains.Substring("non-zero step value"));
        }

        [Test]
        public void Range_UsedInLoop_RendersCorrectTemplate()
        {
            // Arrange
            var template = "{{ for num in range(1, 4) }}Number: {{ num }}{{ if num < 3 }}, {{ /if }}{{ /for }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("Number: 1, Number: 2, Number: 3"));
        }

        [Test]
        public void Range_WithArrayLength_ReturnsExpectedInt()
        {
            // Arrange
            var template = "{{ length(range(1, 5)) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("4"));
        }

        [Test]
        public void Range_UsedWithMap_ReturnsTransformedArray()
        {
            // Arrange
            var template = "{{ map(range(1, 4), (x) => x * 2) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[2, 4, 6]"));
        }

        [Test]
        public void Range_EmptyRange_ReturnsEmptyArray()
        {
            // Arrange
            var template = "{{ range(0, 0) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[]"));
        }

        [Test]
        public void Range_SingleElementRange_ReturnsArrayWithOneElement()
        {
            // Arrange
            var template = "{{ range(5, 6) }}";
            dynamic data = new ExpandoObject();

            // Act
            var result = _interpreter.Interpret(template, data);

            // Assert - verifying exact string output
            Assert.That(result, Is.EqualTo("[5]"));
        }

        [Test]
        public void RangeYear_WithDefaultStep_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeYear(datetime(""2020-03-15""), datetime(""2023-01-01""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            // Parse the result and verify it contains the right dates
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(3, dates.Count);
            Assert.AreEqual(new DateTime(2020, 3, 15), dates[0]);
            Assert.AreEqual(new DateTime(2021, 3, 15), dates[1]);
            Assert.AreEqual(new DateTime(2022, 3, 15), dates[2]);
        }

        [Test]
        public void RangeYear_WithCustomStep_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeYear(datetime(""2020-02-03""), datetime(""2026-01-01""), 2)
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(3, dates.Count);
            Assert.AreEqual(new DateTime(2020, 2, 3), dates[0]);
            Assert.AreEqual(new DateTime(2022, 2, 3), dates[1]);
            Assert.AreEqual(new DateTime(2024, 2, 3), dates[2]);
        }

        [Test]
        public void RangeYear_WithNonIntegerStep_FloorsStepValue()
        {
            // Arrange
            string template = @"{{ 
                rangeYear(datetime(""2020-05-10""), datetime(""2024-01-01""), 1.7)
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(4, dates.Count);
            Assert.AreEqual(new DateTime(2020, 5, 10), dates[0]);
            Assert.AreEqual(new DateTime(2021, 5, 10), dates[1]);
            Assert.AreEqual(new DateTime(2022, 5, 10), dates[2]);
            Assert.AreEqual(new DateTime(2023, 5, 10), dates[3]);
        }

        [Test]
        public void RangeYear_WithStartEqualToEnd_ReturnsEmptyArray()
        {
            // Arrange
            string template = @"{{ 
                rangeYear(datetime(""2022-06-15""), datetime(""2022-06-15""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.AreEqual("[]", result.Trim());
        }

        [Test]
        public void RangeMonth_AdjustsToLastDayOfMonth()
        {
            // Arrange
            string template = @"{{ 
                rangeMonth(datetime(""2020-01-31""), datetime(""2020-04-01""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(3, dates.Count);
            Assert.AreEqual(new DateTime(2020, 1, 31), dates[0]);
            Assert.AreEqual(new DateTime(2020, 2, 29), dates[1]); // Leap year 2020, February has 29 days
            Assert.AreEqual(new DateTime(2020, 3, 31), dates[2]);
        }

        [Test]
        public void RangeMonth_FromExample_MatchesExpectation()
        {
            // Arrange - Test from the requirement example
            string template = @"{{ 
                rangeMonth(datetime(""01-31-2020""), datetime(""08-01-2020""), 1.5)
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(7, dates.Count);
            Assert.AreEqual(new DateTime(2020, 1, 31), dates[0]);
            Assert.AreEqual(new DateTime(2020, 2, 29), dates[1]);
            Assert.AreEqual(new DateTime(2020, 3, 31), dates[2]);
            Assert.AreEqual(new DateTime(2020, 4, 30), dates[3]);
            Assert.AreEqual(new DateTime(2020, 5, 31), dates[4]);
            Assert.AreEqual(new DateTime(2020, 6, 30), dates[5]);
            Assert.AreEqual(new DateTime(2020, 7, 31), dates[6]);
        }

        [Test]
        public void RangeDay_AcrossMonthBoundary_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeDay(datetime(""2020-02-28""), datetime(""2020-03-03""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(4, dates.Count);
            Assert.AreEqual(new DateTime(2020, 2, 28), dates[0]);
            Assert.AreEqual(new DateTime(2020, 2, 29), dates[1]); // Leap year
            Assert.AreEqual(new DateTime(2020, 3, 1), dates[2]);
            Assert.AreEqual(new DateTime(2020, 3, 2), dates[3]);
        }

        [Test]
        public void RangeHour_AcrossDayBoundary_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeHour(datetime(""2020-03-15 22:00:00""), datetime(""2020-03-16 02:00:00""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(4, dates.Count);
            Assert.AreEqual(new DateTime(2020, 3, 15, 22, 0, 0), dates[0]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 23, 0, 0), dates[1]);
            Assert.AreEqual(new DateTime(2020, 3, 16, 0, 0, 0), dates[2]);
            Assert.AreEqual(new DateTime(2020, 3, 16, 1, 0, 0), dates[3]);
        }

        [Test]
        public void RangeMinute_WithCustomStep_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeMinute(datetime(""2020-03-15 10:00:00""), datetime(""2020-03-15 11:00:00""), 15)
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(4, dates.Count);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 0, 0), dates[0]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 15, 0), dates[1]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 30, 0), dates[2]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 45, 0), dates[3]);
        }

        [Test]
        public void RangeSecond_AcrossMinuteBoundary_GeneratesCorrectDateRange()
        {
            // Arrange
            string template = @"{{ 
                rangeSecond(datetime(""2020-03-15 10:00:58""), datetime(""2020-03-15 10:01:03""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(5, dates.Count);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 0, 58), dates[0]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 0, 59), dates[1]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 1, 0), dates[2]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 1, 1), dates[3]);
            Assert.AreEqual(new DateTime(2020, 3, 15, 10, 1, 2), dates[4]);
        }

        [Test]
        public void RangeYear_FromExample_MatchesExpectation()
        {
            // Arrange - Test from the requirement example
            string template = @"{{ 
                rangeYear(datetime(""02-03-2020""), datetime(""04-01-2024""), 2)
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(3, dates.Count);
            Assert.AreEqual(new DateTime(2020, 2, 3), dates[0]);
            Assert.AreEqual(new DateTime(2022, 2, 3), dates[1]);
            Assert.AreEqual(new DateTime(2024, 2, 3), dates[2]);
        }

        [Test]
        public void RangeMonth_FromSecondExample_MatchesExpectation()
        {
            // Arrange - Test from the second requirement example
            string template = @"{{ 
                rangeMonth(datetime(""02-03-2020""), datetime(""05-01-2020""))
            }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            List<DateTime> dates = ParseDateTimeArray(result);
            Assert.AreEqual(3, dates.Count);
            Assert.AreEqual(new DateTime(2020, 2, 3), dates[0]);
            Assert.AreEqual(new DateTime(2020, 3, 3), dates[1]);
            Assert.AreEqual(new DateTime(2020, 4, 3), dates[2]);
        }

        [Test]
        public void RangeYear_WithZeroStep_ThrowsException()
        {
            // Arrange
            string template = @"{{ 
                rangeYear(datetime(""2020-01-01""), datetime(""2022-01-01""), 0)
            }}";

            // Act & Assert
            Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, _emptyData));
        }

        [Test]
        public void MultipleFunctionsInSameTemplate_WorkCorrectly()
        {
            // Arrange
            string template = @"
            Years: {{ rangeYear(datetime(""2020-01-01""), datetime(""2022-01-01"")) }}
            Months: {{ rangeMonth(datetime(""2020-01-31""), datetime(""2020-04-01"")) }}
            Days: {{ rangeDay(datetime(""2020-01-01""), datetime(""2020-01-05"")) }}
            ";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            StringAssert.Contains("Years: [", result);
            StringAssert.Contains("Months: [", result);
            StringAssert.Contains("Days: [", result);

            // Verify the output contains valid date arrays
            Assert.IsTrue(Regex.IsMatch(result, @"Years: \[\d{4}-\d{2}-\d{2}"));
            Assert.IsTrue(Regex.IsMatch(result, @"Months: \[\d{4}-\d{2}-\d{2}"));
            Assert.IsTrue(Regex.IsMatch(result, @"Days: \[\d{4}-\d{2}-\d{2}"));
        }

        private List<DateTime> ParseDateTimeArray(string output)
        {
            string cleanOutput = output.Trim();

            if (cleanOutput == "[]")
                return new List<DateTime>();

            cleanOutput = cleanOutput.Trim('[', ']');
            string[] dateStrings = cleanOutput.Split(',').Select(s => s.Trim()).ToArray();

            List<DateTime> result = new List<DateTime>();
            foreach (string dateString in dateStrings)
            {
                if (DateTime.TryParse(dateString, out DateTime date))
                {
                    result.Add(date);
                }
                else
                {
                    string pattern = @"(\d{4})-(\d{2})-(\d{2})T(\d{2}):(\d{2}):(\d{2})";
                    var match = Regex.Match(dateString, pattern);
                    if (match.Success)
                    {
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        int hour = int.Parse(match.Groups[4].Value);
                        int minute = int.Parse(match.Groups[5].Value);
                        int second = int.Parse(match.Groups[6].Value);

                        result.Add(new DateTime(year, month, day, hour, minute, second));
                    }
                    else
                    {
                        throw new Exception($"Unable to parse date string: {dateString}");
                    }
                }
            }

            return result;
        }

        [Test]
        public void ComplexTemplate_ToString_ValidatesAllAstNodeTypes()
        {
            // This template includes examples of all node types we want to test
            string complexTemplate = @"
{{* This is a comment *}}
{{ literal }}Raw content here{{ /literal }}
Hello, {{ name }}!

{{ let greeting = ""Welcome to our site"" }}
{{ greeting }}

{{ if user.age > 18 }}
  {{ capture adultContent }}You are an adult with {{user.age}} years of experience.{{ /capture }}
  {{ adultContent }}
{{ elseif user.age > 13 }}
  You are a teenager.
{{ else }}
  You are a child.
{{ /if }}

{{ for item in user.items }}
  - {{ item.name }}: ${{ item.price }}
{{ /for }}

{{ length(user.items) }} items found.

{{ obj(id: 123, name: user.name, isActive: true) }}

{{ user.data }}

{{ toUpper(""make this uppercase"") }}

{{ 5 + (10 * 3) }}

{{ [1, 2, 3, ""four"", true] }}

{{ (x, y) => x + y }}

{{ include templateName }}
";

            // Parse the template to generate AST
            var tokens = _interpreter.Lexer.Tokenize(complexTemplate);
            var ast = _interpreter.Parser.Parse(tokens);

            // Get the string representation of the AST
            string astString = ast.ToString();

            // Validate that the string contains the expected node types and formats
            Assert.That(astString, Does.StartWith("TemplateNode("));

            // Test TextNode representation
            Assert.That(astString, Does.Contain("TextNode(text=\"Hello,\")"));

            // Test NewlineNode and WhitespaceNode
            Assert.That(astString, Does.Contain("NewlineNode("));
            Assert.That(astString, Does.Contain("WhitespaceNode("));

            // Test VariableNode
            Assert.That(astString, Does.Contain("VariableNode(path=\"name\")"));

            // Test StringNode with escaped quotes
            Assert.That(astString, Does.Contain("StringNode(value=\"Welcome to our site\")"));

            // Test LetNode
            Assert.That(astString, Does.Contain("LetNode(variableName=\"greeting\""));

            // Test CaptureNode
            Assert.That(astString, Does.Contain("CaptureNode(variableName=\"adultContent\""));

            // Test IfNode with conditional branches
            Assert.That(astString, Does.Contain("IfNode(conditionalBranches=["));
            Assert.That(astString, Does.Contain("elseBranch="));

            // Test BinaryNode
            Assert.That(astString, Does.Contain("BinaryNode(operator=GreaterThan"));

            // Test ForNode
            Assert.That(astString, Does.Contain("ForNode(iteratorName=\"item\", collection="));

            // Test function references and invocation
            Assert.That(astString, Does.Contain("FunctionReferenceNode(name=\"length\")"));
            Assert.That(astString, Does.Contain("InvocationNode(callable="));

            // Test ObjectCreationNode
            Assert.That(astString, Does.Contain("ObjectCreationNode(fields=["));

            // Test BooleanNode
            Assert.That(astString, Does.Contain("BooleanNode(value=true)"));

            // Test NumberNode
            Assert.That(astString, Does.Match(new Regex("NumberNode\\(value=\\d+(\\.\\d+)?\\)")));

            // Test ArrayNode
            Assert.That(astString, Does.Contain("ArrayNode(elements=["));

            // Test LambdaNode
            Assert.That(astString, Does.Contain("LambdaNode(parameters=["));

            // Test IncludeNode
            Assert.That(astString, Does.Contain("IncludeNode(templateName=\"templateName\""));

            // Test LiteralNode
            Assert.That(astString, Does.Contain("LiteralNode(content=\"Raw content here\")"));

            // Check proper nesting structure using parentheses count
            int openParenCount = astString.Count(c => c == '(');
            int closeParenCount = astString.Count(c => c == ')');
            Assert.AreEqual(openParenCount, closeParenCount, "AST string should have balanced parentheses");

            // Validate proper array bracket usage
            int openBracketCount = astString.Count(c => c == '[');
            int closeBracketCount = astString.Count(c => c == ']');
            Assert.AreEqual(openBracketCount, closeBracketCount, "AST string should have balanced brackets");
        }

        [Test]
        public void InfiniteRecursionThrowsError()
        {
            var template = @"{{ let fn = () => fn() }}{{ fn() }}";

            var ex = Assert.Throws<TemplateEvaluationException>(() => _interpreter.Interpret(template, _emptyData));
            Assert.That(ex.Message, Contains.Substring("Maximum call stack depth 1000 has been exceeded."));
        }

        [Test]
        public void NestedClosureTest()
        {
            var template = @"{{ let fn = (x) => let p = 2, (y) => let q = 3, (z) => let r = 1, (p * q + r) + (x * y + z) }}{{ fn(2)(3)(1) }}";
            Assert.That(_interpreter.Interpret(template, _emptyData), Is.EqualTo("14"));
        }

        [Test]
        public void Tokenize_UnterminatedString_ThrowsTemplateParsingException()
        {
            // Arrange
            string template = "{{ \"This string is not terminated }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unterminated string literal"));
            Assert.That(exception.Location, Is.Not.Null);
            Assert.That(exception.Location.Line, Is.EqualTo(1));
        }

        [Test]
        public void Tokenize_InvalidEscapeSequence_ThrowsTemplateParsingException()
        {
            // Arrange
            string template = "{{ \"String with invalid escape: \\z\" }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Invalid escape sequence"));
            Assert.That(exception.Location, Is.Not.Null);
        }

        [Test]
        public void Tokenize_UnterminatedComment_ThrowsTemplateParsingException()
        {
            // Arrange
            string template = "{{* This comment is not terminated }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unterminated comment"));
            Assert.That(exception.Location, Is.Not.Null);
        }

        [Test]
        public void Tokenize_UnterminatedLiteral_ThrowsTemplateParsingException()
        {
            // Arrange
            string template = "{{ literal }}Some literal content";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unterminated literal directive"));
            Assert.That(exception.Location, Is.Not.Null);
        }

        [Test]
        public void Tokenize_UnexpectedCharacter_ThrowsTemplateParsingException()
        {
            // Arrange
            string template = "{{ name @ }}";  // @ is unexpected
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unexpected character"));
            Assert.That(exception.Location, Is.Not.Null);
        }

        [Test]
        public void Tokenize_MultilineTemplate_CorrectLineAndColumnInError()
        {
            // Arrange
            string template = @"
First line
Second line
{{ ""unterminated 
string";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unterminated string literal"));
            Assert.That(exception.Location, Is.Not.Null);
            Assert.That(exception.Location.Line, Is.EqualTo(5)); // Should be line 4
        }

        [Test]
        public void Tokenize_NestedExpression_ErrorLocationIsCorrect()
        {
            // Arrange
            string template = @"{{ 
   for item in items 
      {{ if item == ""value }}
         {{ item }
      {{ /if }}
   {{ /for }}
}}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template));
            
            // Verify exception details - assuming the error is the unterminated string or missing brace
            Assert.That(exception.Location, Is.Not.Null);
        }

        [Test]
        public void Tokenize_EmptyTemplate_NoException()
        {
            // Arrange
            string template = "";
            
            // Act & Assert
            Assert.DoesNotThrow(() => _lexer.Tokenize(template));
        }

        [Test]
        public void Tokenize_WithSourceName_IncludesSourceNameInError()
        {
            // Arrange
            string template = "{{ \"unterminated string }}";
            string sourceName = "test_template.html";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => _lexer.Tokenize(template, sourceName));
            
            // Verify exception details
            Assert.That(exception.Message, Contains.Substring("Unterminated string literal"));
            Assert.That(exception.Location, Is.Not.Null);
            Assert.That(exception.Location.Source, Is.EqualTo(sourceName));
            Assert.That(exception.Message, Contains.Substring(sourceName));
        }
 
        [Test]
        public void Parse_UnclosedIfDirective_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ if x == 1 }}True";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Unclosed if statement: Missing {{/if}} directive", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_UnclosedForDirective_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ for i in [1, 2] }}True";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Unexpected end of template: the template is incomplete or contains a syntax error", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_MissingInKeywordInForLoop_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ for item items }}{{ /for }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected 'in' keyword", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_MismatchedParenthesesInFunctionCall_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ length(items }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected comma between function arguments", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_UnclosedObjectLiteral_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ obj(name: \"Test\" }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Unclosed object literal", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_MissingColonInObjectLiteral_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ obj(name \"Test\") }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected ':'", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_DuplicateFieldNameInObjectLiteral_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ obj(name: \"Test\", name: \"Duplicate\") }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Duplicate field name", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_MissingArrowInLambda_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ ((x) x + 1) }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected <rightparen> but got <variable>", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_DuplicateParameterNameInLambda_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ ((x, x) => x + x) }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Duplicate parameter name", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_MissingCommaInFunctionArguments_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ concat(\"Hello\" \"World\") }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected comma", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_UnexpectedToken_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ 1 + }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected an expression", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_UnexpectedEndOfTemplate_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ 1 + ";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Unexpected end of template", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_InvalidVariableName_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ let 1variable = 5 }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected <variable>", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_NestedIf_CorrectlyParses()
        {
            // Arrange
            string template = "{{ if x == 1 }}{{ if y == 2 }}Nested{{ /if }}{{ /if }}";
            
            // Act
            var tokens = _lexer.Tokenize(template);
            var ast = _interpreter.Parser.Parse(tokens);
            
            // Assert - should not throw exception for valid template
            Assert.NotNull(ast);
        }

        [Test]
        public void Parse_NestedFor_CorrectlyParses()
        {
            // Arrange
            string template = "{{ for x in items }}{{ for y in x.items }}{{ y }}{{ /for }}{{ /for }}";
            
            // Act
            var tokens = _lexer.Tokenize(template);
            var ast = _interpreter.Parser.Parse(tokens);
            
            // Assert - should not throw exception for valid template
            Assert.NotNull(ast);
        }

        [Test]
        public void Parse_InvalidComparisonOperator_ThrowsExceptionWithMessage()
        {
            // Arrange
            string template = "{{ if x === 1 }}True{{ /if }}";
            
            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });
            
            StringAssert.Contains("Expected an expression", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Interpret_InvalidLiteralComparison_ThrowsMeaningfulException()
        {
            // Arrange
            string template = "{{ true == \"true\" }}";  // Comparing bool with string

            dynamic data = new ExpandoObject();

            // Act & Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, data);
            });
            
            StringAssert.Contains("Expected similar types but found Boolean and String", exception.Message);
        }

        [Test]
        public void Interpret_LambdaInFunctionArgument_CorrectlyParsed()
        {
            // Arrange
            string template = "{{ map(range(1, 10), ((x) => x * 2)) }}";

            dynamic data = new ExpandoObject();

            // Act
            string result = _interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Does.Contain("2"));
            Assert.That(result, Does.Contain("18"));
        }

        [Test]
        public void Interpret_UnbalancedNesting_ThrowsMeaningfulException()
        {
            // Arrange
            string template = @"
            {{ if condition1 }}
                {{ if condition2 }}
                    Nested content
                {{ /if }}
            {{ /for }}";  // Mismatched closing tag

            dynamic data = new ExpandoObject();
            data.condition1 = true;
            data.condition2 = true;

            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                _interpreter.Interpret(template, data);
            });

            StringAssert.Contains("Unclosed if statement: Missing {{/if}} directive", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Interpret_ComplexNestedErrors_ThrowsMeaningfulException()
        {
            // Arrange
            string template = @"
            {{- for user in users }}
                <div>
                    <h2>{{ user.name }}</h2>
                    {{- if user.active }}
                        <ul>
                        {{- for order in user.orders }}
                            <li>{{ order.id }: {{ order.date }}</li>
                        {{- /for }}
                        </ul>
                    {{- else }
                        <p>Inactive user</p>
                    {{- /if }}
                </div>
            {{- /for }}";

            dynamic data = new ExpandoObject();
            data.users = new List<ExpandoObject>();

            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                _interpreter.Interpret(template, data);
            });

            StringAssert.Contains("Unexpected character '}'", exception.Message);
        }

        [Test]
        public void Parse_ExpressionWithNestedFunctionCalls_DetectsInnerError()
        {
            // Arrange
            string template = @"
            {{ length(filter(items, ((item) => indexOf(item.name, ""test"" > 0))) }}";  // Missing closing parenthesis

            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });

            StringAssert.Contains("Expected comma between function arguments or a closing parenthesis", exception.InnerExceptions[0].Message);
        }

        [Test]
        public void Parse_UnclosedCaptureBlock_ThrowsMeaningfulException()
        {
            // Arrange
            string template = @"
            {{ capture header }}
                <h1>Title</h1>
            After";  // Missing /capture directive

            // Act & Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                var tokens = _lexer.Tokenize(template);
                _interpreter.Parser.Parse(tokens);
            });

            // Assert message contains expected error indication
            Assert.That(exception.InnerExceptions[0].Message, Does.Contain("Unexpected end"));
        }

        [Test]
        public void TestStackDepthWithBuiltInFunctions()
        {
            string template = @"{{ string(length(range(1, 3))) }}";

            Assert.That("2", Is.EqualTo(_interpreter.Interpret(template, _emptyData)));
        }

        [Test]
        public void NoMatchingOverload_ThrowsAnError()
        {
            string template = @"{{ string(length(nonexistent(1))) }}";

            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("No matching overload found for function 'nonexistent' with the provided arguments", exception.Message);
        }

        [Test]
        public void NoMatchingOverloadInLambda_ThrowsAnError()
        {
            string template = @"{{ map([[1], [2], [3]], (x) => length(nonexistent(x))) }}";

            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("No matching overload found for function 'nonexistent' with the provided arguments", exception.Message);
        }

        [Test]
        public void PassBuiltInFunction()
        {
            // Arrange
            string template = @"{{ let m = mod }}{{ m(10, 4) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("2"));
        }

        [Test]
        public void SpaceCanPrecedeComment()
        {
            // Arrange
            string template = @"{{ * hello world *}}{{ 1 }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void SpaceCanPrecedeCommentWithWhitespaceControl()
        {
            // Arrange
            string template = @"{{- * hello world *}}{{ 1 }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void SpaceCanFollowComment()
        {
            // Arrange
            string template = @"{{* hello world * }}{{ 1 }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void SpaceCanFollowCommentWithWhitespaceControl()
        {
            // Arrange
            string template = @"{{* hello world * -}}{{ 1 }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void IncludeCirclularRefTest()
        {
            // Arrange
            var registry = new TemplateRegistry();
            registry.RegisterTemplate("header", "<h1>{{title}}{{include footer}}</h1>");
            registry.RegisterTemplate("footer", "<footer>{{copyright}}{{include header}}</footer>");

            var interpreter = new Interpreter(registry);

            var template = "{{include header}}";

            var exception = Assert.Throws<TemplateParsingException>(() => {
                interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("Circular template reference detected: 'header'", exception.Message);
        }

        [Test]
        public void CanSpecifyStringType()
        {
            // Arrange
            string template = @"{{ String }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<String>"));
        }

        [Test]
        public void CanSpecifyNumberType()
        {
            // Arrange
            string template = @"{{ Number }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Number>"));
        }

        [Test]
        public void CanSpecifyBooleanType()
        {
            // Arrange
            string template = @"{{ Boolean }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Boolean>"));
        }

        [Test]
        public void CanSpecifyArrayType()
        {
            // Arrange
            string template = @"{{ Array }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Array>"));
        }

        [Test]
        public void CanSpecifyObjectType()
        {
            // Arrange
            string template = @"{{ Object }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Object>"));
        }

        [Test]
        public void CanSpecifyFunctionType()
        {
            // Arrange
            string template = @"{{ Function }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Function>"));
        }

        [Test]
        public void CanSpecifyDateTimeType()
        {
            // Arrange
            string template = @"{{ DateTime }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<DateTime>"));
        }

        [Test]
        public void CanCheckTypeOfString()
        {
            // Arrange
            string template = @"{{ typeof(""abc"") }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<String>"));
        }

        [Test]
        public void CanCheckTypeOfNumber()
        {
            // Arrange
            string template = @"{{ typeof(1) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Number>"));
        }

        [Test]
        public void CanCheckTypeOfBoolean()
        {
            // Arrange
            string template = @"{{ typeof(true) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Boolean>"));
        }

        [Test]
        public void CanCheckTypeOfArray()
        {
            // Arrange
            string template = @"{{ typeof([1, 2]) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Array>"));
        }

        [Test]
        public void CanCheckTypeOfObject()
        {
            // Arrange
            string template = @"{{ typeof(obj(x: 1)) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Object>"));
        }

        [Test]
        public void CanCheckTypeOfFunction()
        {
            // Arrange
            string template = @"{{ typeof(() => 1) }} {{ typeof(map) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<Function> type<Function>"));
        }

        [Test]
        public void CanCheckTypeOfDateTime()
        {
            // Arrange
            string template = @"{{ typeof(datetime(""2024-01-08 10:10:10"")) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<DateTime>"));
        }


        [Test]
        public void CanRuntimeValidateStringType()
        {
            // Arrange
            string template = @"{{ typeof(""abc"") == String }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeValidateNumberType()
        {
            // Arrange
            string template = @"{{ typeof(1) == Number }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeValidateBooleanType()
        {
            // Arrange
            string template = @"{{ typeof(true) == Boolean }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeValidateArrayType()
        {
            // Arrange
            string template = @"{{ typeof([1, 2]) == Array }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeValidateObjectType()
        {
            // Arrange
            string template = @"{{ typeof(obj(x: 1)) == Object }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeValidateFunctionType()
        {
            // Arrange
            string template = @"{{ typeof(() => 1) == Function }} {{ typeof(map) == Function }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true true"));
        }

        [Test]
        public void CanRuntimeValidateDateTimeType()
        {
            // Arrange
            string template = @"{{ typeof(datetime(""2024-01-08 10:10:10"")) == DateTime }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true"));
        }

        [Test]
        public void CanRuntimeTypeCheckVariable()
        {
            // Arrange
            string template = @"{{ let x = ""abc"" }}{{ typeof(x) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<String>"));
        }

        [Test]
        public void CanFilterByType()
        {
            // Arrange
            string template = @"{{ let arr = [1, 2, ""not number"", false, 3] }}{{ filter(arr, (x) => typeof(x) == Number) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("[1, 2, 3]"));
        }

        [Test]
        public void TypesAreFirstClass()
        {
            // Arrange
            string template = @"{{ let type = Array }}{{ type != typeof(3) }} {{ type != typeof([3]) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true false"));
        }

        [Test]
        public void SimpleVariableMutationExists()
        {
            // Arrange
            string template = @"{{ let x = 1 }}{{ x }}{{ mut x = 2 }} {{ x }}";
            string template2 = @"{{ let x = 1 }}{{ x }}{{ x = 2 }} {{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1 2"));
            Assert.That(result2, Is.EqualTo("1 2"));
        }

        [Test]
        public void MutatingIteratorVariableThrowsError()
        {
            // Arrange
            string template = @"{{ for x in [1, 2] }}{{ mut x = 3 }}{{ x }}{{ /for }}";
            string template2 = @"{{ for x in [1, 2] }}{{ x = 3 }}{{ x }}{{ /for }}";

            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Iterator variable x is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Iterator variable x is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void MutatingVariableInLoopExists()
        {
            // Arrange
            string template = @"{{ let a = 0 }}{{ for x in [1, 2] }}{{ mut a = x }}{{ a }}{{ /for }}";
            string template2 = @"{{ let a = 0 }}{{ for x in [1, 2] }}{{ a = x }}{{ a }}{{ /for }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("12"));
            Assert.That(result2, Is.EqualTo("12"));
        }


        [Test]
        public void MutatingVariableInIfExists()
        {
            // Arrange
            string template = @"{{ let a = 0 }}{{ if false }}{{ mut a = ""foo"" }}{{ else }}{{ mut a = ""bar"" }}{{ /if }}{{ a }}";
            string template2 = @"{{ let a = 0 }}{{ if false }}{{ a = ""foo"" }}{{ else }}{{ a = ""bar"" }}{{ /if }}{{ a }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("bar"));
            Assert.That(result2, Is.EqualTo("bar"));
        }

        [Test]
        public void MutatingNonexistentVariableThrowsError()
        {
            // Arrange
            string template = @"{{ mut a = 0 }}";
            string template2 = @"{{ a = 0 }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Cannot mutate variable 'a' because it has not been defined", exception.Message);
            StringAssert.Contains("Cannot mutate variable 'a' because it has not been defined", exception2.Message);
        }

        [Test]
        public void MutatingGlobalDataVariableThrowsError()
        {
            // Arrange
            string template = @"{{ var2 }}{{ mut var2 = 10 }}";
            string template2 = @"{{ var2 }}{{ var2 = 10 }}";
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var2", 2.5);

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, data);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, data);
            });

            StringAssert.Contains("Global variable var2 is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Global variable var2 is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void MutatingVariableInLambdaExists()
        {
            // Arrange
            string template = @"{{ (() => let x = 1, mut x = 2, x)() }}";
            string template2 = @"{{ (() => let x = 1, x = 2, x)() }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("2"));
            Assert.That(result2, Is.EqualTo("2"));
        }

        [Test]
        public void MutatingGlobalVariableInLambdaThrowsError()
        {
            // Arrange
            string template = @"{{ (() => let x = 1, mut var2 = 2, x)() }}";
            string template2 = @"{{ (() => let x = 1, var2 = 2, x)() }}";
            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("var2", 2.5);

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, data);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, data);
            });

            StringAssert.Contains("Global variable var2 is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Global variable var2 is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void MutatingParameterInLambdaThrowsError()
        {
            // Arrange
            string template = @"{{ ((y) => let x = 1, mut y = 2, x * y)(1) }}";
            string template2 = @"{{ ((y) => let x = 1, y = 2, x * y)(1) }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Parameter y is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Parameter y is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void IteratorVariableIsAvailableInClosure()
        {
            // Arrange
            string template = @"{{ for y in [1, 2] }}{{ (() => 2 * y)() }} {{ /for }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("2 4 "));
        }

        [Test]
        public void MutatingIteratorVariableInLambdaThrowsError()
        {
            // Arrange
            string template = @"{{ for y in [1, 2] }}{{ (() => let x = 2, mut y = 2, x * y)() }}{{ /for }}";
            string template2 = @"{{ for y in [1, 2] }}{{ (() => let x = 2, y = 2, x * y)() }}{{ /for }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Iterator variable y is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Iterator variable y is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void MutatingParentClosureParameterThrowsError()
        {
            // Arrange
            string template = @"{{ ((x) => (() => mut x = 2, x)())(1) }}";
            string template2 = @"{{ ((x) => (() => x = 2, x)())(1) }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Parameter x is not mutable and cannot be reassigned", exception.Message);
            StringAssert.Contains("Parameter x is not mutable and cannot be reassigned", exception2.Message);
        }

        [Test]
        public void MutatingFreeVariablesWorks()
        {
            // Arrange
            string template = @"{{ let z = 1 }}{{ (() => let x = 1, (() => mut z = 3, mut x = 2, x * z)())(1) }}";
            string template2 = @"{{ let z = 1 }}{{ (() => let x = 1, (() => z = 3, x = 2, x * z)())(1) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("6"));
            Assert.That(result2, Is.EqualTo("6"));
        }

        [Test]
        public void CounterWorks()
        {
            // Arrange
            string template = @"
{{- let counter = (x) => 
   let self = (() => obj(
     current: x,
     increment: () => self.current = self.current + 1, self.current
   ))(), 
   self 
-}}
{{- let c = counter(0) -}}
{{- let _ = 0 -}}
{{- for i in range(0, 10) -}}
  {{- _ = c.increment() -}}
{{- /for -}}
{{ c.current }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("10"));
        }

        [Test]
        public void CanAccessFieldsOfPassedObject()
        {
            // Arrange
            string template = @"{{ ((o) => o.foo)(obj(foo: 1)) }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("1"));
        }

        [Test]
        public void AllParsingExceptionsThrown()
        {
            // Arrange
            string template = @"{{ let x =  }}{{ mu y = 10 }}{{ nonexistent(3 }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateParsingException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            Assert.That(exception.InnerExceptions.Count, Is.EqualTo(3));
            StringAssert.Contains("Expected an expression but found DirectiveEnd", exception.InnerExceptions[0].Message);
            StringAssert.Contains("Expected <directiveend> but got <variable>", exception.InnerExceptions[1].Message);
            StringAssert.Contains("Expected comma between function arguments or a closing parenthesis: Expected <comma> but got <directiveend>", exception.InnerExceptions[2].Message);
        }

        [Test]
        public void DivisionByZero()
        {
            // Arrange
            string template = @"{{ 1 / 0 }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("Cannot divide by zero", exception.Message);
        }

        [Test]
        public void NumbersAreAssignedByValue()
        {
            // Arrange
            string template = @"{{ let x = 1 }}{{ let y = x }}{{ mut y = 2 }}{{ y }} {{ x }}";
            string template2 = @"{{ let x = 1 }}{{ let y = x }}{{ y = 2 }}{{ y }} {{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("2 1"));
            Assert.That(result2, Is.EqualTo("2 1"));
        }

        [Test]
        public void StringsAreAssignedByValue()
        {
            // Arrange
            string template = @"{{ let x = ""abc"" }}{{ let y = x }}{{ mut y = ""def"" }}{{ y }} {{ x }}";
            string template2 = @"{{ let x = ""abc"" }}{{ let y = x }}{{ y = ""def"" }}{{ y }} {{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("def abc"));
            Assert.That(result2, Is.EqualTo("def abc"));
        }

        [Test]
        public void BooleansAreAssignedByValue()
        {
            // Arrange
            string template = @"{{ let x = false }}{{ let y = x }}{{ mut y = true }}{{ y }} {{ x }}";
            string template2 = @"{{ let x = false }}{{ let y = x }}{{ y = true }}{{ y }} {{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("true false"));
            Assert.That(result2, Is.EqualTo("true false"));
        }

        [Test]
        public void TypesAreAssignedByValue()
        {
            // Arrange
            string template = @"{{ let x = Number }}{{ let y = x }}{{ mut y = String }}{{ y }} {{ x }}";
            string template2 = @"{{ let x = Number }}{{ let y = x }}{{ y = String }}{{ y }} {{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("type<String> type<Number>"));
            Assert.That(result2, Is.EqualTo("type<String> type<Number>"));
        }

        [Test]
        public void ObjectsAreAssignedByReference()
        {
            // Arrange
            string template = @"{{ let x = obj(a: 1, b: 2) }}{{ let y = x }}{{ mut y.a = 3 }}{{ y.a }} {{ x.a }}";
            string template2 = @"{{ let x = obj(a: 1, b: 2) }}{{ let y = x }}{{ y.a = 3 }}{{ y.a }} {{ x.a }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("3 3"));
            Assert.That(result2, Is.EqualTo("3 3"));
        }

        [Test]
        public void MultipleElseIfCanBeChained()
        {
            // Arrange
            string template = @"{{ let x = 1 }}{{ if x > 10 }}>10{{ elseif x > 5 }}>5{{ elseif x > 0}}>0{{ else }}<0{{ /if }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo(">0"));
        }

        [Test]
        public void NumberDeclarationWithDecimalWithoutFractionalPart()
        {
            // Arrange
            string template = @"{{ let x = 0. }}{{ let y = 3. }}{{ x }} {{ y }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("0 3"));
        }

        [Test]
        public void IncludeTemplateInCapture()
        {
            // Arrange
            var registry = new TemplateRegistry();
            registry.RegisterTemplate("header", "<h1>{{title}}</h1>");

            var interpreter = new Interpreter(registry);

            var template = "{{ capture snippet }}<div>{{include header}}</div>{{ /capture }}{{ snippet }}";

            var data = new ExpandoObject();
            ((IDictionary<string, object>)data).Add("title", "My Page");

            // Act
            var result = interpreter.Interpret(template, data);

            // Assert
            Assert.That(result, Is.EqualTo("<div><h1>My Page</h1></div>"));
        }

        
        [Test]
        public void NestedCaptures()
        {
            // Arrange
            string template = @"{{ capture x }}{{ capture y }}foo{{ /capture }}nested val: {{ y }}{{ /capture }}{{ x }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("nested val: foo"));
        }

        [Test]
        public void ArraysCanBeIndexed()
        {
            // Arrange
            string template = @"{{ [1][0] }} {{ let x = [1, 2, 3] }}{{ x[2] }} {{ [[1, 2], [6, 7]][1][1] }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo(@"1 3 7"));
        }

        [Test]
        public void ArrayIndexingIsBoundsChecked()
        {
            // Arrange
            string template1 = @"{{ [1][-1] }}";
            string template2 = @"{{ [1][1] }}";

            // Act && Assert
            var exception1 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template1, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 1: Index -1 is out of bounds for array of length 1", exception1.Message);
            StringAssert.Contains("Error at line 1, column 1: Index 1 is out of bounds for array of length 1", exception2.Message);
        }

        [Test]
        public void ArrayIndexingRequiresWholeNumber()
        {
            // Arrange
            string template1 = @"{{ [1][1.5] }}";
            string template2 = "{{ [1][\"x\"] }}";

            // Act && Assert
            var exception1 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template1, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 1: Expected a whole number while indexing array but found 1.5", exception1.Message);
            StringAssert.Contains("Error at line 1, column 1: Expected a whole number while indexing array but found String", exception2.Message);
        }

        [Test]
        public void IndexingRequiresArrayOrObject()
        {
            // Arrange
            string template1 = "{{ true[1] }}";
            string template2 = "{{ 4[\"x\"] }}";

            // Act && Assert
            var exception1 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template1, _emptyData);
            });
            var exception2 = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template2, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 1: Indexing operation requires an array or an object", exception1.Message);
            StringAssert.Contains("Error at line 1, column 1: Indexing operation requires an array or an object", exception2.Message);
        }

        [Test]
        public void ObjectsCanBeIndexed()
        {
            // Arrange
            string template = @"{{ obj(name: ""John"", age: 30)[""name""] }}";
            string template2 = @"{{ obj(name: ""John"", age: 30, loc: obj(x: 2.4883, y: 82.9))[""loc""][""y""] }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("John"));
            Assert.That(result2, Is.EqualTo("82.9"));
        }

        [Test]
        public void ObjectIndexingRequiresString()
        {
            // Arrange
            string template = @"{{ obj(name: ""John"")[0] }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 1: Expected a string while indexing object but found Number", exception.Message);
        }

        [Test]
        public void ObjectIndexingFailsOnMissingField()
        {
            // Arrange
            string template = @"{{ obj(name: ""John"")[""age""] }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() => {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 1: Object does not contain field 'age'", exception.Message);
        }

        [Test]
        public void MutKeywordIsOptional()
        {
            // Arrange
            string template = @"{{ let x = 1 }}{{ x = 2 }}{{ x }}";
            string template2 = @"{{ (() => let x = 1, x = 2, x)() }}";

            // Act
            string result = _interpreter.Interpret(template, _emptyData);
            string result2 = _interpreter.Interpret(template2, _emptyData);

            // Assert
            Assert.That(result, Is.EqualTo("2"));
            Assert.That(result2, Is.EqualTo("2"));
        }

        [Test]
        public void CannotMutateExistingRegistryFunctionInLambda()
        {
            // Arrange
            string template = @"{{ (() => let x = 1, length = 1, x)() }}";

            // Act && Assert
            var exception = Assert.Throws<TemplateEvaluationException>(() =>
            {
                _interpreter.Interpret(template, _emptyData);
            });

            StringAssert.Contains("Error at line 1, column 36: Cannot mutate variable 'length' because it is an existing function", exception.Message);
        }
    }
}
