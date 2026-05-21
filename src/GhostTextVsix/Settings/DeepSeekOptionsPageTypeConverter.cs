using System;
using System.ComponentModel;
using System.Linq;

namespace GhostTextVsix.Settings;

internal sealed class DeepSeekOptionsPageTypeConverter : TypeConverter
{
    public override bool GetPropertiesSupported(ITypeDescriptorContext context)
    {
        return true;
    }

    public override PropertyDescriptorCollection GetProperties(
        ITypeDescriptorContext context,
        object value,
        Attribute[] attributes)
    {
        var properties = TypeDescriptor.GetProperties(value, attributes, true)
            .Cast<PropertyDescriptor>()
            .OrderBy(GetOrder)
            .ThenBy(property => property.Category, StringComparer.Ordinal)
            .ThenBy(property => property.DisplayName, StringComparer.Ordinal)
            .ToArray();

        return new PropertyDescriptorCollection(properties);
    }

    private static int GetOrder(PropertyDescriptor property)
    {
        return property.Attributes[typeof(PropertyOrderAttribute)] is PropertyOrderAttribute order
            ? order.Order
            : int.MaxValue;
    }
}
