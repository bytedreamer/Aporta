namespace Aporta.Shared.Models;

using System;
using System.Reflection;

public static class EnumExtensions
{
    public static string GetDescription(this Enum value)
    {
        var field = value.GetType().GetField(value.ToString());

        var attribute = field?.GetCustomAttribute<DescriptionAttribute>();
        
        return attribute != null ? attribute.Description : value.ToString();
    }
}