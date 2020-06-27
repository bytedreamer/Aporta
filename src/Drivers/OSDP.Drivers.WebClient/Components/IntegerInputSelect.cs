using Microsoft.AspNetCore.Components.Forms;

namespace OSDP.Drivers.WebClient.Components
{
    public class IntegerInputSelect<TValue> : InputSelect<TValue>
    {
        protected override bool TryParseValueFromString(string value, out TValue result, 
            out string validationErrorMessage)
        {
            if (typeof(TValue) != typeof(int))
                return base.TryParseValueFromString(value, out result,
                    out validationErrorMessage);
            
            if (int.TryParse(value, out var resultInt))
            {
                result = (TValue)(object)resultInt;
                validationErrorMessage = null;
                
                return true;
            }

            result = default;
            validationErrorMessage = 
                $"The selected value {value} is not a valid number.";
            
            return false;
        }
    }
}