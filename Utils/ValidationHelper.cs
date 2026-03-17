using System.ComponentModel.DataAnnotations;
using System.Reflection;

namespace MvcMusic.Utils
{
    public static class ValidationHelper
    {
        public static Dictionary<string, object> GetValidationRules(Type type)
        {
            var rules = new Dictionary<string, object>();
            var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance);

            foreach (var prop in properties)
            {
                var propRules = new Dictionary<string, object>();

                // Check [Required]
                var required = prop.GetCustomAttribute<RequiredAttribute>();
                if (required != null || (prop.PropertyType == typeof(string) && prop.GetCustomAttribute<System.Runtime.CompilerServices.RequiredMemberAttribute>() != null))
                {
                    propRules["required"] = true;
                    propRules["requiredMsg"] = required?.FormatErrorMessage(prop.Name) ?? $"{prop.Name} is required.";
                }

                // Check [StringLength]
                var stringLength = prop.GetCustomAttribute<StringLengthAttribute>();
                if (stringLength != null)
                {
                    propRules["maxLength"] = stringLength.MaximumLength;
                    propRules["maxLengthMsg"] = stringLength.FormatErrorMessage(prop.Name);
                    if (stringLength.MinimumLength > 0)
                    {
                        propRules["minLength"] = stringLength.MinimumLength;
                        propRules["minLengthMsg"] = stringLength.FormatErrorMessage(prop.Name);
                    }
                }

                // Check [Range]
                var range = prop.GetCustomAttribute<RangeAttribute>();
                if (range != null)
                {
                    propRules["min"] = range.Minimum;
                    propRules["max"] = range.Maximum;
                    propRules["rangeMsg"] = range.FormatErrorMessage(prop.Name);
                }

                if (propRules.Count > 0)
                {
                    rules[prop.Name.ToLower()] = propRules;
                }
            }

            return rules;
        }
    }
}
