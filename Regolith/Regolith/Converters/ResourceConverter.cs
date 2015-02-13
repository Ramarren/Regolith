using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using UnityEngine;

namespace Regolith.Common
{
    public struct ConverterResults
    {
        public string Status;
        public double TimeFactor;
    }

    public class ResourceConverter : MonoBehaviour
    {
        private IResourceBroker _broker;

        public ResourceConverter(IResourceBroker broker)
        {
            _broker = broker;
        }

        public ResourceConverter() : this(new ResourceBroker())
        { }

        public ConverterResults ProcessRecipe(double deltaTime, ConversionRecipe recipe, Part resPart, float efficiencyBonus)
        {
            var result = new ConverterResults();
            result.Status = "Idle";
            //Efficiency bonus is comprised of two things.
            //The bonus passed in, and the presence of all required components.
            var bonus = 1d; //We start at 100%.
            foreach (var r in recipe.Requirements)
            {
                var res = resPart.Resources[r.ResourceName];
                var thisRatio = res.amount/Math.Abs(r.Ratio);

                if(r.Ratio < 0) //bad things we need to clean out
                {
                    thisRatio = (1 - thisRatio);
                }
                if (thisRatio < bonus) //It's worse...
                {
                    bonus = thisRatio;
                }
            }
            bonus *= efficiencyBonus;
            //It may be that we're at zero!
            if (bonus <= Utilities.FLOAT_TOLERANCE)
            {
                result.Status = "Missing resources";
                result.TimeFactor = 0;
                return result;
            }

            //We test for availability of all inputs
            double timeFactor = deltaTime;
            foreach (var r in recipe.Inputs)
            {
                // 10 seconds with a ratio of 2/sec and a bonus of 1.5 means we need 30.
                //Include our take amount multiplier to fool us into having less capacity than we
                //really have.
                var avail = _broker.AmountAvailable(resPart, r.ResourceName) * recipe.TakeAmount;
                //If we only have 15...
                //15 < (2*10*1.5)[30]
                if (avail < r.Ratio * timeFactor * bonus)
                {
                    //Then we reduce our timeFactor to 5.
                    // 10 * (15 / (2*10*1.5)[30]) = .5
                    timeFactor = timeFactor * (avail / (r.Ratio * timeFactor * bonus));
                }
            }

            if (timeFactor <= Utilities.FLOAT_TOLERANCE)
            {
                result.Status = "Missing inputs";
                result.TimeFactor = 0;
                return result;
            }


            //test for space of all outputs.  Ignore ones where it's ok to dump them
            //Also:  We do use the FillAmount, this effectively fools the system into thinking there is 
            //less space available than there really is.  The use case would be things where we want a slow charge,
            //such as battery charging, etc.
            foreach (var r in recipe.Outputs.Where(ro=>!ro.DumpExcess))
            {
                var space = (_broker.StorageAvailable(resPart, r.ResourceName))* recipe.FillAmount;
                
                if (space < r.Ratio * timeFactor * bonus)
                {
                    timeFactor = timeFactor * (space / (r.Ratio * timeFactor * bonus));
                }
            }

            if (timeFactor <= Utilities.FLOAT_TOLERANCE)
            {
                result.Status = "no space";
                result.TimeFactor = 0;
                return result;
            }

            //Pull inputs
            foreach (var res in recipe.Inputs)
            {
                double input;
                input = _broker.RequestResource(resPart, res.ResourceName, res.Ratio * timeFactor * bonus);
            }
            
            //Store outputs
            foreach (var res in recipe.Outputs)
            {

                var output = _broker.StoreResource(resPart, res.ResourceName, res.Ratio * timeFactor * bonus);
            }

            //Work in bonus so our efficiency value is correct.
            result.TimeFactor = timeFactor * bonus;
            return result;
        }
    }
}
