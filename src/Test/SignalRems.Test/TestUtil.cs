using System;
using System.Threading.Tasks;

namespace SignalRems.Test
{
    public class TestUtil
    {
        public static async Task<bool> WaitForConditionAsync(Func<bool> condition, int timeout)
        {
            while (timeout > 0)
            {
                if (condition())
                {
                    return true;
                }

                timeout -= 10;
                await Task.Delay(10);
            }

            return false;
        }
    }
}
