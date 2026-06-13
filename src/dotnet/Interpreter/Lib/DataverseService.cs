using Microsoft.Xrm.Sdk;
using Microsoft.Xrm.Sdk.Query;
using PowwowLang.Types;
using System;
using System.Collections.Generic;

namespace PowwowLang.Lib
{
    public class DataverseService : IDataverseService
    {
        private readonly IOrganizationService _organizationService;

        public DataverseService(IOrganizationService organizationService)
        {
            _organizationService = organizationService ?? throw new ArgumentNullException(nameof(organizationService));
        }

        public Value RetrieveMultiple(string fetchXml)
        {
            var fetch = new FetchExpression(fetchXml);
            var results = _organizationService.RetrieveMultiple(fetch);
            return ConvertToObjects(results);
        }

        private Value ConvertToObjects(EntityCollection entityCollection)
        {
            var dicts = new List<Value>();

            foreach (var entity in entityCollection.Entities)
            {
                var dict = new Dictionary<string, Value>();

                foreach (var attribute in entity.Attributes)
                {
                    // Skip null values
                    if (attribute.Value != null)
                    {
                        // Handle special types like EntityReference, OptionSetValue, etc.
                        var value = ConvertAttributeValue(entity, attribute.Key, attribute.Value);
                        if (value != null)
                        {
                            dict[attribute.Key] = value;
                        }
                    }
                }

                dicts.Add(new Value(new ObjectValue(dict)));
            }

            return new Value(new ArrayValue(dicts));
        }

        private Value ConvertAttributeValue(Entity entity, string attributeName, object attributeValue)
        {
            if (attributeValue == null) return null;

            switch (attributeValue)
            {
                case EntityReference entityRef:
                    return new Value(new ObjectValue(new Dictionary<string, Value>()
                    {
                        { "id", new Value(new StringValue(entityRef.Id.ToString())) },
                        { "name", new Value(new StringValue(entityRef.Name)) },
                        { "logicalName", new Value(new StringValue(entityRef.LogicalName)) }
                    }));

                case Guid guid:
                    return new Value(new StringValue(guid.ToString()));

                case OptionSetValue optionSet:
                    return new Value(new ObjectValue(new Dictionary<string, Value>()
                    {
                        { "value", new Value(new NumberValue(Convert.ToDecimal(optionSet.Value))) },
                        { "label", new Value(new StringValue(entity.FormattedValues[attributeName])) },
                    }));

                case Money money:
                    return new Value(new NumberValue(money.Value));

                case AliasedValue aliased:
                    return ConvertAttributeValue(entity, attributeName, aliased.Value);

                case string str:
                    return new Value(new StringValue(str));

                case bool boolean:
                    return new Value(new BooleanValue(boolean));

                case DateTime dateTime:
                    return new Value(new DateTimeValue(dateTime));

                default:
                    if (TypeHelper.IsConvertibleToDecimal(attributeValue))
                    {
                        return new Value(new NumberValue(Convert.ToDecimal(attributeValue)));
                    }

                    throw new Exception($"Unable to convert value to known datatype: {attributeValue}");
            }
        }
    }
}
